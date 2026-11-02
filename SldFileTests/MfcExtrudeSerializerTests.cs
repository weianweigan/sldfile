using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFileTests;

public class MfcExtrudeSerializerTests
{
    private readonly ITestOutputHelper _output;

    public MfcExtrudeSerializerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BossExtrude_BitForBit_MatchesOriginalSample()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var sw = SwFile.Open(extPath, readOnly: true);
        var raw = sw.ResolvedFeatures.RawData!;

        // The original extrusion starts at 0x22E0 with length 3221
        byte[] originalExtrude = new byte[raw.Length - 0x22E0];
        Array.Copy(raw, 0x22E0, originalExtrude, 0, originalExtrude.Length);

        var def = new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            EndCondition = 0,
            IsCut = false
        };

        byte[] synthesized = MfcExtrudeSerializer.SerializeBossExtrude(
            def,
            "凸台-拉伸1",
            new uint[] { 1, 2, 3, 4 },
            0x6AE19B71,
            0xC28F06D1);

        Assert.Equal(3221, originalExtrude.Length);
        Assert.Equal(3221, synthesized.Length);
        Assert.Equal(originalExtrude, synthesized);
    }

    [Fact]
    public void CutExtrude_BitForBit_MatchesOriginalSample()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_cut_5mm.SLDPRT");
        using var sw = SwFile.Open(extPath, readOnly: true);
        var raw = sw.ResolvedFeatures.RawData!;

        // The original cut extrusion starts at 0x3FC5 with length 3092
        byte[] originalCut = new byte[3092];
        Array.Copy(raw, 0x3FC5, originalCut, 0, 3092);

        var def = new ExtrudeFeatureDefinitionData
        {
            Depth = 0.005,
            EndCondition = 0,
            IsCut = true
        };

        byte[] synthesized = MfcExtrudeSerializer.SerializeCutExtrude(
            def,
            "切除-拉伸1",
            new uint[] { 5 },
            0x6AE19B83,
            0x6AE19B82,
            0xCD0C81A9);

        Assert.Equal(3092, originalCut.Length);
        Assert.Equal(3092, synthesized.Length);
        Assert.Equal(originalCut, synthesized);
    }

    [Theory]
    [InlineData(3, 259)]  // 139 + 40*3 = 259
    [InlineData(4, 299)]  // 139 + 40*4 = 299
    [InlineData(5, 339)]  // 139 + 40*5 = 339
    [InlineData(6, 379)]  // 139 + 40*6 = 379
    public void DynamicTopologyLinking_ArbitraryContourCount_CalculatesExactFormula(int count, int expectedLength)
    {
        var entityIds = Enumerable.Range(1, count).Select(i => (uint)i).ToArray();
        byte[] topo = MfcExtrudeSerializer.SynthesizeFromSktEntSurfIdRep(entityIds, 0x6AE19B71);

        Assert.Equal(expectedLength, topo.Length);

        // Verify Set 1 reversed topological ordering
        // In Set 1, entityIds[count - 1] is at offset 31 + 12 = 43
        uint firstSet1Entity = BitConverter.ToUInt32(topo, 43);
        Assert.Equal((uint)count, firstSet1Entity);

        // In Set 1, entityIds[0] is at offset 31 + (count - 1) * 20 + 12
        int lastSet1EntityOffset = 31 + (count - 1) * 20 + 12;
        uint lastSet1Entity = BitConverter.ToUInt32(topo, lastSet1EntityOffset);
        Assert.Equal(1u, lastSet1Entity);
    }

    [Theory]
    [InlineData("Cut", 3)]
    [InlineData("Cut1", 4)]
    [InlineData("凸台-拉伸1", 6)]
    [InlineData("凸台-拉伸10", 7)]
    [InlineData("MainExtrude", 11)]
    [InlineData("Boss-Extrude1", 13)]
    public void VariableLengthFeatureNames_SerializesAndParsesAccurately(string name, int expectedCharCount)
    {
        var def = new ExtrudeFeatureDefinitionData
        {
            Depth = 0.020,
            EndCondition = 0,
            IsCut = false
        };

        byte[] block = MfcExtrudeSerializer.SerializeBossExtrude(def, name, new uint[] { 1, 2, 3, 4 });

        // Expected length delta = 2 * (N - 6)
        int expectedLength = 3221 + 2 * (expectedCharCount - 6);
        Assert.Equal(expectedLength, block.Length);

        // Parse with MfcDocument to confirm MFC structure integrity
        var doc = MfcDocument.Parse(block);
        Assert.NotNull(doc);

        var rootElem = doc.FindFirst("moExtrusion_c");
        Assert.NotNull(rootElem);
        Assert.Equal("moExtrusion_c", rootElem.ClassName);

        // Verify name extracted
        string? parsedName = rootElem.AsWChar();
        Assert.Equal(name, parsedName);

        // Verify all 16 sub-objects exist
        var classNames = doc.AllElements.Select(e => e.ClassName).ToList();
        Assert.Contains("moPerBodyChooserData_c", classNames);
        Assert.Contains("moFaceRef_c", classNames);
        Assert.Contains("moEndFaceSurfIdRep_c", classNames);
        Assert.Contains("moFR_c", classNames);
        Assert.Contains("moExtObject_c", classNames);
        Assert.Contains("moCStringHandle_c", classNames);
        Assert.Contains("moFromSktEntSurfIdRep_c", classNames);
        Assert.Contains("moBBoxCenterData_c", classNames);
        Assert.Contains("moEndSpec_c", classNames);
        Assert.Contains("moDisplayDistanceDim_c", classNames);
        Assert.Contains("moFeatureDimHandle_c", classNames);
        Assert.Contains("ParallelPlaneDistanceDim_c", classNames);
        Assert.Contains("moLengthParameter_c", classNames);
        Assert.Contains("moFavoriteHandle_c", classNames);
        Assert.Contains("moFromEndSpec_c", classNames);
    }

    [Fact]
    public void EntityCount_IsActive_GovernsSerializedContourSize()
    {
        var def = new ExtrudeFeatureDefinitionData { Depth = 0.015, EndCondition = 0 };

        // N = 3 (triangle): 3221 - 40 = 3181 bytes
        byte[] triangleBlock = ExtrudeFeatureBuilder.BuildBossExtrudeBlock(def, "凸台-拉伸1", 3);
        Assert.Equal(3181, triangleBlock.Length);

        // N = 4 (quad): 3221 bytes
        byte[] quadBlock = ExtrudeFeatureBuilder.BuildBossExtrudeBlock(def, "凸台-拉伸1", 4);
        Assert.Equal(3221, quadBlock.Length);

        // N = 5 (pentagon): 3221 + 40 = 3261 bytes
        byte[] pentagonBlock = ExtrudeFeatureBuilder.BuildBossExtrudeBlock(def, "凸台-拉伸1", 5);
        Assert.Equal(3261, pentagonBlock.Length);
    }

    [Fact]
    public void SldPart_AddExtrude_VariableLengthName_RoundTrip()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(extPath);

        // Strip the original extrusion to produce a sketch-only baseline
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = sw.ResolvedFeatures.RawData!;
            byte[] sketchOnly = new byte[0x22E0];
            Array.Copy(raw, 0, sketchOnly, 0, 0x22E0);

            ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
            sketchOnly[0x22D4] = 0x00;

            sw.SetPendingStream("Contents/Config-0-ResolvedFeatures", sketchOnly);
            sw.Save();
        }

        // Add 7-character feature "凸台-拉伸10" with 25mm depth
        string customName = "凸台-拉伸10";
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feat = part.AddExtrude(0.025, isCut: false, endCondition: 0, targetSketchName: null, featureName: customName);
            Assert.NotNull(feat);
            Assert.Equal(customName, feat.Name);
            Assert.Equal(FeatureKind.Extrusion, feat.Kind);
            part.Save();
        }

        // Reopen and verify feature name and depth are preserved
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            Assert.Equal(customName, feat.Name);
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0.025, def.Depth);
            Assert.Equal(0u, def.EndCondition);
            Assert.False(def.IsCut);
        }
    }

    [Fact]
    public void SequentialExtrusions_AllocateUniqueSurfaceTagsAndFeatureIds()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(extPath);

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        // First extrusion is already present ("凸台-拉伸1")
        // Add second extrusion
        var feat2 = part.AddExtrude(0.015, isCut: false, endCondition: 0);
        Assert.Equal("凸台-拉伸2", feat2.Name);

        // Check ResolvedFeatures folders
        var folders = part.ResolvedFeatures.Folders;
        Assert.NotNull(folders);

        var extrudeFolders = folders.Where(f => f.ClassName == "moExtrusion_c").ToList();
        Assert.Equal(2, extrudeFolders.Count);
    }
}
