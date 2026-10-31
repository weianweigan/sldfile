using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.Topology;
using Xunit;

namespace SldFileTests;

public class TopologySelectionTests
{
    private static string GetSamplePath(string fileName)
    {
        var testFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", fileName);
        if (File.Exists(testFilesPath)) return testFilesPath;
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample {fileName} not found");
    }

    [Theory]
    [InlineData("extrude_10mm.SLDPRT")]
    [InlineData("extrude_20mm.SLDPRT")]
    [InlineData("extrude_30mm.SLDPRT")]
    [InlineData("extrude_midplane.SLDPRT")]
    public void Extrude_ShouldExtractSelectedContoursAndSketchName(string fileName)
    {
        var path = GetSamplePath(fileName);
        using var sw = SwFile.Open(path, readOnly: true);
        var extFolder = sw.ResolvedFeatures.Folders.FirstOrDefault(f => f.ClassName is "moExtrusion_c" or "moExtrusion_ct");
        Assert.NotNull(extFolder);

        var extData = Assert.IsType<ExtrudeFeatureDefinitionData>(extFolder!.FeatureData);
        Assert.Equal("草图1", extData.SketchName);
        Assert.NotEmpty(extData.SelectedContours);

        var contour = extData.SelectedContours[0];
        Assert.True(contour.IsValid);
        Assert.True(contour.IsClosed);
        Assert.Equal("Contour", contour.ReferenceType);
        Assert.True(contour.EntityIndices.Count >= 4);
        Assert.Contains(1u, contour.EntityIndices);
        Assert.Contains(2u, contour.EntityIndices);
        Assert.Contains(3u, contour.EntityIndices);
        Assert.Contains(4u, contour.EntityIndices);

        Assert.NotNull(extData.BodyScope);
        Assert.True(extData.BodyScope.AutoSelect);
    }

    [Fact]
    public void Extrude_ShouldExtractSketchRegion()
    {
        var path = GetSamplePath("extrude_10mm.SLDPRT");
        using var sw = SwFile.Open(path, readOnly: true);
        var extFolder = sw.ResolvedFeatures.Folders.FirstOrDefault(f => f.ClassName is "moExtrusion_c" or "moExtrusion_ct");
        Assert.NotNull(extFolder);

        var extData = Assert.IsType<ExtrudeFeatureDefinitionData>(extFolder!.FeatureData);
        Assert.NotEmpty(extData.SelectedRegions);

        var region = extData.SelectedRegions[0];
        Assert.True(region.IsValid);
        Assert.Equal("Region", region.ReferenceType);
        Assert.Equal(2u, region.RegionIndex);
        Assert.Equal(4u, region.LoopCount);
        Assert.Contains("Region #2", region.ToString());
    }

    [Fact]
    public void ExtrudeCut_ShouldExtractEndConditionEntityAndBodyScope()
    {
        var path = GetSamplePath("extrude_cut_5mm.SLDPRT");
        using var sw = SwFile.Open(path, readOnly: true);
        var cutFolder = sw.ResolvedFeatures.Folders.FirstOrDefault(f => f.ClassName == "moICE_c" || f.DisplayName.Contains("切除"));
        Assert.NotNull(cutFolder);

        var cutData = Assert.IsType<ExtrudeFeatureDefinitionData>(cutFolder!.FeatureData);
        Assert.True(cutData.IsCut);
        Assert.Equal("草图1", cutData.SketchName);
        Assert.NotEmpty(cutData.SelectedContours);

        Assert.NotNull(cutData.EndConditionEntity);
        Assert.True(cutData.EndConditionEntity!.IsValid);
        Assert.Equal(SelectionEntityType.Face, cutData.EndConditionEntity.EntityType);
        Assert.True(cutData.EndConditionEntity.FaceCount >= 1);

        Assert.NotNull(cutData.BodyScope);
        Assert.True(cutData.BodyScope.AutoSelect);
    }

    [Fact]
    public void Fillet_ShouldDecodeEdgeReferencesAndComponentEdge()
    {
        var path = GetSamplePath("fillet_2mm.SLDPRT");
        using var sw = SwFile.Open(path, readOnly: true);
        var rf = sw.ResolvedFeatures;

        // Traverse all elements in the stream
        var streamData = sw.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = SldFile.Mfc.MfcDocument.Parse(streamData);

        var edgeRefElem = doc.AllElements.FirstOrDefault(e => e.ClassName == "moEdgeRef_c");
        Assert.NotNull(edgeRefElem);
        var edgeRefData = TopologyParsers.ParseEdgeRef(edgeRefElem!);
        Assert.NotNull(edgeRefData);
        Assert.True(edgeRefData.EdgeCount >= 1);
        Assert.NotEmpty(edgeRefData.EdgeIndices);
        Assert.Contains(1u, edgeRefData.EdgeIndices);

        var compEdgeElem = doc.AllElements.FirstOrDefault(e => e.ClassName == "moCompEdge_c");
        Assert.NotNull(compEdgeElem);
        var compEdgeData = TopologyParsers.ParseCompEdge(compEdgeElem!);
        Assert.NotNull(compEdgeData);
        Assert.Equal(0x802C, compEdgeData.RefEdgeId);
    }

    [Theory]
    [InlineData("sm_base_flange_10mm.SLDPRT", 0.001, 0.002)]
    [InlineData("sm_base_flange_thick2.SLDPRT", 0.002, 0.002)]
    public void SheetMetalBaseFlange_ShouldExtractContourAndBodyScope(
        string fileName,
        double expectedThickness,
        double expectedRadius)
    {
        var path = GetSamplePath(fileName);
        using var sw = SwFile.Open(path, readOnly: true);
        var smFolder = sw.ResolvedFeatures.Folders.FirstOrDefault(f => f.ClassName == "moSMBaseFlange_c");
        Assert.NotNull(smFolder);

        var smData = Assert.IsType<SheetMetalBaseFlangeDefinitionData>(smFolder!.FeatureData);
        Assert.Equal(expectedThickness, smData.Thickness, precision: 6);
        Assert.Equal(expectedRadius, smData.BendRadius, precision: 6);
        Assert.Equal("草图1", smData.SketchName);
        Assert.NotEmpty(smData.SelectedContours);

        var contour = smData.SelectedContours[0];
        Assert.True(contour.IsValid);
        Assert.True(contour.IsClosed);
        Assert.True(contour.EntityIndices.Count >= 4);

        Assert.NotNull(smData.BodyScope);
        Assert.True(smData.BodyScope.AutoSelect);
    }

    [Fact]
    public void Revolve_ShouldContainValidContoursAndReferences()
    {
        var path = GetSamplePath("revolve_360.SLDPRT");
        using var sw = SwFile.Open(path, readOnly: true);
        var streamData = sw.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = SldFile.Mfc.MfcDocument.Parse(streamData);

        var chainElem = doc.AllElements.FirstOrDefault(e => e.ClassName == "moSketchChain_c");
        Assert.NotNull(chainElem);
        var chainData = FeatureDataParsers.ParseSketchChain(chainElem!);
        Assert.NotNull(chainData);
        Assert.True(chainData!.EntityIndices.Count >= 1);

        var contours = TopologyDecoder.BuildContourReferences(chainData, null);
        Assert.Single(contours);
        Assert.True(contours[0].IsValid);
    }

    [Fact]
    public void TopologyDecoder_NullAndBoundaryHandling()
    {
        // 1. BuildContourReferences with nulls
        var emptyContours = TopologyDecoder.BuildContourReferences(null, null);
        Assert.Empty(emptyContours);

        // 2. BuildRegionReferences with null
        var emptyRegions = TopologyDecoder.BuildRegionReferences(null!);
        Assert.Empty(emptyRegions);

        // 3. BuildDirectionReference with nulls
        var nullDirection = TopologyDecoder.BuildDirectionReference(null, null, null, null);
        Assert.Null(nullDirection);

        // 4. BuildEndConditionEntity with nulls
        var nullEndCondition = TopologyDecoder.BuildEndConditionEntity(null, null, null);
        Assert.Null(nullEndCondition);

        // 5. BuildBodyScope with nulls defaults to AutoSelect
        var defaultScope = TopologyDecoder.BuildBodyScope(null, null);
        Assert.NotNull(defaultScope);
        Assert.True(defaultScope.AutoSelect);
        Assert.Equal(0x0002, defaultScope.Flags);
        Assert.Empty(defaultScope.BodyIds);
        Assert.Empty(defaultScope.Bodies);
    }

    [Fact]
    public void SelectionModels_ContractAndToString()
    {
        var contour = new SketchContourReference
        {
            EntityId = 5,
            ChainId = 2,
            EntityIndices = new uint[] { 10, 20, 30 },
            IsClosed = true
        };
        Assert.Equal("Contour", contour.ReferenceType);
        Assert.True(contour.IsValid);
        Assert.Contains("Contour #5", contour.ToString());

        var region = new SketchRegionReference
        {
            EntityId = 12,
            RegionIndex = 1,
            LoopCount = 1
        };
        Assert.Equal("Region", region.ReferenceType);
        Assert.True(region.IsValid);
        Assert.Contains("Region #1", region.ToString());

        var dir = new DirectionReference
        {
            EntityId = 42,
            EntityType = SelectionEntityType.Edge,
            ReferenceName = "TopEdge",
            Vector = new double[] { 0, 0, 1 }
        };
        Assert.Equal("Direction", dir.ReferenceType);
        Assert.True(dir.IsValid);
        Assert.Contains("TopEdge", dir.ToString());

        var endEnt = new EndConditionEntityReference
        {
            EntityId = 99,
            EntityType = SelectionEntityType.Face,
            SurfaceName = "N1",
            FaceCount = 1
        };
        Assert.Equal("EndConditionEntity", endEnt.ReferenceType);
        Assert.True(endEnt.IsValid);
        Assert.Contains("Surface='N1'", endEnt.ToString());

        var scope = new BodyScopeData
        {
            AutoSelect = false,
            Flags = 0x0001,
            BodyIds = new uint[] { 1, 2 }
        };
        Assert.False(scope.AutoSelect);
        Assert.Equal(2, scope.BodyIds.Count);
        Assert.Contains("AutoSelect=False", scope.ToString());
    }
}
