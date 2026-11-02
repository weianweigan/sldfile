using System;
using System.IO;
using System.Linq;
using Xunit;
using SldFile;
using SldFile.Ole;
using SldFile.Tests.Integration.Framework;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SldFile.Tests.Integration.Tests;

[Collection("SolidWorksIntegration")]
public class ExtrudeIntegrationTests
{
    private readonly SolidWorksApplicationFixture _fixture;

    public ExtrudeIntegrationTests(SolidWorksApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    private static string? LocateTestFile(string relativeOrName)
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, relativeOrName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", relativeOrName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SldFileTests", "TestFiles", relativeOrName)
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }
        return null;
    }

    [SolidWorksFact]
    public void NativeExtrude_10mm_OpensAndRebuildsCleanly()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(samplePath);

        IModelDoc2? swDoc = _fixture.OpenDocumentVerified(samplePath!, readOnly: true);
        Assert.NotNull(swDoc);

        try
        {
            bool rebuildSuccess = swDoc!.ForceRebuild3(false);
            Assert.True(rebuildSuccess, "ForceRebuild3 should succeed on native sample");
            SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

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
                Assert.Equal(0.010, depth, precision: 4);
            }
            finally
            {
                extData.ReleaseSelectionAccess();
            }

            var part = swDoc as IPartDoc;
            Assert.NotNull(part);
            var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            Assert.NotNull(bodies);
            Assert.Single(bodies!);
            var body = bodies![0] as IBody2;
            Assert.NotNull(body);

            var massProps = (double[])body!.GetMassProperties(0);
            double volume = massProps[3];
            Assert.Equal(1e-6, volume, precision: 8);
        }
        finally
        {
            _fixture.CloseDocument(samplePath!);
        }
    }

    [SolidWorksFact]
    public void ModifiedExtrude_25mm_OpensAndRebuildsToNewVolume()
    {
        string? samplePath = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(samplePath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"extrude_25mm_test_{Guid.NewGuid():N}.SLDPRT");
        File.Copy(samplePath!, tempPath, true);

        try
        {
            string streamName = "Contents/Config-0-ResolvedFeatures";
            using (var swFile = SwFile.Open(tempPath, readOnly: false))
            {
                var raw = (byte[])swFile.ResolvedFeatures.RawData!.Clone();

                // Replace all occurrences of 0.010 with 0.025
                byte[] oldVal = BitConverter.GetBytes(0.010);
                byte[] newVal = BitConverter.GetBytes(0.025);

                byte[] oldCenter = BitConverter.GetBytes(0.005);
                byte[] newCenter = BitConverter.GetBytes(0.0125);

                // Replace depth in raw bytes
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

            // 2. Open via real SOLIDWORKS application
            IModelDoc2? swDoc = _fixture.OpenDocumentVerified(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                // Force rebuild to recalculate geometry from modified feature parameters!
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should succeed on modified file");
                SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

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
                    Assert.Equal(0.025, depth, precision: 4);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // Verify updated geometry volume: 10mm x 10mm x 25mm = 2.5e-6 m^3!
                var part = swDoc as IPartDoc;
                Assert.NotNull(part);
                var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                Assert.NotNull(bodies);
                Assert.Single(bodies!);
                var body = bodies![0] as IBody2;
                Assert.NotNull(body);

                var massProps = (double[])body!.GetMassProperties(0);
                double volume = massProps[3];
                Assert.Equal(2.5e-6, volume, precision: 8);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }

            // 3. SldFile roundtrip verification
            using (var reopened = SwFile.Open(tempPath))
            {
                var extFeat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion && !f.Name.Contains("切除")));
                var extDef = Assert.IsType<ExtrudeFeatureDefinitionData>(extFeat.Definition);
                Assert.Equal(0.025, extDef.Depth, precision: 4);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectExtrudeOntoSketch_BuildsSolidBodyInSolidWorks()
        => VerifyInjectedExtrude(false);

    [SolidWorksFact]
    public void InjectReverseExtrudeOntoSketch_BuildsSolidOnOppositeSide()
        => VerifyInjectedExtrude(true);

    private void VerifyInjectedExtrude(bool reverse)
    {
        string? extSample = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(extSample);

        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_extrude_test_{Guid.NewGuid():N}.SLDPRT");

        try
        {
            // 1. Prepare stripped sketch part from extrude_10mm (remove moExtrusion_c)
            byte[] extBlock;
            byte[] sketchOnly;
            using (var swExt = SwFile.Open(extSample!, readOnly: true))
            {
                var rawExt = swExt.ResolvedFeatures.RawData!;
                sketchOnly = new byte[0x22E0];
                Array.Copy(rawExt, 0, sketchOnly, 0, 0x22E0);

                // Adjust folder count: 20 -> 19
                ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
                BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);

                // Set sketch child count to 0 at 0x22D4
                sketchOnly[0x22D4] = 0x00;

                // Extract original extrusion block
                extBlock = new byte[rawExt.Length - 0x22E0];
                Array.Copy(rawExt, 0x22E0, extBlock, 0, extBlock.Length);
            }

            // Save the stripped sketch-only file
            File.Copy(extSample!, tempPath, true);
            string streamName = "Contents/Config-0-ResolvedFeatures";
            using (var swSketchOnly = SwFile.Open(tempPath, readOnly: false))
            {
                swSketchOnly.SetPendingStream(streamName, sketchOnly);
                swSketchOnly.Save();
            }

            // Verify sketch-only file has NO extrusion feature
            using (var swSketchOnly = SwFile.Open(tempPath, readOnly: true))
            {
                Assert.DoesNotContain(swSketchOnly.Features, f => f.Kind == FeatureKind.Extrusion);
            }

            // 2. Open sketch-only file, use high-level SldPart.AddExtrude API to inject 15mm extrusion
            double targetDepth = 0.015;
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                var createdFeature = part.AddExtrude(new ExtrudeFeatureDefinitionData
                {
                    Depth = targetDepth,
                    ReverseDirection = reverse
                });
                Assert.NotNull(createdFeature);
                Assert.Equal(FeatureKind.Extrusion, createdFeature.Kind);
                part.Save();
            }

            // 3. Open in native SOLIDWORKS and call ForceRebuild3
            IModelDoc2? swDoc = _fixture.OpenDocumentVerified(tempPath, readOnly: true);
            Assert.NotNull(swDoc);

            try
            {
                bool rebuildSuccess = swDoc!.ForceRebuild3(false);
                Assert.True(rebuildSuccess, "ForceRebuild3 should rebuild the injected feature successfully");
                SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

                // Verify feature exists in tree
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

                // Verify depth is 15mm
                var extData = swFeat.GetDefinition() as IExtrudeFeatureData2;
                Assert.NotNull(extData);
                extData!.AccessSelections(swDoc, null);
                try
                {
                    double depth = extData.GetDepth(true);
                    Assert.Equal(targetDepth, depth, precision: 4);
                    Assert.Equal(reverse, extData.ReverseDirection);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // Verify solid body created! 10mm x 10mm x 15mm = 1.5e-6 m^3
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
                Assert.Equal(reverse ? -targetDepth / 2 : targetDepth / 2, massProps[2], precision: 8);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }

            // 4. Roundtrip check with SldFile
            using (var reopened = SwFile.Open(tempPath))
            {
                var extFeat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
                var extDef = Assert.IsType<ExtrudeFeatureDefinitionData>(extFeat.Definition);
                Assert.Equal(targetDepth, extDef.Depth, precision: 4);
                Assert.Equal(reverse, extDef.ReverseDirection);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectTwoDirectionExtrudeOntoSketch_BuildsBodyOnBothSides()
    {
        string? extSample = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(extSample);
        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_two_direction_{Guid.NewGuid():N}.SLDPRT");

        try
        {
            CreateSketchOnlyPart(extSample!, tempPath);
            double depth1 = 0.015;
            double depth2 = 0.005;
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                part.AddExtrude(new ExtrudeFeatureDefinitionData
                {
                    Depth = depth1,
                    BothDirections = true,
                    DepthDirection2 = depth2
                });
                part.Save();
            }

            IModelDoc2? swDoc = _fixture.OpenDocumentVerified(tempPath, readOnly: true);
            Assert.NotNull(swDoc);
            try
            {
                Assert.True(swDoc!.ForceRebuild3(false));
                SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

                var extData = GetSingleExtrusionDefinition(swDoc);
                extData.AccessSelections(swDoc, null);
                try
                {
                    Assert.True(extData.BothDirections);
                    Assert.False(extData.ReverseDirection);
                    Assert.Equal(depth1, extData.GetDepth(true), precision: 6);
                    Assert.Equal(depth2, extData.GetDepth(false), precision: 6);
                    Assert.Equal(0, extData.GetEndCondition(false));
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // 10mm x 10mm x (15mm + 5mm) = 2.0e-6 m3, centroid at (D1 - D2)/2.
                var massProps = GetSingleBodyMassProperties(swDoc);
                Assert.Equal(2.0e-6, massProps.Volume, precision: 9);
                Assert.Equal((depth1 - depth2) / 2.0, massProps.CentroidZ, precision: 6);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }

            using (var reopened = SwFile.Open(tempPath))
            {
                var extDef = Assert.IsType<ExtrudeFeatureDefinitionData>(
                    reopened.Features.Single(f => f.Kind == FeatureKind.Extrusion).Definition);
                Assert.True(extDef.BothDirections);
                Assert.Equal(depth1, extDef.GetDepth(true), 12);
                Assert.Equal(depth2, extDef.GetDepth(false), 12);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectDraftExtrudeOntoSketch_BuildsTaperedBody()
    {
        string? extSample = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(extSample);
        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_draft_{Guid.NewGuid():N}.SLDPRT");

        try
        {
            CreateSketchOnlyPart(extSample!, tempPath);
            double depth = 0.012;
            double angle = 6.0 * Math.PI / 180.0;
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                part.AddExtrude(new ExtrudeFeatureDefinitionData
                {
                    Depth = depth,
                    DraftWhileExtruding = true,
                    DraftAngle = angle
                });
                part.Save();
            }

            IModelDoc2? swDoc = _fixture.OpenDocumentVerified(tempPath, readOnly: true);
            Assert.NotNull(swDoc);
            try
            {
                Assert.True(swDoc!.ForceRebuild3(false));
                SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

                var extData = GetSingleExtrusionDefinition(swDoc);
                extData.AccessSelections(swDoc, null);
                try
                {
                    Assert.True(extData.GetDraftWhileExtruding(true));
                    Assert.False(extData.GetDraftWhileExtruding(false));
                    Assert.Equal(angle, extData.GetDraftAngle(true), precision: 9);
                    Assert.Equal(0.0, extData.GetDraftAngle(false), precision: 9);
                    Assert.Equal(depth, extData.GetDepth(true), precision: 6);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                double half = 0.005;
                double topHalf = half - depth * Math.Tan(angle);
                double area1 = (2 * half) * (2 * half);
                double area2 = (2 * topHalf) * (2 * topHalf);
                double sqrtArea = Math.Sqrt(area1 * area2);
                double expectedVolume = depth / 3.0 * (area1 + area2 + sqrtArea);
                double expectedCentroid = depth * (area1 + 2 * sqrtArea + 3 * area2) /
                                          (4.0 * (area1 + sqrtArea + area2));

                var massProps = GetSingleBodyMassProperties(swDoc);
                Assert.Equal(expectedVolume, massProps.Volume, precision: 9);
                Assert.Equal(expectedCentroid, massProps.CentroidZ, precision: 6);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }

            using (var reopened = SwFile.Open(tempPath))
            {
                var extDef = Assert.IsType<ExtrudeFeatureDefinitionData>(
                    reopened.Features.Single(f => f.Kind == FeatureKind.Extrusion).Definition);
                Assert.True(extDef.DraftWhileExtruding);
                Assert.Equal(angle, extDef.DraftAngle, 12);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    [SolidWorksFact]
    public void InjectOffsetExtrudeOntoSketch_StartsFromOffsetPlane()
    {
        string? extSample = LocateTestFile(Path.Combine("Features2026", "extrude_10mm.SLDPRT"));
        Assert.NotNull(extSample);
        var tempPath = Path.Combine(Path.GetTempPath(), $"inject_offset_{Guid.NewGuid():N}.SLDPRT");

        try
        {
            CreateSketchOnlyPart(extSample!, tempPath);
            double depth = 0.010;
            double offset = 0.004;
            using (var swPart = SwFile.Open(tempPath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(swPart);
                part.AddExtrude(new ExtrudeFeatureDefinitionData
                {
                    Depth = depth,
                    FromType = 3,
                    FromOffsetDistance = offset
                });
                part.Save();
            }

            IModelDoc2? swDoc = _fixture.OpenDocumentVerified(tempPath, readOnly: true);
            Assert.NotNull(swDoc);
            try
            {
                Assert.True(swDoc!.ForceRebuild3(false));
                SolidWorksApplicationFixture.VerifyExtrusionFeatureStatus(swDoc);

                var extData = GetSingleExtrusionDefinition(swDoc);
                extData.AccessSelections(swDoc, null);
                try
                {
                    Assert.Equal(3, extData.FromType);
                    Assert.Equal(offset, extData.FromOffsetDistance, precision: 6);
                    Assert.False(extData.FromOffsetReverse);
                    Assert.Equal(depth, extData.GetDepth(true), precision: 6);
                }
                finally
                {
                    extData.ReleaseSelectionAccess();
                }

                // Volume is unchanged; the body starts at Z = offset and ends at offset + depth.
                var massProps = GetSingleBodyMassProperties(swDoc);
                Assert.Equal(1.0e-6, massProps.Volume, precision: 9);
                Assert.Equal(offset + depth / 2.0, massProps.CentroidZ, precision: 6);
            }
            finally
            {
                _fixture.CloseDocument(tempPath);
            }

            using (var reopened = SwFile.Open(tempPath))
            {
                var extDef = Assert.IsType<ExtrudeFeatureDefinitionData>(
                    reopened.Features.Single(f => f.Kind == FeatureKind.Extrusion).Definition);
                Assert.Equal(3u, extDef.FromType);
                Assert.Equal(offset, extDef.FromOffsetDistance, 12);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static void CreateSketchOnlyPart(string extSample, string tempPath)
    {
        byte[] sketchOnly;
        using (var swExt = SwFile.Open(extSample, readOnly: true))
        {
            var rawExt = swExt.ResolvedFeatures.RawData!;
            sketchOnly = new byte[0x22E0];
            Array.Copy(rawExt, 0, sketchOnly, 0, 0x22E0);

            ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
            sketchOnly[0x22D4] = 0x00;
        }

        File.Copy(extSample, tempPath, true);
        using (var swSketchOnly = SwFile.Open(tempPath, readOnly: false))
        {
            swSketchOnly.SetPendingStream("Contents/Config-0-ResolvedFeatures", sketchOnly);
            swSketchOnly.Save();
        }
    }

    private static IExtrudeFeatureData2 GetSingleExtrusionDefinition(IModelDoc2 swDoc)
    {
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
        var extData = swFeat!.GetDefinition() as IExtrudeFeatureData2;
        Assert.NotNull(extData);
        return extData!;
    }

    private static (double Volume, double CentroidZ) GetSingleBodyMassProperties(IModelDoc2 swDoc)
    {
        var part = swDoc as IPartDoc;
        Assert.NotNull(part);
        var bodies = part!.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
        Assert.NotNull(bodies);
        Assert.Single(bodies!);
        var body = bodies![0] as IBody2;
        Assert.NotNull(body);
        var massProps = (double[])body!.GetMassProperties(0);
        return (massProps[3], massProps[2]);
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
