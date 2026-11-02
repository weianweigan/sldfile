using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the measured 2026 Draft sample. Only the 3-degree angle and the
/// neutral-plane face reference are decoded; type/alignment/other options have no
/// controlled variation and stay unset.
/// </summary>
public class DraftFeatureReadingTests
{
    [Theory]
    [InlineData("draft_side_3deg_neutral_bottom")]
    [InlineData("draft_side_3deg_neutral_bottom_resave")]
    public void DraftSample_DecodesAngleAndNeutralPlane(string name)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "Draft3deg"));
        Assert.Equal(FeatureKind.Draft, feature.Kind);
        var definition = Assert.IsType<DraftFeatureDefinitionData>(feature.Definition);

        Assert.Equal("moDraft_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(0x51u, definition.TypeId);
        Assert.Equal(3.0 * Math.PI / 180.0, definition.Angle!.Value, 12);

        var neutral = Assert.IsType<EntityReference>(definition.NeutralPlane);
        Assert.Equal(SelectionEntityType.Face, neutral.EntityType);
        Assert.True(neutral.EntityId > 0);
        Assert.StartsWith("moCompFace_c:", neutral.Name);

        Assert.Null(definition.Type);
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.AllowReducedAngle);
        Assert.Null(definition.DirectionPull);
        Assert.Null(definition.FacesToDraft);
        Assert.Null(definition.PartingLines);
        Assert.Null(definition.FacePropagation);
        Assert.Null(definition.StepType);

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.Angle);
        Assert.True(definition.Resolution.NeutralPlane);
        Assert.False(definition.Resolution.Type);
        Assert.False(definition.Resolution.ReverseDirection);
        Assert.False(definition.Resolution.AllowReducedAngle);
        Assert.False(definition.Resolution.FacesToDraft);
        Assert.False(definition.Resolution.PartingLines);
        Assert.False(definition.Resolution.FacePropagation);
        Assert.False(definition.Resolution.StepType);
    }

    [Fact]
    public void DraftParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildDraftStream(new byte[64]);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal("moDraft_c", folder.ClassName);
        Assert.Null(folder.FeatureData);

        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moDraft_c");
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void DraftParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("draft_side_3deg_neutral_bottom"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moDraft_c");

        Assert.NotNull(DraftFeatureParser.Parse(element));
        Assert.Equal(before, stream);
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/DraftOptions", name + ".SLDPRT");

    private static byte[] BuildDraftStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moDraft_c");
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x15, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x09 });
        bytes.AddRange(Encoding.Unicode.GetBytes("Draft3deg"));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
