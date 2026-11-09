using System;
using System.Collections.Generic;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class ExtrudeAuthoringGuardTests
{
    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        yield return new object[] { new ExtrudeFeatureModel
        {
            Depth = .01,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[]
            {
                new SketchContourReference { EntityIndices = new uint[] { 1, 4, 3, 2 } },
                new SketchContourReference { EntityIndices = new uint[] { 5, 8, 7, 6 } },
                new SketchContourReference { EntityIndices = new uint[] { 9, 12, 11, 10 } }
            }
        }, "one or two" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, DraftAngle = .1 }, "DraftAngle" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, BothDirections = true, DepthDirection2 = .02, ReverseDirection = true }, "ReverseDirection" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, Merge = false }, "Merge" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, IsThinFeature = true, ThinWallType = 1, WallThickness = .001 }, "ThinWallType" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, FromOffsetDistance = .001 }, "FromOffsetDistance" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, EndCondition = 5 }, "EndCondition" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, DirectionReference = new DirectionReference() }, "DirectionReference" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, EndConditionEntity = new EndConditionEntityReference() }, "EndConditionEntity" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, SelectedContours = new[] { new SketchContourReference() } }, "SelectedContours" };
        yield return new object[] { new ExtrudeFeatureModel { Depth = .01, BodyScope = new BodyScopeData { BodyIds = new uint[] { 42 } } }, "BodyScope" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedAuthoringDoesNotModifyPendingStream(ExtrudeFeatureModel definition, string property)
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.Resolve("Features2026", "extrude_10mm.SLDPRT"));
        using var file = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(file);
        var original = (byte[])part.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(definition));
        Assert.Contains(property, error.Message);
        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams["Contents/Config-0-ResolvedFeatures"]);
    }

    [Fact]
    public void DefinitionSketchNameIsResolvedInsteadOfSilentlyIgnored()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.Resolve("Features2026", "extrude_10mm.SLDPRT"));
        using var file = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(file);
        var error = Assert.Throws<ArgumentException>(() => part.AddExtrude(new ExtrudeFeatureModel
        {
            Depth = .01,
            SketchName = "MissingSketchFromDefinition"
        }));
        Assert.Contains("MissingSketchFromDefinition", error.Message);
    }

    [Theory]
    [InlineData(false, 0u)]
    [InlineData(false, 6u)]
    [InlineData(true, 0u)]
    public void ExistingBasicAuthoringModesRemainAvailable(bool isCut, uint endCondition)
    {
        ExtrudeFeatureBuilder.ValidateDefinition(new ExtrudeFeatureModel
        {
            Depth = .01, IsCut = isCut, EndCondition = endCondition
        });
    }
}
