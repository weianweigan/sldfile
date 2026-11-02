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
/// Presence-level surface-body folder tests. The controlled samples serialize no
/// per-body id in the ResolvedFeatures stream; only the validated folder header is exposed.
/// </summary>
public class SurfaceBodyFolderTests
{
    [Theory]
    [InlineData("surf_extrude_10mm", "SurfaceExtrudeFeatureDefinitionData")]
    [InlineData("surf_extrude_10mm_resave", "SurfaceExtrudeFeatureDefinitionData")]
    [InlineData("surf_revolve_360", "SurfaceRevolveFeatureDefinitionData")]
    [InlineData("surf_revolve_360_resave", "SurfaceRevolveFeatureDefinitionData")]
    [InlineData("surf_planar_region", "PlanarSurfaceFeatureDefinitionData")]
    [InlineData("surf_planar_region_resave", "PlanarSurfaceFeatureDefinitionData")]
    [InlineData("surf_sweep_basic", "SurfaceSweepFeatureDefinitionData")]
    [InlineData("surf_sweep_basic_resave", "SurfaceSweepFeatureDefinitionData")]
    [InlineData("surf_loft_2sections_loft2", "SurfaceLoftFeatureDefinitionData")]
    [InlineData("surf_loft_2sections_loft2_resave", "SurfaceLoftFeatureDefinitionData")]
    public void SurfaceFeature_DecodesSurfaceBodyFolderPresence(
        string sample, string definitionType)
    {
        using var file = SwFile.Open(Resolve(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition != null && f.Definition.GetType().Name == definitionType));
        var definition = feature.Definition!;

        var folder = GetFolder(definition);
        Assert.NotNull(folder);
        Assert.Equal("moSurfaceBodyFolder_c", folder!.ClassName);
        Assert.Equal(0x0Au, folder.SerializedTypeId);
        Assert.Equal(4u, folder.ChildFlag);
        Assert.Equal(2u, folder.SubType);
        Assert.Equal(0x00010000u, folder.VersionFlag);
        Assert.Equal(0x00010000u, folder.HasData);
        Assert.True(folder.MarkerE > 0);
        Assert.True(folder.IsPresent);
        Assert.True(GetResolution(definition));
    }

    [Fact]
    public void SurfaceBodyFolderParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildStream("moSurfaceBodyFolder_c");
        Assert.Null(SurfaceBodyFolderParser.TryParse(stream));
    }

    [Fact]
    public void SurfaceBodyFolderParser_DoesNotModifyStream()
    {
        using var file = SwFile.Open(Resolve("surf_extrude_10mm"), readOnly: true);
        byte[] stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        Assert.NotNull(SurfaceBodyFolderParser.TryParse(stream));
        Assert.Equal(before, stream);
    }

    private static SurfaceBodyFolderReference? GetFolder(IFeatureData definition) =>
        definition switch
        {
            SurfaceExtrudeFeatureDefinitionData d => d.SurfaceBodyFolder,
            SurfaceRevolveFeatureDefinitionData d => d.SurfaceBodyFolder,
            PlanarSurfaceFeatureDefinitionData d => d.SurfaceBodyFolder,
            SurfaceSweepFeatureDefinitionData d => d.SurfaceBodyFolder,
            SurfaceLoftFeatureDefinitionData d => d.SurfaceBodyFolder,
            _ => null
        };

    private static bool GetResolution(IFeatureData definition) =>
        definition switch
        {
            SurfaceExtrudeFeatureDefinitionData d => d.Resolution.SurfaceBodyFolder,
            SurfaceRevolveFeatureDefinitionData d => d.Resolution.SurfaceBodyFolder,
            PlanarSurfaceFeatureDefinitionData d => d.Resolution.SurfaceBodyFolder,
            SurfaceSweepFeatureDefinitionData d => d.Resolution.SurfaceBodyFolder,
            SurfaceLoftFeatureDefinitionData d => d.Resolution.SurfaceBodyFolder,
            _ => false
        };

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static byte[] BuildStream(string className)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x07 });
        bytes.AddRange(Encoding.Unicode.GetBytes("Feature"));
        bytes.AddRange(new byte[64]);
        return bytes.ToArray();
    }
}
