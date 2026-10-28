using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class DisplayListsParsingTests
{
    [Fact]
    public void DisplayLists_CubePart_ShouldParse()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var dl = file.DisplayLists;

        // Prefix: 12 f64 values
        Assert.Equal(12, dl.Prefix.Count);
        Assert.Equal(0.1, dl.Prefix[5], precision: 6);
        Assert.Equal(1000.0, dl.Prefix[11], precision: 0);

        // User name
        Assert.NotEmpty(dl.UserName);

        // Line font
        Assert.Equal("Detail", dl.LineFontName);

        // Line font config
        Assert.True(dl.LineFontConfig.Count > 10);
        Assert.Contains(dl.LineFontConfig, e => e.StyleName == "CONTINUOUS");
        Assert.Contains(dl.LineFontConfig, e => e.LineType == "Visible");

        // Lights
        Assert.Equal(1, dl.AmbientLightCount);
        Assert.True(dl.DirectionalLightCount >= 1);

        // Visual properties — verify content presence (WChars may be in anonymous siblings)
        // The ZLB stream contains material-related WChar strings

        // Tess data
        Assert.True(dl.BodyTessDataSize > 0);
        Assert.True(dl.FaceTessDataSize > 0);

        // No assembly-specific data for part files
        Assert.False(dl.HasAssemblyData);
    }

    [Fact]
    public void DisplayLists_Assembly_ShouldHaveAssemblyData()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var dl = file.DisplayLists;

        Assert.True(dl.HasAssemblyData);
        Assert.True(dl.HasPartTessData);
    }

    [Fact]
    public void DisplayLists_CylinderPart_ShouldParse()
    {
        using var file = SwFile.Open(TestFileProvider.CylinderPart, readOnly: true);
        var dl = file.DisplayLists;

        Assert.Equal(12, dl.Prefix.Count);
        Assert.NotEmpty(dl.LineFontConfig);
    }

    [Fact]
    public void DisplayLists_RoundTrip_ShouldPreserveData()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        int originalLineFontCount;
        string originalUserName;
        int originalFaceTessSize;

        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalLineFontCount = file.DisplayLists.LineFontConfig.Count;
            originalUserName = file.DisplayLists.UserName;
            originalFaceTessSize = file.DisplayLists.FaceTessDataSize;
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalLineFontCount, file.DisplayLists.LineFontConfig.Count);
            Assert.Equal(originalUserName, file.DisplayLists.UserName);
            Assert.Equal(originalFaceTessSize, file.DisplayLists.FaceTessDataSize);
        }
    }
}
