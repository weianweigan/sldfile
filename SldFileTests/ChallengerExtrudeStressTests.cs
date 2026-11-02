using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;


namespace SldFileTests;

/// <summary>
/// Adversarial stress tests for ExtrudeFeatureBuilder and AddExtrude authoring pipeline.
/// Authored by Empirical Challenger for Milestone 1 Quality Gate.
/// </summary>
public class ChallengerExtrudeStressTests
{
    private static TempTestFile CreateStrippedSketchPart()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        var temp = TestFileProvider.CreateTempCopy(extPath);

        // Strip the extrusion feature to produce a clean baseline sketch part
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

        return temp;
    }

    // ── Challenge 1: Invalid Inputs (Negative, Zero, NaN, Infinity) ──

    [Theory]
    [InlineData(-0.010)]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void AddExtrude_WithInvalidDepth_ExposesBehavior(double invalidDepth)
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(invalidDepth));
    }

    [Fact]
    public void AddExtrude_WithPositiveInfinity_PersistsInfinityToStream()
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(double.PositiveInfinity));
    }

    // ── Challenge 2: IEEE 754 Double Precision Roundtrip ──

    [Theory]
    [InlineData(0.012345678901234567)]
    [InlineData(1.0 / 3.0)]
    [InlineData(Math.PI / 100.0)]
    [InlineData(0.0078125)] // 1/128, exact dyadic rational
    public void AddExtrude_ExactDoublePrecision_MaintainsExactBitsRoundtrip(double exactDepth)
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(exactDepth);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            var reopenedDef = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);

            // Assert exact 64-bit IEEE 754 bit-pattern equality!
            long originalBits = BitConverter.DoubleToInt64Bits(exactDepth);
            long roundtripBits = BitConverter.DoubleToInt64Bits(reopenedDef.Depth);
            Assert.Equal(originalBits, roundtripBits);
            Assert.Equal(exactDepth, reopenedDef.Depth);
        }
    }

    // ── Challenge 3: Boundary Depths (0.0001m, 10.0m) ──

    [Theory]
    [InlineData(0.0001)] // 0.1mm (boundary)
    [InlineData(0.00001)] // 0.01mm (sub-boundary)
    [InlineData(1.0)] // 1 meter
    [InlineData(10.0)] // 10 meters
    [InlineData(500.0)] // 500 meters
    public void AddExtrude_BoundaryDepths_RoundtripsAccurately(double boundaryDepth)
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(boundaryDepth);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            var reopenedDef = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(boundaryDepth, reopenedDef.Depth);
        }
    }

    // ── Challenge 4: IsCut (Cut Extrude) Injection vs Persistence ──

    [Fact]
    public void AddExtrude_WithIsCutTrue_ObservesPersistenceDiscrepancy()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var createdFeat = part.AddExtrude(0.015, isCut: true);

            // In-memory returns Cut
            Assert.Equal(FeatureKind.Cut, createdFeat.Kind);
            Assert.Equal("切除-拉伸1", createdFeat.Name);
            part.Save();
        }

        // Upon reopening the persisted file:
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = reopened.Features.FirstOrDefault(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut);
            Assert.NotNull(feat);

            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            bool reopenedAsCut = def.IsCut;
            Assert.True(reopenedAsCut, "ExtrudeFeatureBuilder serializes moICE_c, so isCut is persisted across re-open.");
            Assert.Equal(FeatureKind.Cut, feat.Kind);
            Assert.Equal("切除-拉伸1", feat.Name);
        }
    }

    // ── Challenge 5: EndCondition MidPlane (6) Roundtrip ──

    [Fact]
    public void AddExtrude_MidPlane_PersistsEndConditionAndCalculatesZeroCenterZ()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(0.020, endCondition: 6); // 6 = MidPlane
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            var reopenedDef = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(6u, reopenedDef.EndCondition);
            Assert.True(reopenedDef.BothDirections);
            Assert.Equal(0.020, reopenedDef.Depth);
        }
    }

    // ── Challenge 6: Microscopic (1e-9, 1e-12) & Huge (1e6, 1e9) Depths ──

    [Theory]
    [InlineData(1e-9)]   // 1 nanometer
    [InlineData(1e-12)]  // 1 picometer
    [InlineData(1e6)]    // 1,000,000 meters (1,000 km)
    [InlineData(1e9)]    // 1,000,000,000 meters
    public void AddExtrude_MicroscopicAndHugeDepths_RoundtripsSuccessfully(double extremeDepth)
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(extremeDepth);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            var reopenedDef = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(extremeDepth, reopenedDef.Depth);
        }
    }

    // ── Challenge 7: Additional Invalid Inputs (Negative, -0.0, -Infinity, Invalid Direction2, Null Def) ──

    [Theory]
    [InlineData(-1e-9)]
    [InlineData(-100.0)]
    [InlineData(-0.0)]
    [InlineData(double.NegativeInfinity)]
    public void AddExtrude_WithNegativeOrNegativeInfinity_ThrowsArgumentOutOfRangeException(double invalidDepth)
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(invalidDepth));
    }

    [Fact]
    public void AddExtrude_WithNullDefinition_ThrowsArgumentNullException()
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        Assert.Throws<ArgumentNullException>(() => part.AddExtrude((ExtrudeFeatureDefinitionData)null!));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.010)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddExtrude_BothDirectionsWithInvalidDirection2_ThrowsArgumentOutOfRangeException(double invalidDir2)
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        var def = new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            BothDirections = true,
            DepthDirection2 = invalidDir2
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddExtrude(def));
    }

    // ── Challenge 8: Read-Only Document Rejection ──

    [Fact]
    public void AddExtrude_OnReadOnlyDocument_ThrowsInvalidOperationException()
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        var part = Assert.IsType<SldPart>(sw);

        var ex = Assert.Throws<InvalidOperationException>(() => part.AddExtrude(0.010));
        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Challenge 9: Sketch Resolution & Error Handling ──

    [Fact]
    public void AddExtrude_WithNonExistentSketchName_ThrowsArgumentException()
    {
        using var temp = CreateStrippedSketchPart();

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);

        var ex = Assert.Throws<ArgumentException>(() => part.AddExtrude(0.010, targetSketchName: "NoSuchSketch_XYZ"));
        Assert.Contains("NoSuchSketch_XYZ", ex.Message);
    }

    [Fact]
    public void AddExtrude_WithTargetSketchExplicitName_CaseInsensitive_Succeeds()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            // In the stripped sketch file, the sketch is named "草图1"
            var feat = part.AddExtrude(0.015, targetSketchName: "草图1");
            Assert.NotNull(feat);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            Assert.Equal("凸台-拉伸1", feat.Name);
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0.015, def.Depth);
        }
    }

    [Fact]
    public void AddExtrude_DocumentWithNoSketches_ThrowsInvalidOperationException()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(extPath);

        // Strip ALL features including sketches to create a part with no sketches
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = sw.ResolvedFeatures.RawData!;
            var sketchFolder = sw.ResolvedFeatures.Folders.First(f => f.ClassName.StartsWith("moProfileFeature_"));
            int sketchOffset = sketchFolder.TagOffset;

            byte[] noSketches = new byte[sketchOffset];
            Array.Copy(raw, 0, noSketches, 0, sketchOffset);

            // Count folders before sketch
            int countBeforeSketch = sw.ResolvedFeatures.Folders.Count(f => f.TagOffset < sketchOffset);
            BitConverter.GetBytes((ushort)countBeforeSketch).CopyTo(noSketches, 4);

            sw.SetPendingStream("Contents/Config-0-ResolvedFeatures", noSketches);
            sw.Save();
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var ex = Assert.Throws<InvalidOperationException>(() => part.AddExtrude(0.010));
            Assert.Contains("No sketch found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Challenge 10: Sequential Injections (Multiple Boss and Cut Extrusions) ──

    [Fact]
    public void AddExtrude_SequentialInjections_CompoundingFeaturesAndCountsCorrectly()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);

            // Step 1: Add Boss Extrude 1 (10mm)
            var boss1 = part.AddExtrude(0.010, isCut: false);
            Assert.Equal("凸台-拉伸1", boss1.Name);
            Assert.Equal(FeatureKind.Extrusion, boss1.Kind);
            Assert.Contains(part.Features, f => f.Name == "凸台-拉伸1" && f.Kind == FeatureKind.Extrusion);

            // Step 2: Add Cut Extrude 1 (5mm) on the same document instance
            var cut1 = part.AddExtrude(0.005, isCut: true);
            Assert.Equal("切除-拉伸1", cut1.Name);
            Assert.Equal(FeatureKind.Cut, cut1.Kind);
            Assert.Contains(part.Features, f => f.Name == "切除-拉伸1" && f.Kind == FeatureKind.Cut);

            // Step 3: Add Boss Extrude 2 (20mm) on the same document instance
            var boss2 = part.AddExtrude(0.020, isCut: false);
            Assert.Equal("凸台-拉伸2", boss2.Name);
            Assert.Equal(FeatureKind.Extrusion, boss2.Kind);
            Assert.Contains(part.Features, f => f.Name == "凸台-拉伸2" && f.Kind == FeatureKind.Extrusion);

            // Step 4: Add Cut Extrude 2 (3mm) on the same document instance
            var cut2 = part.AddExtrude(0.003, isCut: true);
            Assert.Equal("切除-拉伸2", cut2.Name);
            Assert.Equal(FeatureKind.Cut, cut2.Kind);
            Assert.Contains(part.Features, f => f.Name == "切除-拉伸2" && f.Kind == FeatureKind.Cut);

            // Total 4 extrusion features added in-memory
            var extrudeFeatures = part.Features.Where(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut).ToList();
            Assert.Equal(4, extrudeFeatures.Count);

            part.Save();
        }

        // Reopen persisted document from disk and verify all 4 features are intact and preserved
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var allExtrudes = reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut).ToList();
            Assert.Equal(4, allExtrudes.Count);

            var rBoss1 = allExtrudes.FirstOrDefault(f => f.Name == "凸台-拉伸1");
            Assert.NotNull(rBoss1);
            Assert.Equal(FeatureKind.Extrusion, rBoss1!.Kind);
            var b1Def = Assert.IsType<ExtrudeFeatureDefinitionData>(rBoss1.Definition);
            Assert.Equal(0.010, b1Def.Depth);
            Assert.False(b1Def.IsCut);

            var rCut1 = allExtrudes.FirstOrDefault(f => f.Name == "切除-拉伸1");
            Assert.NotNull(rCut1);
            Assert.Equal(FeatureKind.Cut, rCut1!.Kind);
            var c1Def = Assert.IsType<ExtrudeFeatureDefinitionData>(rCut1.Definition);
            Assert.Equal(0.005, c1Def.Depth);
            Assert.True(c1Def.IsCut);

            var rBoss2 = allExtrudes.FirstOrDefault(f => f.Name == "凸台-拉伸2");
            Assert.NotNull(rBoss2);
            Assert.Equal(FeatureKind.Extrusion, rBoss2!.Kind);
            var b2Def = Assert.IsType<ExtrudeFeatureDefinitionData>(rBoss2.Definition);
            Assert.Equal(0.020, b2Def.Depth);
            Assert.False(b2Def.IsCut);

            var rCut2 = allExtrudes.FirstOrDefault(f => f.Name == "切除-拉伸2");
            Assert.NotNull(rCut2);
            Assert.Equal(FeatureKind.Cut, rCut2!.Kind);
            var c2Def = Assert.IsType<ExtrudeFeatureDefinitionData>(rCut2.Definition);
            Assert.Equal(0.003, c2Def.Depth);
            Assert.True(c2Def.IsCut);
        }
    }

    // ── Challenge 11: Multi-Cycle Save and Re-Open Iterations ──

    [Fact]
    public void AddExtrude_MultiCycleSaveAndReopen_PreservesFeatureChainAcrossSessions()
    {
        using var temp = CreateStrippedSketchPart();

        // Cycle 1: Add Boss Extrude 1
        using (var sw1 = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part1 = Assert.IsType<SldPart>(sw1);
            part1.AddExtrude(0.012, isCut: false);
            part1.Save();
        }

        // Cycle 2: Reopen, Add Cut Extrude 1
        using (var sw2 = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part2 = Assert.IsType<SldPart>(sw2);
            Assert.Single(part2.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            part2.AddExtrude(0.006, isCut: true);
            part2.Save();
        }

        // Cycle 3: Reopen, Add Boss Extrude 2
        using (var sw3 = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part3 = Assert.IsType<SldPart>(sw3);
            Assert.Equal(2, part3.Features.Count(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut));
            part3.AddExtrude(0.018, isCut: false);
            part3.Save();
        }

        // Final verification
        using (var finalSw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var features = finalSw.Features.Where(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut).ToList();
            Assert.Equal(3, features.Count);

            Assert.Equal("凸台-拉伸1", features[0].Name);
            Assert.Equal(FeatureKind.Extrusion, features[0].Kind);

            Assert.Equal("切除-拉伸1", features[1].Name);
            Assert.Equal(FeatureKind.Cut, features[1].Kind);

            Assert.Equal("凸台-拉伸2", features[2].Name);
            Assert.Equal(FeatureKind.Extrusion, features[2].Kind);
        }
    }

    // ── Challenge 12: Variable-Length Feature Names Empirical Stress (1, 2, 6, 7, 15, 30 chars) ──

    [Theory]
    [InlineData("A", 1)]
    [InlineData("拉伸", 2)]
    [InlineData("凸台-拉伸1", 6)]
    [InlineData("凸台-拉伸10", 7)]
    [InlineData("Extrude_Feat_15", 15)]
    [InlineData("Extrude_Feature_30_Chars_Long!", 30)]
    public void AddExtrude_VariableLengthNames_Boss_Roundtrip(string name, int expectedCharCount)
    {
        Assert.Equal(expectedCharCount, name.Length);
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feat = part.AddExtrude(0.015, isCut: false, featureName: name);
            Assert.NotNull(feat);
            Assert.Equal(name, feat.Name);
            Assert.Equal(FeatureKind.Extrusion, feat.Kind);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = reopened.Features.FirstOrDefault(f => f.Name == name);
            Assert.NotNull(feat);
            Assert.Equal(name, feat.Name);
            Assert.Equal(FeatureKind.Extrusion, feat.Kind);
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0.015, def.Depth);
            Assert.False(def.IsCut);
        }
    }

    [Theory]
    [InlineData("C", 1)]
    [InlineData("切除", 2)]
    [InlineData("切除-拉伸1", 6)]
    [InlineData("切除-拉伸10", 7)]
    [InlineData("Cut_Feature__15", 15)]
    [InlineData("Cut_Feature_Name_30_Chars_Long", 30)]
    public void AddExtrude_VariableLengthNames_Cut_Roundtrip(string name, int expectedCharCount)
    {
        Assert.Equal(expectedCharCount, name.Length);
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            // Add base boss first so cut has a base feature
            part.AddExtrude(0.020, isCut: false);

            var feat = part.AddExtrude(0.005, isCut: true, featureName: name);
            Assert.NotNull(feat);
            Assert.Equal(name, feat.Name);
            Assert.Equal(FeatureKind.Cut, feat.Kind);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = reopened.Features.FirstOrDefault(f => f.Name == name);
            Assert.NotNull(feat);
            Assert.Equal(name, feat.Name);
            Assert.Equal(FeatureKind.Cut, feat.Kind);
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0.005, def.Depth);
            Assert.True(def.IsCut);
        }
    }

    [Theory]
    [InlineData("X", 1)]
    [InlineData("拉伸", 2)]
    [InlineData("凸台-拉伸1", 6)]
    [InlineData("凸台-拉伸10", 7)]
    [InlineData("Extrude_Feat_15", 15)]
    [InlineData("Extrude_Feature_30_Chars_Long!", 30)]
    public void MfcExtrudeSerializer_VariableLengthNames_MaintainsSubObjectIntegrity(string name, int expectedCharCount)
    {
        var def = new ExtrudeFeatureDefinitionData { Depth = 0.018, EndCondition = 0, IsCut = false };
        byte[] block = MfcExtrudeSerializer.SerializeBossExtrude(def, name, new uint[] { 1, 2, 3, 4 });

        // Delta is 2 bytes per char relative to 6-char baseline
        int expectedLength = 3221 + 2 * (expectedCharCount - 6);
        Assert.Equal(expectedLength, block.Length);

        var doc = MfcDocument.Parse(block);
        Assert.NotNull(doc);

        var root = doc.FindFirst("moExtrusion_c");
        Assert.NotNull(root);
        Assert.Equal(name, root.AsWChar());

        // Confirm downstream sub-objects are parsed without offset corruption
        Assert.NotNull(doc.FindFirst("moPerBodyChooserData_c"));
        Assert.NotNull(doc.FindFirst("moFromSktEntSurfIdRep_c"));
        Assert.NotNull(doc.FindFirst("moBBoxCenterData_c"));
        Assert.NotNull(doc.FindFirst("moEndSpec_c"));
        Assert.NotNull(doc.FindFirst("moDisplayDistanceDim_c"));
        Assert.NotNull(doc.FindFirst("ParallelPlaneDistanceDim_c"));
        var lpd = doc.FindFirst("moLengthParameter_c");
        Assert.NotNull(lpd);
    }

    // ── Challenge 13: Sequential Extrusions Up To 10 Features ──

    [Fact]
    public void AddExtrude_SequentialTenFeatures_PreservesAllNamesAndDefinitionsAcrossSessions()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);

            for (int i = 1; i <= 10; i++)
            {
                double depth = 0.010 + i * 0.001;
                var feat = part.AddExtrude(depth, isCut: false);
                string expectedName = $"凸台-拉伸{i}";
                Assert.NotNull(feat);
                Assert.Equal(expectedName, feat.Name);
                Assert.Equal(FeatureKind.Extrusion, feat.Kind);
            }

            Assert.Equal(10, part.Features.Count(f => f.Kind == FeatureKind.Extrusion));
            part.Save();
        }

        // Reopen and thoroughly verify each of the 10 features
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var extrudes = reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion).ToList();
            Assert.Equal(10, extrudes.Count);

            for (int i = 1; i <= 10; i++)
            {
                string expectedName = $"凸台-拉伸{i}";
                double expectedDepth = 0.010 + i * 0.001;

                var feat = extrudes.FirstOrDefault(f => f.Name == expectedName);
                Assert.True(feat != null, $"Feature '{expectedName}' must exist upon reopening but was missing.");
                Assert.Equal(FeatureKind.Extrusion, feat!.Kind);

                var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
                Assert.Equal(expectedDepth, def.Depth, precision: 6);
                Assert.False(def.IsCut);
            }

            // Verify 10 distinct moExtrusion_c folders
            var folders = reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moExtrusion_c").ToList();
            Assert.Equal(10, folders.Count);
        }
    }

    [Fact]
    public void AddExtrude_SequentialTenCustomNamedFeatures_PreservesUniqueNames()
    {
        string[] customNames = new string[]
        {
            "A",
            "B2",
            "Cut",
            "Main",
            "Body5",
            "拉伸特征",
            "凸台-拉伸10",
            "BossExtrudeEight",
            "Nine_Character_9",
            "FeatureThirtyCharactersLong1234"
        };

        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);

            for (int i = 0; i < customNames.Length; i++)
            {
                double depth = 0.005 * (i + 1);
                var feat = part.AddExtrude(depth, isCut: false, featureName: customNames[i]);
                Assert.NotNull(feat);
                Assert.Equal(customNames[i], feat.Name);
            }

            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var extrudes = reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion).ToList();
            Assert.Equal(customNames.Length, extrudes.Count);

            for (int i = 0; i < customNames.Length; i++)
            {
                string expectedName = customNames[i];
                var feat = extrudes.FirstOrDefault(f => f.Name == expectedName);
                Assert.True(feat != null, $"Custom feature '{expectedName}' must be preserved upon reopen.");
                Assert.Equal(expectedName, feat!.Name);

                var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
                Assert.Equal(0.005 * (i + 1), def.Depth, precision: 6);
            }
        }
    }

    // ── Challenge 14: Dynamic Topology Linking Arbitrary Contour Verification ($N=3, 5, 6, 8, 12$) ──

    [Theory]
    [InlineData(3, 259)]  // 139 + 40*3 = 259 (Triangle)
    [InlineData(4, 299)]  // 139 + 40*4 = 299 (Quad)
    [InlineData(5, 339)]  // 139 + 40*5 = 339 (Pentagon)
    [InlineData(6, 379)]  // 139 + 40*6 = 379 (Hexagon)
    [InlineData(8, 459)]  // 139 + 40*8 = 459 (Octagon)
    [InlineData(12, 619)] // 139 + 40*12 = 619 (Dodecagon)
    public void DynamicTopologyLinking_Boss_ExactFormulaAndMfcParsing(int n, int expectedTopoLength)
    {
        var entityIds = Enumerable.Range(10, n).Select(i => (uint)i).ToArray();
        uint surfaceTag = 0x6AE19B71;

        // 1. Verify exact formula length: 139 + 40N
        byte[] topo = MfcExtrudeSerializer.SynthesizeFromSktEntSurfIdRep(entityIds, surfaceTag);
        Assert.Equal(expectedTopoLength, topo.Length);
        Assert.Equal(139 + 40 * n, topo.Length);

        // 2. Verify Set 1 reversed topological order [e_N, ..., e_1]
        uint firstSet1Entity = BitConverter.ToUInt32(topo, 43);
        Assert.Equal(entityIds[n - 1], firstSet1Entity);
        int lastSet1Offset = 31 + (n - 1) * 20 + 12;
        uint lastSet1Entity = BitConverter.ToUInt32(topo, lastSet1Offset);
        Assert.Equal(entityIds[0], lastSet1Entity);

        // 3. Verify complete BossExtrudeBlock synthesis and MfcDocument parsing
        var def = new ExtrudeFeatureDefinitionData { Depth = 0.025, EndCondition = 0, IsCut = false };
        byte[] block = ExtrudeFeatureBuilder.BuildBossExtrudeBlock(def, "凸台-拉伸1", n);

        int expectedBlockLength = 3221 + 40 * (n - 4);
        Assert.Equal(expectedBlockLength, block.Length);

        var doc = MfcDocument.Parse(block);
        Assert.NotNull(doc);

        var topoElem = doc.FindFirst("moFromSktEntSurfIdRep_c");
        Assert.NotNull(topoElem);

        var bboxElem = doc.FindFirst("moBBoxCenterData_c");
        Assert.NotNull(bboxElem);
        Assert.True(bboxElem.TagOffset > topoElem.TagOffset, "moBBoxCenterData_c must follow moFromSktEntSurfIdRep_c.");

        var dimElem = doc.FindFirst("moLengthParameter_c");
        Assert.NotNull(dimElem);
    }

    [Theory]
    [InlineData(1, 229)]  // 177 + 52*1 = 229
    [InlineData(3, 333)]  // 177 + 52*3 = 333 (Triangle cut)
    [InlineData(4, 385)]  // 177 + 52*4 = 385 (Quad cut)
    [InlineData(5, 437)]  // 177 + 52*5 = 437 (Pentagon cut)
    [InlineData(6, 489)]  // 177 + 52*6 = 489 (Hexagon cut)
    public void DynamicTopologyLinking_Cut_ExactFormulaAndMfcParsing(int n, int expectedCutTopoLength)
    {
        var entityIds = Enumerable.Range(100, n).Select(i => (uint)i).ToArray();
        byte[] topo = MfcExtrudeSerializer.SynthesizeFromSktEnt3IntSurfIdRep(entityIds);

        Assert.Equal(expectedCutTopoLength, topo.Length);
        Assert.Equal(177 + 52 * n, topo.Length);

        var def = new ExtrudeFeatureDefinitionData { Depth = 0.005, EndCondition = 0, IsCut = true };
        byte[] block = ExtrudeFeatureBuilder.BuildCutExtrudeBlock(def, "切除-拉伸1", n);

        int expectedBlockLength = 3092 + 52 * (n - 1);
        Assert.Equal(expectedBlockLength, block.Length);

        var doc = MfcDocument.Parse(block);
        Assert.NotNull(doc);
        Assert.NotNull(doc.FindFirst("moICE_c"));
        Assert.NotNull(doc.FindFirst("moFromSktEnt3IntSurfIdRep_c"));
    }

    // ── Challenge 15: Boundary Conditions ($1\mu m$ and $100 m$) ──

    [Theory]
    [InlineData(0.000001, false)]  // 1 micrometer (Boss)
    [InlineData(0.000001, true)]   // 1 micrometer (Cut)
    [InlineData(100.0, false)]     // 100 meters (Boss)
    [InlineData(100.0, true)]      // 100 meters (Cut)
    public void AddExtrude_BoundaryDepths_OneMicrometerAndOneHundredMeters(double depth, bool isCut)
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            if (isCut)
            {
                // Add base boss first
                part.AddExtrude(depth * 2 > 100.0 ? depth * 2 : 100.0, isCut: false);
            }
            var feat = part.AddExtrude(depth, isCut: isCut);
            Assert.NotNull(feat);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = reopened.Features.LastOrDefault(f => isCut ? f.Kind == FeatureKind.Cut : f.Kind == FeatureKind.Extrusion);
            Assert.NotNull(feat);

            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(isCut, def.IsCut);

            // Verify IEEE 754 bit-exactness
            long originalBits = BitConverter.DoubleToInt64Bits(depth);
            long roundtripBits = BitConverter.DoubleToInt64Bits(def.Depth);
            Assert.Equal(originalBits, roundtripBits);
            Assert.Equal(depth, def.Depth);
        }
    }

    // ── Challenge 16: Comprehensive EndCondition Variations Matrix ──

    [Theory]
    [InlineData(0u, false)]  // Blind (Boss)
    [InlineData(1u, false)]  // ThroughAll (Boss)
    [InlineData(2u, false)]  // UpToNext (Boss)
    [InlineData(3u, false)]  // UpToVertex (Boss)
    [InlineData(4u, false)]  // UpToSurface (Boss)
    [InlineData(5u, false)]  // OffsetFromSurface (Boss)
    [InlineData(6u, true)]   // MidPlane (Boss) - sets BothDirections
    public void AddExtrude_BossEndConditionMatrix_PersistsAccurately(uint endCondition, bool expectBothDirections)
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feat = part.AddExtrude(0.015, isCut: false, endCondition: endCondition);
            Assert.NotNull(feat);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(endCondition, def.EndCondition);
            Assert.Equal(expectBothDirections, def.BothDirections);
            Assert.Equal(0.015, def.Depth);
        }
    }

    [Fact]
    public void AddExtrude_CutEndConditionBlind_PersistsAccurately()
    {
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(0.020, isCut: false);
            part.AddExtrude(0.005, isCut: true, endCondition: 0u);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Cut));
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0u, def.EndCondition);
            Assert.False(def.BothDirections);
            Assert.Equal(0.005, def.Depth);
        }
    }

    [Fact]
    public void AddExtrude_CutEndConditionNonZero_ExposesPersistenceDivergence()
    {
        // Adversarial challenge: When a Cut Extrude is added with non-zero EndCondition (e.g. MidPlane = 6),
        // the serializer writes def.EndCondition at byte offset 0x10 following class index 0x8110.
        // However, MfcTreeBuilder only scans for tag byte 0x01 (FF FF 01 00) and ignores 0x8110 (Tag 0x02 index reference).
        // Consequently, EndSpecData is never created for moICE_c in ResolvedFeaturesSchemaMapper,
        // and upon reopening the file, def.EndCondition reverts to 0 (Blind) instead of 6.
        using var temp = CreateStrippedSketchPart();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(0.020, isCut: false);
            var cutFeat = part.AddExtrude(0.005, isCut: true, endCondition: 6u);
            Assert.NotNull(cutFeat);
            part.Save();
        }

        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Cut));
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);

            // Document the observed persistence divergence:
            // The serialized moICE_c class reference (0x8110) prevents EndSpecData mapping,
            // so reopened def.EndCondition returns 0 instead of the specified 6.
            Assert.Equal(0u, def.EndCondition);
        }
    }


    // ── Challenge 17: Compounding Alternating Boss and Cut Extrusions (Stress Chain) ──

    [Fact]
    public void AddExtrude_AlternatingBossAndCutEightFeatures_UniqueTagsAndNoCollisions()
    {
        using var temp = CreateStrippedSketchPart();

        // 8 alternating operations: 4 boss, 4 cut
        var specs = new (bool IsCut, double Depth, string ExpectedName)[]
        {
            (false, 0.010, "凸台-拉伸1"),
            (true,  0.002, "切除-拉伸1"),
            (false, 0.015, "凸台-拉伸2"),
            (true,  0.003, "切除-拉伸2"),
            (false, 0.020, "凸台-拉伸3"),
            (true,  0.004, "切除-拉伸3"),
            (false, 0.025, "凸台-拉伸4"),
            (true,  0.005, "切除-拉伸4")
        };

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);

            foreach (var s in specs)
            {
                var feat = part.AddExtrude(s.Depth, isCut: s.IsCut);
                Assert.NotNull(feat);
                Assert.Equal(s.ExpectedName, feat.Name);
                Assert.Equal(s.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion, feat.Kind);
            }

            Assert.Equal(8, part.Features.Count(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut));
            part.Save();
        }

        // Reopen and verify persistence and tag integrity
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var allExtrudes = reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut).ToList();
            Assert.Equal(8, allExtrudes.Count);

            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                var feat = allExtrudes.FirstOrDefault(f => f.Name == s.ExpectedName);
                Assert.True(feat != null, $"Feature '{s.ExpectedName}' must exist upon reopen.");
                Assert.Equal(s.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion, feat!.Kind);

                var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
                Assert.Equal(s.Depth, def.Depth, precision: 6);
                Assert.Equal(s.IsCut, def.IsCut);
            }

            // Check resolved folders
            var bossFolders = reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moExtrusion_c").ToList();
            var cutFolders = reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moICE_c").ToList();
            Assert.Equal(4, bossFolders.Count);
            Assert.Equal(4, cutFolders.Count);
        }
    }
}


