using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Template authoring for the measured 0x71 cut layouts: direction-2 draft (D4 angle patched) and
/// Merge=false multi-body ICE feature (measured geometry fixed). Both require their exact native
/// stripped base and measured feature name.
/// </summary>
public class ExtrudeCutTemplateAuthoringTests
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
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moICE_c"));
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

    private static ExtrudeFeatureDefinitionData Draft(double angle = 3.0 * Math.PI / 180.0)
    {
        return new ExtrudeFeatureDefinitionData
        {
            IsCut = true,
            BothDirections = true,
            Depth = 0.010,
            DepthDirection2 = 0.005,
            DraftWhileExtrudingDirection2 = true,
            DraftAngleDirection2 = angle,
            EndCondition = 0,
            EndConditionDirection2 = 0,
            Merge = true
        };
    }

    private static ExtrudeFeatureDefinitionData MergeFalse()
    {
        return new ExtrudeFeatureDefinitionData
        {
            IsCut = true,
            Merge = false,
            Depth = 0.010,
            EndCondition = 0
        };
    }

    [Fact]
    public void Direction2DraftNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("extrude_draft_dir2_3deg", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Draft());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Cut)).Definition);
        Assert.True(definition.IsCut);
        Assert.True(definition.BothDirections);
        Assert.True(definition.DraftWhileExtrudingDirection2);
        Assert.Equal(3.0 * Math.PI / 180.0, definition.DraftAngleDirection2, 12);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(0.005, definition.DepthDirection2, 12);
        Assert.False(definition.DraftOutwardDirection2);
    }

    [Fact]
    public void Direction2DraftAngleIsPatchedAndSurvivesResave()
    {
        using var temp = CreateStrippedBase("extrude_draft_dir2_3deg", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Draft(5.0 * Math.PI / 180.0));
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Cut)).Definition);
        Assert.Equal(5.0 * Math.PI / 180.0, definition.DraftAngleDirection2, 12);
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
    public void MergeFalseNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("extrude_merge_false", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(MergeFalse());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Cut)).Definition);
        Assert.True(definition.IsCut);
        Assert.False(definition.Merge);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));

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
    public void DraftTemplateRejectsUnsupportedParametersAndWrongBase()
    {
        using var temp = CreateStrippedBase("extrude_draft_dir2_3deg", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var noBoth = Draft();
        noBoth.BothDirections = false;
        Assert.Contains("BothDirections",
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(noBoth)).Message);

        var noEnable = Draft();
        noEnable.DraftWhileExtrudingDirection2 = false;
        Assert.Contains("DraftWhileExtrudingDirection2",
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(noEnable)).Message);

        var badAngle = Draft();
        badAngle.DraftAngleDirection2 = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(badAngle));

        var badDepth = Draft();
        badDepth.Depth = 0.020;
        Assert.Contains("Depth", Assert.Throws<NotSupportedException>(() => part.AddExtrude(badDepth)).Message);

        var badDepth2 = Draft();
        badDepth2.DepthDirection2 = 0.004;
        Assert.Contains("DepthDirection2",
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(badDepth2)).Message);

        var nameError = Assert.Throws<NotSupportedException>(
            () => part.AddExtrude(Draft(), featureName: "CustomDraft"));
        Assert.Contains("DraftDir2", nameError.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void MergeFalseTemplateRejectsBossDepthChangesDraftAndWrongBase()
    {
        using var mergeTemp = CreateStrippedBase("extrude_merge_false", out _);
        using (var sw = SwFile.Open(mergeTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var deeper = MergeFalse();
            deeper.Depth = 0.020;
            Assert.Contains("Depth", Assert.Throws<NotSupportedException>(() => part.AddExtrude(deeper)).Message);

            var draft = MergeFalse();
            draft.DraftWhileExtruding = true;
            draft.DraftAngle = 0.1;
            Assert.Contains("DraftWhileExtruding",
                Assert.Throws<NotSupportedException>(() => part.AddExtrude(draft)).Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var draftTemp = CreateStrippedBase("extrude_draft_dir2_3deg", out _);
        using (var sw = SwFile.Open(draftTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(MergeFalse()));
            Assert.Contains("stripped base", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }
}
