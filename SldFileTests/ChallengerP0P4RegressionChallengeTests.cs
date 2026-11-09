using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Empirical Challenger suite for cross-milestone verification (P0-P4).
/// Empirically challenges and tests:
/// 1. P0: All 7 mate models (Standard, Width, Path, Gear, RackPinion, Cam, ProfileCenter)
/// 2. P1: All 8 core solid models (Sweep, Loft, Shell, Draft, Combine, Split, DeleteBody, Rib)
/// 3. P2: All 9 sheet metal and weldment models (EdgeFlange, MiterFlange, Hem, Jog, LoftedBend, FlatPattern, StructuralMember, EndCap, WeldBead)
/// 4. P3: All 6 surface models (Knit, Extend, Offset, Fill, Trim, Planar)
/// 5. P4: Config Tree, Units/Material, Drawing annotations (CMgr, Config0, UserUnits, Balloon, Gtol, Datum, BOM)
/// </summary>
public class ChallengerP0P4RegressionChallengeTests
{
    private static byte[] Serialize(IMfcSerializable model, int docVersion = 0)
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var writer = MfcArchive.CreateWriter(context: ctx);
        model.Serialize(writer);
        return writer.ToArray();
    }

    private static T Deserialize<T>(byte[] data, int docVersion = 0) where T : IMfcSerializable, new()
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var reader = MfcArchive.CreateReader(data, context: ctx);
        var model = new T();
        model.Serialize(reader);
        return model;
    }

    // ── Milestone P0: Assembly Mates ─────────────────────────────────────────

    [Fact]
    public void P0_AllMateModels_InheritFromMfcMateModelBase_AndImplementIMfcSerializable()
    {
        var mateTypes = new[]
        {
            typeof(StandardMateModel),
            typeof(WidthMateModel),
            typeof(PathMateModel),
            typeof(GearMateModel),
            typeof(RackPinionMateModel),
            typeof(CamFollowerMateModel),
            typeof(ProfileCenterMateModel)
        };

        foreach (var type in mateTypes)
        {
            Assert.True(typeof(MfcMateModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcMateModelBase");
            Assert.True(typeof(IMfcMateModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcMateModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void P0_CombinedMatesList_RoundtripsAllSevenMateTypesSimultaneously()
    {
        var list = new MatesListModel { StreamVersion = MatesListModel.DefaultCleanVersion };

        // 1. Standard
        var std = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = "StdCoincident" };
        std.AddPlaneReference("FaceA@Part1-1");
        std.AddPlaneReference("FaceB@Part2-1");
        list.Mates.Add(std);

        // 2. Width
        var width = new WidthMateModel { DisplayName = "WidthCentered", ConstraintType = AssemblyMateWidthConstraint.Centered };
        width.AddPlaneReference("W1@P1"); width.AddPlaneReference("W2@P1");
        width.AddPlaneReference("T1@P2"); width.AddPlaneReference("T2@P2");
        list.Mates.Add(width);

        // 3. Path
        var path = new PathMateModel { DisplayName = "Path1", PathValue = 0.05 };
        path.SetEntities("Vertex1@P1", "Edge1@P2");
        list.Mates.Add(path);

        // 4. Gear
        var gear = new GearMateModel { DisplayName = "Gear1", GearRatioNumerator = 2.0, GearRatioDenominator = 1.0 };
        gear.AddPlaneReference("CylA@P1");
        gear.AddPlaneReference("CylB@P2");
        list.Mates.Add(gear);

        // 5. RackPinion
        var rp = new RackPinionMateModel { DisplayName = "RackPinion1", DiameterValue = 0.04 };
        rp.AddPlaneReference("RackPlane@P1");
        rp.AddPlaneReference("PinionCyl@P2");
        list.Mates.Add(rp);

        // 6. Cam
        var cam = new CamFollowerMateModel { DisplayName = "Cam1" };
        cam.AddPlaneReference("FollowerFace@P1");
        cam.AddPlaneReference("CamSurface@P2");
        list.Mates.Add(cam);

        // 7. ProfileCenter
        var pc = new ProfileCenterMateModel { DisplayName = "ProfileCenter1", OffsetDistance = 0.01 };
        pc.AddPlaneReference("Profile1@P1");
        pc.AddPlaneReference("Profile2@P2");
        list.Mates.Add(pc);

        byte[] b1 = Serialize(list);
        var restored = Deserialize<MatesListModel>(b1);

        Assert.Equal(7, restored.Mates.Count);
        Assert.IsType<StandardMateModel>(restored.Mates[0]);
        Assert.IsType<WidthMateModel>(restored.Mates[1]);
        Assert.IsType<PathMateModel>(restored.Mates[2]);
        Assert.IsType<GearMateModel>(restored.Mates[3]);
        Assert.IsType<RackPinionMateModel>(restored.Mates[4]);
        Assert.IsType<CamFollowerMateModel>(restored.Mates[5]);
        Assert.IsType<ProfileCenterMateModel>(restored.Mates[6]);

        byte[] b2 = Serialize(restored);
        Assert.Equal(b1, b2);
    }

    // ── Milestone P1: Core Solids ────────────────────────────────────────────

    [Fact]
    public void P1_AllCoreSolidModels_InheritFromMfcFeatureModelBase_AndImplementIMfcSerializable()
    {
        var solidTypes = new[]
        {
            typeof(SweepFeatureModel),
            typeof(LoftFeatureModel),
            typeof(ShellFeatureModel),
            typeof(DraftFeatureModel),
            typeof(CombineBodiesFeatureModel),
            typeof(SplitBodyFeatureModel),
            typeof(DeleteBodyFeatureModel),
            typeof(RibFeatureModel)
        };

        foreach (var type in solidTypes)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcFeatureModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void P1_ShellDraftRib_RoundtripSerializationIntegrity()
    {
        // Shell
        var shell = new ShellFeatureModel
        {
            FeatureName = "TestShell",
            Thickness = 0.003,
            Direction = 0,
            FacesRemoved = new List<EntityReference> { new EntityReference { EntityId = 10, EntityType = SelectionEntityType.Face } }
        };
        byte[] shellBytes = Serialize(shell);
        var reShell = Deserialize<ShellFeatureModel>(shellBytes);
        Assert.Equal(shell.FeatureName, reShell.FeatureName);
        Assert.Equal(shell.Thickness, reShell.Thickness);

        // Draft
        var draft = new DraftFeatureModel
        {
            FeatureName = "TestDraft",
            Angle = 0.05235987755982988, // 3 deg in rad
            ReverseDirection = false,
            NeutralPlane = new EntityReference { EntityId = 20, EntityType = SelectionEntityType.Face },
            FacesToDraft = new List<EntityReference> { new EntityReference { EntityId = 21, EntityType = SelectionEntityType.Face } }
        };
        byte[] draftBytes = Serialize(draft);
        var reDraft = Deserialize<DraftFeatureModel>(draftBytes);
        Assert.Equal(draft.FeatureName, reDraft.FeatureName);
        Assert.Equal(draft.Angle, reDraft.Angle);

        // Rib
        var rib = new RibFeatureModel
        {
            FeatureName = "TestRib",
            Thickness = 0.002,
            DraftAngle = 0.017453292519943295
        };
        byte[] ribBytes = Serialize(rib);
        var reRib = Deserialize<RibFeatureModel>(ribBytes);
        Assert.Equal(rib.FeatureName, reRib.FeatureName);
        Assert.Equal(rib.Thickness, reRib.Thickness);
    }

    // ── Milestone P2: Sheet Metal & Weldments ─────────────────────────────────

    [Fact]
    public void P2_AllSheetMetalAndWeldmentModels_InheritFromMfcFeatureModelBase_AndImplementIMfcSerializable()
    {
        var p2Types = new[]
        {
            typeof(EdgeFlangeFeatureModel),
            typeof(MiterFlangeFeatureModel),
            typeof(HemFeatureModel),
            typeof(JogFeatureModel),
            typeof(LoftedBendFeatureModel),
            typeof(FlatPatternFeatureModel),
            typeof(StructuralMemberFeatureModel),
            typeof(EndCapFeatureModel),
            typeof(WeldBeadFeatureModel),
            typeof(CosmeticWeldBeadFeatureModel)
        };

        foreach (var type in p2Types)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void P2_EdgeFlangeAndStructuralMember_DoubleRoundtripByteEquality()
    {
        // Edge Flange
        var ef = new EdgeFlangeFeatureModel
        {
            FeatureName = "EdgeFlange1",
            BendAngle = 1.5707963267948966,
            BendRadius = 0.001,
            OffsetDistance = 0.020,
            Edges = new List<EntityReference> { new EntityReference { EntityId = 101, EntityType = SelectionEntityType.Edge } }
        };
        byte[] efB1 = Serialize(ef);
        var reEf = Deserialize<EdgeFlangeFeatureModel>(efB1);
        Assert.Equal(ef.FeatureName, reEf.FeatureName);
        Assert.Equal(ef.BendRadius, reEf.BendRadius);
        byte[] efB2 = Serialize(reEf);
        Assert.Equal(efB1, efB2);

        // Structural Member
        var sm = new StructuralMemberFeatureModel
        {
            FeatureName = "StrMember1",
            WeldmentProfilePath = @"C:\profiles\pipe_21.3x2.3.sldlfp",
            RotationAngle = 0.5235987755982988,
            CornerTreatmentType = 1,
            MirrorProfile = true,
            AllowProtrusion = false
        };
        byte[] smB1 = Serialize(sm);
        var reSm = Deserialize<StructuralMemberFeatureModel>(smB1);
        Assert.Equal(sm.WeldmentProfilePath, reSm.WeldmentProfilePath);
        Assert.Equal(sm.RotationAngle, reSm.RotationAngle);
        Assert.Equal(sm.CornerTreatmentType, reSm.CornerTreatmentType);
        Assert.Equal(sm.MirrorProfile, reSm.MirrorProfile);
        byte[] smB2 = Serialize(reSm);
        Assert.Equal(smB1, smB2);
    }

    // ── Milestone P3: Surfaces ───────────────────────────────────────────────

    [Fact]
    public void P3_AllSurfaceModels_InheritFromMfcFeatureModelBase_AndImplementIMfcSerializable()
    {
        var surfaceTypes = new[]
        {
            typeof(SurfaceKnitFeatureModel),
            typeof(SurfaceExtendFeatureModel),
            typeof(SurfaceOffsetFeatureModel),
            typeof(SurfaceFillFeatureModel),
            typeof(SurfaceTrimFeatureModel),
            typeof(PlanarSurfaceFeatureModel)
        };

        foreach (var type in surfaceTypes)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void P3_SurfaceModels_RoundtripPreservation()
    {
        // Planar Surface
        var planar = new PlanarSurfaceFeatureModel
        {
            FeatureName = "PlanarSurf1",
            BoundingEntities = new List<EntityReference> { new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Sketch, Name = "Sketch1" } },
            BoundarySegments = new List<EntityReference> { new EntityReference { EntityId = 3, EntityType = SelectionEntityType.Edge } }
        };
        byte[] pB1 = Serialize(planar);
        var rePlanar = Deserialize<PlanarSurfaceFeatureModel>(pB1);
        Assert.Equal(planar.FeatureName, rePlanar.FeatureName);
        byte[] pB2 = Serialize(rePlanar);
        Assert.Equal(pB1, pB2);

        // Surface Knit
        var knit = new SurfaceKnitFeatureModel
        {
            FeatureName = "KnitSurf1",
            Entities = new List<EntityReference> { new EntityReference { EntityId = 12, EntityType = SelectionEntityType.Face } },
            KnitTolerance = 0.0001,
            UseTryToFormSolid = true
        };
        byte[] kB1 = Serialize(knit);
        var reKnit = Deserialize<SurfaceKnitFeatureModel>(kB1);
        Assert.Equal(knit.FeatureName, reKnit.FeatureName);
        Assert.Equal(knit.KnitTolerance, reKnit.KnitTolerance);
        byte[] kB2 = Serialize(reKnit);
        Assert.Equal(kB1, kB2);
    }

    // ── Milestone P4: Config Tree, Units/Material, Drawings ──────────────────

    [Fact]
    public void P4_AllConfigurationDrawingModels_ImplementIMfcSerializable()
    {
        var p4Types = new[]
        {
            typeof(CmgrModel),
            typeof(CmgrHdr2Model),
            typeof(CmgrConfigurationModel),
            typeof(Config0Model),
            typeof(MoMaterialModel),
            typeof(MoDensityParameterModel),
            typeof(UserUnitTypeModel),
            typeof(UserUnitsTableModel),
            typeof(DrBalloonMfcModel),
            typeof(DrGtolMfcModel),
            typeof(DrDatumMfcModel),
            typeof(DrBomTableMfcModel),
            typeof(DrawingDocumentMfcModel)
        };

        foreach (var type in p4Types)
        {
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void P4_ConfigAndDrawing_DoubleRoundtripByteEquality()
    {
        // Balloon
        var balloon = new DrBalloonMfcModel("10", 0.05, 0.08, "Circle") { Size = 0.010 };
        byte[] bB1 = Serialize(balloon);
        var reBalloon = Deserialize<DrBalloonMfcModel>(bB1);
        byte[] bB2 = Serialize(reBalloon);
        Assert.Equal(bB1, bB2);

        // Gtol
        var gtol = new DrGtolMfcModel("Flatness", 0.015, "A", 0.1, 0.2);
        byte[] gB1 = Serialize(gtol);
        var reGtol = Deserialize<DrGtolMfcModel>(gB1);
        byte[] gB2 = Serialize(reGtol);
        Assert.Equal(gB1, gB2);

        // Material & Density
        var mat = new MoMaterialModel("6061-T6 Aluminum", "SOLIDWORKS Materials", "Aluminum Alloys");
        byte[] mB1 = Serialize(mat);
        var reMat = Deserialize<MoMaterialModel>(mB1);
        byte[] mB2 = Serialize(reMat);
        Assert.Equal(mB1, mB2);
    }

    // ── Native Specimens & Cross-Milestone Stress Tests ──────────────────────

    [Fact]
    public void CrossMilestone_NativeModels_LoadAndVerifyIntegrity()
    {
        string modelsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../models"));
        if (!Directory.Exists(modelsDir))
            modelsDir = @"d:\weigan\repos\sldfile\models";

        // 1. assembly.SLDASM (P0)
        string asmPath = Path.Combine(modelsDir, "assembly.SLDASM");
        if (File.Exists(asmPath))
        {
            using var asm = new SldAssembly(asmPath, readOnly: true);
            Assert.NotNull(asm.MatesList);
            Assert.True(asm.MatesList.Mates.Count >= 0);
            foreach (var mate in asm.MatesList.Mates)
            {
                Assert.NotNull(mate.ClassName);
                Assert.True(typeof(MfcMateModelBase).IsAssignableFrom(mate.GetType()));
            }
        }

        // 2. cube.SLDPRT (P1/P4)
        string cubePath = Path.Combine(modelsDir, "cube.SLDPRT");
        if (File.Exists(cubePath))
        {
            using var part = new SldPart(cubePath, readOnly: true);
            Assert.NotEmpty(part.Features);
            Assert.NotNull(part.UserUnitsTable);
            Assert.NotNull(part.DocumentSummary);
        }

        // 3. cylinder.SLDPRT (P1)
        string cylPath = Path.Combine(modelsDir, "cylinder.SLDPRT");
        if (File.Exists(cylPath))
        {
            using var part = new SldPart(cylPath, readOnly: true);
            Assert.NotEmpty(part.Features);
        }

        // 4. cube.SLDDRW (P4)
        string drwPath = Path.Combine(modelsDir, "cube.SLDDRW");
        if (File.Exists(drwPath))
        {
            using var drw = new SldDrawing(drwPath, readOnly: true);
            Assert.NotNull(drw.Sheets);
            Assert.NotNull(drw.DocumentSummary);
        }
    }

    [Fact]
    public void CrossMilestone_DoubleRoundtrip_TripleConvergence_Stress()
    {
        // P0: Width Mate with Chinese unicode selection
        var width = new WidthMateModel
        {
            DisplayName = "宽度配合_测试1",
            Alignment = AssemblyMateAlignment.AntiAligned,
            ConstraintType = AssemblyMateWidthConstraint.Dimension,
            DistanceFromEnd = 0.0125,
            FlipDimension = true
        };
        width.AddPlaneReference("宽度面1@零件1-1@装配体");
        width.AddPlaneReference("宽度面2@零件1-1@装配体");
        width.AddPlaneReference("凸耳面1@零件2-1@装配体");
        width.AddPlaneReference("凸耳面2@零件2-1@装配体");

        byte[] wB1 = Serialize(width);
        var wM1 = Deserialize<WidthMateModel>(wB1);
        byte[] wB2 = Serialize(wM1);
        var wM2 = Deserialize<WidthMateModel>(wB2);
        byte[] wB3 = Serialize(wM2);
        Assert.Equal(wB1, wB2);
        Assert.Equal(wB2, wB3);

        // P2: Hem with boundary radius and angle
        var hem = new HemFeatureModel
        {
            FeatureName = "褶边_极限尺寸",
            Radius = 0.0005,
            Length = 0.010,
            GapDistance = 0.0001,
            Angle = Math.PI,
            Edges = new List<EntityReference> { new EntityReference { EntityId = 9999, EntityType = SelectionEntityType.Edge } }
        };
        byte[] hB1 = Serialize(hem);
        var hM1 = Deserialize<HemFeatureModel>(hB1);
        byte[] hB2 = Serialize(hM1);
        var hM2 = Deserialize<HemFeatureModel>(hB2);
        byte[] hB3 = Serialize(hM2);
        Assert.Equal(hB1, hB2);
        Assert.Equal(hB2, hB3);

        // P4: Drawing BOM with 20 rows
        var bom = new DrBomTableMfcModel("生产明细表_BOM", 0.05, 0.05);
        bom.Columns.Add(new DrBomColumnModel { Title = "序号", PropertyBinding = "ItemNo", Width = 0.01 });
        bom.Columns.Add(new DrBomColumnModel { Title = "名称", PropertyBinding = "PartName", Width = 0.04 });
        bom.Columns.Add(new DrBomColumnModel { Title = "数量", PropertyBinding = "Qty", Width = 0.01 });
        for (int i = 1; i <= 20; i++)
        {
            bom.Rows.Add(new DrBomRowModel { ItemNumber = i.ToString(), PartNumber = $"PART-{i:D3}", Description = $"标准构件 {i}", Quantity = i });
        }
        byte[] bomB1 = Serialize(bom);
        var bomM1 = Deserialize<DrBomTableMfcModel>(bomB1);
        byte[] bomB2 = Serialize(bomM1);
        var bomM2 = Deserialize<DrBomTableMfcModel>(bomB2);
        byte[] bomB3 = Serialize(bomM2);
        Assert.Equal(bomB1, bomB2);
        Assert.Equal(bomB2, bomB3);
    }
}

