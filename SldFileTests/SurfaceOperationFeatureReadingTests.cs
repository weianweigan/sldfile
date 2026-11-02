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
/// Reading tests for the measured 2026 surface operations: knit, extend, offset, fill.
/// Only class/type codes and structurally identified reference wrappers are decoded;
/// numeric/option fields have no controlled variation and stay unset.
/// </summary>
public class SurfaceOperationFeatureReadingTests
{
    [Theory]
    [InlineData("surf_knit_two")]
    [InlineData("surf_knit_two_resave")]
    public void KnitSample_DecodesBodyReference(string name)
    {
        var definition = Assert.IsType<SurfaceKnitFeatureDefinitionData>(ReadDefinition(name, "曲面-缝合2"));
        Assert.Equal("moSewRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Eu, definition.SerializedTypeId);
        var entity = Assert.Single(definition.Entities!);
        Assert.Equal(SelectionEntityType.Body, entity.EntityType);
        Assert.True(entity.EntityId > 0);
        var stitch = Assert.Single(definition.StitchReferences!);
        Assert.Equal(entity.EntityId, stitch.EntityId);
        Assert.Equal(SelectionEntityType.Body, stitch.EntityType);
        Assert.Null(definition.KnitTolerance);
        Assert.Null(definition.UseTryToFormSolid);
        Assert.True(definition.Resolution.Entities);
        Assert.True(definition.Resolution.StitchReferences);
    }

    [Theory]
    [InlineData("surf_extend_5mm")]
    [InlineData("surf_extend_5mm_resave")]
    public void ExtendSample_DecodesEdgeItem(string name)
    {
        var definition = Assert.IsType<SurfaceExtendFeatureDefinitionData>(ReadDefinition(name, "曲面-延伸1"));
        Assert.Equal("moExtendRefSurface_c", definition.ClassName);
        Assert.Equal(0x54u, definition.SerializedTypeId);
        var item = Assert.Single(definition.Items!);
        Assert.Equal(SelectionEntityType.Edge, item.EntityType);
        Assert.True(item.EntityId > 0);
        Assert.Null(definition.Distance);
        Assert.Null(definition.EndCondition);
        Assert.True(definition.Resolution.Items);
    }

    [Theory]
    [InlineData("surf_offset_5mm")]
    [InlineData("surf_offset_5mm_resave")]
    public void OffsetSample_DecodesBodyReference(string name)
    {
        var definition = Assert.IsType<SurfaceOffsetFeatureDefinitionData>(ReadDefinition(name, "曲面-等距1"));
        Assert.Equal("moOffsetRefSurface_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        var entity = Assert.Single(definition.Entities!);
        Assert.Equal(SelectionEntityType.Body, entity.EntityType);
        Assert.True(entity.EntityId > 0);
        var bodyReference = Assert.Single(definition.BodyReferences!);
        Assert.Equal(entity.EntityId, bodyReference.EntityId);
        Assert.Equal(SelectionEntityType.Body, bodyReference.EntityType);
        Assert.Null(definition.Distance);
        Assert.Null(definition.Flip);
        Assert.True(definition.Resolution.Entities);
        Assert.True(definition.Resolution.BodyReferences);
    }

    [Theory]
    [InlineData("surf_fill_region")]
    [InlineData("surf_fill_region_resave")]
    public void FillSample_DecodesClassTypeAndResolutionControl(string name)
    {
        var definition = Assert.IsType<FillSurfaceFeatureDefinitionData>(ReadDefinition(name, "曲面填充1"));
        Assert.Equal("moFillRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Au, definition.SerializedTypeId);
        Assert.Null(definition.Merge);
        Assert.Null(definition.OptimizeSurface);
        Assert.Equal(1, definition.ResolutionControl);
        Assert.Null(definition.TryToFormSolid);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.ResolutionControl);
        Assert.False(definition.Resolution.Merge);
    }

    [Theory]
    [InlineData("surf_knit_two", "Knit")]
    [InlineData("surf_knit_two_resave", "Knit")]
    [InlineData("surf_offset_5mm", "Offset")]
    [InlineData("surf_offset_5mm_resave", "Offset")]
    public void SurfaceOperationBodyReferenceLists_AreStructured(string name, string kind)
    {
        var definition = ReadDefinition(name, kind == "Knit" ? "曲面-缝合2" : "曲面-等距1");
        if (kind == "Knit")
        {
            var knit = Assert.IsType<SurfaceKnitFeatureDefinitionData>(definition);
            var reference = Assert.Single(knit.StitchReferences!);
            Assert.Equal(SelectionEntityType.Body, reference.EntityType);
            Assert.True(reference.EntityId > 0);
            Assert.True(knit.Resolution.StitchReferences);
        }
        else
        {
            var offset = Assert.IsType<SurfaceOffsetFeatureDefinitionData>(definition);
            var reference = Assert.Single(offset.BodyReferences!);
            Assert.Equal(SelectionEntityType.Body, reference.EntityType);
            Assert.True(reference.EntityId > 0);
            Assert.True(offset.Resolution.BodyReferences);
        }
    }

    [Theory]
    [InlineData("surf_fill_boundary10", 1)]
    [InlineData("surf_fill_boundary10_resave", 1)]
    [InlineData("surf_fill_boundary20", 1)]
    [InlineData("surf_fill_boundary20_resave", 1)]
    [InlineData("surf_fill_merge", 1)]
    [InlineData("surf_fill_merge_resave", 1)]
    [InlineData("surf_fill_curvature1", 1)]
    [InlineData("surf_fill_curvature1_resave", 1)]
    [InlineData("surf_fill_res2", 2)]
    [InlineData("surf_fill_res2_resave", 2)]
    public void FillVariants_DecodeResolutionControlFromSingleVariableVariant(
        string name, int resolutionControl)
    {
        var definition = Assert.IsType<FillSurfaceFeatureDefinitionData>(
            ReadDefinition(name, "曲面填充1"));
        Assert.Equal("moFillRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Au, definition.SerializedTypeId);
        Assert.Equal(resolutionControl, definition.ResolutionControl);
        Assert.True(definition.Resolution.ResolutionControl);
        // No variant established these options; they must stay unset.
        Assert.Null(definition.Merge);
        Assert.Null(definition.OptimizeSurface);
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.ReverseSurface);
        Assert.Null(definition.TryToFormSolid);
        Assert.Null(definition.PatchBoundaryCount);
        Assert.Null(definition.PatchBoundary);
        Assert.Null(definition.BoundaryCurvatureControls);
        Assert.False(definition.Resolution.Merge);
        Assert.False(definition.Resolution.OptimizeSurface);
        Assert.False(definition.Resolution.ReverseDirection);
        Assert.False(definition.Resolution.ReverseSurface);
        Assert.False(definition.Resolution.TryToFormSolid);
        Assert.False(definition.Resolution.PatchBoundary);
        Assert.False(definition.Resolution.BoundaryCurvature);
    }

    [Theory]
    [InlineData("moSewRefSurface_c", (ushort)0x72)]
    [InlineData("moExtendRefSurface_c", (ushort)0x70)]
    [InlineData("moOffsetRefSurface_c", (ushort)0x71)]
    [InlineData("moFillRefSurface_c", (ushort)0x70)]
    public void SurfaceOperationParsers_RejectUnknownLayout(string className, ushort version)
    {
        byte[] stream = BuildStream(className, version);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);
        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Null(folder.FeatureData);
    }

    [Fact]
    public void Parsers_DoNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("surf_knit_two"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moSewRefSurface_c");
        Assert.NotNull(SurfaceOperationFeatureParsers.ParseKnit(element));
        Assert.Equal(before, stream);
    }

    private static object ReadDefinition(string name, string featureName)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == featureName));
        return feature.Definition!;
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static byte[] BuildStream(string className, ushort version)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { (byte)(version & 0xFF), (byte)(version >> 8), 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x07 });
        bytes.AddRange(Encoding.Unicode.GetBytes("Feature"));
        bytes.AddRange(new byte[64]);
        return bytes.ToArray();
    }
}
