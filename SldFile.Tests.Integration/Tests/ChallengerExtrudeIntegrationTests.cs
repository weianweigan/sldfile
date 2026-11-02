using System;
using System.IO;
using System.Linq;
using Xunit;
using SldFile;
using SldFile.Tests.Integration.Framework;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SldFile.Tests.Integration.Tests;

/// <summary>
/// Empirical integration stress tests using real SolidWorks COM automation.
/// Authored by Empirical Challenger for Milestone 1 Quality Gate.
/// Tests ForceRebuild3 across boundary depths (0.0001m, 10.0m), MidPlane, and geometric variations.
/// </summary>
[Collection("SolidWorksIntegration")]
public class ChallengerExtrudeIntegrationTests
{
    private readonly SolidWorksApplicationFixture _fixture;

    public ChallengerExtrudeIntegrationTests(SolidWorksApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    private static string? LocateTestFile(string relativeOrName)
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, relativeOrName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", relativeOrName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SldFileTests", "TestFiles", relativeOrName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "models", relativeOrName)
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }
        return null;
    }

    private static string PrepareStrippedSketchFile(string samplePath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"challenger_sketch_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(samplePath, tempPath, true);

        using (var sw = SwFile.Open(tempPath, readOnly: false))
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

        return tempPath;
    }

    private void RunDepthRebuildVerification(double depth, double expectedVolume)
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(samplePath);

        string tempPath = PrepareStrippedSketchFile(samplePath!);

        try
        {
            // 1. Synthesize extrusion with target depth using SldPart.AddExtrude
            using (var sw = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                part.AddExtrude(depth);
                part.Save();
            }

            // 2. Open in native SolidWorks
            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                // Verify ForceRebuild3 succeeds
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, $"ForceRebuild3 should succeed for depth {depth}m");

                // Verify feature exists and depth is recognized by SolidWorks API
                IFeature? swFeat = null;
                for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
                {
                    if (f.GetTypeName2() == "Extrusion" || f.GetTypeName2() == "Boss")
                    {
                        swFeat = f;
                        break;
                    }
                }
                Assert.NotNull(swFeat);

                var extData = swFeat.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(extData);
                extData!.AccessSelections(swDoc, null);
                try
                {
                    double swDepth = extData.GetDepth(true);
                    Assert.Equal(depth, swDepth, precision: 6);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // Verify solid body volume matches exact physical formula
                var partDoc = swDoc as IPartDoc;
                Assert.NotNull(partDoc);
                var bodies = partDoc!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double actualVolume = massProps[3];
                Assert.Equal(expectedVolume, actualVolume, precision: 8);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void AddExtrude_SmallBoundary_0_1mm_SolidWorksRebuildsWithAccurateVolume()
    {
        // 0.1mm: 10mm x 10mm x 0.1mm = 1.0e-8 m^3
        RunDepthRebuildVerification(0.0001, 1.0e-8);
    }

    [SolidWorksFact]
    public void AddExtrude_LargeBoundary_10m_SolidWorksRebuildsWithAccurateVolume()
    {
        // 10.0m: 10mm x 10mm x 10.0m = 1.0e-3 m^3
        RunDepthRebuildVerification(10.0, 1.0e-3);
    }

    [SolidWorksFact]
    public void AddExtrude_Standard_50mm_SolidWorksRebuildsWithAccurateVolume()
    {
        // 50mm: 10mm x 10mm x 50mm = 5.0e-6 m^3
        RunDepthRebuildVerification(0.050, 5.0e-6);
    }

    [SolidWorksFact]
    public void AddExtrude_MidPlaneEndCondition_SolidWorksRebuildsWithCenteredGeometry()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(samplePath);

        string tempPath = PrepareStrippedSketchFile(samplePath!);

        try
        {
            double depth = 0.020; // 20mm MidPlane
            using (var sw = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                part.AddExtrude(depth, isCut: false, endCondition: 6); // 6 = MidPlane
                part.Save();
            }

            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should succeed for MidPlane extrusion");

                // Verify solid body volume = 10mm x 10mm x 20mm = 2.0e-6 m^3
                var partDoc = swDoc as IPartDoc;
                Assert.NotNull(partDoc);
                var bodies = partDoc!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double actualVolume = massProps[3];
                Assert.Equal(2.0e-6, actualVolume, precision: 8);

                // For MidPlane on XY sketch plane (Z=0), center of mass Z should be approximately 0.0
                double centerZ = massProps[2];
                Assert.True(Math.Abs(centerZ) < 1e-6, $"Center of mass Z should be ~0 for MidPlane, got {centerZ}");
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void NativeCutExtrude_OpensAndRebuildsCleanly_RecognizesDistinctCutFeature()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_cut_5mm.SLDPRT"));
        Assert.NotNull(samplePath);

        IModelDoc2? swDoc = _fixture.OpenDocument(samplePath!, readOnly: true);
        Assert.NotNull(swDoc);

        try
        {
            bool rebuildSuccess = swDoc!.ForceRebuild3(false);
            Assert.True(rebuildSuccess, "ForceRebuild3 should succeed on native cut sample");

            IFeature? bossFeat = null;
            IFeature? cutFeat = null;

            for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
            {
                string typeName = f.GetTypeName2();
                if (typeName == "Extrusion" || typeName == "Boss")
                {
                    bossFeat = f;
                }
                else if (typeName == "Cut" || typeName == "ICE")
                {
                    cutFeat = f;
                }
            }

            Assert.NotNull(bossFeat);
            Assert.Equal("凸台-拉伸1", bossFeat!.Name);

            Assert.NotNull(cutFeat);
            Assert.Equal("切除-拉伸1", cutFeat!.Name);

            // Check Boss definition: typeName is Extrusion/Boss, depth is 0.010m
            Assert.Equal("Extrusion", bossFeat.GetTypeName2());
            var bossData = bossFeat.GetDefinition() as IExtrudeFeatureData2;
            Assert.NotNull(bossData);
            bossData!.AccessSelections(swDoc, null);
            try
            {
                double depth = bossData.GetDepth(true);
                Assert.Equal(0.010, depth, precision: 4);
            }
            finally
            {
                bossData.ReleaseSelectionAccess();
            }

            // Check Cut definition: typeName is ICE, depth is 0.005m
            Assert.Equal("ICE", cutFeat.GetTypeName2());
            var cutData = cutFeat.GetDefinition() as IExtrudeFeatureData2;
            Assert.NotNull(cutData);
            cutData!.AccessSelections(swDoc, null);
            try
            {
                double depth = cutData.GetDepth(true);
                Assert.Equal(0.005, depth, precision: 4);
            }
            finally
            {
                cutData.ReleaseSelectionAccess();
            }

            // Verify body volume: 1000 - pi * 3^2 * 5 = 858.628 mm^3 = 8.58628e-7 m^3
            var part = swDoc as IPartDoc;
            Assert.NotNull(part);
            var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            Assert.NotNull(bodies);
            Assert.Single(bodies!);
            var body = bodies![0] as IBody2;
            Assert.NotNull(body);

            var massProps = (double[])body!.GetMassProperties(0);
            double volume = massProps[3];
            Assert.Equal(8.58628e-7, volume, precision: 6);
        }
        finally
        {
            _fixture.CloseDocument(samplePath!);
        }
    }

    [SolidWorksFact]
    public void ModifiedCutExtrude_2_5mm_SolidWorksRebuildsWithAccurateVolume()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_cut_5mm.SLDPRT"));
        Assert.NotNull(samplePath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"modified_cut_test_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(samplePath!, tempPath, true);

        try
        {
            string streamName = "Contents/Config-0-ResolvedFeatures";
            using (var swFile = SwFile.Open(tempPath, readOnly: false))
            {
                var raw = (byte[])swFile.ResolvedFeatures.RawData!.Clone();

                // Cut depth in extrude_cut_5mm is 0.005. Replace with 0.0025 (2.5mm)
                byte[] oldVal = BitConverter.GetBytes(0.005);
                byte[] newVal = BitConverter.GetBytes(0.0025);

                byte[] oldCenter = BitConverter.GetBytes(0.0025);
                byte[] newCenter = BitConverter.GetBytes(0.00125);

                // Find moICE_c block and replace depth
                for (int i = 0x2000; i < raw.Length - 8; i++)
                {
                    if (Match(raw, i, oldVal))
                    {
                        Array.Copy(newVal, 0, raw, i, 8);
                        i += 7;
                    }
                    else if (Match(raw, i, oldCenter))
                    {
                        Array.Copy(newCenter, 0, raw, i, 8);
                        i += 7;
                    }
                }

                swFile.SetPendingStream(streamName, raw);
                swFile.Save();
            }

            // Open via real SOLIDWORKS application
            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should succeed on modified cut file");

                IFeature? cutFeat = null;
                for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
                {
                    if (f.GetTypeName2() == "Cut" || f.GetTypeName2() == "ICE")
                    {
                        cutFeat = f;
                        break;
                    }
                }

                Assert.NotNull(cutFeat);
                Assert.Equal("切除-拉伸1", cutFeat!.Name);

                var cutData = cutFeat.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(cutData);
                cutData!.AccessSelections(swDoc, null);
                try
                {
                    double depth = cutData.GetDepth(true);
                    Assert.Equal(0.0025, depth, precision: 4);
                }
                finally
                {
                    cutData.ReleaseSelectionAccess();
                }

                // Verify updated geometry volume: 1000 - pi * 3^2 * 2.5 = 1000 - 70.6858 = 929.314 mm^3 = 9.29314e-7 m^3
                var part = swDoc as IPartDoc;
                Assert.NotNull(part);
                var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double volume = massProps[3];
                Assert.Equal(9.29314e-7, volume, precision: 6);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectCutExtrudeOntoSketch_BuildsCutFeatureInSolidWorks()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_cut_5mm.SLDPRT"));
        Assert.NotNull(samplePath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_cut_test_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(samplePath!, tempPath, true);

        try
        {
            string streamName = "Contents/Config-0-ResolvedFeatures";

            using (var swFile = SwFile.Open(tempPath, readOnly: false))
            {
                var raw = swFile.ResolvedFeatures.RawData!;

                // Find cut folder offset
                var folders = FlattenFolders(swFile.ResolvedFeatures.Folders);
                var cutFolder = folders.FirstOrDefault(f => f.ClassName == "moICE_c" || f.DisplayName.Contains("切除"));
                Assert.NotNull(cutFolder);
                int cutOffset = cutFolder!.TagOffset;

                // Slice stream to remove cut feature
                byte[] sketchOnly = new byte[cutOffset];
                Array.Copy(raw, 0, sketchOnly, 0, cutOffset);

                // Decrement folder count
                ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
                BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);

                // Reset sketch2 child count flag (tag 0x67)
                for (int i = sketchOnly.Length - 4; i >= Math.Max(0, sketchOnly.Length - 128); i--)
                {
                    if (sketchOnly[i] == 0x67 && sketchOnly[i + 1] == 0x00 && sketchOnly[i + 2] == 0x00 && sketchOnly[i + 3] == 0x00)
                    {
                        BitConverter.GetBytes(0u).CopyTo(sketchOnly, i + 4);
                        break;
                    }
                }

                swFile.SetPendingStream(streamName, sketchOnly);
                swFile.Save();
            }

            // Verify cut feature is removed
            using (var swCheck = SwFile.Open(tempPath, readOnly: true))
            {
                Assert.DoesNotContain(swCheck.Features, f => f.Kind == FeatureKind.Cut || f.Name.Contains("切除"));
            }

            // Add new 7mm Cut using SldPart.AddExtrude API
            double newCutDepth = 0.007; // 7mm cut
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                var feat = part.AddExtrude(newCutDepth, isCut: true);
                Assert.NotNull(feat);
                Assert.Equal(FeatureKind.Cut, feat.Kind);
                Assert.Equal("切除-拉伸1", feat.Name);
                part.Save();
            }

            // Open in native SOLIDWORKS and call ForceRebuild3
            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should succeed for injected cut feature");

                IFeature? cutFeat = null;
                for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
                {
                    if (f.GetTypeName2() == "Cut" || f.GetTypeName2() == "ICE")
                    {
                        cutFeat = f;
                        break;
                    }
                }

                Assert.NotNull(cutFeat);
                Assert.Equal("切除-拉伸1", cutFeat!.Name);
                Assert.Equal("ICE", cutFeat!.GetTypeName2());
                var cutData = cutFeat.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(cutData);
                cutData!.AccessSelections(swDoc, null);
                try
                {
                    double depth = cutData.GetDepth(true);
                    Assert.Equal(newCutDepth, depth, precision: 4);
                }
                finally
                {
                    cutData.ReleaseSelectionAccess();
                }

                // Verify updated geometry volume: 1000 - pi * 3^2 * 7 = 1000 - 197.9203 = 802.08 mm^3 = 8.0208e-7 m^3
                var part = swDoc as IPartDoc;
                Assert.NotNull(part);
                var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double volume = massProps[3];
                Assert.Equal(8.0208e-7, volume, precision: 6);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectBossExtrudeOntoSketch_BuildsSolidBodyInSolidWorks()
    {
        string? extSample = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(extSample);

        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_boss_test_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(extSample!, tempPath, true);

        try
        {
            // 1. Strip extrusion feature to create sketch-only baseline
            string streamName = "Contents/Config-0-ResolvedFeatures";
            using (var sw = SwFile.Open(tempPath, readOnly: false))
            {
                var raw = sw.ResolvedFeatures.RawData!;
                byte[] sketchOnly = new byte[0x22E0];
                Array.Copy(raw, 0, sketchOnly, 0, 0x22E0);

                ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
                BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
                sketchOnly[0x22D4] = 0x00;

                sw.SetPendingStream(streamName, sketchOnly);
                sw.Save();
            }

            // 2. Inject 15mm Boss Extrude via SldPart.AddExtrude API
            double targetDepth = 0.015;
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                var createdFeature = part.AddExtrude(targetDepth, isCut: false);
                Assert.NotNull(createdFeature);
                Assert.Equal(FeatureKind.Extrusion, createdFeature.Kind);
                part.Save();
            }

            // 3. Open in native SOLIDWORKS and verify ForceRebuild3
            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should rebuild the injected Boss feature cleanly without errors");

                IFeature? swFeat = null;
                for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
                {
                    if (f.GetTypeName2() == "Extrusion" || f.GetTypeName2() == "Boss")
                    {
                        swFeat = f;
                        break;
                    }
                }

                Assert.NotNull(swFeat);
                Assert.Equal("凸台-拉伸1", swFeat!.Name);

                var extData = swFeat.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(extData);
                extData!.AccessSelections(swDoc, null);
                try
                {
                    double depth = extData.GetDepth(true);
                    Assert.Equal(targetDepth, depth, precision: 4);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // Verify solid body volume matches exact analytical kernel geometry: 10mm x 10mm x 15mm = 1.5e-6 m^3
                var part = swDoc as IPartDoc;
                Assert.NotNull(part);
                var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double volume = massProps[3];
                Assert.Equal(1.5e-6, volume, precision: 8);

                // Verify center of mass matches analytical centroid: (0.005, 0.005, 0.0075)
                Assert.Equal(0.005, massProps[0], precision: 4);
                Assert.Equal(0.005, massProps[1], precision: 4);
                Assert.Equal(0.0075, massProps[2], precision: 4);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void MultiFeatureCompounding_SequentialBossAndCut_SolidWorksRebuildsWithNetVolume()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_cut_5mm.SLDPRT"));
        Assert.NotNull(samplePath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"multi_feature_compound_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(samplePath!, tempPath, true);

        try
        {
            string streamName = "Contents/Config-0-ResolvedFeatures";

            // 1. Prepare file: modify Boss depth from 10mm to 20mm, and strip Cut feature
            using (var swFile = SwFile.Open(tempPath, readOnly: false))
            {
                var raw = swFile.ResolvedFeatures.RawData!;

                var folders = FlattenFolders(swFile.ResolvedFeatures.Folders);
                var bossFolder = folders.FirstOrDefault(f => f.ClassName == "moExtrusion_c" || f.DisplayName.Contains("凸台"));
                var cutFolder = folders.FirstOrDefault(f => f.ClassName == "moICE_c" || f.DisplayName.Contains("切除"));
                Assert.NotNull(bossFolder);
                Assert.NotNull(cutFolder);
                int cutOffset = cutFolder!.TagOffset;

                // SldFile/MfcExtrudeSerializer confirms moExtrusion_c block is exactly 3221 bytes long.
                // Strictly bound the replacement range so sketch2 (which begins at ~0x2F75) is never touched.
                int bossEndOffset = bossFolder!.TagOffset + 3221;

                // Slice stream to remove cut feature
                byte[] streamWithoutCut = new byte[cutOffset];
                Array.Copy(raw, 0, streamWithoutCut, 0, cutOffset);

                // Modify Boss depth ONLY within the 3221-byte moExtrusion_c block from 10mm (0.010) to 20mm (0.020)
                byte[] oldVal = BitConverter.GetBytes(0.010);
                byte[] newVal = BitConverter.GetBytes(0.020);
                byte[] oldCenter = BitConverter.GetBytes(0.005);
                byte[] newCenter = BitConverter.GetBytes(0.010);

                for (int i = bossFolder!.TagOffset; i < bossEndOffset - 8; i++)
                {
                    if (Match(streamWithoutCut, i, oldVal))
                    {
                        Array.Copy(newVal, 0, streamWithoutCut, i, 8);
                        i += 7;
                    }
                    else if (Match(streamWithoutCut, i, oldCenter))
                    {
                        Array.Copy(newCenter, 0, streamWithoutCut, i, 8);
                        i += 7;
                    }
                }

                // Decrement folder count
                ushort folderCount = BitConverter.ToUInt16(streamWithoutCut, 4);
                BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(streamWithoutCut, 4);

                // Reset sketch2 child count flag
                for (int i = streamWithoutCut.Length - 4; i >= Math.Max(0, streamWithoutCut.Length - 128); i--)
                {
                    if (streamWithoutCut[i] == 0x67 && streamWithoutCut[i + 1] == 0x00 && streamWithoutCut[i + 2] == 0x00 && streamWithoutCut[i + 3] == 0x00)
                    {
                        BitConverter.GetBytes(0u).CopyTo(streamWithoutCut, i + 4);
                        break;
                    }
                }

                swFile.SetPendingStream(streamName, streamWithoutCut);
                swFile.Save();
            }

            // 2. Inject Cut Extrude onto Sketch 2 with depth 8mm (0.008m) using SldPart.AddExtrude API
            double injectedCutDepth = 0.008; // 8mm cut
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                var cutFeat = part.AddExtrude(injectedCutDepth, isCut: true);
                Assert.NotNull(cutFeat);
                Assert.Equal(FeatureKind.Cut, cutFeat.Kind);
                part.Save();
            }

            // 3. Open in native SOLIDWORKS and verify clean ForceRebuild3
            IModelDoc2? swDoc = _fixture.OpenDocument(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should succeed for multi-feature compounded part");

                // Locate both Boss and Cut features
                IFeature? bossFeat = null;
                IFeature? cutFeat = null;
                for (var f = (IFeature?)swDoc.FirstFeature(); f != null; f = (IFeature?)f.GetNextFeature())
                {
                    string typeName = f.GetTypeName2();
                    if (typeName == "Extrusion" || typeName == "Boss")
                        bossFeat = f;
                    else if (typeName == "Cut" || typeName == "ICE")
                        cutFeat = f;
                }

                Assert.NotNull(bossFeat);
                Assert.NotNull(cutFeat);

                // Verify Boss depth is 20mm
                var bossData = bossFeat!.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(bossData);
                bossData!.AccessSelections(swDoc, null);
                try
                {
                    Assert.Equal(0.020, bossData.GetDepth(true), precision: 4);
                }
                finally
                {
                    bossData.ReleaseSelectionAccess();
                }

                // Verify Cut depth is 8mm
                var cutData = cutFeat!.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(cutData);
                cutData!.AccessSelections(swDoc, null);
                try
                {
                    Assert.Equal(injectedCutDepth, cutData.GetDepth(true), precision: 4);
                }
                finally
                {
                    cutData.ReleaseSelectionAccess();
                }

                // Verify net volume:
                // Boss: 10mm x 10mm x 20mm = 2000 mm^3 = 2.0e-6 m^3
                // Cut: pi * (3mm)^2 * 8mm = 72 * pi = 226.19467 mm^3 = 2.261947e-7 m^3
                // Net: 2000 - 226.19467 = 1773.80533 mm^3 = 1.773805e-6 m^3
                var partDoc = swDoc as IPartDoc;
                Assert.NotNull(partDoc);
                var bodies = partDoc!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double volume = massProps[3];
                double expectedNetVolume = (10.0 * 10.0 * 20.0 - Math.PI * 3.0 * 3.0 * 8.0) * 1e-9;
                Assert.Equal(expectedNetVolume, volume, precision: 8);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static System.Collections.Generic.List<SldFile.Binary.FeatureFolder> FlattenFolders(System.Collections.Generic.IEnumerable<SldFile.Binary.FeatureFolder> folders)
    {
        var list = new System.Collections.Generic.List<SldFile.Binary.FeatureFolder>();
        void Walk(System.Collections.Generic.IEnumerable<SldFile.Binary.FeatureFolder> current)
        {
            foreach (var f in current)
            {
                list.Add(f);
                if (f.Children.Count > 0)
                    Walk(f.Children);
            }
        }
        Walk(folders);
        return list;
    }

    private static bool Match(byte[] data, int offset, byte[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (data[offset + i] != pattern[i]) return false;
        }
        return true;
    }
}

