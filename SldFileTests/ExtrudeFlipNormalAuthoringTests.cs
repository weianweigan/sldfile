using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Measured flip-side and normal-cut layouts: exact-tail recognition for reading and fixed template
/// authoring on the source/resave stripped bases. Flags are carried by the complete native block.
/// </summary>
public class ExtrudeFlipNormalAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, int strippedFolders, out byte[] nativeStream)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            nativeStream = raw;
            var doc = MfcDocument.Parse(raw);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moICE_c"));
            int tag = feature.TagOffset;
            byte[] stripped = new byte[tag];
            Array.Copy(raw, 0, stripped, 0, tag);
            BitConverter.GetBytes((ushort)strippedFolders).CopyTo(stripped, 4);
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

    private static ExtrudeFeatureModel Flip() => new()
    {
        IsCut = true,
        FlipSideToCut = true,
        EndCondition = 1,
        Depth = 0,
        Merge = true
    };

    private static ExtrudeFeatureModel Normal() => new()
    {
        IsCut = true,
        NormalCut = true,
        EndCondition = 1,
        Depth = 0,
        Merge = true
    };

    private static ExtrudeFeatureModel ReadCut(SwFile file)
    {
        var feature = Assert.Single(file.Features.Where(f => f.TypeName == "moICE_c"
            && f.Model is ExtrudeFeatureModel));
        return Assert.IsType<ExtrudeFeatureModel>(feature.Model);
    }

    [Theory]
    [InlineData("extrude_flip_side_cut", true, false)]
    [InlineData("extrude_flip_side_cut_resave", true, false)]
    [InlineData("extrude_normal_cut", false, true)]
    [InlineData("extrude_normal_cut_resave", false, true)]
    public void NativeSamplesRecognizeFlipOrNormalFlag(string sample, bool flip, bool normal)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var definition = ReadCut(file);
        Assert.Equal(flip, definition.FlipSideToCut);
        Assert.Equal(normal, definition.NormalCut);
        if (flip) Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.FlipSideToCut)));
        if (normal) Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.NormalCut)));
    }

    [Theory]
    [InlineData("extrude_flip_side_cut", 21, true)]
    [InlineData("extrude_flip_side_cut_resave", 21, true)]
    [InlineData("extrude_normal_cut", 19, false)]
    [InlineData("extrude_normal_cut_resave", 19, false)]
    public void FixedTemplateAuthoringReproducesTheMeasuredStream(string sample, int strippedFolders, bool flip)
    {
        using var temp = CreateStrippedBase(sample, strippedFolders, out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(flip ? Flip() : Normal());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = ReadCut(reopened);
        Assert.Equal(flip, definition.FlipSideToCut);
        Assert.Equal(!flip, definition.NormalCut);

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
    public void InvalidFlagCombinationsAndParametersAreRejected()
    {
        using var temp = CreateStrippedBase("extrude_flip_side_cut", 21, out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var both = Flip();
        both.NormalCut = true;
        Assert.Contains("exactly one",
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(both)).Message);

        var depth = Flip();
        depth.Depth = 0.010;
        Assert.Contains("Depth", Assert.Throws<NotSupportedException>(() => part.AddExtrude(depth)).Message);

        var end = Flip();
        end.EndCondition = 0;
        Assert.Contains("EndCondition", Assert.Throws<NotSupportedException>(() => part.AddExtrude(end)).Message);

        var name = Assert.Throws<NotSupportedException>(
            () => part.AddExtrude(Flip(), featureName: "CustomFlip"));
        Assert.Contains("FlipSideCut", name.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void WrongBaseFolderMarkerOrVersionIsRejected()
    {
        using var source = CreateStrippedBase("extrude_flip_side_cut", 21, out _);
        using (var sw = SwFile.Open(source.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Normal()));
            Assert.Contains("stripped base length", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var folder = CreateStrippedBase("extrude_flip_side_cut", 21, out _);
        using (var sw = SwFile.Open(folder.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            BitConverter.GetBytes((ushort)20).CopyTo(raw, 4);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using (var sw = SwFile.Open(folder.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Flip()));
            Assert.Contains("stripped base", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var marker = CreateStrippedBase("extrude_flip_side_cut", 21, out _);
        using (var sw = SwFile.Open(marker.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            BitConverter.GetBytes(1u).CopyTo(raw, raw.Length - 0x10 + 4);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using (var sw = SwFile.Open(marker.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Flip()));
            Assert.Contains("0x67", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }
}
