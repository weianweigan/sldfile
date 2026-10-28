using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SldFile.Tests.Integration.Framework;

/// <summary>
/// Manages the lifecycle of a single background SOLIDWORKS session across integration test collections.
/// Follows the xCAD integration test fixture pattern using strong-typed SolidWorks Interop.
/// </summary>
public class SolidWorksApplicationFixture : IDisposable
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    private static readonly Lazy<bool> _isAvailable = new(CheckSolidWorksAvailable);
    private ISldWorks? _app;
    private bool _startedProcess;
    private bool _disposed;

    public static bool IsSolidWorksAvailable => _isAvailable.Value;

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

            // 1. Try connecting to already running instance first for speed
            try
            {
                var clsid = swType.GUID;
                GetActiveObject(ref clsid, IntPtr.Zero, out var active);
                if (active is ISldWorks activeApp)
                {
                    _app = activeApp;
                    _startedProcess = false;
                    return;
                }
            }
            catch
            {
                // No active instance, proceed to create one
            }

            // 2. Start a new instance
            _app = Activator.CreateInstance(swType) as ISldWorks;
            _startedProcess = true;
            if (_app != null)
            {
                try
                {
                    _app.Visible = false;
                    _app.UserControl = false;
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to initialize SOLIDWORKS application instance: {ex.Message}");
            _app = null;
        }
    }

    /// <summary>
    /// Opens a document silently in SOLIDWORKS for verification.
    /// </summary>
    public IModelDoc2? OpenDocument(string filePath, bool readOnly = true)
    {
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

        return Application.OpenDoc6(filePath, docType, options, "", ref errors, ref warnings) as IModelDoc2;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_app != null && _startedProcess)
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
            }
        }
    }
}
