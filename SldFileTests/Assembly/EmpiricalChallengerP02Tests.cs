using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests.Assembly;

public class EmpiricalChallengerP02Tests
{
    private readonly ITestOutputHelper _output;

    public EmpiricalChallengerP02Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_Models_AssemblySldasm_MatesList_Inspection()
    {
        string modelsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../models/assembly.SLDASM"));
        if (!File.Exists(modelsPath))
        {
            modelsPath = @"d:\weigan\repos\sldfile\models\assembly.SLDASM";
        }
        Assert.True(File.Exists(modelsPath), $"File not found at {modelsPath}");

        using var assembly = new SldAssembly(modelsPath, readOnly: true);
        var matesList = assembly.MatesList;
        _output.WriteLine($"models/assembly.SLDASM: StreamVersion=0x{matesList.StreamVersion:X4}, Mates.Count={matesList.Mates.Count}");

        foreach (var mate in matesList.Mates)
        {
            _output.WriteLine($"  Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}', MateType={mate.MateType}, TypeId={mate.TypeId}, Alignment={mate.Alignment}");
        }
    }

    [Fact]
    public void Test_Existing_Models_AssemblySldasm_Resave_Integrity()
    {
        string modelsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../models/assembly.SLDASM"));
        if (!File.Exists(modelsPath))
            modelsPath = @"d:\weigan\repos\sldfile\models\assembly.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_resave_{Guid.NewGuid():N}.SLDASM");
        File.Copy(modelsPath, tempFile, true);

        try
        {
            byte[] origStream;
            using (var assyOrig = new SldAssembly(tempFile, readOnly: true))
            {
                origStream = (byte[])assyOrig.RawStreams.First(k => k.Key.Contains("MatesList")).Value.Clone();
                _output.WriteLine($"Original MatesList stream length: {origStream.Length}");
                _output.WriteLine($"Original Mates count: {assyOrig.MatesList.Mates.Count}");
            }

            // Now open read-write, add a mate or just resave
            using (var assyRw = new SldAssembly(tempFile, readOnly: false))
            {
                // Touch mates stream by serializing
                byte[] reserialized = MfcArchive.Serialize(assyRw.MatesList);
                _output.WriteLine($"Reserialized MatesList length: {reserialized.Length}");
                _output.WriteLine($"Orig first 32 bytes: {BitConverter.ToString(origStream, 0, Math.Min(32, origStream.Length))}");
                _output.WriteLine($"Reser first 32 bytes: {BitConverter.ToString(reserialized, 0, Math.Min(32, reserialized.Length))}");
                assyRw.SetPendingStream("Contents/Config-0-MatesList", reserialized);
                assyRw.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                _output.WriteLine($"Reopened Mates count: {reopened.MatesList.Mates.Count}");
                _output.WriteLine($"Reopened SldAssembly.Mates count: {reopened.Mates.Count}");
                foreach (var m in reopened.MatesList.Mates)
                {
                    _output.WriteLine($"  Reopened mate: Class={m.ClassName}, DisplayName='{m.DisplayName}', Type={m.GetType().Name}");
                }
                Assert.Equal(4, reopened.MatesList.Mates.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_InspectNativeCoincidentRecordVsSerialized()
    {
        // Now create a StandardMateModel
        var mate = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident)
        {
            DisplayName = "Coincident1",
            Alignment = AssemblyMateAlignment.Aligned
        };
        mate.AddPlaneReference("鍓嶈鍩哄噯闈cube-1@assembly_nomate_base");
        mate.AddPlaneReference("鍓嶈鍩哄噯闈cylinder-1@assembly_nomate_base");

        byte[] serialized = MfcArchive.Serialize(mate);
        Assert.NotEmpty(serialized);
        _output.WriteLine($"Serialized StandardMateModel length = {serialized.Length}");

        int serWchar = -1;
        for (int i = 0; i < serialized.Length - 3; i++)
        {
            if (serialized[i] == 0xFF && serialized[i + 1] == 0xFE && serialized[i + 2] == 0xFF)
            {
                serWchar = i;
                break;
            }
        }
        Assert.True(serWchar >= 0);
        int cur = serWchar;
        string? name = MfcPrimitives.ReadWChar(serialized, ref cur, serialized.Length);
        Assert.Equal("Coincident1", name);
    }

    [Fact]
    public void Test_TestFiles_Solidworks2012_AssemblySldasm_MatesList()
    {
        string path = TestFileProvider.Assembly;
        Assert.True(File.Exists(path), $"File not found: {path}");

        using var assembly = new SldAssembly(path, readOnly: true);
        var matesList = assembly.MatesList;
        _output.WriteLine($"Solidworks2012/assembly.SLDASM: StreamVersion=0x{matesList.StreamVersion:X4}, Mates.Count={matesList.Mates.Count}");

        foreach (var mate in matesList.Mates)
        {
            _output.WriteLine($"  Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}', MateType={mate.MateType}, TypeId={mate.TypeId}");
            Assert.False(mate is GenericMateModel, $"Mate {mate.DisplayName} ({mate.ClassName}) fell back to GenericMateModel!");
        }
    }

    [Fact]
    public void Test_MotionPathTutorial_MatesList_Inspection()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/Path/motion_path_tutorial.SLDASM"));
        if (!File.Exists(path))
        {
            path = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\Path\motion_path_tutorial.SLDASM";
        }
        Assert.True(File.Exists(path), $"File not found: {path}");

        using var assembly = new SldAssembly(path, readOnly: true);
        var matesList = assembly.MatesList;
        _output.WriteLine($"motion_path_tutorial.SLDASM: StreamVersion=0x{matesList.StreamVersion:X4}, Mates.Count={matesList.Mates.Count}");

        int pathMateCount = 0;
        int genericCount = 0;
        foreach (var mate in matesList.Mates)
        {
            _output.WriteLine($"  Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}', MateType={mate.MateType}, TypeId={mate.TypeId}");
            if (mate is GenericMateModel)
                genericCount++;
            if (mate is PathMateModel)
                pathMateCount++;
        }

        _output.WriteLine($"Found {pathMateCount} PathMateModel instances, {genericCount} GenericMateModel instances.");
        Assert.Equal(0, genericCount);
        Assert.Equal(4, pathMateCount);
    }

    [Fact]
    public void Test_SldAssembly_AddMate_Coincident_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_mate_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                var def = new StandardMateDefinitionData(AssemblyMateType.Coincident)
                {
                    DisplayName = "TestCoincident1",
                    Alignment = AssemblyMateAlignment.Aligned
                };
                def.EntitiesToMate.Add("FrontPlane@cube-1@assembly");
                def.EntitiesToMate.Add("FrontPlane@cylinder-1@assembly");

                assy.AddMate(def);
                assy.Save();
            }

            // Reopen and verify
            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}'");
                Assert.Equal("TestCoincident1", mate.DisplayName);
                Assert.IsType<StandardMateModel>(mate);
                var std = (StandardMateModel)mate;
                _output.WriteLine($"  PlaneReferences.Count = {std.PlaneReferences.Count}");
                _output.WriteLine($"  EntityReferences.Count = {std.EntityReferences.Count}");
                _output.WriteLine($"  Structure == null? {std.Structure == null}");
                if (std.Structure != null)
                {
                    _output.WriteLine($"  Structure.Layout = {std.Structure.Layout}");
                    _output.WriteLine($"  Structure.EntityReferences.Count = {std.Structure.EntityReferences.Count}");
                }
                var mateInfo = reopened.Mates[0];
                _output.WriteLine($"  AssemblyMateInfo.Entities.Count = {mateInfo.Entities.Count}");
                _output.WriteLine($"  AssemblyMateInfo.MateType = {mateInfo.MateType}");
                _output.WriteLine($"  AssemblyMateInfo.Alignment = {mateInfo.Alignment}");
                Assert.Equal(AssemblyMateType.Coincident, std.MateType);
                Assert.Equal(AssemblyMateAlignment.Aligned, std.Alignment);
                Assert.True(std.PlaneReferences.Count == 2 || std.EntityReferences.Count == 2, "Entities not recovered");
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddWidthMate_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_width_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                assy.AddWidthMate(
                    widthFace1: "FaceW1@compA",
                    widthFace2: "FaceW2@compA",
                    tabFace1: "FaceT1@compB",
                    tabFace2: "FaceT2@compB",
                    constraintType: AssemblyMateWidthConstraint.Dimension,
                    distance: 0.02,
                    flipDimension: true);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened Width Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}'");
                Assert.IsType<WidthMateModel>(mate);
                var width = (WidthMateModel)mate;
                Assert.Equal(AssemblyMateWidthConstraint.Dimension, width.ConstraintType);
                Assert.Equal(0.02, width.DistanceFromEnd, precision: 6);
                Assert.True(width.FlipDimension);
                Assert.Equal(4, width.EntityReferences.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddProfileCenterMate_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_pc_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                assy.AddProfileCenterMate(
                    entity1: "Face1@compA",
                    entity2: "Face2@compB",
                    offsetDistance: 0.015,
                    flipDimension: false,
                    lockRotation: true);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened ProfileCenter Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}'");
                Assert.IsType<ProfileCenterMateModel>(mate);
                var pc = (ProfileCenterMateModel)mate;
                Assert.Equal(0.015, pc.OffsetDistance, precision: 6);
                Assert.True(pc.LockRotation);
                Assert.Equal(2, pc.EntityReferences.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddCamFollowerMate_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_cam_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                assy.AddCamFollowerMate(
                    followerEntity: "FollowerFace@follower",
                    camFaces: new[] { "CamFace1@cam", "CamFace2@cam", "CamFace3@cam" },
                    isTangent: true);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened Cam Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}'");
                Assert.IsType<CamFollowerMateModel>(mate);
                var cam = (CamFollowerMateModel)mate;
                Assert.True(cam.IsTangent);
                Assert.Equal("FollowerFace@follower", cam.FollowerEntity);
                Assert.Equal(3, cam.CamProfileFaces.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddDistanceMate_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_dist_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                var def = new StandardMateDefinitionData(AssemblyMateType.Distance)
                {
                    DisplayName = "TestDistance1",
                    Alignment = AssemblyMateAlignment.Aligned,
                    Distance = 0.035
                };
                def.EntitiesToMate.Add("RightPlane@cube-1@assembly");
                def.EntitiesToMate.Add("RightPlane@cylinder-1@assembly");

                assy.AddMate(def);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened Distance Mate: Class={mate.ClassName}, Type={mate.GetType().Name}, DisplayName='{mate.DisplayName}'");
                Assert.Equal("TestDistance1", mate.DisplayName);
                var std = Assert.IsType<StandardMateModel>(mate);
                Assert.Equal(AssemblyMateType.Distance, std.MateType);
                Assert.Equal(0.035, std.Distance);
                Assert.Equal(2, std.PlaneReferences.Count);
                Assert.Equal(2, reopened.Mates[0].Entities.Count);
                Assert.Equal(0.035, reopened.Mates[0].Distance);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddCoincidentAndDistanceMates_Together_GeneratesValidStream_AndReopens()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_dual_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                var defCoin = new StandardMateDefinitionData(AssemblyMateType.Coincident)
                {
                    DisplayName = "CoincidentPair",
                    Alignment = AssemblyMateAlignment.Aligned
                };
                defCoin.EntitiesToMate.Add("FrontPlane@cube-1@assembly");
                defCoin.EntitiesToMate.Add("FrontPlane@cylinder-1@assembly");
                assy.AddMate(defCoin);

                var defDist = new StandardMateDefinitionData(AssemblyMateType.Distance)
                {
                    DisplayName = "DistancePair",
                    Alignment = AssemblyMateAlignment.Aligned,
                    Distance = 0.042
                };
                defDist.EntitiesToMate.Add("RightPlane@cube-1@assembly");
                defDist.EntitiesToMate.Add("RightPlane@cylinder-1@assembly");
                assy.AddMate(defDist);

                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Equal(2, reopened.MatesList.Mates.Count);
                Assert.Equal(2, reopened.Mates.Count);

                var coin = Assert.IsType<StandardMateModel>(reopened.MatesList.Mates[0]);
                Assert.Equal("CoincidentPair", coin.DisplayName);
                Assert.Equal(AssemblyMateType.Coincident, coin.MateType);
                Assert.Equal(2, coin.PlaneReferences.Count);
                Assert.Equal(2, reopened.Mates[0].Entities.Count);

                var dist = Assert.IsType<StandardMateModel>(reopened.MatesList.Mates[1]);
                Assert.Equal("DistancePair", dist.DisplayName);
                Assert.Equal(AssemblyMateType.Distance, dist.MateType);
                Assert.Equal(0.042, dist.Distance);
                Assert.Equal(2, dist.PlaneReferences.Count);
                Assert.Equal(2, reopened.Mates[1].Entities.Count);
                Assert.Equal(0.042, reopened.Mates[1].Distance);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddWidthMate_FreeConstraint_ReopensAsFree()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_width_free_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                assy.AddWidthMate(
                    widthFace1: "FaceW1@compA",
                    widthFace2: "FaceW2@compA",
                    tabFace1: "FaceT1@compB",
                    tabFace2: "FaceT2@compB",
                    constraintType: AssemblyMateWidthConstraint.Free);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened Width Free Mate: Class={mate.ClassName}, DisplayName='{mate.DisplayName}'");
                var width = Assert.IsType<WidthMateModel>(mate);
                Assert.Equal(AssemblyMateWidthConstraint.Free, width.ConstraintType);
                Assert.Equal(4, width.EntityReferences.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SldAssembly_AddRackPinionMate_NegativeTravel_ReopensNegative()
    {
        string sourceBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SldFileTests/TestFiles/Mates2026/NoMate/assembly_nomate_base.SLDASM"));
        if (!File.Exists(sourceBase))
            sourceBase = @"d:\weigan\repos\sldfile\SldFileTests\TestFiles\Mates2026\NoMate\assembly_nomate_base.SLDASM";

        string tempFile = Path.Combine(Path.GetTempPath(), $"emp_add_rp_neg_{Guid.NewGuid():N}.SLDASM");
        File.Copy(sourceBase, tempFile, true);

        try
        {
            using (var assy = new SldAssembly(tempFile, readOnly: false))
            {
                var def = new RackPinionMateDefinitionData
                {
                    DisplayName = "RackPinionNegative",
                    DiameterType = AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
                    DiameterValue = -0.05,
                    Reverse = true,
                    RackEntity = "EdgeRack@compA",
                    PinionEntity = "CylPinion@compB"
                };
                assy.AddMate(def);
                assy.Save();
            }

            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Single(reopened.MatesList.Mates);
                var mate = reopened.MatesList.Mates[0];
                _output.WriteLine($"Reopened RackPinion Mate: Class={mate.ClassName}, DisplayName='{mate.DisplayName}'");
                var rp = Assert.IsType<RackPinionMateModel>(mate);
                Assert.Equal(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, rp.DiameterType);
                Assert.Equal(-0.05, rp.DiameterValue, precision: 6);
                Assert.True(rp.Reverse);
                Assert.Equal("EdgeRack@compA", rp.RackComponent);
                Assert.Equal("CylPinion@compB", rp.PinionComponent);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

