using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests.Features;

/// <summary>
/// Empirical adversarial stress and double-roundtrip test suite authored by Challenger P1.3.1.
/// Rigorously validates bit-exact double roundtrip byte preservation, extreme boundary condition stability,
/// composite lofts (profiles + guides + centerline + thin wall + tangency conditions),
/// and authentic SldPart.Authoring MFC stream emission.
/// </summary>
public class SolidFeaturesDeepAdversarialTests
{
    private readonly ITestOutputHelper _output;

    public SolidFeaturesDeepAdversarialTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static byte[] SerializeModel(IMfcSerializable model, int docVersion = 0)
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var writer = MfcArchive.CreateWriter(context: ctx);
        model.Serialize(writer);
        return writer.ToArray();
    }

    private static T DeserializeModel<T>(byte[] data, int docVersion = 0) where T : IMfcSerializable, new()
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var reader = MfcArchive.CreateReader(data, context: ctx);
        var model = new T();
        model.Serialize(reader);
        return model;
    }

    private static (T Reloaded1, T Reloaded2, byte[] B1, byte[] B2, byte[] B3) ExecuteDoubleRoundtrip<T>(T original, int docVersion = 0)
        where T : IMfcSerializable, new()
    {
        byte[] b1 = SerializeModel(original, docVersion);
        var reloaded1 = DeserializeModel<T>(b1, docVersion);
        byte[] b2 = SerializeModel(reloaded1, docVersion);
        var reloaded2 = DeserializeModel<T>(b2, docVersion);
        byte[] b3 = SerializeModel(reloaded2, docVersion);
        return (reloaded1, reloaded2, b1, b2, b3);
    }

    // =========================================================================
    // Category 1: Composite Loft with Three Profiles, Guides, Thin Wall & Tangency
    // =========================================================================

    [Fact]
    public void Loft_Composite_ThreeProfiles_Guides_ThinWall_Tangency_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "Loft_Adv_Composite",
            IsThinFeature = true,
            WallThickness = 0.0035,
            ThinWallType = 1,
            WallThicknessDirection2 = 0.0015,
            StartTangencyType = 2,
            EndTangencyType = 3,
            StartTangentLength = 1.5,
            EndTangentLength = 2.0,
            StartDraftAngle = 5.0,
            EndDraftAngle = 3.0,
            Close = false,
            Merge = true,
            MaintainTangency = true,
            GuideCurveInfluence = 1
        };

        // 3 Profiles
        original.ProfileReferences.Add(new MfcSketchCurveReference(0x101, SketchCurveRole.Profile, 0xC5));
        original.ProfileReferences.Add(new MfcSketchCurveReference(0x102, SketchCurveRole.Profile, 0xC6));
        original.ProfileReferences.Add(new MfcSketchCurveReference(0x103, SketchCurveRole.Profile, 0xC7));

        // 2 Guides
        original.GuideReferences.Add(new MfcSketchCurveReference(0x201, SketchCurveRole.Guide, 0xC8));
        original.GuideReferences.Add(new MfcSketchCurveReference(0x202, SketchCurveRole.Guide, 0xC9));

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        _output.WriteLine($"B1 Length: {b1.Length}, B2 Length: {b2.Length}, B3 Length: {b3.Length}");

        // 1. Bit-exact double roundtrip byte preservation
        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        // 2. Profile and Guide retention
        Assert.Equal(3, m1.ProfileReferences.Count);
        Assert.Equal(0x101u, m1.ProfileReferences[0].EntityId);
        Assert.Equal(0x102u, m1.ProfileReferences[1].EntityId);
        Assert.Equal(0x103u, m1.ProfileReferences[2].EntityId);

        Assert.Equal(2, m1.GuideReferences.Count);
        Assert.Equal(0x201u, m1.GuideReferences[0].EntityId);
        Assert.Equal(0x202u, m1.GuideReferences[1].EntityId);

        // 3. Thin wall retention
        Assert.True(m1.IsThinFeature);
        Assert.NotNull(m1.WallThickness);
        Assert.Equal(0.0035, m1.WallThickness.Value, precision: 6);
        Assert.Equal(1u, m1.ThinWallType);
        Assert.Equal(0.0015, m1.WallThicknessDirection2, precision: 6);

        // 4. Tangency condition retention
        Assert.Equal(2, m1.StartTangencyType);
        Assert.Equal(3, m1.EndTangencyType);
        Assert.True(m1.Resolution.StartTangencyType);
        Assert.True(m1.Resolution.EndTangencyType);

        // 5. Check idempotence on m2
        Assert.Equal(3, m2.ProfileReferences.Count);
        Assert.Equal(2, m2.GuideReferences.Count);
        Assert.True(m2.IsThinFeature);
        Assert.Equal(2, m2.StartTangencyType);
        Assert.Equal(3, m2.EndTangencyType);
    }

    [Fact]
    public void Loft_Composite_Centerline_Guides_ThinWall_Tangency_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "Loft_Centerline_Thin_Tangency",
            IsThinFeature = true,
            WallThickness = 0.002,
            ThinWallType = 0,
            StartTangencyType = 1,
            EndTangencyType = 1,
            Centerline = new SketchCurveReference { EntityId = 0x301, Role = SketchCurveRole.Centerline, HeaderSelectionId = 0xCA }
        };

        original.ProfileReferences.Add(new MfcSketchCurveReference(0x101, SketchCurveRole.Profile, 0xC5));
        original.ProfileReferences.Add(new MfcSketchCurveReference(0x102, SketchCurveRole.Profile, 0xC6));
        original.GuideReferences.Add(new MfcSketchCurveReference(0x201, SketchCurveRole.Guide, 0xC7));

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(2, m1.ProfileReferences.Count);
        Assert.NotNull(m1.Centerline);
        Assert.Equal(0x301u, m1.Centerline.EntityId);
        Assert.True(m1.IsThinFeature);
        Assert.Equal(0.002, m1.WallThickness!.Value, precision: 6);
        Assert.Equal(1, m1.StartTangencyType);
        Assert.Equal(1, m1.EndTangencyType);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    public void Loft_AllTangencyPairs_With_ThinWall_DoubleRoundtripMatrix(int startTan, int endTan)
    {
        var original = new LoftFeatureModel
        {
            FeatureName = $"Loft_Tan_{startTan}_{endTan}_Thin",
            IsThinFeature = true,
            WallThickness = 0.004,
            ThinWallType = 2,
            WallThicknessDirection2 = 0.002,
            StartTangencyType = startTan,
            EndTangencyType = endTan
        };
        original.ProfileReferences.Add(new MfcSketchCurveReference(10, SketchCurveRole.Profile, 0xC5));
        original.ProfileReferences.Add(new MfcSketchCurveReference(20, SketchCurveRole.Profile, 0xC6));
        original.ProfileReferences.Add(new MfcSketchCurveReference(30, SketchCurveRole.Profile, 0xC7));

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(startTan, m1.StartTangencyType);
        Assert.Equal(endTan, m1.EndTangencyType);
        Assert.True(m1.IsThinFeature);
        Assert.Equal(0.004, m1.WallThickness!.Value, precision: 6);
    }

    [Fact]
    public void Loft_Cut_And_Surface_With_ThinWall_And_Tangency_DoubleRoundtrip()
    {
        // Cut Loft
        var cutLoft = new LoftFeatureModel
        {
            FeatureName = "Cut_Loft_Adv",
            IsCut = true,
            IsThinFeature = true,
            WallThickness = 0.001,
            StartTangencyType = 1,
            EndTangencyType = 2
        };
        cutLoft.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        cutLoft.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));

        var (cutM1, _, cutB1, cutB2, cutB3) = ExecuteDoubleRoundtrip(cutLoft);
        Assert.Equal(cutB1, cutB2);
        Assert.Equal(cutB2, cutB3);
        Assert.True(cutM1.IsCut);
        Assert.False(cutM1.IsSurface);
        Assert.Equal(LoftFeatureModel.CutClassName, cutM1.ClassName);
        Assert.Equal(1, cutM1.StartTangencyType);
        Assert.Equal(2, cutM1.EndTangencyType);

        // Surface Loft
        var surfLoft = new LoftFeatureModel
        {
            FeatureName = "Surface_Loft_Adv",
            IsSurface = true,
            IsThinFeature = false,
            StartTangencyType = 3,
            EndTangencyType = 1
        };
        surfLoft.ProfileReferences.Add(new MfcSketchCurveReference(11, SketchCurveRole.Profile, 0));
        surfLoft.ProfileReferences.Add(new MfcSketchCurveReference(12, SketchCurveRole.Profile, 1));

        var (surfM1, _, surfB1, surfB2, surfB3) = ExecuteDoubleRoundtrip(surfLoft);
        Assert.Equal(surfB1, surfB2);
        Assert.Equal(surfB2, surfB3);
        Assert.True(surfM1.IsSurface);
        Assert.False(surfM1.IsCut);
        Assert.Equal(LoftFeatureModel.SurfaceClassName, surfM1.ClassName);
        Assert.Equal(3, surfM1.StartTangencyType);
        Assert.Equal(1, surfM1.EndTangencyType);
    }

    [Fact]
    public void Loft_ExtremeBoundaryInputs_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "Loft_Extreme_Boundary",
            IsThinFeature = true,
            WallThickness = 1e-8,
            ThinWallType = 3,
            WallThicknessDirection2 = 500.0,
            StartTangencyType = 1,
            EndTangencyType = 2
        };

        // 8 profiles and 6 guides
        for (uint i = 1; i <= 8; i++)
        {
            original.ProfileReferences.Add(new MfcSketchCurveReference(i * 10, SketchCurveRole.Profile, (ushort)(0xC0 + i)));
        }
        for (uint j = 1; j <= 6; j++)
        {
            original.GuideReferences.Add(new MfcSketchCurveReference(j * 100, SketchCurveRole.Guide, (ushort)(0xD0 + j)));
        }

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(8, m1.ProfileReferences.Count);
        Assert.Equal(6, m1.GuideReferences.Count);
        Assert.True(m1.IsThinFeature);
        Assert.Equal(1e-8, m1.WallThickness!.Value, precision: 12);
        Assert.Equal(500.0, m1.WallThicknessDirection2, precision: 4);
    }

    // =========================================================================
    // Category 2: Sweep Adversarial Stress & Double Roundtrip
    // =========================================================================

    [Fact]
    public void Sweep_Composite_Guides_ThinWall_Twist_DoubleRoundtrip()
    {
        var original = new SweepFeatureModel
        {
            FeatureName = "Sweep_Composite_Adv",
            ProfileReference = new MfcSketchCurveReference(10, SketchCurveRole.Profile, 0xC5),
            PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path, 0xC6),
            IsThinFeature = true,
            WallThickness = 0.002,
            WallThicknessDirection2 = 0.001,
            ThinWallType = 2,
            TwistControlType = 1,
            TwistAngleRad = 3.141592653589793,
            Merge = true
        };

        original.GuideReferences.Add(new MfcSketchCurveReference(21, SketchCurveRole.Guide, 0xC7));
        original.GuideReferences.Add(new MfcSketchCurveReference(22, SketchCurveRole.Guide, 0xC8));
        original.GuideReferences.Add(new MfcSketchCurveReference(23, SketchCurveRole.Guide, 0xC9));

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.NotNull(m1.ProfileReference);
        Assert.Equal(10u, m1.ProfileReference.EntityId);
        Assert.NotNull(m1.PathReference);
        Assert.Equal(11u, m1.PathReference.EntityId);
        Assert.Equal(3, m1.GuideReferences.Count);
        Assert.True(m1.IsThinFeature);
        Assert.Equal(0.002, m1.WallThickness!.Value, precision: 6);
        Assert.Equal(2u, m1.ThinWallType);
        Assert.Equal(3.141592653589793, m1.TwistAngleRad!.Value, precision: 6);
    }

    [Fact]
    public void Sweep_CircularProfile_Cut_ThinWall_DoubleRoundtrip()
    {
        var original = new SweepFeatureModel
        {
            FeatureName = "Sweep_Circ_Cut",
            CircularProfile = true,
            CircularProfileDiameter = 0.045,
            IsCut = true,
            PathReference = new MfcSketchCurveReference(50, SketchCurveRole.Path, 0xC5),
            IsThinFeature = true,
            WallThickness = 0.0015
        };

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.True(m1.CircularProfile);
        Assert.Equal(0.045, m1.CircularProfileDiameter!.Value, precision: 6);
        Assert.True(m1.IsCut);
        Assert.True(m1.IsThinFeature);
        Assert.Equal(0.0015, m1.WallThickness!.Value, precision: 6);
    }

    // =========================================================================
    // Category 3: Shell & Draft Adversarial Stress & Double Roundtrip
    // =========================================================================

    [Fact]
    public void Shell_Composite_MultiThickness_And_RemovedFaces_DoubleRoundtrip()
    {
        var removed = Enumerable.Range(1, 6)
            .Select(i => new EntityReference { EntityId = (uint)(i * 10), EntityType = SelectionEntityType.Face })
            .ToArray();

        var original = new ShellFeatureModel
        {
            FeatureName = "Shell_Adv_Multi",
            Thickness = 0.004,
            Direction = 1,
            FacesRemoved = removed
        };

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(0.004, m1.Thickness!.Value, precision: 6);
        Assert.Equal(1, m1.Direction);
        Assert.Equal(6, m1.FacesRemovedCount);
    }

    [Fact]
    public void Draft_Composite_MultipleNeutralPlanes_And_Faces_DoubleRoundtrip()
    {
        var faces = Enumerable.Range(1, 8)
            .Select(i => new EntityReference { EntityId = (uint)(i * 20), EntityType = SelectionEntityType.Face })
            .ToArray();

        var original = new DraftFeatureModel
        {
            FeatureName = "Draft_Adv_MultiFace",
            Angle = 0.218166, // 12.5 deg
            Type = 1,
            ReverseDirection = true,
            NeutralPlane = new EntityReference { EntityId = 999, EntityType = SelectionEntityType.Face },
            FacesToDraft = faces
        };

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(0.218166, m1.Angle!.Value, precision: 5);
        Assert.Equal(1, m1.Type);
        Assert.True(m1.ReverseDirection);
        Assert.NotNull(m1.NeutralPlane);
        Assert.Equal(999u, m1.NeutralPlane.EntityId);
        Assert.Equal(8, m1.FacesToDraft!.Count);
    }

    // =========================================================================
    // Category 4: Body Operations Adversarial Stress & Double Roundtrip
    // =========================================================================

    [Fact]
    public void CombineBodies_Operations_And_LargeToolBodyList_DoubleRoundtrip()
    {
        int[] ops = { 0, 1, 2 }; // Add, Subtract, Common
        foreach (var op in ops)
        {
            var tools = Enumerable.Range(1, 15)
                .Select(i => new EntityReference { EntityId = (uint)(i * 50), EntityType = SelectionEntityType.Body })
                .ToArray();

            var original = new CombineBodiesFeatureModel
            {
                FeatureName = $"Combine_Op{op}_15Tools",
                OperationType = op,
                MainBody = new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Body },
                BodiesToCombine = tools
            };

            var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

            Assert.Equal(b1, b2);
            Assert.Equal(b2, b3);

            Assert.Equal(op, m1.OperationType);
            Assert.NotNull(m1.MainBody);
            Assert.Equal(1u, m1.MainBody.EntityId);
            Assert.Equal(15, m1.BodiesToCombineCount);
            Assert.Equal(750u, m1.BodiesToCombine![14].EntityId);
        }
    }

    [Fact]
    public void SplitBody_MultipleTrimTools_ConsumeToggle_DoubleRoundtrip()
    {
        bool[] consumeFlags = { true, false };
        foreach (var flag in consumeFlags)
        {
            var trimTools = Enumerable.Range(1, 5)
                .Select(i => new EntityReference { EntityId = (uint)(i * 15), EntityType = SelectionEntityType.Face })
                .ToArray();

            var original = new SplitBodyFeatureModel
            {
                FeatureName = $"Split_Consume_{flag}",
                Consume = flag,
                State = flag ? 2 : 0,
                TrimTools = trimTools,
                TargetBodyModel = new MfcCompSolidBodyModel { RefBodyId = 101 },
                TemplatePath = "C:\\Templates\\Adv.prtdot",
                OverrideDefaultTemplateSettings = true
            };

            var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

            Assert.Equal(b1, b2);
            Assert.Equal(b2, b3);

            Assert.Equal(flag, m1.Consume);
            Assert.Equal(5, m1.TrimToolsCount);
            Assert.NotNull(m1.TargetBodyModel);
            Assert.Equal(101, m1.TargetBodyModel.RefBodyId);
        }
    }

    [Fact]
    public void DeleteBody_FiftyBodies_KeepBodiesToggle_DoubleRoundtrip()
    {
        bool[] keepFlags = { true, false };
        foreach (var keep in keepFlags)
        {
            var bodies = Enumerable.Range(1, 50)
                .Select(i => new EntityReference { EntityId = (uint)(i * 100), EntityType = SelectionEntityType.Body })
                .ToArray();

            var original = new DeleteBodyFeatureModel
            {
                FeatureName = $"DeleteBody_50_{keep}",
                KeepBodies = keep,
                Bodies = bodies
            };

            var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

            Assert.Equal(b1, b2);
            Assert.Equal(b2, b3);

            Assert.Equal(keep, m1.KeepBodies);
            Assert.Equal(50, m1.BodiesCount);
            Assert.Equal(5000u, m1.Bodies![49].EntityId);
        }
    }

    [Fact]
    public void Rib_NormalAndParallel_ExtremeDraft_DoubleRoundtrip()
    {
        bool[] parallelFlags = { false, true };
        foreach (var parallel in parallelFlags)
        {
            var original = new RibFeatureModel
            {
                FeatureName = $"Rib_Parallel_{parallel}",
                Thickness = 0.003,
                DraftAngle = 4.5,
                DraftOutward = true,
                DraftFromWall = false,
                EnableDraft = true,
                IsTwoSided = true,
                ExtrusionDirection = parallel ? 1 : 0,
                FlipSide = false,
                RefSketchIndex = 1,
                BodyModel = new MfcCompSolidBodyModel { RefBodyId = 99 }
            };

            var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

            Assert.Equal(b1, b2);
            Assert.Equal(b2, b3);

            Assert.Equal(0.003, m1.Thickness!.Value, precision: 6);
            Assert.Equal(4.5, m1.DraftAngle!.Value, precision: 5);
            Assert.True(m1.DraftOutward);
            Assert.True(m1.EnableDraft);
            Assert.NotNull(m1.BodyModel);
            Assert.Equal(99, m1.BodyModel.RefBodyId);
        }
    }

    // =========================================================================
    // Category 5: Authentic SldPart.Authoring MFC Stream Emission Integrity
    // =========================================================================

    [Fact]
    public void SldPart_Authoring_AddLoft_GenuineSerialization_StreamIntegrity()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        var loftModel = new LoftFeatureModel
        {
            FeatureName = "放样_真实MFC注入",
            IsThinFeature = true,
            WallThickness = 0.0028,
            ThinWallType = 1,
            StartTangencyType = 2,
            EndTangencyType = 3
        };
        loftModel.ProfileReferences.Add(new MfcSketchCurveReference(101, SketchCurveRole.Profile, 0xC5));
        loftModel.ProfileReferences.Add(new MfcSketchCurveReference(102, SketchCurveRole.Profile, 0xC6));
        loftModel.ProfileReferences.Add(new MfcSketchCurveReference(103, SketchCurveRole.Profile, 0xC7));
        loftModel.GuideReferences.Add(new MfcSketchCurveReference(201, SketchCurveRole.Guide, 0xC8));

        int initialFeatureCount;
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            initialFeatureCount = part.Features.Count;
            _output.WriteLine($"Initial features ({part.Features.Count}): " + string.Join(", ", part.Features.Select(f => f.Name)));
            var addedFeature = part.AddLoft(loftModel);
            _output.WriteLine($"Added feature: Name={addedFeature.Name}, Kind={addedFeature.Kind}");
            _output.WriteLine($"After AddLoft features ({part.Features.Count}): " + string.Join(", ", part.Features.Select(f => f.Name)));

            Assert.NotNull(addedFeature);
            Assert.Equal("放样_真实MFC注入", addedFeature.Name);
            Assert.Equal(FeatureKind.Loft, addedFeature.Kind);
            Assert.Contains(part.Features, f => f.Name == "放样_真实MFC注入" && f.Kind == FeatureKind.Loft);

            part.Save();
        }

        // Direct low-level CFB inspection: verify "moBlend_c" and "ParallelPlaneDistanceDim_c" are in stream
        byte[]? streamBytes;
        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            Assert.Contains(reopened.Features, f => f.Name == "放样_真实MFC注入" && f.Kind == FeatureKind.Loft);
            var lastFeat = reopened.Features.Last();
            Assert.Equal("放样_真实MFC注入", lastFeat.Name);
            Assert.Equal(FeatureKind.Loft, lastFeat.Kind);
            streamBytes = reopened.GetStreamBytes("Contents/Config-0-ResolvedFeatures");
        }
        Assert.NotNull(streamBytes);
        int blendIndex = MfcPrimitives.FindAscii(streamBytes!, "moBlend_c");
        int dimIndex = MfcPrimitives.FindAscii(streamBytes!, "ParallelPlaneDistanceDim_c");

        Assert.True(blendIndex >= 0, "Expected authentic 'moBlend_c' MFC class header in ResolvedFeatures stream");
        Assert.True(dimIndex >= 0, "Expected authentic 'ParallelPlaneDistanceDim_c' MFC thin wall in ResolvedFeatures stream");
    }

    [Fact]
    public void SldPart_Authoring_AddSweep_Shell_Draft_Combine_GenuineSerialization()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            // 1. Add Sweep
            var sweep = new SweepFeatureModel
            {
                FeatureName = "扫描_真实注入",
                CircularProfile = true,
                CircularProfileDiameter = 0.03,
                PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path)
            };
            part.AddSweep(sweep);

            // 2. Add Shell
            var shell = new ShellFeatureModel
            {
                FeatureName = "抽壳_真实注入",
                Thickness = 0.003
            };
            part.AddShell(shell);

            // 3. Add Draft
            var draft = new DraftFeatureModel
            {
                FeatureName = "拔模_真实注入",
                Angle = 0.087266 // ~5 deg
            };
            part.AddDraft(draft);

            // 4. Add Combine
            var combine = new CombineBodiesFeatureModel
            {
                FeatureName = "组合_真实注入",
                OperationType = 0
            };
            part.AddCombineBodies(combine);

            Assert.Contains(part.Features, f => f.Name == "扫描_真实注入" && f.Kind == FeatureKind.Sweep);
            Assert.Contains(part.Features, f => f.Name == "抽壳_真实注入" && f.Kind == FeatureKind.Shell);
            Assert.Contains(part.Features, f => f.Name == "拔模_真实注入" && f.Kind == FeatureKind.Draft);
            Assert.Contains(part.Features, f => f.Name == "组合_真实注入" && f.Kind == FeatureKind.CombineBodies);
            part.Save();
        }

        // Reopen and inspect stream
        byte[]? streamBytes;
        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            Assert.Contains(reopened.Features, f => f.Name == "扫描_真实注入" && f.Kind == FeatureKind.Sweep);
            Assert.Contains(reopened.Features, f => f.Name == "抽壳_真实注入" && f.Kind == FeatureKind.Shell);
            Assert.Contains(reopened.Features, f => f.Name == "拔模_真实注入" && f.Kind == FeatureKind.Draft);
            Assert.Contains(reopened.Features, f => f.Name == "组合_真实注入" && f.Kind == FeatureKind.CombineBodies);
            streamBytes = reopened.GetStreamBytes("Contents/Config-0-ResolvedFeatures");
        }
        Assert.NotNull(streamBytes);
        Assert.True(MfcPrimitives.FindAscii(streamBytes!, "moSweep_c") >= 0, "Missing moSweep_c");
        Assert.True(MfcPrimitives.FindAscii(streamBytes!, "moShell_c") >= 0, "Missing moShell_c");
        Assert.True(MfcPrimitives.FindAscii(streamBytes!, "moDraft_c") >= 0, "Missing moDraft_c");
        Assert.True(MfcPrimitives.FindAscii(streamBytes!, "moCombineBodies_c") >= 0, "Missing moCombineBodies_c");
    }
}
