using SldFile;

namespace SldFileTests;

public class TrivialStreamsParsingTests
{
    // ── DLUpdateStamp ────────────────────────────────────────────────

    [Fact]
    public void DlUpdateStamp_PartFile_ShouldHaveTypeFlag()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.DlUpdateStamp.TypeFlag != 0);
        Assert.Equal((ushort)0, file.DlUpdateStamp.ZeroPad);
    }

    [Fact]
    public void DlUpdateStamp_DrawingFile_ShouldHaveTypeFlag()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing, readOnly: true);
        Assert.True(file.DlUpdateStamp.TypeFlag != 0);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    public void DlUpdateStamp_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.True(file.DlUpdateStamp.TypeFlag != 0);
    }

    // ── VisualStates_DisplayPane ─────────────────────────────────────

    [Fact]
    public void VisualStatesDisplayPane_PartFile_ShouldHaveDisplayValue()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.True(file.VisualStatesDisplayPane.DisplayValue != 0);
        Assert.Equal(0u, file.VisualStatesDisplayPane.ZeroPad);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    public void VisualStatesDisplayPane_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.True(file.VisualStatesDisplayPane.DisplayValue != 0);
    }

    // ── CMMotionLoadMapU ─────────────────────────────────────────────

    [Fact]
    public void CmMotionLoadMapU_PartFile_ShouldHaveName()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        Assert.Equal(1u, file.CmMotionLoadMapU.Version);
        Assert.Contains("CM_MOTION_LOAD_1", file.CmMotionLoadMapU.Name);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    public void CmMotionLoadMapU_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SwFile.Open(filePath, readOnly: true);
        Assert.Contains("CM_MOTION_LOAD_1", file.CmMotionLoadMapU.Name);
    }

    // ── SheetNames (Drawing only) ────────────────────────────────────

    [Fact]
    public void SheetNames_DrawingFile_ShouldHaveSheetNames()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing, readOnly: true);
        Assert.True(file.SheetNames.Names.Count > 0);
        Assert.Contains("Sheet1", file.SheetNames.Names);
    }

    // ── Roundtrip ────────────────────────────────────────────────────

    [Fact]
    public void DlUpdateStamp_RoundTrip_ShouldPreserveValues()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        uint originalTypeFlag;

        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalTypeFlag = file.DlUpdateStamp.TypeFlag;
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalTypeFlag, file.DlUpdateStamp.TypeFlag);
        }
    }

    private static string ResolveProviderPath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);
        var value = prop!.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }
}
