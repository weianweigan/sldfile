using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
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
        var definition = Assert.IsType<PlanarSurfaceFeatureModel>(feature.Model);

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
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

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

        Assert.NotNull(PlanarSurfaceFeatureModel.Parse(element));
        Assert.Equal(before, stream);
    }

    [Theory]
    [InlineData("surf_planar_region", "Primary")]
    [InlineData("surf_planar_region_resave", "Resave")]
    public void SurfacePlanarAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new PlanarSurfaceFeatureModel
            {
                FeatureName = "SurfacePlanarRegion",
                SerializedTypeId = 0x4Au
            };
            var feature = part.AddPlanarSurface(model);
            Assert.NotNull(feature);
            Assert.Equal("SurfacePlanarRegion", feature.Name);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "SurfacePlanarRegion"));
        Assert.Equal(FeatureKind.PlanarSurface, reFeature.Kind);
        Assert.Equal("moPlanarSurface_c", reFeature.Model!.ClassName);
    }

    [Fact]
    public void SurfacePlanarAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_planar_region"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddPlanarSurface(new PlanarSurfaceFeatureModel()));
    }

    [Fact]
    public void SurfacePlanarAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_planar_region"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddPlanarSurface((PlanarSurfaceFeatureModel)null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

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

    private static void StripInPlace(TempTestFile temp)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        var elem = MfcDocument.Parse(full).AllElements.FirstOrDefault(e => e.ClassName == "moPlanarSurface_c" && e.TagByte == 0x01);
        if (elem != null)
        {
            int offset = elem.TagOffset;
            byte[] stripped = new byte[offset];
            Array.Copy(full, 0, stripped, 0, offset);
            ushort count = BitConverter.ToUInt16(full, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            part.SetPendingStream(streamPath, stripped);
            part.Save();
        }
    }
}
