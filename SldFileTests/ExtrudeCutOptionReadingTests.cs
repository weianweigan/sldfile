using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading coverage for the measured version-0x71 extrusion options: direction-1 draft,
/// direction-2 draft with direction-2 depth, and the Merge flag (1 in native merge=true samples,
/// 0 in extrude_merge_false). DraftOutward has no positive sample and is never claimed.
/// </summary>
public class ExtrudeCutOptionReadingTests
{
    private static string SamplePath(string sample)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");
        return File.Exists(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", sample + ".SLDPRT");
    }

    private static ExtrudeFeatureDefinitionData ReadCut(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Cut));
        return Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
    }

    [Theory]
    [InlineData("extrude_draft_outward_5deg")]
    [InlineData("extrude_draft_outward_5deg_resave")]
    public void DirectionOneDraftCut_ReadsAngleAndResolution(string sample)
    {
        var definition = ReadCut(sample);
        double angle = 5.0 * Math.PI / 180.0;

        Assert.Equal(0.010, definition.Depth, 12);
        Assert.True(definition.DraftWhileExtruding);
        Assert.Equal(angle, definition.DraftAngle, 12);
        Assert.False(definition.BothDirections);
        Assert.Equal(0, definition.DepthDirection2, 12);
        Assert.False(definition.DraftWhileExtrudingDirection2);
        Assert.Equal(0, definition.DraftAngleDirection2, 12);
        // The native sample was created with DraftOutward=false and reads back false; no field is claimed.
        Assert.False(definition.DraftOutward);
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftOutward)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtrudingDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Depth)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtruding)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftAngle)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
        Assert.True(definition.Merge);
    }

    [Theory]
    [InlineData("extrude_draft_dir2_3deg")]
    [InlineData("extrude_draft_dir2_3deg_resave")]
    public void DirectionTwoDraftCut_ReadsReverseDepthAndDraft(string sample)
    {
        var definition = ReadCut(sample);
        double angle = 3.0 * Math.PI / 180.0;

        Assert.Equal(0.010, definition.Depth, 12);
        Assert.True(definition.BothDirections);
        Assert.Equal(0.005, definition.DepthDirection2, 12);
        Assert.Equal(0u, definition.EndConditionDirection2);
        Assert.False(definition.DraftWhileExtruding);
        Assert.Equal(0, definition.DraftAngle, 12);
        Assert.True(definition.DraftWhileExtrudingDirection2);
        Assert.Equal(angle, definition.DraftAngleDirection2, 12);
        Assert.False(definition.DraftOutwardDirection2);
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftOutwardDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.BothDirections)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DepthDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.EndConditionDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtrudingDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftAngleDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
        Assert.True(definition.Merge);
    }

    [Theory]
    [InlineData("extrude_merge_false", false)]
    [InlineData("extrude_merge_false_resave", false)]
    [InlineData("extrude_cut_5mm", true)]
    public void MergeFlag_ReadsMeasuredEndFaceBoolean(string sample, bool expected)
    {
        var definition = ReadCut(sample);
        Assert.Equal(expected, definition.Merge);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
        Assert.Equal(sample.StartsWith("extrude_merge_false") ? 0.010 : 0.005, definition.Depth, 12);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Depth)));
    }

    [Fact]
    public void ThroughAllCutWithoutEndFaceRecord_LeavesMergeUnresolved()
    {
        var definition = ReadCut("extrude_flip_side_cut");
        Assert.True(definition.Merge); // model default, not evidence
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Depth)));
    }

    private static ExtrudeFeatureDefinitionData ApplyToMutatedStream(
        string sample, Action<byte[], MfcDocument> mutate)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        mutate(stream, doc);
        var cut = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moICE_c"));
        var reparsed = MfcDocument.Parse(stream);
        var definition = new ExtrudeFeatureDefinitionData();
        ExtrudeCutOptionParser.Apply(reparsed, BitConverter.ToUInt16(stream, 0),
            "moICE_c", cut.TagOffset, stream.Length, definition);
        return definition;
    }

    [Fact]
    public void DraftEnableFlagIsRequired()
    {
        var forward = ApplyToMutatedStream("extrude_draft_outward_5deg", (stream, doc) =>
        {
            var display = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moDisplayAngularDim_c"));
            BitConverter.GetBytes(0u).CopyTo(stream, display.TagOffset - 24);
        });
        Assert.False(forward.DraftWhileExtruding);
        Assert.False(forward.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtruding)));

        var reverse = ApplyToMutatedStream("extrude_draft_dir2_3deg", (stream, doc) =>
        {
            var display = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moDisplayAngularDim_c"));
            BitConverter.GetBytes(0u).CopyTo(stream, display.TagOffset - 22);
        });
        Assert.False(reverse.DraftWhileExtrudingDirection2);
        Assert.False(reverse.BothDirections);
        Assert.False(reverse.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtrudingDirection2)));
    }

    [Fact]
    public void DraftAngleNameAndValueAreValidated()
    {
        var renamed = ApplyToMutatedStream("extrude_draft_outward_5deg", (stream, doc) =>
        {
            var parameter = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moAngleParameter_c"));
            stream[parameter.DataStart + 2 + 4 + 3] = (byte)'9'; // D3 -> D9 in the WChar
        });
        Assert.False(renamed.DraftWhileExtruding);

        var invalid = ApplyToMutatedStream("extrude_draft_dir2_3deg", (stream, doc) =>
        {
            var parameter = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moAngleParameter_c"));
            BitConverter.GetBytes(double.NaN).CopyTo(stream, parameter.DataStart + 32);
        });
        Assert.False(invalid.DraftWhileExtrudingDirection2);
        Assert.False(invalid.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftAngleDirection2)));
    }

    [Fact]
    public void MergeFlagOutsideZeroOrOneIsRejected()
    {
        var definition = ApplyToMutatedStream("extrude_merge_false", (stream, doc) =>
        {
            var endFace = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEndFace3IntSurfIdRep_c"));
            BitConverter.GetBytes(2u).CopyTo(stream, endFace.DataStart + 14);
        });
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
        Assert.True(definition.Merge); // default only; no resolved value was promoted
    }

    [Fact]
    public void MergeReferenceHeaderIsValidated()
    {
        var definition = ApplyToMutatedStream("extrude_cut_5mm", (stream, doc) =>
        {
            var endFace = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEndFace3IntSurfIdRep_c"));
            stream[endFace.DataStart + 6] = 0x4F; // break the measured TypeId 0x56 reference header
        });
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Merge)));
    }
}
