namespace SldFileTests;

/// <summary>
/// Provides well-known test file paths and disposable temporary copies for write tests.
/// <para>
/// Directory layout convention:
/// <code>
///   TestFiles/
///   ├── Solidworks2012/          ← version-tagged folders
///   │   ├── cube.SLDPRT
///   │   ├── cylinder.SLDPRT
///   │   ├── cube.SLDDRW
///   │   └── assembly.SLDASM
///   └── Solidworks2024/          ← add more versions here
/// </code>
/// To add new test files, drop them in the appropriate version folder;
/// they will be copied to the output directory automatically via the csproj
/// <c>&lt;None Include="TestFiles\**\*" CopyToOutputDirectory="PreserveNewest" /&gt;</c>.
/// </para>
/// </summary>
internal static class TestFileProvider
{
    /// <summary>Root of the TestFiles tree, relative to the test runner's working directory.</summary>
    private static readonly string TestFilesRoot =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles");

    private static readonly string ModelsRoot =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");

    // ── Solidworks 2012 ────────────────────────────────────────────

    public static string CubePart       => Resolve("Solidworks2012", "cube.SLDPRT");
    public static string CylinderPart   => Resolve("Solidworks2012", "cylinder.SLDPRT");
    public static string CubeDrawing    => Resolve("Solidworks2012", "cube.SLDDRW");
    public static string Assembly       => Resolve("Solidworks2012", "assembly.SLDASM");

    // ── Solidworks 2026 ────────────────────────────────────────────

    public static string CubePart2026       => Resolve("Solidworks2026", "cube.SLDPRT");
    public static string CylinderPart2026   => Resolve("Solidworks2026", "cylinder.SLDPRT");
    public static string CubeDrawing2026    => Resolve("Solidworks2026", "cube.SLDDRW");
    public static string Assembly2026       => Resolve("Solidworks2026", "assembly.SLDASM");

    /// <summary>
    /// Returns the absolute path to a test file.
    /// Throws <see cref="FileNotFoundException"/> if the file is missing from the output directory
    /// (usually means the csproj CopyToOutputDirectory rule is wrong or the file was not added).
    /// </summary>
    public static string Resolve(string versionFolder, string fileName)
    {
        var path = Path.Combine(TestFilesRoot, versionFolder, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Test file not found at '{path}'. " +
                $"Ensure the file exists under SldFileTests/TestFiles/{versionFolder}/ " +
                "and the csproj copies it to the output directory.", path);
        return path;
    }

    public static string ResolveModel(string fileName)
    {
        var path = Path.Combine(ModelsRoot, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Model file not found at '{path}'. " +
                "Ensure the file exists under models/ and the csproj copies it to the output directory.", path);
        return path;
    }

    /// <summary>
    /// Creates a temporary copy of a test file for write/roundtrip tests.
    /// Returns a <see cref="TempTestFile"/> whose <see cref="IDisposable.Dispose"/>
    /// deletes the copy automatically.
    /// </summary>
    public static TempTestFile CreateTempCopy(string sourceFilePath)
    {
        var dir = Path.GetDirectoryName(sourceFilePath)!;
        var ext = Path.GetExtension(sourceFilePath);
        var tempPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_tmp_{Guid.NewGuid():N}{ext}");
        File.Copy(sourceFilePath, tempPath, true);
        return new TempTestFile(tempPath);
    }
}

/// <summary>
/// A temporary file that is deleted on <see cref="Dispose"/>.
/// Use with <c>using</c> to ensure cleanup even when assertions fail.
/// </summary>
internal sealed class TempTestFile : IDisposable
{
    public string FilePath { get; }

    public TempTestFile(string filePath) => FilePath = filePath;

    public void Dispose()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch { /* best-effort cleanup */ }
    }
}
