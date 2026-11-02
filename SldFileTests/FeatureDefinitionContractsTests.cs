using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Contract tests for the Task 1.3/1.4/1.5 offline foundation:
/// interop API inventory completeness, model property coverage, resolution defaults and the
/// strict parser skeletons (no samples =&gt; null, no stream mutation).
/// </summary>
public class FeatureDefinitionContractsTests
{
    private static readonly Dictionary<string, HashSet<string>> ModeledMethods = new()
    {
        ["ISimpleFilletFeatureData2"] = new HashSet<string>
        {
            "GetRadius", "SetRadius", "GetConicRhoOrRadius", "SetConicRhoOrRadius", "GetDistance", "SetDistance"
        },
        ["IChamferFeatureData2"] = new HashSet<string>
        {
            "GetEdgeChamferDistance", "SetEdgeChamferDistance", "GetVertexChamferDistance",
            "SetVertexChamferDistance", "GetIsFlipped", "SetIsFlipped"
        },
        ["IWizardHoleFeatureData2"] = new HashSet<string>
        {
            "GetEndConditionReference", "GetSketchPointCount", "GetFeatureScopeBodiesCount"
        },
        ["IAdvancedHoleFeatureData"] = new HashSet<string>
        {
            "GetNearSideElements", "GetFarSideElements", "SetNearSideElements", "SetFarSideElements"
        },
        ["ILinearPatternFeatureData"] = new HashSet<string>
        {
            "GetD1AxisType", "GetD2AxisType", "GetPatternFeatureCount", "GetSkippedItemCount",
            "GetPatternFaceCount", "GetPatternBodyCount", "IsDirection2Specified", "SetFeatureScope"
        },
        ["ICircularPatternFeatureData"] = new HashSet<string>
        {
            "GetAxisType", "GetPatternFeatureCount", "GetSkippedItemCount", "GetPatternFaceCount",
            "GetPatternBodyCount", "SetFeatureScope"
        },
        ["IMirrorPatternFeatureData"] = new HashSet<string>
        {
            "GetMirrorPlaneType", "GetPatternFeatureCount", "GetMirrorFaceCount", "GetFeatureScopeBodiesCount"
        }
    };

    private static readonly HashSet<string> SelectionMethods = new()
    {
        "AccessSelections", "ReleaseSelectionAccess"
    };

    private static readonly HashSet<string> ComOperationMethods = new()
    {
        "GetTransform", "GetInstanceToVaryOptions", "SetInstanceToVaryOptions",
        "ChangeStandard", "InitializeHole", "RestoreDefaultValues", "Initialize",
        "RepairMissingReferences", "SetEndConditionReference"
    };

    private static readonly HashSet<string> ComArrayMethods = new()
    {
        "GetFilletItemAtIndex", "GetEdges", "SetEdges", "GetFaces", "SetFaces", "GetLoops", "SetLoops",
        "GetFeatures", "SetFeatures", "GetHoldLines", "SetHoldLines", "GetSetbackVertices",
        "SetSetbackVertices", "GetSetbackVertexDistance", "SetSetbackVertexDistance",
        "GetSetbackVerticesCount", "GetSetbackDistanceCount", "GetPartialEdgeFilletData",
        "SetPartialEdgeFilletData", "GetSketchPoints", "GetPatternFeatureArray", "SetPatternFeatureArray",
        "GetSkippedItemArray", "SetSkippedItemArray", "GetPatternFaceArray", "SetPatternFaceArray",
        "GetPatternBodyArray", "SetPatternBodyArray", "GetMirrorFaceArray", "SetMirrorFaceArray",
        "GetFeatureScopeBodies", "SetFeatureScopeBodies",
        "GetEdgeCount", "GetFaceCount", "GetLoopCount", "GetFeatureCount", "GetHoldLineCount"
    };

    public static IEnumerable<object[]> InterfaceCounts()
    {
        // Expected counts frozen from SolidWorks.Interop.sldworks 34.0.0.5070.
        yield return new object[] { "ISimpleFilletFeatureData2", 24, 46 };
        yield return new object[] { "IChamferFeatureData2", 12, 17 };
        yield return new object[] { "IWizardHoleFeatureData2", 61, 14 };
        yield return new object[] { "IAdvancedHoleFeatureData", 4, 6 };
        yield return new object[] { "ILinearPatternFeatureData", 37, 23 };
        yield return new object[] { "ICircularPatternFeatureData", 24, 21 };
        yield return new object[] { "IMirrorPatternFeatureData", 7, 14 };
    }

    public static IEnumerable<object[]> ModelTypeNames() => ModeledMethods.Keys
        .Select(i => new object[] { FeatureDefinitionApiCatalog.Find(i)!.ModelTypeName });

    [Theory]
    [MemberData(nameof(InterfaceCounts))]
    public void CatalogMatchesReflectedInteropSurface(string interfaceName, int propertyCount, int methodCount)
    {
        var descriptor = FeatureDefinitionApiCatalog.Find(interfaceName);
        Assert.NotNull(descriptor);
        Assert.Equal(propertyCount, descriptor!.Members.Count(m => m.Kind == "Property"));
        Assert.Equal(methodCount, descriptor.Members.Count(m => m.Kind == "Method"));

        var modelType = typeof(FeatureDefinitionApiCatalog).Assembly.GetType("SldFile." + descriptor.ModelTypeName);
        Assert.NotNull(modelType);

        var modelProperties = modelType!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var property in descriptor.Members.Where(m => m.Kind == "Property"))
            Assert.Contains(property.Name, modelProperties);

        var instance = Assert.IsAssignableFrom<FeatureDefinitionData>(Activator.CreateInstance(modelType));
        Assert.Equal(descriptor.MfcClass, instance.ClassName);
        Assert.Equal(descriptor.TypeId, instance.TypeId);
    }

    public static IEnumerable<object[]> InterfaceNames() =>
        InterfaceCounts().Select(row => new[] { row[0] });

    [Theory]
    [MemberData(nameof(InterfaceNames))]
    public void EveryApiMethodIsModeledOrExplicitlyClassified(string interfaceName)
    {
        var descriptor = FeatureDefinitionApiCatalog.Find(interfaceName);
        Assert.NotNull(descriptor);
        var apiMethodNames = descriptor!.Members
            .Where(m => m.Kind == "Method")
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var modeled in ModeledMethods[interfaceName])
            Assert.Contains(modeled, apiMethodNames);

        foreach (var method in descriptor.Members.Where(m => m.Kind == "Method"))
        {
            bool classified =
                ModeledMethods[interfaceName].Contains(method.Name) ||
                SelectionMethods.Contains(method.Name) ||
                method.Name.StartsWith("IAccessSelections", StringComparison.Ordinal) ||
                ComOperationMethods.Contains(method.Name) ||
                ComArrayMethods.Contains(method.Name) ||
                IsComInteropDuplicate(method.Name);

            Assert.True(classified,
                $"{interfaceName}.{method.Name} is neither modeled nor explicitly classified " +
                "(selection access, COM array accessor, COM operation or interop duplicate).");
        }
    }

    private static bool IsComInteropDuplicate(string methodName) =>
        // COM interop variants are emitted as IGetXxx/ISetXxx next to the managed signature.
        methodName.Length > 1 && methodName[0] == 'I' && char.IsUpper(methodName[1]);

    [Theory]
    [MemberData(nameof(ModelTypeNames))]
    public void ModelsStartUnresolvedAndWithoutValues(string modelTypeName)
    {
        var modelType = typeof(FeatureDefinitionApiCatalog).Assembly.GetType("SldFile." + modelTypeName);
        Assert.NotNull(modelType);
        var model = Assert.IsAssignableFrom<FeatureDefinitionData>(Activator.CreateInstance(modelType!));

        Assert.Empty(model.ResolvedProperties);
        foreach (var property in modelType!.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == nameof(FeatureDefinitionData.ResolvedProperties) ||
                property.GetIndexParameters().Length > 0)
                continue;

            Assert.False(model.IsResolved(property.Name), $"{modelTypeName}.{property.Name} must default unresolved");
            Assert.Null(property.GetValue(model));
        }
    }

    [Fact]
    public void ResolutionFlagsAreExplicitAndDefaultFalse()
    {
        var fillet = new FilletFeatureDefinitionData();
        Assert.False(fillet.IsResolved(nameof(FilletFeatureDefinitionData.DefaultRadius)));
        Assert.False(fillet.IsResolved("Type"));

        fillet.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.DefaultRadius));
        Assert.True(fillet.IsResolved(nameof(FilletFeatureDefinitionData.DefaultRadius)));
        Assert.False(fillet.IsResolved("Type"));
        Assert.Single(fillet.ResolvedProperties);

        var hole = new HoleWizardFeatureDefinitionData();
        Assert.False(hole.IsResolved(nameof(HoleWizardFeatureDefinitionData.Standard)));
        Assert.Empty(hole.ResolvedProperties);
    }

    [Fact]
    public void ParserSkeletonsReturnNullAndDoNotModifyTheStream()
    {
        var doc = CreateRepresentativeDocument();
        int length = doc.SourceData.Length;
        var snapshot = (byte[])doc.SourceData.Clone();

        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            doc, FilletChamferFeatureParser.FilletClassName, FilletChamferFeatureParser.FilletTypeId, 0, length));
        Assert.Null(FilletChamferFeatureParser.ParseChamfer(
            doc, FilletChamferFeatureParser.ChamferClassName, FilletChamferFeatureParser.ChamferTypeId, 0, length));
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            doc, HoleWizardFeatureParser.HoleWizardClassName, HoleWizardFeatureParser.HoleWizardTypeId, 0, length));
        Assert.Null(HoleWizardFeatureParser.ParseAdvancedHole(
            doc, HoleWizardFeatureParser.AdvancedHoleClassName, HoleWizardFeatureParser.AdvancedHoleTypeId, 0, length));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(
            doc, PatternFeatureParser.LinearPatternClassName, PatternFeatureParser.LinearPatternTypeId, 0, length));
        Assert.Null(PatternFeatureParser.ParseCircularPattern(
            doc, PatternFeatureParser.CircularPatternClassName, PatternFeatureParser.CircularPatternTypeId, 0, length));
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(
            doc, PatternFeatureParser.MirrorPatternClassName, PatternFeatureParser.MirrorPatternTypeId, 0, length));

        Assert.Equal(snapshot, doc.SourceData);
    }

    [Fact]
    public void ParserSkeletonsRejectUnknownClassTypeOrRange()
    {
        var doc = CreateRepresentativeDocument();
        int length = doc.SourceData.Length;

        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, "moUnknown_c", 0x4C, 0, length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, "moFillet_c", 0x99, 0, length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, "moFillet_c", 0x4C, length, length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, "moFillet_c", 0x4C, -1, length));

        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(doc, "moHoleWizard_c", 0x52, 0, length));
        Assert.Null(HoleWizardFeatureParser.ParseAdvancedHole(doc, "moAdvHoleWizard_c", 0x51, 0, length));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(doc, "moLPattern_c", 0x53, 0, length));
        Assert.Null(PatternFeatureParser.ParseCircularPattern(doc, "moCirPattern_c", 0x54, 0, length));
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(doc, "moMirrorPattern_c", 0x54, 0, length));
    }

    [Fact]
    public void CatalogContainsTheSevenExpectedFamilies()
    {
        Assert.Equal(7, FeatureDefinitionApiCatalog.Features.Count);
        Assert.Equal(0x51u, FeatureDefinitionApiCatalog.Find("ISimpleFilletFeatureData2")!.TypeId);
        Assert.Equal(0x52u, FeatureDefinitionApiCatalog.Find("IChamferFeatureData2")!.TypeId);
        Assert.Equal(0x54u, FeatureDefinitionApiCatalog.Find("IWizardHoleFeatureData2")!.TypeId);
        Assert.Equal(0x52u, FeatureDefinitionApiCatalog.Find("IAdvancedHoleFeatureData")!.TypeId);
        Assert.Equal(0x63u, FeatureDefinitionApiCatalog.Find("ICircularPatternFeatureData")!.TypeId);
        Assert.Equal(0x5Du, FeatureDefinitionApiCatalog.Find("ILinearPatternFeatureData")!.TypeId);
        Assert.Equal(0x57u, FeatureDefinitionApiCatalog.Find("IMirrorPatternFeatureData")!.TypeId);
    }

    private static MfcDocument CreateRepresentativeDocument()
    {
        var path = TestFileProvider.Resolve("Features2026", "extrude_10mm.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        return MfcDocument.Parse(file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
    }
}
