using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed-template authoring for the Paper Airplane tutorial BaseFlange custom
/// bend table. The path is patched in-place with a same-length name and native
/// reopen reports the new SheetMetal/BaseFlange BendTableFile.
/// </summary>
public class TutorialBendTableAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x630F;
    private const string MeasuredPath =
        @"C:\Program Files\SolidWorks2012\SolidWorks\lang\english\Sheetmetal Bend Tables\base bend table.xls";
    private const string PatchedPath =
        @"C:\Program Files\SolidWorks2012\SolidWorks\lang\english\Sheetmetal Bend Tables\bend table copy.xls";

    private static string SamplePath()
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "TutorialSheetMetal", "Paper Airplane.SLDPRT");
        if (File.Exists(direct)) return direct;
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026",
                "TutorialSheetMetal", "Paper Airplane.SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException("Paper Airplane.SLDPRT not found");
    }

    private static TempTestFile CreateStrippedBase()
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[PrefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    [Fact]
    public void AuthoringRoundTripsPatchedPath()
    {
        Assert.Equal(MeasuredPath.Length, PatchedPath.Length);
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddTutorialBaseFlangeBendTable(PatchedPath);
            part.Save();
            Assert.Equal("Base-Flange1", feature.Name);
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.True(definition.ParameterResolution.BendTableFile);
        Assert.Equal(PatchedPath, definition.BendTableFile);

        byte[] before;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStream].Clone();
            second.Save();
        }
        using var resaved = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, resaved.RawStreams[ResolvedStream]);
        Assert.Equal(PatchedPath, Assert.IsType<SheetMetalBaseFlangeModel>(
            resaved.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c")
                .FeatureData).BendTableFile);
    }

    [Fact]
    public void DefaultAuthoringMatchesNativeSource()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddTutorialBaseFlangeBendTable(MeasuredPath);
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void WrongLengthPathWrongBaseAndTamperingAreRejected()
    {
        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddTutorialBaseFlangeBendTable(@"C:\temp\short.xls"));
            Assert.Contains("same-length path", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath()))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddTutorialBaseFlangeBendTable(PatchedPath));
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x100] ^= 0x01;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddTutorialBaseFlangeBendTable(PatchedPath));
                Assert.Contains("normalized prefix SHA-256", error.Message);
            }
        }
    }
}
