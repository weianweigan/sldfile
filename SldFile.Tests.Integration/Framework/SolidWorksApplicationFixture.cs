using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SldFile.Tests.Integration.Framework;

/// <summary>
/// Manages the lifecycle of an owned background SOLIDWORKS session across integration test collections.
///
/// Isolation contract:
/// - By default the fixture never attaches to a user's running SOLIDWORKS instance. It either owns
///   the instance created by COM activation (verified by PID) or launches a distinct SLDWORKS.exe
///   and binds the ROT server whose PID matches the child process.
/// - Reusing an already running instance requires an explicit opt-in through the
///   SLDFILE_REUSE_SOLIDWORKS_INSTANCE=1 environment variable or the bool constructor overload.
/// - Every file opened for native verification is copied to an artifact directory with its SHA-256
///   before/after the open, OpenDoc6 error/warning counts, document title and process identity.
///   This preserves injected files even when the test later deletes its temporary copy or fails.
/// </summary>
public class SolidWorksApplicationFixture : IDisposable
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    private const string DefaultSolidWorksExe = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe";
    private const string ReuseEnvironmentVariable = "SLDFILE_REUSE_SOLIDWORKS_INSTANCE";
    private const string ArtifactDirectoryEnvironmentVariable = "SLDFILE_NATIVE_ARTIFACT_DIR";
    private const string SolidWorksExecutableEnvironmentVariable = "SLDFILE_SOLIDWORKS_EXE";

    private static readonly Lazy<bool> _isAvailable = new(CheckSolidWorksAvailable);
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new() { WriteIndented = true };

    private ISldWorks? _app;
    private Process? _ownedProcess;
    private bool _ownsProcess;
    private bool _disposed;
    private readonly bool _reuseExistingInstance;
    private string? _artifactDirectory;
    private readonly List<NativeArtifactRecord> _artifacts = new();

    public SolidWorksApplicationFixture()
    {
        _reuseExistingInstance = ReadReuseSetting();
    }

    public static bool IsSolidWorksAvailable => _isAvailable.Value;

    public int LastOpenErrors { get; private set; }
    public int LastOpenWarnings { get; private set; }

    /// <summary>PID of the SOLIDWORKS instance used by this fixture (owned or explicitly reused).</summary>
    public int? ProcessId { get; private set; }

    /// <summary>SOLIDWORKS revision reported by the API, e.g. "34.0.0".</summary>
    public string? Revision { get; private set; }

    /// <summary>True when this fixture started and therefore owns the SOLIDWORKS process.</summary>
    public bool OwnsProcess => _ownsProcess;

    /// <summary>True when the fixture was explicitly allowed to attach to an existing instance.</summary>
    public bool ReuseExistingInstance => _reuseExistingInstance;

    /// <summary>Directory that receives a copy plus SHA-256 of every native-opened artifact.</summary>
    public string ArtifactDirectory => _artifactDirectory ??= ResolveArtifactDirectory();

    public IReadOnlyList<NativeArtifactRecord> Artifacts => _artifacts;

    public ISldWorks? Application
    {
        get
        {
            if (_app == null && IsSolidWorksAvailable)
            {
                InitApp();
            }
            return _app;
        }
    }

    private static bool ReadReuseSetting()
    {
        string? value = System.Environment.GetEnvironmentVariable(ReuseEnvironmentVariable);
        return value != null &&
               (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool CheckSolidWorksAvailable()
    {
        try
        {
            var swType = Type.GetTypeFromProgID("SldWorks.Application");
            return swType != null;
        }
        catch
        {
            return false;
        }
    }

    private void InitApp()
    {
        try
        {
            var swType = Type.GetTypeFromProgID("SldWorks.Application");
            if (swType == null) return;

            var existing = SnapshotProcesses();

            if (_reuseExistingInstance)
            {
                try
                {
                    var clsid = swType.GUID;
                    GetActiveObject(ref clsid, IntPtr.Zero, out var active);
                    if (active is ISldWorks activeApp)
                    {
                        _app = activeApp;
                        _ownsProcess = false;
                        _ownedProcess = null;
                        ConfigureApplication(activeApp);
                        return;
                    }
                }
                catch
                {
                    // No active instance; fall through and own a new one.
                }
            }

            _app = CreateOwnedInstance(swType, existing);
            if (_app != null)
            {
                _ownsProcess = true;
                ConfigureApplication(_app);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to initialize SOLIDWORKS application instance: {ex.Message}");
            _app = null;
            _ownsProcess = false;
        }
    }

    private ISldWorks? CreateOwnedInstance(Type swType, List<ProcessSnapshot> existing)
    {
        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(swType);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"COM activation failed: {ex.Message}");
        }

        if (instance is ISldWorks comApp)
        {
            int pid = SafeGetProcessId(comApp);
            if (pid > 0 && !existing.Any(p => p.Id == pid))
            {
                _ownedProcess = TryGetProcess(pid);
                return comApp;
            }

            // COM activation attached to a pre-existing server. Release the reference without
            // closing the user's instance and launch a distinct executable instead.
            try { Marshal.ReleaseComObject(comApp); } catch { }
        }

        return LaunchDistinctInstance();
    }

    private ISldWorks? LaunchDistinctInstance()
    {
        string executable = System.Environment.GetEnvironmentVariable(SolidWorksExecutableEnvironmentVariable)
                            ?? DefaultSolidWorksExe;
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException(
                $"Cannot own a new SOLIDWORKS instance: executable not found at '{executable}'. " +
                $"Close other SLDWORKS processes or set {ReuseEnvironmentVariable}=1 to explicitly reuse one.");
        }

        var child = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Failed to start the SOLIDWORKS executable.");
        _ownedProcess = child;

        var sw = WaitForRunningObject(child, TimeSpan.FromSeconds(90));
        if (sw == null)
        {
            throw new TimeoutException(
                $"No SOLIDWORKS ROT server matched owned PID {child.Id}. The executable may have handed off to a running instance.");
        }
        try { sw.Visible = false; } catch { }
        try { sw.UserControl = false; } catch { }
        return sw;
    }

    private static ISldWorks? WaitForRunningObject(Process process, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The owned SOLIDWORKS executable (PID {process.Id}) exited during startup with code {process.ExitCode}.");
            }
            if (GetRunningObjectTable(0, out var rot) == 0)
            {
                rot.EnumRunning(out var enumerator);
                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    try
                    {
                        rot.GetObject(monikers[0], out var candidate);
                        if (candidate is ISldWorks sw && SafeGetProcessId(sw) == process.Id)
                        {
                            return sw;
                        }
                    }
                    catch (COMException)
                    {
                    }
                }
            }
            Thread.Sleep(250);
        }
        return null;
    }

    private void ConfigureApplication(ISldWorks application)
    {
        try { application.Visible = false; } catch { }
        try { application.UserControl = false; } catch { }
        try
        {
            int pid = application.GetProcessID();
            ProcessId = pid > 0 ? pid : null;
            if (_ownsProcess && pid > 0)
            {
                _ownedProcess ??= TryGetProcess(pid);
            }
        }
        catch
        {
        }
        try { Revision = application.RevisionNumber(); } catch { }
    }

    private static int SafeGetProcessId(ISldWorks application)
    {
        try { return application.GetProcessID(); } catch { return 0; }
    }

    private static Process? TryGetProcess(int pid)
    {
        try { return Process.GetProcessById(pid); } catch { return null; }
    }

    private sealed record ProcessSnapshot(int Id, DateTime? StartTime);

    private static List<ProcessSnapshot> SnapshotProcesses()
    {
        var list = new List<ProcessSnapshot>();
        foreach (var process in Process.GetProcessesByName("SLDWORKS"))
        {
            DateTime? started = null;
            try { started = process.StartTime; } catch { }
            list.Add(new ProcessSnapshot(process.Id, started));
        }
        return list;
    }

    /// <summary>
    /// Opens a document silently in SOLIDWORKS for verification and preserves a copy plus SHA-256.
    /// </summary>
    public IModelDoc2? OpenDocument(string filePath, bool readOnly = true)
    {
        LastOpenErrors = 0;
        LastOpenWarnings = 0;
        if (Application == null) return null;
        if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);

        int errors = 0;
        int warnings = 0;
        int docType = filePath.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
            ? (int)swDocumentTypes_e.swDocASSEMBLY
            : filePath.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocDRAWING
                : (int)swDocumentTypes_e.swDocPART;

        int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
        if (readOnly) options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

        var artifact = CaptureArtifactBeforeOpen(filePath);
        var document = Application.OpenDoc6(filePath, docType, options, "", ref errors, ref warnings) as IModelDoc2;
        LastOpenErrors = errors;
        LastOpenWarnings = warnings;
        CompleteArtifactCapture(artifact, document, errors, warnings);
        return document;
    }

    /// <summary>Copies an arbitrary verification artifact (for example an injected part) and records its SHA-256.</summary>
    public NativeArtifactRecord PreserveArtifact(string filePath, string? label = null)
    {
        var record = CaptureArtifactBeforeOpen(filePath, label);
        CompleteArtifactCapture(record, null, null, null);
        return record;
    }

    /// <summary>Opens a verification artifact and rejects every native open error or warning.</summary>
    public IModelDoc2 OpenDocumentVerified(string filePath, bool readOnly = true)
    {
        var document = OpenDocument(filePath, readOnly);
        if (document == null || LastOpenErrors != 0 || LastOpenWarnings != 0)
        {
            if (document != null) CloseDocument(filePath);
            throw new InvalidOperationException(
                $"SOLIDWORKS OpenDoc6 failed clean-open verification for '{filePath}': errors={LastOpenErrors}, warnings={LastOpenWarnings}, document={(document == null ? "null" : "returned")}.");
        }
        return document;
    }

    /// <summary>
    /// Checks every top-level solid extrusion (including cuts) after rebuilding.
    /// System folders, sketches and unrelated feature families are outside this check.
    /// </summary>
    public static void VerifyExtrusionFeatureStatus(IModelDoc2 document)
    {
        int checkedCount = 0;
        for (var feature = (IFeature?)document.FirstFeature(); feature != null;
             feature = (IFeature?)feature.GetNextFeature())
        {
            string type = feature.GetTypeName2();
            if (type != "Extrusion" && type != "Boss" && type != "Cut") continue;

            // Local SOLIDWORKS interop: Int32 GetErrorCode2(out Boolean IsWarning).
            int code = feature.GetErrorCode2(out bool isWarning);
            checkedCount++;
            if (code != 0 || isWarning)
                throw new InvalidOperationException(
                    $"SOLIDWORKS feature '{feature.Name}' ({type}) has rebuild status code={code}, isWarning={isWarning}.");
        }
        if (checkedCount == 0)
            throw new InvalidOperationException("No solid extrusion features were checked after rebuilding.");
    }

    /// <summary>
    /// Closes a document in SOLIDWORKS.
    /// </summary>
    public void CloseDocument(string filePath)
    {
        if (Application != null)
        {
            try
            {
                Application.CloseDoc(Path.GetFileName(filePath));
            }
            catch
            {
            }
        }
    }

    private NativeArtifactRecord CaptureArtifactBeforeOpen(string filePath, string? label = null)
    {
        Directory.CreateDirectory(ArtifactDirectory);
        int sequence = _artifacts.Count + 1;
        string safeName = string.IsNullOrWhiteSpace(label) ? Path.GetFileName(filePath) : label;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalid, '_');
        string copied = Path.Combine(ArtifactDirectory, $"{sequence:D3}_{safeName}");
        if (!copied.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase) &&
            !copied.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) &&
            !copied.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase))
            copied += Path.GetExtension(filePath);

        string hashBefore = HashFile(filePath);
        File.Copy(filePath, copied, overwrite: true);
        var record = new NativeArtifactRecord
        {
            Sequence = sequence,
            OriginalPath = Path.GetFullPath(filePath),
            CopiedPath = Path.GetFullPath(copied),
            Label = label,
            Bytes = new FileInfo(filePath).Length,
            Sha256Before = hashBefore,
            CapturedUtc = DateTime.UtcNow,
            ProcessId = ProcessId,
            Revision = Revision
        };
        _artifacts.Add(record);
        WriteArtifactManifest();
        return record;
    }

    private void CompleteArtifactCapture(NativeArtifactRecord record, IModelDoc2? document, int? errors, int? warnings)
    {
        try { record.Sha256After = HashFile(record.OriginalPath); } catch { }
        if (errors.HasValue) record.OpenErrors = errors.Value;
        if (warnings.HasValue) record.OpenWarnings = warnings.Value;
        if (document != null)
        {
            try { record.DocumentTitle = document.GetTitle(); } catch { }
            try { record.DocumentPath = document.GetPathName(); } catch { }
        }
        record.CompletedUtc = DateTime.UtcNow;
        WriteArtifactManifest();
    }

    private void WriteArtifactManifest()
    {
        try
        {
            Directory.CreateDirectory(ArtifactDirectory);
            var manifest = new
            {
                fixture = "SolidWorksApplicationFixture",
                processId = ProcessId,
                ownsProcess = _ownsProcess,
                reuseExistingInstance = _reuseExistingInstance,
                revision = Revision,
                processStartTime = _ownedProcess != null ? SafeStartTime(_ownedProcess) : null,
                artifactDirectory = ArtifactDirectory,
                updatedUtc = DateTime.UtcNow,
                artifacts = _artifacts
            };
            File.WriteAllText(Path.Combine(ArtifactDirectory, "artifacts.json"),
                JsonSerializer.Serialize(manifest, ArtifactJsonOptions));
        }
        catch
        {
        }
    }

    private static DateTime? SafeStartTime(Process process)
    {
        try { return process.StartTime; } catch { return null; }
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string ResolveArtifactDirectory()
    {
        string? configured = System.Environment.GetEnvironmentVariable(ArtifactDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        return Path.Combine(Path.GetTempPath(), "sldfile-native-artifacts", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        WriteArtifactManifest();

        if (_app != null && _ownsProcess)
        {
            try
            {
                _app.ExitApp();
            }
            catch
            {
            }
            finally
            {
                try
                {
                    Marshal.ReleaseComObject(_app);
                }
                catch
                {
                }
                _app = null;

                var process = _ownedProcess ?? (ProcessId.HasValue ? TryGetProcess(ProcessId.Value) : null);
                if (process != null)
                {
                    try { process.WaitForExit(60_000); } catch { }
                }
            }
        }
        _ownedProcess = null;
    }
}

/// <summary>Copy and SHA-256 record for a file opened by the native integration fixture.</summary>
public sealed class NativeArtifactRecord
{
    public int Sequence { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
    public string CopiedPath { get; set; } = string.Empty;
    public string? Label { get; set; }
    public long Bytes { get; set; }
    public string Sha256Before { get; set; } = string.Empty;
    public string? Sha256After { get; set; }
    public int? OpenErrors { get; set; }
    public int? OpenWarnings { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentPath { get; set; }
    public int? ProcessId { get; set; }
    public string? Revision { get; set; }
    public DateTime CapturedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

