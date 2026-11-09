using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

using SldFile.Mfc;
namespace SldFileTests;

/// <summary>
/// Phase 4 / 5 model-shape tests. No samples exist, so the definitions must expose
/// exactly the official interface property set, all values unset, and all resolution
/// flags false. Parser skeletons must reject every unvalidated layout.
/// </summary>
public class SurfaceWeldmentFeatureModelTests
{
    public static IEnumerable<object[]> SurfaceDefinitions()
    {
        yield return new object[]
        {
            new SurfaceExtrudeFeatureModel(),
            "Depth,EndCondition,BothDirections,ReverseDirection,D1CapEnd,D2CapEnd,D1DraftOn,D2DraftOn,D1DraftAngle,D2DraftAngle,D1DraftOutward,D2DraftOutward,DeleteOriginalFace,KnitResult,SurfaceBodyFolder"
        };
        yield return new object[]
        {
            new SurfaceRevolveFeatureModel(),
            "Angle,ReverseDirection,Type,SurfaceBodyFolder"
        };
        yield return new object[]
        {
            new FillSurfaceFeatureModel(),
            "Merge,OptimizeSurface,ResolutionControl,ReverseDirection,ReverseSurface,TryToFormSolid,PatchBoundary,BoundaryCurvatureControls"
        };
        yield return new object[]
        {
            new SurfaceKnitFeatureModel(),
            "Entities,KnitTolerance,MaxValueForGapRange,MinValueForGapRange,SeedFace,UseGapFilters,UseMergeEntities,UseTryToFormSolid,StitchReferences"
        };
        yield return new object[]
        {
            new SurfaceTrimFeatureModel(),
            "PiecesToKeep,TrimTools,TrimType"
        };
        yield return new object[]
        {
            new SurfaceExtendFeatureModel(),
            "Distance,EndCondition,Face,Items,PropagatingEdges,Type,Vertex"
        };
        yield return new object[]
        {
            new SurfaceOffsetFeatureModel(),
            "Distance,Entities,Faces,Flip,BodyReferences"
        };
    }

    public static IEnumerable<object[]> WeldmentDefinitions()
    {
        yield return new object[]
        {
            new StructuralMemberFeatureModel(),
            "AllowProtrusion,ApplyCornerTreatment,ConfigurationName,ConnectedSegmentsOption,ConnectionTypes,CornerTreatmentType,Groups,LibraryProfileMaterial,LocateProfilePoint,PathSegments,RotationAngle,TransferMaterial,WeldmentProfilePath"
        };
        yield return new object[]
        {
            new EndCapFeatureModel(),
            "ChamferDistance,DepthDistance,Face,IsEndCapInward,OffsetDistance,Thickness,ThicknessRatioForOffset,UseChamferCorners,UseCornerTreatment,UseReverse,UseThicknessRatioForOffset"
        };
        yield return new object[]
        {
            new GussetFeatureModel(),
            "FlipOffsetDirection,FlipProfileDistanceParameters,OffsetUsed,ProfileAngle,ProfileDistance1,ProfileDistance2,ProfileDistance3,ProfileDistance4,ProfileLocation,ProfileOffsetDistance,ProfileType,Thickness,ThicknessType,UseAngle,SupportingFace1,SupportingFace2"
        };
        yield return new object[]
        {
            new WeldBeadFeatureModel(),
            "BeadLengths,BeadPitches,BeadSizes,BeadTypes,TangentPropagation,UseOtherSide,BeadSize,EdgeReferences,Side"
        };
    }

    [Theory]
    [MemberData(nameof(SurfaceDefinitions))]
    public void SurfaceDefinition_ExposesExactApiShape_WithNoValues(IFeatureData definition, string expectedProperties)
    {
        AssertModelShape(definition, expectedProperties);
    }

    [Theory]
    [MemberData(nameof(WeldmentDefinitions))]
    public void WeldmentDefinition_ExposesExactApiShape_WithNoValues(IFeatureData definition, string expectedProperties)
    {
        AssertModelShape(definition, expectedProperties);
    }

    [Fact]
    public void SurfaceParserSkeletons_RejectEveryElement()
    {
        var element = ParseElement("moUnknownSurfaceFeature_c");

        Assert.Null(SurfaceFeatureParsers.ParseSurfaceExtrude(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceRevolve(element));
        Assert.Null(SurfaceFeatureParsers.ParseFillSurface(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceKnit(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceTrim(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceExtend(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceOffset(element));
        Assert.Null(SurfaceFeatureParsers.ParseSurfaceExtrude(null));
    }

    [Theory]
    [InlineData("moStructuralMember_c")]
    [InlineData("moEndCap_c")]
    [InlineData("moGusset_c")]
    [InlineData("moWeldBead_c")]
    public void WeldmentParserSkeletons_RejectCatalogCandidateClasses(string className)
    {
        var element = ParseElement(className);

        Assert.Null(StructuralMemberFeatureModel.Parse(element));
        Assert.Null(EndCapFeatureModel.Parse(element));
        Assert.Null(WeldBeadFeatureModel.Parse(element));
        Assert.Null(EndCapFeatureModel.Parse(null));
    }

    [Theory]
    [InlineData("moSurfaceExtrude_c")]
    [InlineData("moStructuralMember_c")]
    [InlineData("moWeldBead_c")]
    public void Registry_HasNoParser_AndMapperLeavesStreamUnchanged(string className)
    {
        var element = ParseElement(className);
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);

        byte[] stream = BuildResolvedFeaturesStream(className, "Feature1");
        byte[] before = (byte[])stream.Clone();
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        Assert.Empty(model.Folders);
        Assert.Equal(before, stream);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void AssertModelShape(IFeatureData definition, string expectedProperties)
    {
        var expected = expectedProperties.Split(',').ToHashSet(StringComparer.Ordinal);

        var actual = definition.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name != nameof(IFeatureData.ClassName) &&
                           name != nameof(IFeatureData.TypeId) &&
                           name != "Resolution" &&
                           name != "SerializedClassName" &&
                           name != "SerializedTypeId" &&
                           name != "FeatureName" &&
                           name != "Version" &&
                           name != "RawBlock" &&
                           name != "ResolvedProperties" &&
                           name != "MirrorProfile" &&
                           name != "BeadLength" &&
                           name != "BeadPitch" &&
                           name != "BeadType" &&
                           !name.EndsWith("Count", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected, actual);

        foreach (string name in expected)
        {
            var property = definition.GetType().GetProperty(name)!;
            Assert.Null(property.GetValue(definition));
        }

        Assert.Equal(definition.ClassName, (definition as IMfcFeatureModel)?.ClassName ?? string.Empty);
        Assert.Equal(0u, definition.TypeId);

        object resolution = definition.GetType().GetProperty("Resolution")!.GetValue(definition)!;
        var flags = resolution.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool))
            .ToList();

        Assert.True(flags.Count >= expected.Count + 2); // property flags + ClassName/SerializedTypeId plumbing
        Assert.All(flags, p => Assert.False((bool)p.GetValue(resolution)!, $"{definition.GetType().Name}.{p.Name}"));
    }

    private static MfcElement ParseElement(string className)
    {
        var document = MfcDocument.Parse(BuildTaggedStream(className, "Feature1"));
        return Assert.Single(document.AllElements);
    }

    private static byte[] BuildTaggedStream(string className, string displayName)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) };
        bytes.AddRange(ascii);
        bytes.AddRange(WChar(displayName));
        bytes.AddRange(new byte[48]);
        return bytes.ToArray();
    }

    private static byte[] BuildResolvedFeaturesStream(string className, string displayName)
    {
        var bytes = new List<byte> { 0x0F, 0x00 };
        bytes.AddRange(BuildTaggedStream(className, displayName));
        return bytes.ToArray();
    }

    private static byte[] WChar(string value)
    {
        var bytes = new List<byte> { 0xFF, 0xFE, 0xFF, (byte)value.Length };
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        return bytes.ToArray();
    }
}
