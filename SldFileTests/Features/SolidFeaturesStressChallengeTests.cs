using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Features;

/// <summary>
/// Empirical adversarial stress test harness executed by Challenger P1.1.
/// Tests all 8 MFC solid feature models + MfcSketchCurveReference for:
/// 1. Bit-exact double roundtrip equality: Serialize(A) -> b1; Deserialize(b1) -> A'; Serialize(A') -> b2; b1 == b2.
/// 2. Attribute fidelity across serialization cycles.
/// 3. Extreme boundary conditions: 0 thickness, negative/extreme angles, multi-body lists (0, 1, 5, 10), empty/full references.
/// </summary>
public class SolidFeaturesStressChallengeTests
{
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

    private static (T Reloaded, byte[] B1, byte[] B2) ExecuteDoubleRoundtrip<T>(T original, int docVersion = 0) where T : IMfcSerializable, new()
    {
        byte[] b1 = SerializeModel(original, docVersion);
        var reloaded = DeserializeModel<T>(b1, docVersion);
        byte[] b2 = SerializeModel(reloaded, docVersion);
        return (reloaded, b1, b2);
    }

    // =========================================================================
    // 1. MfcSketchCurveReference Stress Tests
    // =========================================================================

    [Fact]
    public void MfcSketchCurveReference_ExtremeValues_DoubleRoundtrip()
    {
        var original = new MfcSketchCurveReference
        {
            EntityId = uint.MaxValue,
            ArchiveSerial = uint.MaxValue,
            Flags = 0xFFFFFFFF,
            RoleTag = 0xFFFFFFFF,
            Opaque28 = 0
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(52, b1.Length);
        Assert.Equal(b1, b2);
        Assert.Equal(uint.MaxValue, reloaded.EntityId);
        Assert.Equal(uint.MaxValue, reloaded.ArchiveSerial);
        Assert.Equal(0xFFFFFFFF, reloaded.Flags);
        Assert.Equal(0xFFFFFFFF, reloaded.RoleTag);
        Assert.Equal(0u, reloaded.Opaque28);
    }

    [Fact]
    public void MfcSketchCurveReference_ZeroValues_DoubleRoundtrip()
    {
        var original = new MfcSketchCurveReference
        {
            EntityId = 0,
            ArchiveSerial = 0,
            Flags = 0,
            RoleTag = 0,
            Opaque28 = 0
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(52, b1.Length);
        Assert.Equal(b1, b2);
        Assert.Equal(0u, reloaded.EntityId);
        Assert.Equal(0u, reloaded.ArchiveSerial);
        Assert.Equal(0u, reloaded.Flags);
        Assert.Equal(0u, reloaded.RoleTag);
        Assert.Equal(0u, reloaded.Opaque28);
    }

    // =========================================================================
    // 2. SweepFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void Sweep_CircularProfile_ExtremeDiameters()
    {
        double[] diameters = { 0.0, 1e-6, 0.025, 0.5 };
        foreach (var diam in diameters)
        {
            var original = new SweepFeatureModel
            {
                FeatureName = $"Sweep_Diam_{diam}",
                CircularProfile = true,
                CircularProfileDiameter = diam,
                PathReference = new MfcSketchCurveReference(10, SketchCurveRole.Path)
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.True(reloaded.CircularProfile);
            Assert.NotNull(reloaded.CircularProfileDiameter);
            Assert.Equal(diam, reloaded.CircularProfileDiameter.Value, precision: 6);
        }
    }

    [Fact]
    public void Sweep_ThinWall_ExtremeThicknesses()
    {
        double[] thicknesses = { 0.0, 0.001, 0.05 };
        foreach (var th in thicknesses)
        {
            var original = new SweepFeatureModel
            {
                FeatureName = $"Sweep_Thin_{th}",
                IsThinFeature = true,
                WallThickness = th,
                WallThicknessDirection2 = th * 0.5,
                ThinWallType = 2,
                ProfileReference = new MfcSketchCurveReference(10, SketchCurveRole.Profile),
                PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path)
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.True(reloaded.IsThinFeature);
            Assert.NotNull(reloaded.WallThickness);
            Assert.Equal(th, reloaded.WallThickness.Value, precision: 6);
            Assert.Equal(th * 0.5, reloaded.WallThicknessDirection2, precision: 6);
            Assert.Equal(2u, reloaded.ThinWallType);
        }
    }

    [Fact]
    public void Sweep_TwistAngles_Extreme()
    {
        double[] angles = { 0.0, -Math.PI, 4.0 * Math.PI };
        foreach (var angle in angles)
        {
            var original = new SweepFeatureModel
            {
                FeatureName = "Sweep_Twist",
                TwistControlType = 1,
                TwistAngleRad = angle,
                ProfileReference = new MfcSketchCurveReference(10, SketchCurveRole.Profile),
                PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path)
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(1, reloaded.TwistControlType);
            Assert.NotNull(reloaded.TwistAngleRad);
            Assert.Equal(angle, reloaded.TwistAngleRad.Value, precision: 5);
        }
    }

    [Fact]
    public void Sweep_CutAndSurface_Variants_DoubleRoundtrip()
    {
        // Cut Sweep
        var cutSweep = new SweepFeatureModel
        {
            FeatureName = "SweepCut_Test",
            IsCut = true,
            ProfileReference = new MfcSketchCurveReference(1, SketchCurveRole.Profile),
            PathReference = new MfcSketchCurveReference(2, SketchCurveRole.Path)
        };
        var (cutReloaded, cb1, cb2) = ExecuteDoubleRoundtrip(cutSweep);
        Assert.Equal(cb1, cb2);
        Assert.True(cutReloaded.IsCut);
        Assert.Equal(SweepFeatureModel.CutClassName, cutReloaded.ClassName);

        // Surface Sweep
        var surfSweep = new SweepFeatureModel
        {
            FeatureName = "SweepSurface_Test",
            IsSurface = true,
            ProfileReference = new MfcSketchCurveReference(1, SketchCurveRole.Profile),
            PathReference = new MfcSketchCurveReference(2, SketchCurveRole.Path)
        };
        var (surfReloaded, sb1, sb2) = ExecuteDoubleRoundtrip(surfSweep);
        Assert.Equal(sb1, sb2);
        Assert.True(surfReloaded.IsSurface);
        Assert.Equal(SweepFeatureModel.SurfaceClassName, surfReloaded.ClassName);
    }

    [Fact]
    public void Sweep_WithGuideCurves_DoubleRoundtripAndRetention()
    {
        var original = new SweepFeatureModel
        {
            FeatureName = "Sweep_WithGuide",
            ProfileReference = new MfcSketchCurveReference(10, SketchCurveRole.Profile, 0),
            PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path, 1),
            TwistControlType = 0,
            TwistAngleRad = 0.0,
            Merge = true
        };
        original.GuideReferences.Add(new MfcSketchCurveReference(12, SketchCurveRole.Guide, 2));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);

        // Verify bit-exact double roundtrip
        Assert.Equal(b1, b2);

        // Verify Guide curve retention
        Assert.Single(reloaded.GuideReferences);
        Assert.Equal(12u, reloaded.GuideReferences[0].EntityId);
    }

    // =========================================================================
    // 3. LoftFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void Loft_TwoProfiles_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel { FeatureName = "Loft_Two" };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(2, reloaded.ProfileReferences.Count);
        Assert.Equal(1u, reloaded.ProfileReferences[0].EntityId);
        Assert.Equal(2u, reloaded.ProfileReferences[1].EntityId);
        Assert.Equal(LoftFeatureModel.TwoProfilesTypeId, reloaded.TypeId);
    }

    [Fact]
    public void Loft_ThreeProfiles_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel { FeatureName = "Loft_Three" };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));
        original.ProfileReferences.Add(new MfcSketchCurveReference(3, SketchCurveRole.Profile, 2));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(3, reloaded.ProfileReferences.Count);
        Assert.Equal(LoftFeatureModel.ThreeProfilesTypeId, reloaded.TypeId);
    }

    [Fact]
    public void Loft_MultipleGuides_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel { FeatureName = "Loft_MultiGuides" };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));
        original.GuideReferences.Add(new MfcSketchCurveReference(10, SketchCurveRole.Guide, 2));
        original.GuideReferences.Add(new MfcSketchCurveReference(20, SketchCurveRole.Guide, 3));
        original.GuideReferences.Add(new MfcSketchCurveReference(30, SketchCurveRole.Guide, 4));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(2, reloaded.ProfileReferences.Count);
        Assert.Equal(3, reloaded.GuideReferences.Count);
        Assert.Equal(10u, reloaded.GuideReferences[0].EntityId);
        Assert.Equal(20u, reloaded.GuideReferences[1].EntityId);
        Assert.Equal(30u, reloaded.GuideReferences[2].EntityId);
    }

    [Fact]
    public void Loft_Centerline_DoubleRoundtrip()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "Loft_Centerline",
            Centerline = new SketchCurveReference { EntityId = 77, Role = SketchCurveRole.Centerline }
        };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.NotNull(reloaded.Centerline);
        Assert.Equal(77u, reloaded.Centerline.EntityId);
    }

    [Fact]
    public void Loft_TangencyBoundaryConditions_DoubleRoundtrip()
    {
        int[] conditions = { 0, 1, 2, 3 };
        foreach (var startCond in conditions)
        {
            foreach (var endCond in conditions)
            {
                var original = new LoftFeatureModel
                {
                    FeatureName = $"Loft_T_{startCond}_{endCond}",
                    StartTangencyType = startCond,
                    EndTangencyType = endCond
                };
                original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
                original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));

                var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
                Assert.Equal(b1, b2);
                Assert.Equal(startCond, reloaded.StartTangencyType);
                Assert.Equal(endCond, reloaded.EndTangencyType);
            }
        }
    }

    [Fact]
    public void Loft_ThinFeature_Retention_StressTest()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "Loft_Thin",
            IsThinFeature = true,
            WallThickness = 0.003
        };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.True(reloaded.IsThinFeature);
        Assert.NotNull(reloaded.WallThickness);
        Assert.Equal(0.003, reloaded.WallThickness.Value, precision: 5);
    }

    // =========================================================================
    // 4. ShellFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void Shell_ZeroRemovedFaces_DoubleRoundtrip()
    {
        var original = new ShellFeatureModel
        {
            FeatureName = "Shell_0Faces",
            Thickness = 0.005,
            Direction = 0,
            FacesRemoved = Array.Empty<EntityReference>()
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(0.005, reloaded.Thickness!.Value, precision: 5);
        Assert.Equal(0, reloaded.Direction);
        Assert.Equal(0, reloaded.FacesRemovedCount);
    }

    [Fact]
    public void Shell_TenRemovedFaces_DoubleRoundtrip()
    {
        var faces = Enumerable.Range(100, 10)
            .Select(id => new EntityReference { EntityId = (uint)id, EntityType = SelectionEntityType.Face })
            .ToArray();

        var original = new ShellFeatureModel
        {
            FeatureName = "Shell_10Faces",
            Thickness = 0.002,
            Direction = 1,
            FacesRemoved = faces
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(10, reloaded.FacesRemovedCount);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal((uint)(100 + i), reloaded.FacesRemoved![i].EntityId);
        }
    }

    [Fact]
    public void Shell_ExtremeThicknesses_DoubleRoundtrip()
    {
        double[] thicknesses = { 0.0, 1e-6, 0.05, 1.0 };
        foreach (var th in thicknesses)
        {
            var original = new ShellFeatureModel
            {
                FeatureName = $"Shell_Th_{th}",
                Thickness = th,
                Direction = 0,
                FacesRemoved = new[] { new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Face } }
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(th, reloaded.Thickness!.Value, precision: 6);
        }
    }

    // =========================================================================
    // 5. DraftFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void Draft_ZeroFaces_DoubleRoundtrip()
    {
        var original = new DraftFeatureModel
        {
            FeatureName = "Draft_0Faces",
            Angle = 0.05,
            Type = 0,
            ReverseDirection = false,
            NeutralPlane = null,
            FacesToDraft = Array.Empty<EntityReference>()
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Null(reloaded.NeutralPlane);
        Assert.Empty(reloaded.FacesToDraft!);
    }

    [Fact]
    public void Draft_TenFaces_DoubleRoundtrip()
    {
        var faces = Enumerable.Range(200, 10)
            .Select(id => new EntityReference { EntityId = (uint)id, EntityType = SelectionEntityType.Face })
            .ToArray();

        var original = new DraftFeatureModel
        {
            FeatureName = "Draft_10Faces",
            Angle = 0.087266, // 5 deg
            Type = 0,
            ReverseDirection = true,
            NeutralPlane = new EntityReference { EntityId = 50, EntityType = SelectionEntityType.Face },
            FacesToDraft = faces
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.NotNull(reloaded.NeutralPlane);
        Assert.Equal(50u, reloaded.NeutralPlane.EntityId);
        Assert.Equal(10, reloaded.FacesToDraft!.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal((uint)(200 + i), reloaded.FacesToDraft[i].EntityId);
        }
    }

    [Fact]
    public void Draft_ExtremeAngles_DoubleRoundtrip()
    {
        double[] angles = { 0.0, 0.01745, Math.PI / 4, 1.5 };
        foreach (var angle in angles)
        {
            var original = new DraftFeatureModel
            {
                FeatureName = $"Draft_Ang_{angle}",
                Angle = angle,
                Type = 0,
                NeutralPlane = new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Face },
                FacesToDraft = new[] { new EntityReference { EntityId = 2, EntityType = SelectionEntityType.Face } }
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(angle, reloaded.Angle!.Value, precision: 5);
        }
    }

    // =========================================================================
    // 6. CombineBodiesFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void CombineBodies_OperationTypes_DoubleRoundtrip()
    {
        int[] operations = { 0, 1, 2 }; // Add, Subtract, Common
        foreach (var op in operations)
        {
            var original = new CombineBodiesFeatureModel
            {
                FeatureName = $"Combine_Op_{op}",
                OperationType = op,
                MainBody = new EntityReference { EntityId = 10, EntityType = SelectionEntityType.Body },
                BodiesToCombine = new[] { new EntityReference { EntityId = 20, EntityType = SelectionEntityType.Body } }
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(op, reloaded.OperationType);
        }
    }

    [Fact]
    public void CombineBodies_NullMainBody_StressTest()
    {
        var original = new CombineBodiesFeatureModel
        {
            FeatureName = "Combine_NoMainBody",
            OperationType = 0,
            MainBody = null,
            BodiesToCombine = new[] { new EntityReference { EntityId = 20, EntityType = SelectionEntityType.Body } }
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void CombineBodies_ToolBodyCounts_0_1_5_10_DoubleRoundtrip()
    {
        int[] counts = { 0, 1, 5, 10 };
        foreach (var count in counts)
        {
            var tools = Enumerable.Range(100, count)
                .Select(id => new EntityReference { EntityId = (uint)id, EntityType = SelectionEntityType.Body })
                .ToArray();

            var original = new CombineBodiesFeatureModel
            {
                FeatureName = $"Combine_Count_{count}",
                OperationType = 0,
                MainBody = new EntityReference { EntityId = 5, EntityType = SelectionEntityType.Body },
                BodiesToCombine = tools
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(count, reloaded.BodiesToCombineCount ?? 0);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal((uint)(100 + i), reloaded.BodiesToCombine![i].EntityId);
            }
        }
    }

    // =========================================================================
    // 7. SplitBodyFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void SplitBody_TrimToolCounts_0_1_5_10_DoubleRoundtrip()
    {
        int[] counts = { 0, 1, 5, 10 };
        foreach (var count in counts)
        {
            var tools = Enumerable.Range(300, count)
                .Select(id => new EntityReference { EntityId = (uint)id, EntityType = SelectionEntityType.Face })
                .ToArray();

            var original = new SplitBodyFeatureModel
            {
                FeatureName = $"Split_Count_{count}",
                Consume = false,
                State = 0,
                TrimTools = tools,
                TemplatePath = "C:\\Tpl.prtdot",
                OverrideDefaultTemplateSettings = true
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(count, reloaded.TrimToolsCount ?? 0);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal((uint)(300 + i), reloaded.TrimTools![i].EntityId);
            }
        }
    }

    [Fact]
    public void SplitBody_ConsumeTrueAndFalse_DoubleRoundtrip()
    {
        foreach (var consume in new[] { true, false })
        {
            var original = new SplitBodyFeatureModel
            {
                FeatureName = $"Split_Consume_{consume}",
                Consume = consume,
                State = consume ? 2 : 0,
                TrimTools = new[] { new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Face } }
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(consume, reloaded.Consume);
            Assert.Equal(consume ? 2 : 0, reloaded.State);
        }
    }

    [Fact]
    public void SplitBody_WithTargetBodyModel_DoubleRoundtripAndRetention()
    {
        var original = new SplitBodyFeatureModel
        {
            FeatureName = "Split_TargetBody",
            Consume = true,
            TrimTools = new[] { new EntityReference { EntityId = 10, EntityType = SelectionEntityType.Face } },
            TargetBodyModel = new MfcCompSolidBodyModel { RefBodyId = 42 },
            TemplatePath = "C:\\Parts\\Part.prtdot",
            OverrideDefaultTemplateSettings = true
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);

        // Verify bit-exact double roundtrip
        Assert.Equal(b1, b2);

        // Verify target body retention
        Assert.NotNull(reloaded.TargetBodyModel);
        Assert.Equal(42, reloaded.TargetBodyModel.RefBodyId);

        // Verify TemplatePath retention
        Assert.Equal("C:\\Parts\\Part.prtdot", reloaded.TemplatePath);
        Assert.True(reloaded.OverrideDefaultTemplateSettings);
    }

    // =========================================================================
    // 8. DeleteBodyFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void DeleteBody_BodyCounts_0_1_5_10_DoubleRoundtrip()
    {
        int[] counts = { 0, 1, 5, 10 };
        foreach (var count in counts)
        {
            var bodies = Enumerable.Range(500, count)
                .Select(id => new EntityReference { EntityId = (uint)id, EntityType = SelectionEntityType.Body })
                .ToArray();

            var original = new DeleteBodyFeatureModel
            {
                FeatureName = $"Del_Count_{count}",
                KeepBodies = (count % 2 == 0),
                Bodies = bodies
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(count, reloaded.BodiesCount ?? 0);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal((uint)(500 + i), reloaded.Bodies![i].EntityId);
            }
        }
    }

    [Fact]
    public void DeleteBody_ExtremeEntityIds_DoubleRoundtrip()
    {
        var bodies = new[]
        {
            new EntityReference { EntityId = 0, EntityType = SelectionEntityType.Body },
            new EntityReference { EntityId = 65535, EntityType = SelectionEntityType.Body }
        };

        var original = new DeleteBodyFeatureModel
        {
            FeatureName = "Del_ExtremeIds",
            KeepBodies = true,
            Bodies = bodies
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(2, reloaded.BodiesCount);
        Assert.Equal(0u, reloaded.Bodies![0].EntityId);
        Assert.Equal(65535u, reloaded.Bodies![1].EntityId);
    }

    // =========================================================================
    // 9. RibFeatureModel Stress Tests
    // =========================================================================

    [Fact]
    public void Rib_ExtremeThicknessAndDraftAngles_DoubleRoundtrip()
    {
        double[] thicknesses = { 0.0, 0.001, 0.05, 1.0 };
        double[] angles = { 0.0, 0.01745, 0.5, 1.5 };

        for (int i = 0; i < thicknesses.Length; i++)
        {
            var original = new RibFeatureModel
            {
                FeatureName = $"Rib_Stress_{i}",
                Thickness = thicknesses[i],
                DraftAngle = angles[i],
                EnableDraft = angles[i] > 0,
                DraftOutward = (i % 2 == 0),
                DraftFromWall = (i % 2 != 0),
                IsTwoSided = (i % 2 == 0),
                ExtrusionDirection = (i % 2),
                FlipSide = (i % 2 != 0),
                RefSketchIndex = (i % 3)
            };

            var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);
            Assert.Equal(b1, b2);
            Assert.Equal(thicknesses[i], reloaded.Thickness!.Value, precision: 6);
            Assert.Equal(angles[i], reloaded.DraftAngle!.Value, precision: 6);
            Assert.Equal(angles[i] > 0, reloaded.EnableDraft);
            Assert.Equal(i % 2 == 0, reloaded.DraftOutward);
            Assert.Equal(i % 2 != 0, reloaded.DraftFromWall);
            Assert.Equal(i % 2 == 0, reloaded.IsTwoSided);
            Assert.Equal(i % 2, reloaded.ExtrusionDirection);
            Assert.Equal(i % 2 != 0, reloaded.FlipSide);
            Assert.Equal(i % 3, reloaded.RefSketchIndex);
        }
    }

    [Fact]
    public void Rib_WithBodyModel_DoubleRoundtripAndRetention()
    {
        var original = new RibFeatureModel
        {
            FeatureName = "Rib_WithBody",
            Thickness = 0.003,
            DraftAngle = 0.0,
            BodyModel = new MfcCompSolidBodyModel { RefBodyId = 88 }
        };

        var (reloaded, b1, b2) = ExecuteDoubleRoundtrip(original);

        // Verify bit-exact double roundtrip
        Assert.Equal(b1, b2);

        // Verify target body retention
        Assert.NotNull(reloaded.BodyModel);
        Assert.Equal(88, reloaded.BodyModel.RefBodyId);
    }
}
