using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;

namespace SldFileTests;

/// <summary>
/// Reading tests for the native EndCap and cosmetic weld-bead samples.
/// The cosmetic bead is nested under the weld cut-list folder, not a top-level node.
/// </summary>
public class WeldmentFeatureReadingTests
{
    [Theory]
    [InlineData("endcap_t2_structural_2", false, false)]
    [InlineData("endcap_t2_structural_resave_2", false, false)]
    [InlineData("endcap_t2_structural_chamfer", true, true)]
    [InlineData("endcap_t2_structural_chamfer_resave", true, true)]
    public void EndCap_ParsesThicknessAndFlags(string sample, bool chamfer, bool cornerTreatment)
    {
        using var file = SwFile.Open(Resolve(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is EndCapFeatureDefinitionData));
        Assert.Equal(FeatureKind.EndCap, feature.Kind);
        var definition = Assert.IsType<EndCapFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moEndCap_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(0.002, definition.Thickness!.Value, 12);
        Assert.Equal(chamfer, definition.UseChamferCorners);
        Assert.Equal(cornerTreatment, definition.UseCornerTreatment);
        if (chamfer) Assert.Equal(0.001, definition.ChamferDistance!.Value, 12);
        else Assert.Null(definition.ChamferDistance);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.Resolution.UseChamferCorners);
        Assert.True(definition.Resolution.UseCornerTreatment);
        Assert.Equal(chamfer, definition.Resolution.ChamferDistance);
    }

    [Fact]
    public void EndCap_ChamferDistanceRejectsTamperedCopy()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("endcap_t2_structural_chamfer"));
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stream = (byte[])part.GetStreamBytes(streamPath)!.Clone();
            var handle = SldFile.Mfc.MfcPrimitives.ScanMfcTags(stream)
                .First(t => t.ClassName == "moFeatureDimHandle_c");
            int pos = handle.TagOffset + 0xF05;
            BitConverter.GetBytes(0.002).CopyTo(stream, pos);
            part.SetPendingStream(streamPath, stream);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<EndCapFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Definition is EndCapFeatureDefinitionData)).Definition);
        Assert.Null(definition.ChamferDistance);
        Assert.False(definition.Resolution.ChamferDistance);
        Assert.Equal(0.002, definition.Thickness!.Value, 12);
        Assert.True(definition.UseChamferCorners);
    }

    [Theory]
    [InlineData("weldbead_edges")]
    [InlineData("weldbead_edges_resave")]
    public void CosmeticWeldBead_ParsesNestedBeadSizeAndEdge(string sample)
    {
        using var file = SwFile.Open(Resolve(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is WeldBeadFeatureDefinitionData));
        Assert.Equal(FeatureKind.WeldBead, feature.Kind);
        var definition = Assert.IsType<WeldBeadFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moCosmeticWeldBead_c", definition.ClassName);
        Assert.Equal(0x50u, definition.SerializedTypeId);
        Assert.Equal(0.003, definition.BeadSize!.Value, 12);
        var edge = Assert.Single(definition.EdgeReferences!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.True(edge.EntityId > 0);
        Assert.True(definition.Resolution.BeadSize);
        Assert.True(definition.Resolution.EdgeReferences);
        // No controlled variation exists for Side.
        Assert.Null(definition.Side);
        Assert.False(definition.Resolution.Side);
        Assert.Null(definition.BeadLengths);
        Assert.Null(definition.BeadTypes);
    }

    private static string Resolve(string sample) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", sample + ".SLDPRT");
}
