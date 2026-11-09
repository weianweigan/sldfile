using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring coverage for the first controlled edge-flange combination:
/// the measured single-edge specimen (FlangeOptions/edge_flange_90_len10,
/// ResolvedFeatures version 0x74), a stripped base prefix, and BendAngle /
/// BendRadius slot replacement in the measured subsystem.
/// </summary>
public class EdgeFlangeAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "FlangeOptions", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", "FlangeOptions", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"FlangeOptions/{fileName} not found");
    }

    private static TempTestFile CreateStrippedBase(
        ushort? versionOverride = null, string sampleFile = "edge_flange_90_len10.SLDPRT")
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleFile));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[EdgeFlangeAuthoringSegments.MeasuredPrefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        if (versionOverride.HasValue)
            BitConverter.GetBytes(versionOverride.Value).CopyTo(stripped, 0);

        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static EdgeFlangeFeatureModel ValidDefinition(double angle = Math.PI / 2.0, double radius = 0.002)
        => new() { BendAngle = angle, BendRadius = radius, EdgeCount = 1 };

    private static EdgeFlangeFeatureModel AuthorAndReopen(
        EdgeFlangeFeatureModel definition, out string featureName)
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddEdgeFlange(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moEdgeFlange_c"));
        return Assert.IsType<EdgeFlangeFeatureModel>(folder.FeatureData);
    }

    [Fact]
    public void StrippedBaselineOfflineValidation()
    {
        using var temp = CreateStrippedBase();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var raw = reopened.RawStreams[ResolvedStream];

        Assert.Equal((ushort)0x74, BitConverter.ToUInt16(raw, 0));
        Assert.Equal(EdgeFlangeAuthoringSegments.MeasuredPrefixLength, raw.Length);
        Assert.Empty(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moEdgeFlange_c"));
        Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moTemplateSheetMetal_c"));
        Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moProfileFeature_c"));

        using var sha = SHA256.Create();
        string hash = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "");
        Assert.Equal(EdgeFlangeAuthoringSegments.MeasuredPrefixSha256, hash);

        Assert.Equal((ushort)24, BitConverter.ToUInt16(raw, 4));
        Assert.Equal(22u, BitConverter.ToUInt32(raw,
            EdgeFlangeAuthoringSegments.SketchChildCountMarkerOffset + 4));
        var chain = Assert.Single(MfcDocument.Parse(raw).AllElements.Where(e => e.ClassName == "moSketchChain_c"));
        Assert.True(chain.DataEnd > chain.DataStart);
    }

    [Theory]
    [InlineData(Math.PI / 2.0, 0.002)]
    [InlineData(2.0 * Math.PI / 3.0, 0.002)]
    [InlineData(Math.PI / 2.0, 0.003)]
    [InlineData(1.0, 0.0025)]
    public void AuthoringRoundtripsBendAngleAndRadius(double angle, double radius)
    {
        var parsed = AuthorAndReopen(ValidDefinition(angle, radius), out string featureName);

        Assert.Equal("EdgeFlange", featureName);
        Assert.Equal(angle, parsed.BendAngle!.Value, 12);
        Assert.Equal(radius, parsed.BendRadius!.Value, 12);
        Assert.Equal(1, parsed.EdgeCount ?? -1);
        Assert.True(parsed.ParameterResolution.BendAngle);
        Assert.True(parsed.ParameterResolution.BendRadius);
        Assert.True(parsed.ParameterResolution.EdgeCount);
        Assert.Null(parsed.Edge);
        Assert.Null(parsed.Edges);
        Assert.Equal(0.010, parsed.OffsetDistance!.Value, 12);
        Assert.Equal(1, parsed.PositionType ?? -1);
        Assert.Equal(0.001, parsed.ReliefWidth!.Value, 12);
        Assert.Equal(0.001, parsed.ReliefDepth!.Value, 12);
        Assert.True(parsed.ParameterResolution.OffsetDistance);
        Assert.True(parsed.ParameterResolution.PositionType);
        Assert.True(parsed.ParameterResolution.ReliefWidth);
        Assert.True(parsed.ParameterResolution.ReliefDepth);
    }

    public static IEnumerable<object[]> VariantAuthoringDefinitions()
    {
        yield return new object[]
        {
            With(d => d.OffsetDistance = 0.02),
            Math.PI / 2.0, 0.002, 0.02, 1, 0.001, 0.001
        };
        yield return new object[]
        {
            With(d => d.PositionType = 2),
            Math.PI / 2.0, 0.002, 0.01, 2, 0.001, 0.001
        };
        yield return new object[]
        {
            With(d => d.PositionType = 3),
            Math.PI / 2.0, 0.002, 0.01, 3, 0.001, 0.001
        };
        yield return new object[]
        {
            With(d => { d.ReliefWidth = 0.0015; d.ReliefDepth = 0.0025; }),
            Math.PI / 2.0, 0.002, 0.01, 1, 0.0015, 0.0025
        };
        yield return new object[]
        {
            With(d =>
            {
                d.BendAngle = 2.0 * Math.PI / 3.0;
                d.BendRadius = 0.0025;
                d.OffsetDistance = 0.02;
                d.PositionType = 3;
                d.ReliefWidth = 0.0015;
                d.ReliefDepth = 0.0025;
            }),
            2.0 * Math.PI / 3.0, 0.0025, 0.02, 3, 0.0015, 0.0025
        };

        static EdgeFlangeFeatureModel With(Action<EdgeFlangeFeatureModel> configure)
        {
            var definition = ValidDefinition();
            configure(definition);
            return definition;
        }
    }

    [Theory]
    [MemberData(nameof(VariantAuthoringDefinitions))]
    public void AuthoringRoundtripsVerifiedVariantFields(
        EdgeFlangeFeatureModel definition, double angle, double radius,
        double offsetDistance, int positionType, double reliefWidth, double reliefDepth)
    {
        var parsed = AuthorAndReopen(definition, out string featureName);

        Assert.Equal("EdgeFlange", featureName);
        Assert.Equal(angle, parsed.BendAngle!.Value, 12);
        Assert.Equal(radius, parsed.BendRadius!.Value, 12);
        Assert.Equal(1, parsed.EdgeCount ?? -1);
        Assert.Equal(offsetDistance, parsed.OffsetDistance!.Value, 12);
        Assert.Equal(positionType, parsed.PositionType ?? -1);
        Assert.Equal(reliefWidth, parsed.ReliefWidth!.Value, 12);
        Assert.Equal(reliefDepth, parsed.ReliefDepth!.Value, 12);

        Assert.True(parsed.ParameterResolution.OffsetDistance);
        Assert.True(parsed.ParameterResolution.PositionType);
        Assert.True(parsed.ParameterResolution.ReliefWidth);
        Assert.True(parsed.ParameterResolution.ReliefDepth);

        // Unvaried defaults stay unknown even after authoring.
        Assert.False(parsed.UsePositionOffset ?? true);
        Assert.Equal(0.010, parsed.PositionOffsetDistance!.Value, 12);
        Assert.True(parsed.ParameterResolution.UsePositionOffset);
        Assert.True(parsed.ParameterResolution.PositionOffsetDistance);
        Assert.Null(parsed.PositionOffsetType);
        Assert.Null(parsed.ReliefRatio);
        Assert.Null(parsed.UseReliefRatio);
        Assert.Null(parsed.AutoReliefType);
        Assert.Null(parsed.UseDefaultBendRelief);
    }

    [Fact]
    public void AuthoredVariantSlotsMatchDefinition()
    {
        var definition = ValidDefinition(2.0 * Math.PI / 3.0, 0.0025);
        definition.OffsetDistance = 0.02;
        definition.PositionType = 3;
        definition.ReliefWidth = 0.0015;
        definition.ReliefDepth = 0.0025;

        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddEdgeFlange(definition);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var raw = reopened.RawStreams[ResolvedStream];
        foreach (int slot in EdgeFlangeAuthoringSegments.PrefixOffsetDistanceSlots)
            Assert.Equal(0.02, BitConverter.ToDouble(raw, slot), 12);
        int suffixStart = raw.Length - EdgeFlangeAuthoringSegments.GetSuffix().Length;
        foreach (int slot in EdgeFlangeAuthoringSegments.OffsetDistanceSlots)
            Assert.Equal(0.02, BitConverter.ToDouble(raw, suffixStart + slot), 12);
        Assert.Equal(3u, BitConverter.ToUInt32(raw,
            suffixStart + EdgeFlangeAuthoringSegments.PositionTypeSlot));
        Assert.Equal(0.0015, BitConverter.ToDouble(raw,
            suffixStart + EdgeFlangeAuthoringSegments.ReliefWidthSlot), 12);
        Assert.Equal(0.0025, BitConverter.ToDouble(raw,
            suffixStart + EdgeFlangeAuthoringSegments.ReliefDepthSlot), 12);
        foreach (int slot in EdgeFlangeAuthoringSegments.ReliefDepthDisplaySlots)
            Assert.Equal(0.0025, BitConverter.ToDouble(raw, suffixStart + slot), 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidOffsetDistanceIsRejected(double offsetDistance)
    {
        var definition = ValidDefinition();
        definition.OffsetDistance = offsetDistance;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(definition));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void InvalidPositionTypeIsRejected(int positionType)
    {
        var definition = ValidDefinition();
        definition.PositionType = positionType;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(definition));
    }

    [Fact]
    public void OneSidedOrNegativeReliefIsRejected()
    {
        var widthOnly = ValidDefinition();
        widthOnly.ReliefWidth = 0.002;
        Assert.Throws<NotSupportedException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(widthOnly));

        var depthOnly = ValidDefinition();
        depthOnly.ReliefDepth = 0.002;
        Assert.Throws<NotSupportedException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(depthOnly));

        var negative = ValidDefinition();
        negative.ReliefWidth = -0.001;
        negative.ReliefDepth = 0.002;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(negative));
    }

    [Theory]
    [InlineData("edge_flange_position_offset.SLDPRT", 0.005)]
    [InlineData("edge_flange_position_offset10.SLDPRT", 0.010)]
    public void AuthoringReconstructsPositionOffsetTemplates(string sampleFile, double distance)
    {
        using var temp = CreateStrippedBase(sampleFile: sampleFile);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = ValidDefinition();
            definition.UsePositionOffset = true;
            definition.PositionOffsetType = 1;
            definition.PositionOffsetDistance = distance;
            definition.PositionType = 1;
            part.AddEdgeFlange(definition);
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(sampleFile), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);

        using var parsedFile = SwFile.Open(temp.FilePath, readOnly: true);
        var parsed = Assert.IsType<EdgeFlangeFeatureModel>(
            parsedFile.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
        Assert.True(parsed.UsePositionOffset ?? false);
        Assert.Equal(distance, parsed.PositionOffsetDistance!.Value, 12);
        Assert.Equal(1, parsed.PositionType ?? -1);
        Assert.Equal(0.010, parsed.OffsetDistance!.Value, 12);
        Assert.True(parsed.ParameterResolution.UsePositionOffset);
        Assert.True(parsed.ParameterResolution.PositionOffsetDistance);
        Assert.Null(parsed.PositionOffsetType);
    }

    [Fact]
    public void PositionOffsetAuthoringRejectsBoundariesAndMismatchedTemplate()
    {
        foreach (double distance in new[] { 0.004, 0.006, 0.0, -0.005 })
        {
            using var temp = CreateStrippedBase(sampleFile: "edge_flange_position_offset.SLDPRT");
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var definition = ValidDefinition();
            definition.UsePositionOffset = true;
            definition.PositionOffsetType = 1;
            definition.PositionOffsetDistance = distance;
            Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(definition));
        }

        // Value must be supplied when UsePositionOffset is true.
        using (var temp = CreateStrippedBase(sampleFile: "edge_flange_position_offset.SLDPRT"))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = ValidDefinition();
            definition.UsePositionOffset = true;
            definition.PositionOffsetType = 1;
            Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(definition));
        }

        // PositionType must stay 1 for the measured position-offset templates.
        using (var temp = CreateStrippedBase(sampleFile: "edge_flange_position_offset.SLDPRT"))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = ValidDefinition();
            definition.UsePositionOffset = true;
            definition.PositionOffsetType = 1;
            definition.PositionOffsetDistance = 0.005;
            definition.PositionType = 2;
            Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(definition));
        }

        // 0.005 definition against the 0.010 stripped base must fail the prefix hash.
        using (var temp = CreateStrippedBase(sampleFile: "edge_flange_position_offset10.SLDPRT"))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = ValidDefinition();
            definition.UsePositionOffset = true;
            definition.PositionOffsetType = 1;
            definition.PositionOffsetDistance = 0.005;
            var error = Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(definition));
            Assert.Contains("SHA-256", error.Message);
        }
    }

    [Fact]
    public void AuthoredStreamContainsOneCompleteMeasuredSubsystem()
    {
        double angle = 2.0 * Math.PI / 3.0, radius = 0.003;
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddEdgeFlange(ValidDefinition(angle, radius));
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var raw = reopened.RawStreams[ResolvedStream];
        var doc = MfcDocument.Parse(raw);
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEdgeFlange_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moOneBend_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moMultiEdgeFlangeDataPerEdge_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moLinearEdgeGeom_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEdgeRef_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompEdge_c"));

        Assert.Equal((ushort)24, BitConverter.ToUInt16(raw, 4));
        Assert.Equal(22u, BitConverter.ToUInt32(raw,
            EdgeFlangeAuthoringSegments.SketchChildCountMarkerOffset + 4));

        int suffixStart = raw.Length - EdgeFlangeAuthoringSegments.GetSuffix().Length;
        foreach (int slot in EdgeFlangeAuthoringSegments.BendAngleSlots)
            Assert.Equal(angle, BitConverter.ToDouble(raw, suffixStart + slot), 12);
        foreach (int slot in EdgeFlangeAuthoringSegments.BendRadiusSlots)
            Assert.Equal(radius, BitConverter.ToDouble(raw, suffixStart + slot), 12);

        // Unproven radius-derived caches stay at the measured value.
        foreach (int slot in EdgeFlangeAuthoringSegments.LazyRadiusCaches)
            Assert.Equal(0.002, BitConverter.ToDouble(raw, suffixStart + slot), 12);
    }

    [Fact]
    public void ResavePreservesAuthoredStreamAndDefinition()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddEdgeFlange(ValidDefinition(1.1, 0.0022));
            part.Save();
        }

        byte[] before;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStream].Clone();
            second.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<EdgeFlangeFeatureModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
        Assert.Equal(1.1, definition.BendAngle!.Value, 12);
        Assert.Equal(0.0022, definition.BendRadius!.Value, 12);
        Assert.Equal(1, definition.EdgeCount ?? -1);
    }

    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        EdgeFlangeFeatureModel Base() => ValidDefinition();

        var offsetType = Base(); offsetType.OffsetType = 2;
        yield return new object[] { offsetType, "OffsetType" };
        var defaultRadius = Base(); defaultRadius.UseDefaultBendRadius = true;
        yield return new object[] { defaultRadius, "UseDefaultBendRadius" };
        var defaultRelief = Base(); defaultRelief.UseDefaultBendRelief = true;
        yield return new object[] { defaultRelief, "UseDefaultBendRelief" };
        var gap = Base(); gap.GapDistance = 0.002;
        yield return new object[] { gap, "GapDistance" };
        var edges = Base(); edges.EdgeCount = 2;
        yield return new object[] { edges, "EdgeCount" };
        var edgeRef = Base(); edgeRef.Edge = new EntityReference { EntityId = 1 };
        yield return new object[] { edgeRef, "Edge" };
        var cba = Base(); cba.CustomBendAllowance = new FlangeCustomBendAllowanceData { Type = 2 };
        yield return new object[] { cba, "CustomBendAllowance" };
        var offsetDim = Base(); offsetDim.OffsetDimType = 1;
        yield return new object[] { offsetDim, "OffsetDimType" };
        var reversePos = Base(); reversePos.ReversePositionOffset = true;
        yield return new object[] { reversePos, "ReversePositionOffset" };
        var positionOffset = Base(); positionOffset.PositionOffsetDistance = 0.02;
        yield return new object[] { positionOffset, "PositionOffsetDistance" };
        var positionOffsetType = Base(); positionOffsetType.PositionOffsetType = 2;
        yield return new object[] { positionOffsetType, "PositionOffsetType" };
        var reliefRatio = Base(); reliefRatio.ReliefRatio = 0.4;
        yield return new object[] { reliefRatio, "ReliefRatio" };
        var useReliefRatio = Base(); useReliefRatio.UseReliefRatio = false;
        yield return new object[] { useReliefRatio, "UseReliefRatio" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedFieldsAreRejectedWithoutModifyingStream(
        EdgeFlangeFeatureModel definition, string property)
    {
        using var temp = CreateStrippedBase();
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(definition));
            Assert.Contains(property, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Theory]
    [InlineData(0.0, 0.002)]
    [InlineData(-1.0, 0.002)]
    [InlineData(Math.PI + 0.01, 0.002)]
    [InlineData(double.NaN, 0.002)]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, -0.001)]
    [InlineData(1.0, double.PositiveInfinity)]
    public void InvalidAngleOrRadiusIsRejected(double angle, double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeFlangeFeatureBuilder.ValidateDefinition(ValidDefinition(angle, radius)));
    }

    [Fact]
    public void NullDefinitionAndReadOnlyDocumentAreRejected()
    {
        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<ArgumentNullException>(() => part.AddEdgeFlange(null!));
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<InvalidOperationException>(() => part.AddEdgeFlange(ValidDefinition()));
        }
    }

    [Fact]
    public void CustomFeatureNameIsRejectedWithoutModifyingStream()
    {
        using var temp = CreateStrippedBase();
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddEdgeFlange(ValidDefinition(), featureName: "EdgeFlange2"));
            Assert.Contains("display name", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Theory]
    [InlineData(0x70)]
    [InlineData(0x73)]
    [InlineData(0x75)]
    public void Non0x74StreamVersionIsRejectedWithoutModifyingStream(ushort version)
    {
        using var temp = CreateStrippedBase(version);
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(ValidDefinition()));
            Assert.Contains("0x74", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ExistingEdgeFlangeOrTamperedPrefixIsRejectedWithoutModifyingStream()
    {
        // Full sample already contains an edge flange and is not the stripped prefix.
        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("edge_flange_90_len10.SLDPRT")))
        {
            byte[] before;
            using (var read = SwFile.Open(temp.FilePath, readOnly: true))
                before = (byte[])read.RawStreams[ResolvedStream].Clone();

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(ValidDefinition()));
                Assert.Contains("stripped", error.Message);
                part.Save();
            }

            using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
            Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        }

        // Tampering the controlled prefix invalidates the SHA-256 signature.
        using (var temp = CreateStrippedBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x236F] ^= 0xFF; // corrupt the 0x67 marker
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            byte[] before;
            using (var read = SwFile.Open(temp.FilePath, readOnly: true))
                before = (byte[])read.RawStreams[ResolvedStream].Clone();

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddEdgeFlange(ValidDefinition()));
                Assert.Contains("SHA-256", error.Message);
                part.Save();
            }

            using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
            Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        }
    }
}
