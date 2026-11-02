using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Offline groundwork tests for Task 2.3 / 2.4 (hem, jog, lofted bend, normal
/// cut, flat pattern, fold/unfold). No native sample exists yet, so these tests
/// lock down class-name recognition, strict null parsing, non-mutation, nullable
/// API models with no invented defaults, and unencoded TypeId candidates.
/// </summary>
public class SheetMetalFormingSkeletonTests
{
    private static readonly string[] HemApi =
    {
        "Angle", "BendPosition", "Edges", "GapDistance", "Length", "MiterGap",
        "Radius", "ReverseDirection", "Type", "UseDefaultBendAllowance"
    };

    private static readonly string[] JogApi =
    {
        "BendRadius", "DimensionPositionType", "FixedFace", "FixedPoint", "FixProjectedLength",
        "JogAngle", "JogPositionType", "OffsetDistance", "OffsetReference", "OffsetType",
        "ReverseDirection", "ReverseOffset", "UseDefaultBendAllowance", "UseDefaultBendRadius"
    };

    private static readonly string[] LoftedBendApi =
    {
        "BendLineControlOption", "Direction", "FacetingOption", "FacetValue", "FormedMethod",
        "MaximumDeviation", "NumberOfBendLines", "Profiles", "ReferToEndPoint",
        "SymmetricThickness", "Thickness"
    };

    private static readonly string[] NormalCutApi =
    {
        "AutoPropagation", "CutDirection", "LayerAdjustmentValue", "LinkToKFactor",
        "NormalCutParameters", "OffsetPlaneReference", "OptimizeGeometry"
    };

    private static readonly string[] FlatPatternApi =
    {
        "BreakCornerRadius", "BreakCornerType", "CornerTreatment", "CornerTrimRatioToThickness",
        "CornerTrimReliefDistance", "CornerTrimReliefType", "ExcludedFaces", "FixedFace",
        "FixedFace2", "MergeFace", "ShowSlitInCornerRelief", "SimplifyBends", "UseRatioToThickness"
    };

    private static readonly string[] FoldsApi = { "Bends", "FixedFace" };

    public static IEnumerable<object[]> ModelCases()
    {
        yield return new object[] { new HemFeatureDefinitionData(), typeof(HemParameterResolution), HemApi,
            new[] { "CustomBendAllowance", "EdgeCount" } };
        yield return new object[] { new JogFeatureDefinitionData(), typeof(JogParameterResolution), JogApi,
            new[] { "CustomBendAllowance", "OffsetReferenceType" } };
        yield return new object[] { new LoftedBendFeatureDefinitionData(), typeof(LoftedBendParameterResolution), LoftedBendApi,
            new[] { "ProfileCount" } };
        yield return new object[] { new NormalCutFeatureDefinitionData(), typeof(NormalCutParameterResolution), NormalCutApi,
            Array.Empty<string>() };
        yield return new object[] { new FlatPatternFeatureDefinitionData(), typeof(FlatPatternParameterResolution), FlatPatternApi,
            new[] { "AddCornerTrim", "BreakCorners" } };
        yield return new object[] { new FoldFeatureDefinitionData(), typeof(FoldsParameterResolution), FoldsApi,
            new[] { "BendsCount" } };
        yield return new object[] { new UnFoldFeatureDefinitionData(), typeof(FoldsParameterResolution), FoldsApi,
            new[] { "BendsCount" } };
    }

    private static byte[] BuildSyntheticFolderStream(string className, string displayName)
    {
        var bytes = new List<byte> { 0x73, 0x00 };
        var classBytes = Encoding.ASCII.GetBytes(className);
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00 });
        bytes.Add((byte)(classBytes.Length & 0xFF));
        bytes.Add((byte)(classBytes.Length >> 8));
        bytes.AddRange(classBytes);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));
        bytes.AddRange(new byte[90]);
        return bytes.ToArray();
    }

    [Theory]
    [InlineData("moHem_c", "Hem1")]
    [InlineData("moJog_c", "Jog1")]
    [InlineData("moLoftedBend_c", "LoftedBend1")]
    [InlineData("moSMNormalCut_c", "NormalCut1")]
    [InlineData("moFlatPattern_c", "FlatPattern1")]
    [InlineData("moFold_c", "Fold1")]
    [InlineData("moUnFold_c", "Unfold1")]
    public void CatalogCandidateClassNamesAreRecognizedAsFeatureFolders(string className, string displayName)
    {
        var raw = BuildSyntheticFolderStream(className, displayName);
        var doc = MfcDocument.Parse(raw);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(doc, model);

        var folder = Assert.Single(model.Folders, f => f.ClassName == className);
        Assert.Equal(displayName, folder.DisplayName);
        Assert.Null(folder.FeatureData);
    }

    [Theory]
    [InlineData("moHem_c")]
    [InlineData("moJog_c")]
    [InlineData("moLoftedBend_c")]
    [InlineData("moSMNormalCut_c")]
    [InlineData("moFlatPattern_c")]
    [InlineData("moFold_c")]
    [InlineData("moUnFold_c")]
    public void RegisteredSkeletonParsersRejectEveryUnknownLayout(string className)
    {
        var raw = BuildSyntheticFolderStream(className, "Feature1");
        var element = Assert.Single(MfcDocument.Parse(raw).AllElements, e => e.ClassName == className);

        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
        Assert.Null(ParseByClass(className, element));
    }

    private static IFeatureData? ParseByClass(string className, MfcElement element) => className switch
    {
        SheetMetalFormingParsers.HemClassName => SheetMetalFormingParsers.ParseHem(element),
        SheetMetalFormingParsers.JogClassName => SheetMetalFormingParsers.ParseJog(element),
        SheetMetalFormingParsers.LoftedBendClassName => SheetMetalFormingParsers.ParseLoftedBend(element),
        SheetMetalFormingParsers.NormalCutClassName => SheetMetalFormingParsers.ParseNormalCut(element),
        SheetMetalFormingParsers.FlatPatternClassName => SheetMetalFormingParsers.ParseFlatPattern(element),
        SheetMetalFormingParsers.FoldClassName => SheetMetalFormingParsers.ParseFold(element),
        SheetMetalFormingParsers.UnFoldClassName => SheetMetalFormingParsers.ParseUnFold(element),
        _ => throw new ArgumentOutOfRangeException(nameof(className))
    };

    [Fact]
    public void SkeletonParserRejectsTheWrongClass()
    {
        var hem = Assert.Single(MfcDocument.Parse(
            BuildSyntheticFolderStream("moHem_c", "Hem1")).AllElements);
        Assert.Null(SheetMetalFormingParsers.ParseJog(hem));

        var jog = Assert.Single(MfcDocument.Parse(
            BuildSyntheticFolderStream("moJog_c", "Jog1")).AllElements);
        Assert.Null(SheetMetalFormingParsers.ParseHem(jog));
    }

    [Fact]
    public void MappingDoesNotModifyTheSourceStream()
    {
        var raw = BuildSyntheticFolderStream("moHem_c", "Hem1");
        var before = (byte[])raw.Clone();
        var doc = MfcDocument.Parse(raw);
        ResolvedFeaturesSchemaMapper.Map(doc, new ResolvedFeaturesData());
        Assert.Equal(before, doc.SourceData);
    }

    [Fact]
    public void MappingARealPartStreamDoesNotModifyIt()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalOptions", "sm_l_d10.SLDPRT");
        Assert.True(File.Exists(path), $"Sample not found: {path}");

        using var file = SwFile.Open(path, readOnly: true);
        var raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        ResolvedFeaturesSchemaMapper.Map(doc, new ResolvedFeaturesData());
        Assert.Equal(raw, doc.SourceData);
    }

    [Theory]
    [MemberData(nameof(ModelCases))]
    public void ModelExposesTheCompleteApiPropertySet(
        object model, Type resolutionType, string[] apiProperties, string[] methodDerived)
    {
        Assert.False(resolutionType.IsAbstract);
        var modelOnly = new HashSet<string>(StringComparer.Ordinal) { "ClassName", "TypeId", "ParameterResolution" };
        foreach (var extra in methodDerived) modelOnly.Add(extra);

        var actual = model.GetType().GetProperties()
            .Select(p => p.Name)
            .Where(name => !modelOnly.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(apiProperties.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
    }

    [Theory]
    [MemberData(nameof(ModelCases))]
    public void UnverifiedValuesHaveNoDefaultsAndResolutionFlagsAreFalse(
        object model, Type resolutionType, string[] apiProperties, string[] methodDerived)
    {
        _ = apiProperties;
        _ = methodDerived;
        foreach (var property in model.GetType().GetProperties())
        {
            if (property.Name is "ClassName" or "TypeId" or "ParameterResolution") continue;
            Assert.Null(property.GetValue(model));
        }

        var resolution = Activator.CreateInstance(resolutionType)!;
        foreach (var property in resolutionType.GetProperties())
            Assert.False((bool)property.GetValue(resolution)!);
    }

    [Fact]
    public void TypeIdCandidatesAreNotHardcoded()
    {
        // Hem is sample-confirmed at 0x67 (version 0x73) and LoftedBend at 0x72
        // (version 0x74). Remaining catalog candidates 0x66 Jog, 0x67 NormalCut,
        // 0x62 FlatPattern, 0x6D Fold and 0x6E UnFold are not sample-confirmed and
        // stay unencoded.
        Assert.Equal(0x67u, new HemFeatureDefinitionData().TypeId);
        Assert.Equal(0x72u, new LoftedBendFeatureDefinitionData().TypeId);
        var unconfirmed = new IFeatureData[]
        {
            new JogFeatureDefinitionData(),
            new NormalCutFeatureDefinitionData(),
            new FlatPatternFeatureDefinitionData(),
            new FoldFeatureDefinitionData(),
            new UnFoldFeatureDefinitionData()
        };
        Assert.All(unconfirmed, model => Assert.Equal(0u, model.TypeId));

        Assert.Equal("moHem_c", HemFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moJog_c", JogFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moLoftedBend_c", LoftedBendFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moSMNormalCut_c", NormalCutFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moFlatPattern_c", FlatPatternFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moFold_c", FoldFeatureDefinitionData.SerializedClassName);
        Assert.Equal("moUnFold_c", UnFoldFeatureDefinitionData.SerializedClassName);
    }

    [Fact]
    public void ModelsArePurePocoWithoutSolidWorksInteropTypes()
    {
        var assembly = typeof(HemFeatureDefinitionData).Assembly;
        Assert.Equal("SldFile", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
            a.Name!.Contains("SolidWorks", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Interop", StringComparison.OrdinalIgnoreCase));

        var types = new[]
        {
            typeof(HemFeatureDefinitionData), typeof(HemParameterResolution),
            typeof(JogFeatureDefinitionData), typeof(JogParameterResolution),
            typeof(LoftedBendFeatureDefinitionData), typeof(LoftedBendParameterResolution),
            typeof(NormalCutFeatureDefinitionData), typeof(NormalCutParameterResolution),
            typeof(FlatPatternFeatureDefinitionData), typeof(FlatPatternParameterResolution),
            typeof(FoldFeatureDefinitionData), typeof(UnFoldFeatureDefinitionData),
            typeof(FoldsParameterResolution)
        };
        foreach (var type in types)
        {
            Assert.Equal("SldFile", type.Namespace);
            foreach (var property in type.GetProperties())
                AssertNoComType(property.PropertyType);
        }
    }

    private static void AssertNoComType(Type type)
    {
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                AssertNoComType(argument);
        }

        string name = type.FullName ?? type.Name;
        Assert.DoesNotContain("SolidWorks", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Interop", name, StringComparison.OrdinalIgnoreCase);
    }
}
