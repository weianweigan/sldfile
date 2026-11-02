using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Round-trip and structural coverage for the evidenced extrusion option authoring slices:
/// both directions (D2), direction-1 draft (D3) and start offset (D19).
/// </summary>
public class ExtrudeOptionAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static TempTestFile CreateStrippedSketchPart()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        var temp = TestFileProvider.CreateTempCopy(extPath);

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = sw.ResolvedFeatures.RawData!;
            byte[] sketchOnly = new byte[0x22E0];
            Array.Copy(raw, 0, sketchOnly, 0, 0x22E0);

            ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
            sketchOnly[0x22D4] = 0x00;

            sw.SetPendingStream(ResolvedStream, sketchOnly);
            sw.Save();
        }

        return temp;
    }

    private static ExtrudeFeatureDefinitionData AuthorAndReopen(ExtrudeFeatureDefinitionData definition, out byte[] stream)
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(definition);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        return Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
    }

    private static MfcElement SingleFeatureElement(MfcDocument doc) =>
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));

    private static MfcElement ElementInFeature(MfcDocument doc, MfcElement feature, string className) =>
        Assert.Single(doc.AllElements.Where(e => e.ClassName == className &&
                                                e.TagOffset >= feature.TagOffset &&
                                                e.TagOffset < doc.SourceData.Length));

    [Fact]
    public void TwoDirectionAuthoring_RoundtripsBothDepths()
    {
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.012,
            BothDirections = true,
            DepthDirection2 = 0.004
        }, out _);

        Assert.True(read.BothDirections);
        Assert.Equal(0.012, read.GetDepth(true), 12);
        Assert.Equal(0.004, read.GetDepth(false), 12);
        Assert.Equal(0u, read.GetEndCondition(true));
        Assert.Equal(0u, read.GetEndCondition(false));
    }

    [Fact]
    public void TwoDirectionAuthoring_EmitsIndexedSecondEndSpecAndD2Parameter()
    {
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            BothDirections = true,
            DepthDirection2 = 0.003
        }, out byte[] stream);
        Assert.True(read.BothDirections);

        var doc = MfcDocument.Parse(stream);
        var feature = SingleFeatureElement(doc);
        var endSpec = ElementInFeature(doc, feature, "moEndSpec_c");
        Assert.Equal(0u, BitConverter.ToUInt32(stream, endSpec.DataStart + 2));

        var favorite = ElementInFeature(doc, feature, "moFavoriteHandle_c");
        Assert.Equal(0x810B, BitConverter.ToUInt16(stream, favorite.DataStart + 436));
        for (int i = favorite.DataStart + 438; i < favorite.DataStart + 454; i++)
            Assert.Equal(0, stream[i]);

        int parameter = favorite.DataStart + 1111;
        Assert.Equal(0x810F, BitConverter.ToUInt16(stream, parameter - 8));
        Assert.Equal(2u, BitConverter.ToUInt32(stream, parameter - 6));
        Assert.Equal(0x8111, BitConverter.ToUInt16(stream, parameter - 2));
        var rawParameter = stream.Skip(parameter).ToArray();
        Assert.Equal("D2", MfcPrimitives.PeekWChar(rawParameter, 2));
        Assert.Equal(0.003, ExtrudeAdditionalOptionParser.ReadNamedParameter(rawParameter, 0)!.Value, 12);
    }

    [Fact]
    public void DraftAuthoring_RoundtripsEnableFlagAndAngle()
    {
        double angle = 7.0 * Math.PI / 180.0;
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            DraftWhileExtruding = true,
            DraftAngle = angle
        }, out _);

        Assert.True(read.DraftWhileExtruding);
        Assert.Equal(angle, read.DraftAngle, 12);
        Assert.False(read.DraftOutward);
        Assert.Equal(0.010, read.Depth, 12);
    }

    [Fact]
    public void DraftAuthoring_EmitsOwnedEnableFlagAndNamedAngleParameter()
    {
        double angle = 5.5 * Math.PI / 180.0;
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            DraftWhileExtruding = true,
            DraftAngle = angle
        }, out byte[] stream);
        Assert.True(read.DraftWhileExtruding);

        var doc = MfcDocument.Parse(stream);
        var feature = SingleFeatureElement(doc);
        var display = ElementInFeature(doc, feature, "moDisplayAngularDim_c");
        Assert.Equal(1u, BitConverter.ToUInt32(stream, display.TagOffset - 24));
        for (int i = display.TagOffset - 20; i < display.TagOffset; i++)
            Assert.Equal(0, stream[i]);

        var angleParameter = ElementInFeature(doc, feature, "moAngleParameter_c");
        var raw = stream.Skip(angleParameter.DataStart).ToArray();
        Assert.Equal("D3", MfcPrimitives.PeekWChar(raw, 2));
        Assert.Equal(angle, ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, 0)!.Value, 12);
    }

    [Fact]
    public void StartOffsetAuthoring_RoundtripsTypeAndDistance()
    {
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            FromType = 3,
            FromOffsetDistance = 0.006
        }, out _);

        Assert.Equal(3u, read.FromType);
        Assert.Equal(0.006, read.FromOffsetDistance, 12);
        Assert.False(read.FromOffsetReverse);
        Assert.Equal(0.010, read.Depth, 12);
    }

    [Fact]
    public void StartOffsetAuthoring_EmitsDiscriminatorAndSingleD19Parameter()
    {
        var read = AuthorAndReopen(new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            FromType = 3,
            FromOffsetDistance = 0.004
        }, out byte[] stream);
        Assert.Equal(3u, read.FromType);

        var doc = MfcDocument.Parse(stream);
        var feature = SingleFeatureElement(doc);
        var fromEnd = ElementInFeature(doc, feature, "moFromEndSpec_c");
        var raw = stream.Skip(fromEnd.DataStart).ToArray();
        Assert.Equal(5u, BitConverter.ToUInt32(raw, 0));
        Assert.Equal(0x810B, BitConverter.ToUInt16(raw, 4));
        for (int i = 6; i < 22; i++)
            Assert.Equal(0, raw[i]);
        Assert.Equal(0.004, ExtrudeAdditionalOptionParser.ReadStartOffset(raw)!.Value, 12);
    }

    public static System.Collections.Generic.IEnumerable<object[]> UnsupportedOptionCombinations()
    {
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, BothDirections = true, DepthDirection2 = .02, ReverseDirection = true }, "ReverseDirection" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, BothDirections = true, DepthDirection2 = .02, EndCondition = 6 }, "EndCondition" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, BothDirections = true, DepthDirection2 = .02, DraftWhileExtruding = true, DraftAngle = .1 }, "DraftWhileExtruding" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtruding = true, DraftAngle = .1, IsCut = true }, "IsCut" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtruding = true, DraftAngle = .1, ReverseDirection = true }, "ReverseDirection" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtruding = true, DraftAngle = .1, EndCondition = 6 }, "EndCondition" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtruding = true, DraftAngle = .1, DraftOutward = true }, "DraftOutward" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtruding = true, DraftAngle = .1, DraftAngleDirection2 = .1 }, "DraftAngleDirection2" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, FromType = 3, FromOffsetDistance = .001, IsCut = true }, "IsCut" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, FromType = 3, FromOffsetDistance = .001, ReverseDirection = true }, "ReverseDirection" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, FromType = 3, FromOffsetDistance = .001, FromOffsetReverse = true }, "FromOffsetReverse" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, FromType = 3, FromOffsetDistance = .001, DraftWhileExtruding = true, DraftAngle = .1 }, "FromType" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, FromType = 1, FromOffsetDistance = .001 }, "FromType" };
        // Thin authoring now supports the measured OneDirection/CapEnds templates; only the
        // unparameterized variants remain rejected.
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, IsThinFeature = true, ThinWallType = 1, WallThickness = .001 }, "ThinWallType" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, IsThinFeature = true, ThinWallType = 3, WallThickness = .001, WallThicknessDirection2 = .002 }, "BothDirections" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, CapEnds = true }, "CapEnds" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, Merge = false }, "Merge" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtrudingDirection2 = true, DraftAngleDirection2 = .1 }, "DraftWhileExtrudingDirection2" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, BothDirections = true, DepthDirection2 = .005, DraftAngleDirection2 = .1 }, "DraftAngleDirection2" };
        yield return new object[] { new ExtrudeFeatureDefinitionData { Depth = .01, DraftWhileExtrudingDirection2 = true, DraftAngleDirection2 = .1, DraftOutwardDirection2 = true }, "DraftWhileExtrudingDirection2" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedOptionCombinations))]
    public void UnsupportedOptionCombinationsAreRejectedWithoutModifyingTheStream(ExtrudeFeatureDefinitionData definition, string property)
    {
        using var temp = CreateStrippedSketchPart();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(definition));
        Assert.Contains(property, error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void OptionAuthoringRejectsUnmeasuredStreamVersions()
    {
        using var temp = CreateStrippedSketchPart();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var stream = (byte[])part.RawStreams[ResolvedStream].Clone();
        var folder = new SldFile.Binary.FeatureFolder
        {
            ClassName = "moProfileFeature_c",
            DisplayName = "Sketch1",
            TagOffset = 6055
        };
        const string FeatureName = "凸台-拉伸1";

        var twoDirections = new ExtrudeFeatureDefinitionData
        {
            Depth = .01,
            BothDirections = true,
            DepthDirection2 = .005
        };
        var draft = new ExtrudeFeatureDefinitionData
        {
            Depth = .01,
            DraftWhileExtruding = true,
            DraftAngle = .1
        };
        var offset = new ExtrudeFeatureDefinitionData
        {
            Depth = .01,
            FromType = 3,
            FromOffsetDistance = .002
        };

        // 0x6E sketch-only parts cannot carry the synthesized feature layout at all.
        var unsupported = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x6E).CopyTo(unsupported, 0);
        var versionError = Assert.Throws<NotSupportedException>(
            () => ExtrudeFeatureBuilder.BuildExtrusionStream(unsupported, new ExtrudeFeatureDefinitionData { Depth = .01 }, folder, unsupported.Length, FeatureName));
        Assert.Contains("0x6E", versionError.Message);

        // 0x71 archives allow the basic payload (used by the native cut tests) but not options.
        var compatible = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x71).CopyTo(compatible, 0);
        var optionError = Assert.Throws<NotSupportedException>(
            () => ExtrudeFeatureBuilder.BuildExtrusionStream(compatible, twoDirections, folder, compatible.Length, FeatureName));
        Assert.Contains("0x70", optionError.Message);
        Assert.Throws<NotSupportedException>(
            () => ExtrudeFeatureBuilder.BuildExtrusionStream(compatible, draft, folder, compatible.Length, FeatureName));
        Assert.Throws<NotSupportedException>(
            () => ExtrudeFeatureBuilder.BuildExtrusionStream(compatible, offset, folder, compatible.Length, FeatureName));

        // 0x71 basic authoring reaches the chain check and succeeds when the stream owns a chain.
        Assert.NotNull(ExtrudeFeatureBuilder.BuildExtrusionStream(compatible,
            new ExtrudeFeatureDefinitionData { Depth = .01 }, folder, compatible.Length, FeatureName));

        // A folder range that does not contain the chain is rejected with a version-bearing message.
        var noChainFolder = new SldFile.Binary.FeatureFolder
        {
            ClassName = "moProfileFeature_c",
            DisplayName = "Sketch1",
            TagOffset = compatible.Length - 4
        };
        var chainError = Assert.Throws<NotSupportedException>(
            () => ExtrudeFeatureBuilder.BuildExtrusionStream(compatible,
                new ExtrudeFeatureDefinitionData { Depth = .01 }, noChainFolder, compatible.Length, FeatureName));
        Assert.Contains("moSketchChain_c", chainError.Message);
        Assert.Contains("0x71", chainError.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(Math.PI / 2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DraftAuthoringRejectsInvalidAngles(double angle)
    {
        var definition = new ExtrudeFeatureDefinitionData
        {
            Depth = .01,
            DraftWhileExtruding = true,
            DraftAngle = angle
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtrudeFeatureBuilder.ValidateDefinition(definition));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void StartOffsetAuthoringRejectsInvalidDistances(double distance)
    {
        var definition = new ExtrudeFeatureDefinitionData
        {
            Depth = .01,
            FromType = 3,
            FromOffsetDistance = distance
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtrudeFeatureBuilder.ValidateDefinition(definition));
    }
}
