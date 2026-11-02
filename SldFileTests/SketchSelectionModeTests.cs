using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class SketchSelectionModeTests
{
    [Theory]
    [InlineData("whole", SketchSelectionMode.WholeSketch, 0, 2, 3e-6)]
    [InlineData("whole_resave", SketchSelectionMode.WholeSketch, 0, 2, 3e-6)]
    [InlineData("contour", SketchSelectionMode.ExplicitSelection, 1, 1, 1e-6)]
    [InlineData("region", SketchSelectionMode.ExplicitSelection, 1, 1, 2e-6)]
    public void ControlledSamples_DistinguishStrategyAndDecodeSelectionEnvelope(
        string name, SketchSelectionMode expectedMode, int nativeCount, int nativeBodies, double nativeVolume)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes");
        string path = Path.Combine(directory, name + ".SLDPRT");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "native-manifest.json")));
        var evidence = manifest.RootElement.GetProperty("observations").EnumerateArray()
            .Single(e => e.GetProperty("observation").GetProperty("file").GetString() == name + ".SLDPRT");
        Assert.Equal(evidence.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var native = evidence.GetProperty("observation");
        Assert.True(native.GetProperty("rebuilt").GetBoolean());
        Assert.Equal(0, native.GetProperty("error").GetInt32());
        Assert.False(native.GetProperty("warning").GetBoolean());
        Assert.Equal(nativeCount, native.GetProperty("selectedCount").GetInt32());
        Assert.Equal(nativeBodies, native.GetProperty("bodies").GetInt32());
        Assert.Equal(nativeVolume, native.GetProperty("volume").GetDouble(), 12);

        using var file = SwFile.Open(path, readOnly: true);
        Assert.Equal((ushort)(name.StartsWith("whole") ? 0x71 : 0x70), file.ResolvedFeatures.StreamVersion);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.Equal(expectedMode, definition.SketchSelectionMode);
        Assert.Equal("SelectionSeed", definition.SketchName);
        Assert.NotEmpty(definition.AvailableSketchRegions);

        if (name == "contour")
        {
            var contour = Assert.Single(definition.SelectedContours);
            Assert.Equal(new uint[] { 1, 4, 3, 2 }, contour.EntityIndices);
            Assert.False(contour.IsCandidateOnly);
            Assert.Empty(definition.SelectedRegions);
        }
        else if (name == "region")
        {
            // Region selection stores its boundary chain entity set in the chooser
            // (region vs region_second differential: {5,6,7,8} for the 20x10 mm
            // rectangle of this specimen). The persistent region handle/ordinal is
            // not persisted, so EntityId/RegionIndex stay 0.
            Assert.Empty(definition.SelectedContours);
            var region = Assert.Single(definition.SelectedRegions);
            Assert.Equal(new uint[] { 5, 6, 7, 8 }, region.EdgeIds);
            Assert.Equal(0u, region.EntityId);
            Assert.Equal(0u, region.RegionIndex);
            Assert.True(region.IsValid);
        }
        else
        {
            Assert.Empty(definition.SelectedContours);
            Assert.Empty(definition.SelectedRegions);
        }
    }

    [Fact]
    public void WholeSketchResave_ProducesStableSelectionDecoding()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes");
        var results = new[] { "whole", "whole_resave" }.Select(name =>
        {
            using var file = SwFile.Open(Path.Combine(directory, name + ".SLDPRT"), readOnly: true);
            var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
                Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
            return definition;
        }).ToArray();

        foreach (var definition in results)
        {
            Assert.Equal(SketchSelectionMode.WholeSketch, definition.SketchSelectionMode);
            Assert.Empty(definition.SelectedContours);
            Assert.Empty(definition.SelectedRegions);
            Assert.NotEmpty(definition.AvailableSketchRegions);
        }

        // Save noise must not change the sketch-side candidate structures either.
        Assert.Equal(
            string.Join(";", results[0].AvailableSketchContours.Select(c => string.Join(",", c.EntityIndices))),
            string.Join(";", results[1].AvailableSketchContours.Select(c => string.Join(",", c.EntityIndices))));
        Assert.Equal(results[0].AvailableSketchRegions.Count, results[1].AvailableSketchRegions.Count);
    }

    [Fact]
    public void RegionSecondSpecimen_DecodesDifferentialBoundaryEdgeSet()
    {
        // Additional ordinal specimen from the controlled experiment workspace
        // (region ordinal 1 => 10x10 mm rectangle, native volume 1e-6 m^3).
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026",
            "SelectionModes", "region_second.SLDPRT");
        Assert.True(File.Exists(path), "Missing selection specimen: " + path);

        using var file = SwFile.Open(path, readOnly: true);
        Assert.Equal((ushort)0x70, file.ResolvedFeatures.StreamVersion);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Empty(definition.SelectedContours);
        var region = Assert.Single(definition.SelectedRegions);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, region.EdgeIds);
        Assert.Equal(0u, region.EntityId);
    }

    [Fact]
    public void UnrecognizedRegionChooser_StaysUnknownWithoutSelectedReferences()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "region.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc0 = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc0.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        // Corrupt the 0x80EC class reference of the region chooser prefix.
        stream[chooser.DataStart + 3] = 0x00;
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(model.Folders.Where(f => f.ClassName == "moExtrusion_c")).FeatureData);
        Assert.Equal(SketchSelectionMode.Unknown, definition.SketchSelectionMode);
        Assert.Empty(definition.SelectedContours);
        Assert.Empty(definition.SelectedRegions);
    }

    [Fact]
    public void UnestablishedSelectionStrategy_DefaultsToUnknown()
    {
        Assert.Equal(SketchSelectionMode.Unknown, new ExtrudeFeatureDefinitionData().SketchSelectionMode);
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "extrude_cut_5mm.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var cuts = file.Features.Select(f => f.Definition).OfType<ExtrudeFeatureDefinitionData>().Where(d => d.IsCut).ToArray();
        Assert.NotEmpty(cuts);
        foreach (var definition in cuts)
        {
            Assert.Equal(SketchSelectionMode.Unknown, definition.SketchSelectionMode);
            Assert.Empty(definition.SelectedContours);
            Assert.Empty(definition.SelectedRegions);
        }
    }

    [Theory]
    [InlineData(0x6f)]
    [InlineData(0x72)]
    [InlineData(0x73)]
    public void ExplicitChooser_OutsideObservedVersionRemainsUnknown(int version)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "contour.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        BitConverter.GetBytes((ushort)version).CopyTo(stream, 0);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(model.Folders.Where(f => f.ClassName == "moExtrusion_c")).FeatureData);
        Assert.Equal(SketchSelectionMode.Unknown, definition.SketchSelectionMode);
        Assert.Empty(definition.SelectedContours);
        Assert.Empty(definition.SelectedRegions);
    }
}
