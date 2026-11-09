using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Template authoring for the measured OneDirection thin extrude (one-dir and CapEnds blocks).
/// Authoring is limited to the exact native stripped base; Depth and WallThickness are patched, while
/// CapEnds selects its own measured block. MidPlane/TwoDirection remain rejected.
/// </summary>
public class ThinExtrudeAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeStream)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            nativeStream = raw;
            var doc = MfcDocument.Parse(raw);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtruThin_c"));
            int tag = feature.TagOffset;
            byte[] stripped = new byte[tag];
            Array.Copy(raw, 0, stripped, 0, tag);
            BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(stripped, 4) - 1)).CopyTo(stripped, 4);
            int marker = tag - 0x10;
            Assert.Equal(0x67, stripped[marker]);
            BitConverter.GetBytes(0u).CopyTo(stripped, marker + 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using var check = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.True(check.RawStreams[ResolvedStream].Length < nativeStream.Length);
        return temp;
    }

    private static ExtrudeFeatureModel Definition(bool capEnds, double depth = 0.010, double wall = 0.001)
    {
        return new ExtrudeFeatureModel
        {
            IsThinFeature = true,
            ThinWallType = 0,
            Depth = depth,
            WallThickness = wall,
            WallThicknessDirection2 = 0,
            CapEnds = capEnds,
            Merge = true
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeValuesReproduceTheMeasuredThinFile(bool capEnds)
    {
        string sample = capEnds ? "extrude_thin_cap_1mm" : "extrude_thin_one_dir_1mm";
        using var temp = CreateStrippedBase(sample, out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddExtrude(Definition(capEnds));
            Assert.Equal(FeatureKind.Extrusion, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        Assert.True(definition.IsThinFeature);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(0.001, definition.WallThickness, 12);
        Assert.Equal(capEnds, definition.CapEnds);
    }

    [Fact]
    public void DepthAndWallThicknessArePatchedAndSurviveResave()
    {
        using var temp = CreateStrippedBase("extrude_thin_one_dir_1mm", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(false, 0.020, 0.002));
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        Assert.Equal(0.020, definition.Depth, 12);
        Assert.Equal(0.002, definition.WallThickness, 12);
        Assert.True(definition.IsThinFeature);
        Assert.Equal(0u, definition.ThinWallType);
        Assert.False(definition.CapEnds);
        Assert.NotEqual(nativeStream, reopened.RawStreams[ResolvedStream]);

        byte[] before;
        using (var resave = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])resave.RawStreams[ResolvedStream].Clone();
            resave.Save();
        }
        using var after = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, after.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void CapTemplateMustMatchItsOwnStrippedBase()
    {
        using var temp = CreateStrippedBase("extrude_thin_one_dir_1mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();
        var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(true)));
        Assert.Contains("signature", error.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void MidPlaneNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("extrude_thin_midplane_1mm", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(new ExtrudeFeatureModel
            {
                IsThinFeature = true,
                ThinWallType = 2,
                Depth = 0.010,
                WallThickness = 0.001,
                Merge = true
            });
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        Assert.Equal(2u, definition.ThinWallType);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(0.001, definition.WallThickness, 12);
    }

    [Fact]
    public void TwoDirectionNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("extrude_thin_two_dir_1_2mm", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(new ExtrudeFeatureModel
            {
                IsThinFeature = true,
                ThinWallType = 3,
                Depth = 0.010,
                DepthDirection2 = 0.005,
                WallThickness = 0.001,
                WallThicknessDirection2 = 0.002,
                BothDirections = true,
                Merge = true
            });
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        Assert.Equal(3u, definition.ThinWallType);
        Assert.True(definition.BothDirections);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(0.005, definition.DepthDirection2, 12);
        Assert.Equal(0.001, definition.WallThickness, 12);
        Assert.Equal(0.002, definition.WallThicknessDirection2, 12);
    }

    [Fact]
    public void MidPlaneDepthAndWallArePatchedAndSurviveResave()
    {
        using var temp = CreateStrippedBase("extrude_thin_midplane_1mm", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(new ExtrudeFeatureModel
            {
                IsThinFeature = true,
                ThinWallType = 2,
                Depth = 0.020,
                WallThickness = 0.002,
                Merge = true
            });
            part.Save();
        }
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var definition = Assert.IsType<ExtrudeFeatureModel>(
                Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
            Assert.Equal(2u, definition.ThinWallType);
            Assert.Equal(0.020, definition.Depth, 12);
            Assert.Equal(0.002, definition.WallThickness, 12);
        }

        byte[] before;
        using (var resave = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])resave.RawStreams[ResolvedStream].Clone();
            resave.Save();
        }
        using var after = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, after.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void TwoDirectionAllDepthsAndWallsArePatchedAndSurviveResave()
    {
        using var temp = CreateStrippedBase("extrude_thin_two_dir_1_2mm", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(new ExtrudeFeatureModel
            {
                IsThinFeature = true,
                ThinWallType = 3,
                Depth = 0.015,
                DepthDirection2 = 0.004,
                WallThickness = 0.0015,
                WallThicknessDirection2 = 0.0025,
                BothDirections = true,
                Merge = true
            });
            part.Save();
        }
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var definition = Assert.IsType<ExtrudeFeatureModel>(
                Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
            Assert.Equal(3u, definition.ThinWallType);
            Assert.True(definition.BothDirections);
            Assert.Equal(0.015, definition.Depth, 12);
            Assert.Equal(0.004, definition.DepthDirection2, 12);
            Assert.Equal(0.0015, definition.WallThickness, 12);
            Assert.Equal(0.0025, definition.WallThicknessDirection2, 12);
        }

        byte[] before;
        using (var resave = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])resave.RawStreams[ResolvedStream].Clone();
            resave.Save();
        }
        using var after = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, after.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void MidPlaneAndTwoDirectionInvalidCombinationsAreRejected()
    {
        using var midTemp = CreateStrippedBase("extrude_thin_midplane_1mm", out _);
        using (var sw = SwFile.Open(midTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var cap = new ExtrudeFeatureModel
            {
                IsThinFeature = true, ThinWallType = 2, Depth = .01, WallThickness = .001, CapEnds = true, Merge = true
            };
            var capError = Assert.Throws<NotSupportedException>(() => part.AddExtrude(cap));
            Assert.Contains("CapEnds", capError.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var twoTemp = CreateStrippedBase("extrude_thin_two_dir_1_2mm", out _);
        using (var sw = SwFile.Open(twoTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();

            var notBoth = new ExtrudeFeatureModel
            {
                IsThinFeature = true, ThinWallType = 3, Depth = .01, DepthDirection2 = .005,
                WallThickness = .001, WallThicknessDirection2 = .002, Merge = true
            };
            var bothError = Assert.Throws<NotSupportedException>(() => part.AddExtrude(notBoth));
            Assert.Contains("BothDirections", bothError.Message);

            var noDirection2 = new ExtrudeFeatureModel
            {
                IsThinFeature = true, ThinWallType = 3, Depth = .01,
                WallThickness = .001, WallThicknessDirection2 = .002, BothDirections = true, Merge = true
            };
            // AddExtrude performs the generic direction-2 range check before the builder contract.
            var depthError = Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(noDirection2));
            Assert.Contains("DepthDirection2", depthError.Message);

            var noWall2 = new ExtrudeFeatureModel
            {
                IsThinFeature = true, ThinWallType = 3, Depth = .01, DepthDirection2 = .005,
                WallThickness = .001, BothDirections = true, Merge = true
            };
            var wallError = Assert.Throws<NotSupportedException>(() => part.AddExtrude(noWall2));
            Assert.Contains("WallThicknessDirection2", wallError.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void ThinWallTypeOutsideMeasuredTemplatesIsRejected()
    {
        using var temp = CreateStrippedBase("extrude_thin_one_dir_1mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();
        var invalid = Definition(false);
        invalid.ThinWallType = 1;
        var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(invalid));
        Assert.Contains("ThinWallType", error.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void MidPlaneAndTwoDirectionTemplatesRequireTheirOwnBases()
    {
        using var midTemp = CreateStrippedBase("extrude_thin_midplane_1mm", out _);
        using (var sw = SwFile.Open(midTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var two = new ExtrudeFeatureModel
            {
                IsThinFeature = true, ThinWallType = 3, Depth = .01, DepthDirection2 = .005,
                WallThickness = .001, WallThicknessDirection2 = .002, BothDirections = true, Merge = true
            };
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(two));
            Assert.Contains("signature", error.Message);
        }
    }

    [Fact]
    public void CustomNameOrModifiedBaseIsRejectedBeforeMutation()
    {
        using var temp = CreateStrippedBase("extrude_thin_one_dir_1mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var nameError = Assert.Throws<NotSupportedException>(
            () => part.AddExtrude(Definition(false), featureName: "CustomThin"));
        Assert.Contains("Thin_0_0.001", nameError.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ModifiedBaseIsRejectedBeforeMutation()
    {
        using var temp = CreateStrippedBase("extrude_thin_one_dir_1mm", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            raw[100] ^= 0xFF;
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using var part = SwFile.Open(temp.FilePath, readOnly: false);
        var typed = Assert.IsType<SldPart>(part);
        var original = (byte[])typed.RawStreams[ResolvedStream].Clone();
        var error = Assert.Throws<NotSupportedException>(() => typed.AddExtrude(Definition(false)));
        Assert.Contains("signature", error.Message);
        Assert.Equal(original, typed.RawStreams[ResolvedStream]);
    }
}
