using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests.Features;

/// <summary>
/// Empirical challenge suite authored by Challenger P1.2 (Solid Features Native Specimen Challenger).
/// Rigorously tests real SolidWorks native specimens across models/, SweepOptions, LoftOptions,
/// ShellOptions, DraftOptions, CombineSplitOptions, and LegacyTutorialSamples in both
/// read-only and read-write modes, verifying feature deserialization, attribute accuracy,
/// and round-trip resave/reopen consistency.
/// </summary>
public class EmpiricalChallengerP12Tests
{
    private readonly ITestOutputHelper _output;

    public EmpiricalChallengerP12Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string ResolveModels(string fileName)
    {
        string candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "models", fileName));
        if (File.Exists(candidate)) return candidate;
        candidate = Path.Combine(@"d:\weigan\repos\sldfile\models", fileName);
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException($"Model specimen not found: {fileName}");
    }

    private static string ResolveOption(string folder, string fileName)
    {
        return TestFileProvider.Resolve($"Features2026/{folder}", fileName);
    }

    private static string ResolveTutorial(string fileName)
    {
        return TestFileProvider.Resolve("Features2026/LegacyTutorialSamples/Samples", fileName);
    }

    private static TempTestFile CreateTempCopy(string sourcePath)
    {
        string dir = Path.GetDirectoryName(sourcePath)!;
        string ext = Path.GetExtension(sourcePath);
        string tempPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(sourcePath)}_chlng_{Guid.NewGuid():N}{ext}");
        File.Copy(sourcePath, tempPath, true);
        return new TempTestFile(tempPath);
    }

    private static IEnumerable<Feature> Flatten(IEnumerable<Feature> features)
    {
        foreach (var f in features)
        {
            yield return f;
            foreach (var child in Flatten(f.Children))
                yield return child;
        }
    }

    // =========================================================================
    // Category 1: Real Native Models in models/
    // =========================================================================

    [Theory]
    [InlineData("cube.SLDPRT")]
    [InlineData("cylinder.SLDPRT")]
    [InlineData("cube_20.SLDPRT")]
    [InlineData("cube_red.SLDPRT")]
    public void Models_NativeParts_OpenReadOnlyAndReadWrite_AndResaveConsistently(string fileName)
    {
        string path = ResolveModels(fileName);
        _output.WriteLine($"Testing native model: {path}");

        // 1. Read-only open via SwFile.Open and SldPart
        using (var swFile = SwFile.Open(path, readOnly: true))
        {
            Assert.NotNull(swFile);
            Assert.Equal(DocumentKind.Part, swFile.Kind);
            var part = Assert.IsType<SldPart>(swFile);
            Assert.True(part.Features.Count > 0, $"Expected features in {fileName}");
            _output.WriteLine($"  {fileName} features count: {part.Features.Count}");
        }

        using (var directPart = new SldPart(path, readOnly: true))
        {
            Assert.NotNull(directPart);
            Assert.True(directPart.Features.Count > 0);
        }

        // 2. Read-write open and round-trip resave
        using var temp = CreateTempCopy(path);
        byte[] originalBytes = File.ReadAllBytes(temp.FilePath);

        int originalFeatureCount;
        using (var writePart = new SldPart(temp.FilePath, readOnly: false))
        {
            Assert.False(writePart.IsReadOnly);
            originalFeatureCount = writePart.Features.Count;
            writePart.Save();
        }

        byte[] savedBytes = File.ReadAllBytes(temp.FilePath);
        Assert.True(savedBytes.Length > 0);

        // 3. Reopen after resave and verify consistency
        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalFeatureCount, reopened.Features.Count);
            _output.WriteLine($"  {fileName} resave/reopen passed with {reopened.Features.Count} features.");
        }
    }

    // =========================================================================
    // Category 2: Native Sweep Specimen Challenge (SweepOptions/)
    // =========================================================================

    [Theory]
    [InlineData("sweep_circle10_path40.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_circle10_path40_resave.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_circle10_path60.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_circle10_path60_resave.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_circular_profile12.SLDPRT", 0x4Bu, true, false, 0.012, null)]
    [InlineData("sweep_circular_profile12_resave.SLDPRT", 0x4Bu, true, false, 0.012, null)]
    [InlineData("sweep_profile_rect.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_profile_rect_resave.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_guide1.SLDPRT", 0x4Du, false, false, null, null)]
    [InlineData("sweep_guide1_resave.SLDPRT", 0x4Du, false, false, null, null)]
    [InlineData("sweep_thin_one_dir_t1.SLDPRT", 0x4Cu, false, true, null, 0.001)]
    [InlineData("sweep_thin_one_dir_t1_resave.SLDPRT", 0x4Cu, false, true, null, 0.001)]
    [InlineData("sweep_merge_false.SLDPRT", 0x4Cu, false, false, null, null)]
    [InlineData("sweep_merge_false_resave.SLDPRT", 0x4Cu, false, false, null, null)]
    public void Sweep_NativeSpecimens_DeserializeToSweepFeatureModel_AndVerifyAttributes(
        string fileName, uint expectedTypeId, bool expectedCircular, bool expectedThin,
        double? expectedDiameter, double? expectedThickness)
    {
        string path = ResolveOption("SweepOptions", fileName);
        _output.WriteLine($"Evaluating Sweep specimen: {fileName}");

        using var file = SwFile.Open(path, readOnly: true);
        var sweepFeature = Flatten(file.Features).FirstOrDefault(f => f.Kind == FeatureKind.Sweep || f.TypeName == "moSweep_c");
        Assert.NotNull(sweepFeature);

        var model = Assert.IsType<SweepFeatureModel>(sweepFeature.Model);
        Assert.Equal("moSweep_c", model.ClassName);
        Assert.Equal(expectedTypeId, model.TypeId);
        Assert.Equal(expectedCircular, model.CircularProfile);
        Assert.Equal(expectedThin, model.IsThinFeature);

        if (expectedDiameter.HasValue)
        {
            Assert.NotNull(model.CircularProfileDiameter);
            Assert.Equal(expectedDiameter.Value, model.CircularProfileDiameter.Value, precision: 4);
        }

        if (expectedThickness.HasValue)
        {
            Assert.NotNull(model.WallThickness);
            Assert.Equal(expectedThickness.Value, model.WallThickness.Value, precision: 4);
        }

        if (fileName.Contains("guide1"))
        {
            Assert.Contains(model.References, r => r.Role == SketchCurveRole.Guide);
        }

        if (fileName.Contains("merge_false"))
        {
            Assert.False(model.Merge);
        }
    }

    [Theory]
    [InlineData("sweep_circle10_path40.SLDPRT")]
    [InlineData("sweep_circular_profile12.SLDPRT")]
    [InlineData("sweep_thin_one_dir_t1.SLDPRT")]
    [InlineData("sweep_guide1.SLDPRT")]
    public void Sweep_NativeSpecimens_ResaveAndReopen_PreservesFeatureModelAndAttributes(string fileName)
    {
        string source = ResolveOption("SweepOptions", fileName);
        using var temp = CreateTempCopy(source);

        uint origTypeId;
        bool origCircular;
        bool origThin;
        double? origDiameter;
        double? origThickness;

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var feat = Flatten(part.Features).First(f => f.Kind == FeatureKind.Sweep);
            var model = Assert.IsType<SweepFeatureModel>(feat.Model);
            origTypeId = model.TypeId;
            origCircular = model.CircularProfile;
            origThin = model.IsThinFeature;
            origDiameter = model.CircularProfileDiameter;
            origThickness = model.WallThickness;

            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feat = Flatten(reopened.Features).First(f => f.Kind == FeatureKind.Sweep);
            var reloadedModel = Assert.IsType<SweepFeatureModel>(feat.Model);
            Assert.Equal(origTypeId, reloadedModel.TypeId);
            Assert.Equal(origCircular, reloadedModel.CircularProfile);
            Assert.Equal(origThin, reloadedModel.IsThinFeature);
            Assert.Equal(origDiameter, reloadedModel.CircularProfileDiameter);
            Assert.Equal(origThickness, reloadedModel.WallThickness);
            _output.WriteLine($"  {fileName} round-trip resave/reopen verified successfully.");
        }
    }

    // =========================================================================
    // Category 3: Native Loft Specimen Challenge (LoftOptions/)
    // =========================================================================

    [Theory]
    [InlineData("loft_circle10_20.SLDPRT", 0x4Du, 2, false, false)]
    [InlineData("loft_circle10_20_resave.SLDPRT", 0x4Du, 2, false, false)]
    [InlineData("loft_three_sections.SLDPRT", 0x50u, 3, false, false)]
    [InlineData("loft_three_sections_resave.SLDPRT", 0x50u, 3, false, false)]
    [InlineData("loft_guide1.SLDPRT", 0x4Eu, 2, true, false)]
    [InlineData("loft_guide1_resave.SLDPRT", 0x4Eu, 2, true, false)]
    [InlineData("loft_start_end_tangency.SLDPRT", 0x4Du, 2, false, false)]
    [InlineData("loft_start_end_tangency_resave.SLDPRT", 0x4Du, 2, false, false)]
    [InlineData("loft_centerline.SLDPRT", 0x4Eu, 2, false, true)]
    [InlineData("loft_centerline_resave.SLDPRT", 0x4Eu, 2, false, true)]
    public void Loft_NativeSpecimens_DeserializeToLoftFeatureModel_AndVerifyAttributes(
        string fileName, uint expectedTypeId, int minProfiles, bool hasGuide, bool hasCenterline)
    {
        string path = ResolveOption("LoftOptions", fileName);
        _output.WriteLine($"Evaluating Loft specimen: {fileName}");

        using var file = SwFile.Open(path, readOnly: true);
        var loftFeature = Flatten(file.Features).FirstOrDefault(f => f.Kind == FeatureKind.Loft || f.TypeName == "moBlend_c");
        Assert.NotNull(loftFeature);

        var model = Assert.IsType<LoftFeatureModel>(loftFeature.Model);
        Assert.Equal("moBlend_c", model.ClassName);
        Assert.Equal(expectedTypeId, model.TypeId);
        Assert.True(model.References.Count >= minProfiles);

        if (hasGuide)
        {
            Assert.Contains(model.References, r => r.Role == SketchCurveRole.Guide);
        }

        if (hasCenterline)
        {
            Assert.NotNull(model.Centerline);
            Assert.Equal(SketchCurveRole.Centerline, model.Centerline!.Role);
        }

        if (fileName.Contains("start_end_tangency"))
        {
            Assert.Equal(1, model.StartTangencyType);
            Assert.Equal(1, model.EndTangencyType);
        }
    }

    [Theory]
    [InlineData("loft_circle10_20.SLDPRT")]
    [InlineData("loft_three_sections.SLDPRT")]
    [InlineData("loft_guide1.SLDPRT")]
    [InlineData("loft_start_end_tangency.SLDPRT")]
    public void Loft_NativeSpecimens_ResaveAndReopen_PreservesFeatureModelAndAttributes(string fileName)
    {
        string source = ResolveOption("LoftOptions", fileName);
        using var temp = CreateTempCopy(source);

        uint origTypeId;
        int origRefsCount;
        int? origStartTangency;
        int? origEndTangency;

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var feat = Flatten(part.Features).First(f => f.Kind == FeatureKind.Loft);
            var model = Assert.IsType<LoftFeatureModel>(feat.Model);
            origTypeId = model.TypeId;
            origRefsCount = model.References.Count;
            origStartTangency = model.StartTangencyType;
            origEndTangency = model.EndTangencyType;

            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feat = Flatten(reopened.Features).First(f => f.Kind == FeatureKind.Loft);
            var reloadedModel = Assert.IsType<LoftFeatureModel>(feat.Model);
            Assert.Equal(origTypeId, reloadedModel.TypeId);
            Assert.Equal(origRefsCount, reloadedModel.References.Count);
            Assert.Equal(origStartTangency, reloadedModel.StartTangencyType);
            Assert.Equal(origEndTangency, reloadedModel.EndTangencyType);
            _output.WriteLine($"  {fileName} round-trip resave/reopen verified successfully.");
        }
    }

    // =========================================================================
    // Category 4: Native Shell & Draft Specimen Challenge
    // =========================================================================

    [Theory]
    [InlineData("shell_t2_remove_top.SLDPRT", 0.002)]
    [InlineData("shell_t2_remove_top_resave.SLDPRT", 0.002)]
    [InlineData("shell_t3_remove_top.SLDPRT", 0.003)]
    [InlineData("shell_t3_remove_top_resave.SLDPRT", 0.003)]
    public void Shell_NativeSpecimens_DeserializeToShellFeatureModel_AndVerifyAttributes(string fileName, double expectedThickness)
    {
        string path = ResolveOption("ShellOptions", fileName);
        using var file = SwFile.Open(path, readOnly: true);

        var shellFeature = Flatten(file.Features).FirstOrDefault(f => f.Kind == FeatureKind.Shell);
        Assert.NotNull(shellFeature);

        var model = Assert.IsType<ShellFeatureModel>(shellFeature.Model);
        Assert.Equal("moShell_c", model.ClassName);
        Assert.Equal(0x51u, model.TypeId);
        Assert.NotNull(model.Thickness);
        Assert.Equal(expectedThickness, model.Thickness.Value, precision: 5);
        Assert.NotNull(model.FacesRemoved);
        Assert.Single(model.FacesRemoved!);
    }

    [Fact]
    public void Shell_NativeSpecimen_ResaveAndReopen_PreservesThickness()
    {
        string source = ResolveOption("ShellOptions", "shell_t2_remove_top.SLDPRT");
        using var temp = CreateTempCopy(source);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var feat = Flatten(part.Features).First(f => f.Kind == FeatureKind.Shell);
            var model = Assert.IsType<ShellFeatureModel>(feat.Model);
            Assert.Equal(0.002, model.Thickness!.Value, precision: 5);
            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feat = Flatten(reopened.Features).First(f => f.Kind == FeatureKind.Shell);
            var model = Assert.IsType<ShellFeatureModel>(feat.Model);
            Assert.Equal(0.002, model.Thickness!.Value, precision: 5);
        }
    }

    [Theory]
    [InlineData("draft_side_3deg_neutral_bottom.SLDPRT")]
    [InlineData("draft_side_3deg_neutral_bottom_resave.SLDPRT")]
    public void Draft_NativeSpecimens_DeserializeToDraftFeatureModel_AndVerifyAttributes(string fileName)
    {
        string path = ResolveOption("DraftOptions", fileName);
        using var file = SwFile.Open(path, readOnly: true);

        var draftFeature = Flatten(file.Features).FirstOrDefault(f => f.Kind == FeatureKind.Draft);
        Assert.NotNull(draftFeature);

        var model = Assert.IsType<DraftFeatureModel>(draftFeature.Model);
        Assert.Equal("moDraft_c", model.ClassName);
        Assert.Equal(0x51u, model.TypeId);
        Assert.NotNull(model.Angle);
        Assert.Equal(3.0 * Math.PI / 180.0, model.Angle.Value, precision: 5);
        Assert.NotNull(model.NeutralPlane);
    }

    // =========================================================================
    // Category 5: Native Body Operations Specimen Challenge
    // =========================================================================

    [Theory]
    [InlineData("delete_body_second.SLDPRT")]
    [InlineData("delete_body_second_resave.SLDPRT")]
    public void DeleteBody_NativeSpecimens_DeserializeToDeleteBodyFeatureModel(string fileName)
    {
        string path = ResolveOption("CombineSplitOptions", fileName);
        using var file = SwFile.Open(path, readOnly: true);

        var deleteFeature = Flatten(file.Features).FirstOrDefault(f => f.Kind == FeatureKind.DeleteBody);
        Assert.NotNull(deleteFeature);

        var model = Assert.IsType<DeleteBodyFeatureModel>(deleteFeature.Model);
        Assert.Equal("moDeleteBody_c", model.ClassName);
        Assert.Equal(0x57u, model.TypeId);
        Assert.NotNull(model.Bodies);
        Assert.Single(model.Bodies!);
    }

    [Theory]
    [InlineData("tutorial_molded_mousemultibody", "Combine1")]
    [InlineData("learn_Mouse_MainHousing", "Merge Shells")]
    [InlineData("pf_sldBenchmarking_carrier", "Combine1")]
    public void CombineBodies_NativeTutorialSpecimens_DeserializeToCombineBodiesFeatureModel(string sampleName, string featureName)
    {
        string path = ResolveTutorial(sampleName + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        var feat = Flatten(file.Features).FirstOrDefault(f => f.Name == featureName && f.Model is CombineBodiesFeatureModel);
        Assert.NotNull(feat);
        Assert.Equal(FeatureKind.CombineBodies, feat.Kind);

        var model = Assert.IsType<CombineBodiesFeatureModel>(feat.Model);
        Assert.Equal("moCombineBodies_c", model.ClassName);
        Assert.True(model.TypeId > 0);
    }

    [Theory]
    [InlineData("tutorial_molded_mousemultibody", "Split1")]
    [InlineData("learn_KnifeHandle", "Split1")]
    [InlineData("learn_Mouse_MainHousing", "Split for Shell")]
    public void SplitBody_NativeTutorialSpecimens_DeserializeToSplitBodyFeatureModel(string sampleName, string featureName)
    {
        string path = ResolveTutorial(sampleName + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        var feat = Flatten(file.Features).FirstOrDefault(f => f.Name == featureName && f.Model is SplitBodyFeatureModel);
        Assert.NotNull(feat);
        Assert.Equal(FeatureKind.SplitBody, feat.Kind);

        var model = Assert.IsType<SplitBodyFeatureModel>(feat.Model);
        Assert.Equal("moSplit_c", model.ClassName);
        Assert.True(model.TypeId > 0);
    }

    [Theory]
    [InlineData("tutorial_sustainability_drive_sled", "Rib1", 0.0015)]
    [InlineData("tutorial_advdrawings_shaft_gear", "boss rib", 0.002)]
    [InlineData("tutorial_advdrawings_switch_plate", "rocket tail rib a", 0.00122)]
    [InlineData("learn_Mouse_MainHousing", "Screw Boss Rib1", 0.000762)]
    [InlineData("learn_Mouse_BottomHousing", "Main Rib- Rib5", 0.000762)]
    public void Rib_NativeTutorialSpecimens_DeserializeToRibFeatureModel_AndMatchThickness(string sampleName, string featureName, double expectedThickness)
    {
        string path = ResolveTutorial(sampleName + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        var feat = Flatten(file.Features).FirstOrDefault(f => f.Name == featureName && f.Model is RibFeatureModel);
        Assert.NotNull(feat);
        Assert.Equal(FeatureKind.Rib, feat.Kind);

        var model = Assert.IsType<RibFeatureModel>(feat.Model);
        Assert.Equal("moRib_c", model.ClassName);
        Assert.NotNull(model.Thickness);
        Assert.Equal(expectedThickness, model.Thickness.Value, precision: 6);
    }

    // =========================================================================
    // Category 6: Mandate Compliance & Reflection Verification
    // =========================================================================

    [Fact]
    public void MandateCheck_NoExternalFeatureDefinitionDataWrappers_ExposedDirectly()
    {
        var asm = typeof(SweepFeatureModel).Assembly;
        var types = asm.GetTypes();

        // 1. Ensure no *DefinitionData wrappers exist for the 8 solid features
        string[] forbiddenWrapperNames =
        {
            "SweepFeatureDefinitionData",
            "LoftFeatureDefinitionData",
            "ShellFeatureDefinitionData",
            "DraftFeatureDefinitionData",
            "CombineBodiesFeatureDefinitionData",
            "SplitBodyFeatureDefinitionData",
            "DeleteBodyFeatureDefinitionData",
            "RibFeatureDefinitionData"
        };

        foreach (var forbidden in forbiddenWrapperNames)
        {
            var matchedType = types.FirstOrDefault(t => t.Name == forbidden);
            Assert.True(matchedType == null, $"Forbidden wrapper type '{forbidden}' must not exist per mandate.");
        }

        // 2. Ensure all 8 solid models implement IMfcFeatureModel and IMfcSerializable
        Type[] solidModels =
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

        foreach (var modelType in solidModels)
        {
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(modelType),
                $"{modelType.Name} must implement IMfcFeatureModel.");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(modelType),
                $"{modelType.Name} must implement IMfcSerializable.");
        }

        // 3. Ensure MfcSketchCurveReference implements IMfcSerializable
        Assert.True(typeof(IMfcSerializable).IsAssignableFrom(typeof(MfcSketchCurveReference)),
            "MfcSketchCurveReference must implement IMfcSerializable.");
    }
}
