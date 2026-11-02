using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading and fixed-authoring tests for the measured 2026 planar surface.
/// Only the class/type code and four ordered boundary sketch segments are decoded.
/// </summary>
public class SurfacePlanarFeatureTests
{
    [Theory]
    [InlineData("surf_planar_region")]
    [InlineData("surf_planar_region_resave")]
    public void SurfacePlanarSample_DecodesBoundingEntities(string name)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "SurfacePlanarRegion"));
        Assert.Equal(FeatureKind.PlanarSurface, feature.Kind);
        var definition = Assert.IsType<PlanarSurfaceFeatureDefinitionData>(feature.Definition);

        Assert.Equal("moPlanarSurface_c", definition.ClassName);
        Assert.Equal(0x4Au, definition.SerializedTypeId);
        var bounding = Assert.Single(definition.BoundingEntities!);
        Assert.Equal(SelectionEntityType.Sketch, bounding.EntityType);
        Assert.Equal("草图1", bounding.Name);
        Assert.Equal(1, definition.BoundingEntitiesCount);

        Assert.Equal(4, definition.BoundarySegmentsCount);
        Assert.Equal(new uint[] { 3, 2, 1, 4 }, definition.BoundarySegments!.Select(e => e.EntityId).ToArray());
        Assert.All(definition.BoundarySegments!, e => Assert.Equal(SelectionEntityType.Edge, e.EntityType));
        Assert.True(definition.Resolution.BoundingEntities);
        Assert.True(definition.Resolution.BoundarySegments);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
    }

    [Fact]
    public void SurfacePlanarParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildSurfacePlanarStream(new byte[64]);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal("moPlanarSurface_c", folder.ClassName);
        Assert.Null(folder.FeatureData);

        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moPlanarSurface_c");
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void SurfacePlanarParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("surf_planar_region"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moPlanarSurface_c");

        Assert.NotNull(SurfacePlanarFeatureParser.Parse(element));
        Assert.Equal(before, stream);
    }

    [Theory]
    [InlineData("surf_planar_region", "Primary")]
    [InlineData("surf_planar_region_resave", "Resave")]
    public void SurfacePlanarAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        var template = Template(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddPlanarSurface();
            byte[] authored = part.GetStreamBytes(streamPath)!;

            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            for (int i = 0; i < stripped.Length; i++)
            {
                if (i == 4 || i == 5 ||
                    (i >= template.MarkerOffset + 4 && i < template.MarkerOffset + 8))
                    continue;
                Assert.Equal(stripped[i], authored[i]);
            }
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            Assert.Equal(template.MarkerValue, BitConverter.ToUInt32(authored, template.MarkerOffset + 4));
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            AssertDefinition(feature);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "SurfacePlanarRegion"));
        Assert.Equal(FeatureKind.PlanarSurface, reFeature.Kind);
        AssertDefinition(reFeature);
    }

    [Fact]
    public void SurfacePlanarAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_planar_region"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddPlanarSurface(featureName: "MyPlanar"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddPlanarSurface());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void SurfacePlanarAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_planar_region"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddPlanarSurface());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SurfacePlanarAuthoringTemplate Template(string variant) =>
        SurfacePlanarAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, SurfacePlanarAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        BitConverter.GetBytes((ushort)template.StrippedFolderCount).CopyTo(stripped, 4);
        BitConverter.GetBytes(0u).CopyTo(stripped, template.MarkerOffset + 4);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static void AssertDefinition(Feature feature)
    {
        var definition = Assert.IsType<PlanarSurfaceFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moPlanarSurface_c", definition.ClassName);
        Assert.Equal(0x4Au, definition.SerializedTypeId);
        Assert.Equal(1, definition.BoundingEntitiesCount);
        Assert.Equal(4, definition.BoundarySegmentsCount);
        Assert.True(definition.Resolution.BoundingEntities);
        Assert.True(definition.Resolution.BoundarySegments);
    }

    private static byte[] BuildSurfacePlanarStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moPlanarSurface_c");
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x13 });
        bytes.AddRange(Encoding.Unicode.GetBytes("SurfacePlanarRegion"));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
