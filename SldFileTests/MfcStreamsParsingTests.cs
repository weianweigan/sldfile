using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class MfcStreamsParsingTests
{
    // ── CMgrHdr2 ─────────────────────────────────────────────────────

    [Fact]
    public void CmgrHdr2_PartFile_ShouldHavePartTypeFlag()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.CmgrHdr2.MagicMarker != 0);
        Assert.True(file.CmgrHdr2.IsPart);
        Assert.False(file.CmgrHdr2.IsAssembly);
    }

    [Fact]
    public void CmgrHdr2_AssemblyFile_ShouldHaveAssemblyTypeFlag()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        Assert.True(file.CmgrHdr2.MagicMarker != 0);
        Assert.True(file.CmgrHdr2.IsAssembly);
        Assert.False(file.CmgrHdr2.IsPart);
    }

    // ── Biography ──────────────────────────────────────────────────

    [Fact]
    public void Biography_PartFile_ShouldHavePaths()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.Biography.PathEntries.Count > 0);
    }

    [Fact]
    public void Biography_DrawingFile_ShouldHaveFewerPaths()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var partCount = file.Biography.PathEntries.Count;
        Assert.True(partCount > 0);

        using var drwFile = SwFile.Open(TestFileProvider.CubeDrawing, readOnly: true);
        var drwCount = drwFile.Biography.PathEntries.Count;
        Assert.True(drwCount > 0);
        Assert.True(drwCount < partCount,
            $"Drawing biography ({drwCount}) expected < Part ({partCount})");
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    [InlineData(nameof(TestFileProvider.Assembly))]
    public void Biography_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.True(file.Biography.PathEntries.Count > 0);
    }

    // ── History ────────────────────────────────────────────────────

    [Fact]
    public void History_PartFile_ShouldHaveVersions()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.History.VersionCount > 0);
        Assert.True(file.History.Versions.Count > 0);
    }

    [Fact]
    public void History_DrawingFile_ShouldHaveFewerVersions()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var partVersions = file.History.VersionCount;

        using var drwFile = SwFile.Open(TestFileProvider.CubeDrawing, readOnly: true);
        Assert.True(drwFile.History.VersionCount >= 1);
        Assert.True(drwFile.History.VersionCount < partVersions,
            $"Drawing versions ({drwFile.History.VersionCount}) expected < Part ({partVersions})");
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    [InlineData(nameof(TestFileProvider.Assembly))]
    public void History_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.True(file.History.VersionCount > 0);
    }

    // ── CMgr ───────────────────────────────────────────────────────

    [Fact]
    public void Cmgr_PartFile_ShouldHaveDisplayName()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.False(string.IsNullOrEmpty(file.Cmgr.DisplayName));
        Assert.NotNull(file.Cmgr.VisualProperties);
    }

    [Fact]
    public void Cmgr_VisualProperties_ShouldHaveValues()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var vp = file.Cmgr.VisualProperties;
        Assert.NotNull(vp);
        Assert.True(vp.ColorRgb != 0);
    }

    // ── LWDATA ─────────────────────────────────────────────────────

    [Fact]
    public void LwData_PartFile_ShouldHaveTypeFlag()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.Equal(LwData.TypeFlagPart, file.LwData.TypeFlag);
        Assert.True(file.LwData.BodyTransformData.Length > 0);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    public void LwData_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.Equal(LwData.TypeFlagPart, file.LwData.TypeFlag);
    }

    // ── MatesList (Assembly only) ──────────────────────────────────

    [Fact]
    public void MatesList_AssemblyFile_ShouldHaveMates()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        Assert.Equal(MatesListData.ExpectedVersion, file.MatesList.StreamVersion);
        Assert.True(file.MatesList.Mates.Count > 0);
    }

    [Fact]
    public void MatesList_Mates_ShouldHaveNames()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        foreach (var mate in file.MatesList.Mates)
        {
            Assert.False(string.IsNullOrEmpty(mate.ClassName));
        }
    }

    // ── Definition ─────────────────────────────────────────────────

    [Fact]
    public void Definition_PartFile_ShouldHavePartType()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.Equal(DefinitionFileType.Part, file.Definition.FileType);
    }

    [Fact]
    public void Definition_DrawingFile_ShouldHaveDrawingType()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing, readOnly: true);
        Assert.Equal(DefinitionFileType.Drawing, file.Definition.FileType);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    public void Definition_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.NotEqual(DefinitionFileType.Unknown, file.Definition.FileType);
    }

    // ── Config-0 ───────────────────────────────────────────────────

    [Fact]
    public void Config0_PartFile_ShouldHaveBodyData()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.Config0.BodyData.Length > 0);
    }

    // ── Content streams ────────────────────────────────────────────

    [Fact]
    public void Preview_PartFile_ShouldHaveImageData()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.NotNull(file.PreviewImage);
        Assert.True(file.PreviewImage!.Length > 0);
    }

    [Fact]
    public void MaterialTree_PartFile_ShouldHaveXml()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.NotNull(file.MaterialTreeXml);
        Assert.Contains("<?xml", file.MaterialTreeXml);
    }

    [Fact]
    public void GhostPartition_PartFile_ShouldHaveData()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.NotNull(file.GhostPartition);
        Assert.True(file.GhostPartition!.Length > 0);
    }

    [Fact]
    public void Partition_PartFile_ShouldHaveData()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.NotNull(file.Partition);
        Assert.True(file.Partition!.Length > 0);
    }

    // ── Roundtrip tests ────────────────────────────────────────────

    [Fact]
    public void CmgrHdr2_RoundTrip_ShouldPreserveValues()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        uint originalTimestamp;

        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalTimestamp = file.CmgrHdr2.Timestamp;
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalTimestamp, file.CmgrHdr2.Timestamp);
            Assert.True(file.CmgrHdr2.IsPart);
        }
    }

    [Fact]
    public void Biography_RoundTrip_ShouldPreservePaths()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        int originalCount;

        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalCount = file.Biography.PathEntries.Count;
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalCount, file.Biography.PathEntries.Count);
        }
    }

    [Fact]
    public void Definition_RoundTrip_ShouldPreserveFileType()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            Assert.Equal(DefinitionFileType.Part, file.Definition.FileType);
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(DefinitionFileType.Part, file.Definition.FileType);
        }
    }

    // ── Conditional presence ───────────────────────────────────────

    [Fact]
    public void MatesList_PartFile_ShouldBeEmpty()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.Empty(file.MatesList.Mates);
    }

    [Fact]
    public void SheetNames_PartFile_ShouldBeEmpty()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.Empty(file.SheetNames.Names);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string ResolveProviderPath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);
        var value = prop!.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }
}
