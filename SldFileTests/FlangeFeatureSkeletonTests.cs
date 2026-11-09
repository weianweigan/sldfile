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
/// Offline groundwork tests for Task 2.2 (edge / miter flange). No native
/// flange sample exists yet, so these tests lock down:
/// - catalog/TODO class-name recognition without payload interpretation,
/// - strict null parsing for every unknown layout,
/// - no mutation of the source stream,
/// - complete, nullable, COM-free API models with no invented defaults,
/// - TypeId candidates 0x63/0x64 are NOT hardcoded.
/// </summary>
public class FlangeFeatureSkeletonTests
{
    private static readonly string[] EdgeFlangeApiProperties =
    {
        "AngleReference", "AutoReliefType", "BendAngle", "BendRadius", "Edge", "Edges",
        "GapDistance", "LockAngle", "NormalToFlangePlane", "OffsetDimType", "OffsetDistance",
        "OffsetReference", "OffsetType", "PerpendicularToFace", "PositionOffsetDistance",
        "PositionOffsetReference", "PositionOffsetType", "PositionType", "ReliefDepth",
        "ReliefRatio", "ReliefTearType", "ReliefWidth", "ReverseOffset", "ReversePositionOffset",
        "UseDefaultBendAllowance", "UseDefaultBendRadius", "UseDefaultBendRelief",
        "UsePositionOffset", "UsePositionTrimSideBends", "UseReliefRatio"
    };

    private static readonly string[] MiterFlangeApiProperties =
    {
        "BendRadius", "Edges", "EndOffset", "GapDistance", "PositionType", "ReliefDepth",
        "ReliefRatio", "ReliefType", "ReliefWidth", "StartOffset", "UseDefaultBendAllowance",
        "UseDefaultBendRadius", "UseDefaultBendRelief", "UsePositionTrimSideBends", "UseReliefRatio"
    };

    private static byte[] BuildSyntheticFolderStream(string className, string displayName)
    {
        var bytes = new List<byte> { 0x73, 0x00 }; // stream version (part-like, observed)
        var classBytes = Encoding.ASCII.GetBytes(className);
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00 });
        bytes.Add((byte)(classBytes.Length & 0xFF));
        bytes.Add((byte)(classBytes.Length >> 8));
        bytes.AddRange(classBytes);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));
        bytes.AddRange(new byte[90]); // minimal placeholder folder header
        return bytes.ToArray();
    }

    private static ResolvedFeaturesModel MapSynthetic(string className, string displayName)
    {
        var raw = BuildSyntheticFolderStream(className, displayName);
        var doc = MfcDocument.Parse(raw);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(doc, model);
        return model;
    }

    [Theory]
    [InlineData("moEdgeFlange_c", "EdgeFlange1")]
    [InlineData("moMiterFlange_c", "MiterFlange1")]
    public void CatalogCandidateClassNamesAreRecognizedAsFeatureFolders(string className, string displayName)
    {
        var model = MapSynthetic(className, displayName);
        var folder = Assert.Single(model.Folders, f => f.ClassName == className);
        Assert.Equal(displayName, folder.DisplayName);
        // Recognition only: no payload parser exists until a controlled sample.
        Assert.Null(folder.FeatureData);
    }

    [Theory]
    [InlineData("moEdgeFlange_c")]
    [InlineData("moMiterFlange_c")]
    public void RegisteredSkeletonParsersRejectEveryUnknownLayout(string className)
    {
        var raw = BuildSyntheticFolderStream(className, "Feature1");
        var element = Assert.Single(MfcDocument.Parse(raw).AllElements, e => e.ClassName == className);

        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);

        var parse = className == FlangeFeatureParsers.EdgeFlangeClassName
            ? FlangeFeatureParsers.ParseEdgeFlange(element)
            : FlangeFeatureParsers.ParseMiterFlange(element);
        Assert.Null(parse);
    }

    [Fact]
    public void SkeletonParserRejectsTheWrongClass()
    {
        var edgeRaw = BuildSyntheticFolderStream("moEdgeFlange_c", "EdgeFlange1");
        var edgeElement = Assert.Single(MfcDocument.Parse(edgeRaw).AllElements);
        Assert.Null(FlangeFeatureParsers.ParseMiterFlange(edgeElement));

        var miterRaw = BuildSyntheticFolderStream("moMiterFlange_c", "MiterFlange1");
        var miterElement = Assert.Single(MfcDocument.Parse(miterRaw).AllElements);
        Assert.Null(FlangeFeatureParsers.ParseEdgeFlange(miterElement));
    }

    [Fact]
    public void MappingDoesNotModifyTheSourceStream()
    {
        var raw = BuildSyntheticFolderStream("moEdgeFlange_c", "EdgeFlange1");
        var before = (byte[])raw.Clone();
        var doc = MfcDocument.Parse(raw);
        ResolvedFeaturesModel.Map(doc, new ResolvedFeaturesModel());
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
        ResolvedFeaturesModel.Map(doc, new ResolvedFeaturesModel());
        Assert.Equal(raw, doc.SourceData);
    }

    [Fact]
    public void EdgeFlangeModelExposesTheCompleteApiPropertySet()
    {
        var modelProperties = typeof(EdgeFlangeFeatureModel)
            .GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var modelOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            "ClassName", "TypeId", "ParameterResolution",
            "FeatureName", "Version", "RawBlock", "ResolvedProperties", "SerializedClassName", "SerializedTypeId",
            // Method-derived API data (GetCustomBendAllowance / GetEdgeCount / GetPositionReferenceType)
            "CustomBendAllowance", "EdgeCount", "PositionReferenceType"
        };
        var apiProperties = modelProperties.Where(p => !modelOnly.Contains(p)).OrderBy(p => p).ToArray();
        Assert.Equal(EdgeFlangeApiProperties.OrderBy(p => p).ToArray(), apiProperties);
    }

    [Fact]
    public void MiterFlangeModelExposesTheCompleteApiPropertySet()
    {
        var modelProperties = typeof(MiterFlangeFeatureModel)
            .GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var modelOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            "ClassName", "TypeId", "ParameterResolution",
            "FeatureName", "Version", "RawBlock", "ResolvedProperties", "SerializedClassName", "SerializedTypeId",
            "CustomBendAllowance", "EdgeCount"
        };
        var apiProperties = modelProperties.Where(p => !modelOnly.Contains(p)).OrderBy(p => p).ToArray();
        Assert.Equal(MiterFlangeApiProperties.OrderBy(p => p).ToArray(), apiProperties);
    }

    [Fact]
    public void UnverifiedValuesHaveNoDefaultsAndEveryResolutionFlagIsFalse()
    {
        AssertNoValues(new EdgeFlangeFeatureModel());
        AssertNoValues(new MiterFlangeFeatureModel());

        foreach (var property in typeof(EdgeFlangeParameterResolution).GetProperties())
            Assert.False((bool)property.GetValue(new EdgeFlangeParameterResolution())!);
        foreach (var property in typeof(MiterFlangeParameterResolution).GetProperties())
            Assert.False((bool)property.GetValue(new MiterFlangeParameterResolution())!);
    }

    private static void AssertNoValues(object model)
    {
        foreach (var property in model.GetType().GetProperties())
        {
            if (property.Name is "ClassName" or "TypeId" or "ParameterResolution"
                or "FeatureName" or "Version" or "RawBlock" or "ResolvedProperties"
                or "SerializedClassName" or "SerializedTypeId") continue;
            Assert.Null(property.GetValue(model));
        }
    }

    [Fact]
    public void ObservedEdgeFlangeTypeIdIsEncodedAndMiterRemainsCandidate()
    {
        // samples4/edge_flange_90_len10 (version 0x74) serialized
        // moEdgeFlange_c with FolderHeader TypeId 0x67, not 0x63.
        Assert.Equal(0x67u, EdgeFlangeFeatureModel.ObservedSerializedTypeId);
        Assert.Equal(0x67u, new EdgeFlangeFeatureModel().TypeId);
        Assert.Equal("moEdgeFlange_c", EdgeFlangeFeatureModel.SerializedClassName);

        // No miter-flange sample has been processed yet: keep the candidate class
        // name but do not encode a TypeId.
        Assert.Equal(0u, new MiterFlangeFeatureModel().TypeId);
        Assert.Equal("moMiterFlange_c", MiterFlangeFeatureModel.SerializedClassName);
    }

    [Fact]
    public void ModelsArePurePocoWithoutSolidWorksInteropTypes()
    {
        var assembly = typeof(EdgeFlangeFeatureModel).Assembly;
        Assert.Equal("SldFile", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
            a.Name!.Contains("SolidWorks", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Interop", StringComparison.OrdinalIgnoreCase));

        foreach (var type in new[]
        {
            typeof(EdgeFlangeFeatureModel),
            typeof(MiterFlangeFeatureModel),
            typeof(EdgeFlangeParameterResolution),
            typeof(MiterFlangeParameterResolution),
            typeof(FlangeCustomBendAllowanceData)
        })
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
