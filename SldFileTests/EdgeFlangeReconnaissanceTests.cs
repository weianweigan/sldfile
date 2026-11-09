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
/// Read-only reconnaissance for the first native edge-flange sample
/// (samples4/edge_flange_90_len10.SLDPRT, ResolvedFeatures version 0x74).
/// It pins the observed class/TypeId, the stable feature payloads across resave,
/// the associated element sequence, and candidate scalar offsets. No field is
/// marked resolved: the parser still returns null until single-variable samples
/// establish the semantics.
/// </summary>
public class EdgeFlangeReconnaissanceTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string Sample(string fileName)
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

    private static byte[] ReadStream(string fileName)
    {
        using var file = SwFile.Open(Sample(fileName), readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    private static int FindNamedParameter(byte[] raw, string name, int start, int end)
    {
        for (int p = Math.Max(0, start); p <= Math.Min(end, raw.Length) - 38; p++)
        {
            if (raw[p] == 0xFF && raw[p + 1] == 0xFE && raw[p + 2] == 0xFF &&
                raw[p + 3] == 2 && raw[p + 4] == (byte)name[0] && raw[p + 5] == 0 &&
                raw[p + 6] == (byte)name[1] && raw[p + 7] == 0 &&
                SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8))
                return p;
        }
        return -1;
    }

    private static EdgeFlangeFeatureModel? ParseWithWindow(byte[] raw)
    {
        var doc = MfcDocument.Parse(raw);
        var element = doc.AllElements.Single(e => e.ClassName == "moEdgeFlange_c");
        return FlangeFeatureParsers.ParseEdgeFlange(doc, element, 0x450F, 0xA376)
            as EdgeFlangeFeatureModel;
    }

    private static byte[] BuildSyntheticEdgeFlangeStream(uint typeId, int dataLength = 90)
    {        var bytes = new List<byte> { 0x74, 0x00 };
        const string className = "moEdgeFlange_c";
        var classBytes = Encoding.ASCII.GetBytes(className);
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00 });
        bytes.Add((byte)(classBytes.Length & 0xFF));
        bytes.Add((byte)(classBytes.Length >> 8));
        bytes.AddRange(classBytes);

        const string displayName = "EdgeFlange1";
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));

        var header = new byte[Math.Max(0, dataLength)];
        if (header.Length >= 12)
            BitConverter.GetBytes(typeId).CopyTo(header, 8); // f64 timestamp at 0, u32 TypeId at 8
        bytes.AddRange(header);
        return bytes.ToArray();
    }

    [Fact]
    public void NativeEdgeFlangeReadsVerifiedFields()
    {
        byte[] raw = ReadStream("edge_flange_90_len10.SLDPRT");
        Assert.Equal((ushort)0x74, BitConverter.ToUInt16(raw, 0));

        using var file = SwFile.Open(Sample("edge_flange_90_len10.SLDPRT"), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders.Where(f => f.ClassName == "moEdgeFlange_c"));
        Assert.Equal(0x67u, folder.Header.TypeId);

        // Cross-checked against Agent 5 create4.json targetDefinition:
        // bendAngle=1.5707963267948966, bendRadius=0.002, edgeCount=1,
        // offsetDistance=0.01, positionType=1, reliefWidth=reliefDepth=0.001.
        var definition = Assert.IsType<EdgeFlangeFeatureModel>(folder.FeatureData);
        Assert.Equal(Math.PI / 2.0, definition.BendAngle!.Value, 12);
        Assert.Equal(0.002, definition.BendRadius!.Value, 12);
        Assert.Equal(1, definition.EdgeCount ?? -1);
        Assert.Equal(0.010, definition.OffsetDistance!.Value, 12);
        Assert.Equal(1, definition.PositionType ?? -1);
        Assert.False(definition.UsePositionOffset ?? true);
        Assert.Equal(0.010, definition.PositionOffsetDistance!.Value, 12);
        Assert.Equal(0.001, definition.ReliefWidth!.Value, 12);
        Assert.Equal(0.001, definition.ReliefDepth!.Value, 12);

        Assert.True(definition.ParameterResolution.BendAngle);
        Assert.True(definition.ParameterResolution.BendRadius);
        Assert.True(definition.ParameterResolution.EdgeCount);
        Assert.True(definition.ParameterResolution.OffsetDistance);
        Assert.True(definition.ParameterResolution.PositionType);
        Assert.True(definition.ParameterResolution.UsePositionOffset);
        Assert.True(definition.ParameterResolution.PositionOffsetDistance);
        Assert.True(definition.ParameterResolution.ReliefWidth);
        Assert.True(definition.ParameterResolution.ReliefDepth);

        // Everything else remains unresolved and must not expose a default value.
        Assert.Null(definition.Edge);
        Assert.Null(definition.Edges);
        Assert.Null(definition.OffsetType);
        Assert.Null(definition.UseDefaultBendRadius);
        Assert.Null(definition.UseDefaultBendAllowance);
        Assert.Null(definition.UseDefaultBendRelief);
        Assert.Null(definition.AutoReliefType);
        Assert.Null(definition.ReliefRatio);
        Assert.Null(definition.UseReliefRatio);
        Assert.Null(definition.GapDistance);
        Assert.Null(definition.CustomBendAllowance);

        Assert.False(definition.ParameterResolution.Edge);
        Assert.False(definition.ParameterResolution.Edges);
        Assert.False(definition.ParameterResolution.OffsetType);
        Assert.False(definition.ParameterResolution.UseDefaultBendRadius);
        Assert.False(definition.ParameterResolution.UseDefaultBendRelief);
        Assert.False(definition.ParameterResolution.AutoReliefType);
        Assert.False(definition.ParameterResolution.ReliefRatio);
        Assert.False(definition.ParameterResolution.UseReliefRatio);
        Assert.False(definition.ParameterResolution.GapDistance);
        Assert.False(definition.ParameterResolution.CustomBendAllowance);
    }

    [Fact]
    public void ResavePreservesClassSequenceAndEdgeFlangePayloads()
    {
        byte[] main = ReadStream("edge_flange_90_len10.SLDPRT");
        byte[] resave = ReadStream("edge_flange_90_len10_resave.SLDPRT");
        var mainDoc = MfcDocument.Parse(main);
        var resaveDoc = MfcDocument.Parse(resave);

        // The resave adds path/metadata strings and shifts absolute offsets, but
        // the design-tree class sequence is unchanged.
        Assert.Equal(
            mainDoc.AllElements.Select(e => e.ClassName).ToArray(),
            resaveDoc.AllElements.Select(e => e.ClassName).ToArray());

        foreach (string className in new[]
        {
            "moEdgeFlange_c",
            "moOneBend_c",
            "moMultiEdgeFlangeDataPerEdge_c",
            "moLinearEdgeGeom_c"
        })
        {
            var a = Assert.Single(mainDoc.AllElements.Where(e => e.ClassName == className));
            var b = Assert.Single(resaveDoc.AllElements.Where(e => e.ClassName == className));
            Assert.Equal(a.RawData, b.RawData);
        }

        using var mainFile = SwFile.Open(Sample("edge_flange_90_len10.SLDPRT"), readOnly: true);
        using var resaveFile = SwFile.Open(Sample("edge_flange_90_len10_resave.SLDPRT"), readOnly: true);
        var mainDef = Assert.IsType<EdgeFlangeFeatureModel>(
            mainFile.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
        var resaveDef = Assert.IsType<EdgeFlangeFeatureModel>(
            resaveFile.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
        Assert.Equal(mainDef.BendAngle, resaveDef.BendAngle);
        Assert.Equal(mainDef.BendRadius, resaveDef.BendRadius);
        Assert.Equal(mainDef.EdgeCount, resaveDef.EdgeCount);
        Assert.Equal(mainDef.OffsetDistance, resaveDef.OffsetDistance);
        Assert.Equal(mainDef.PositionType, resaveDef.PositionType);
        Assert.Equal(mainDef.ReliefWidth, resaveDef.ReliefWidth);
        Assert.Equal(mainDef.ReliefDepth, resaveDef.ReliefDepth);
    }

    /// <summary>
    /// Pins the measured variant slots. D4 (angle), D1 (radius), D7 (offset),
    /// the anonymous relief block and its position u32 are resolved; D6 is the
    /// position-offset-distance candidate and stays unresolved because no sample
    /// varies it. D8/D9 are display copies of the relief depth (both 0.0025 in
    /// relief_custom), not the authoritative width. The offsets are those of the
    /// main sample; resave shifts them by +0x114 while keeping the payload bytes
    /// identical.
    /// </summary>
    [Fact]
    public void VariantFieldSlotsArePinnedAndResolvedWhereProven()
    {
        byte[] raw = ReadStream("edge_flange_90_len10.SLDPRT");
        const int windowStart = 0x525A; // edge selection block start
        const int windowEnd = 0xA376;   // moTemplateFlatPattern_c

        int d1 = FindNamedParameter(raw, "D1", 0x5CDA, windowEnd);
        Assert.Equal(0x60F9, d1);
        Assert.Equal(0.002, BitConverter.ToDouble(raw, d1 + 0x1E)); // BendRadius (resolved)

        int d4 = FindNamedParameter(raw, "D4", windowStart, windowEnd);
        Assert.Equal(0x69C1, d4);
        Assert.Equal(Math.PI / 2.0, BitConverter.ToDouble(raw, d4 + 0x1E), 12); // BendAngle (resolved)

        int d6 = FindNamedParameter(raw, "D6", windowStart, windowEnd);
        int d7 = FindNamedParameter(raw, "D7", windowStart, windowEnd);
        Assert.Equal(0x972D, d6);
        Assert.Equal(0x9DB4, d7);
        Assert.Equal(0.010, BitConverter.ToDouble(raw, d6 + 0x1E), 12); // PositionOffsetDistance (resolved)
        Assert.Equal(0.010, BitConverter.ToDouble(raw, d7 + 0x1E), 12); // OffsetDistance (resolved)

        int d8 = FindNamedParameter(raw, "D8", windowStart, windowEnd);
        int d9 = FindNamedParameter(raw, "D9", windowStart, windowEnd);
        Assert.Equal(0x76A3, d8);
        Assert.Equal(0x7C80, d9);
        Assert.Equal(0.001, BitConverter.ToDouble(raw, d8 + 0x1E), 12); // relief depth display copies
        Assert.Equal(0.001, BitConverter.ToDouble(raw, d9 + 0x1E), 12);

        // Anonymous relief/position block: [ratio=0.5][width][depth][u64=1][u32 pos].
        const int reliefBlock = 0x9AC7;
        Assert.Equal(0.5, BitConverter.ToDouble(raw, reliefBlock), 12);
        Assert.Equal(0.001, BitConverter.ToDouble(raw, reliefBlock + 8), 12);  // ReliefWidth (resolved)
        Assert.Equal(0.001, BitConverter.ToDouble(raw, reliefBlock + 16), 12); // ReliefDepth (resolved)
        Assert.Equal(1UL, BitConverter.ToUInt64(raw, reliefBlock + 24));
        Assert.Equal(1u, BitConverter.ToUInt32(raw, reliefBlock + 32)); // PositionType (resolved)

        using var file = SwFile.Open(Sample("edge_flange_90_len10.SLDPRT"), readOnly: true);
        var definition = Assert.IsType<EdgeFlangeFeatureModel>(
            file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
        var resolution = definition.ParameterResolution;
        var resolved = new HashSet<string>
        {
            "BendAngle", "BendRadius", "EdgeCount",
            "OffsetDistance", "PositionType",
            "UsePositionOffset", "PositionOffsetDistance",
            "ReliefWidth", "ReliefDepth"
        };
        foreach (var property in typeof(EdgeFlangeParameterResolution).GetProperties())
        {
            Assert.Equal(resolved.Contains(property.Name),
                (bool)property.GetValue(resolution)!);
        }
    }

    public static IEnumerable<object[]> VariantSamples()
    {
        // Native targetDefinition snapshots from Agent 5 create12.json
        // (and create4.json for the baseline).
        yield return new object[] { "edge_flange_90_len10.SLDPRT", Math.PI / 2.0, 0.010, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_90_len10_resave.SLDPRT", Math.PI / 2.0, 0.010, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_120_len10.SLDPRT", 2.0 * Math.PI / 3.0, 0.010, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_120_len10_resave.SLDPRT", 2.0 * Math.PI / 3.0, 0.010, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_len20.SLDPRT", Math.PI / 2.0, 0.020, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_len20_resave.SLDPRT", Math.PI / 2.0, 0.020, 1, 0.001, 0.001 };
        yield return new object[] { "edge_flange_pos_outer.SLDPRT", Math.PI / 2.0, 0.010, 2, 0.001, 0.001 };
        yield return new object[] { "edge_flange_pos_outer_resave.SLDPRT", Math.PI / 2.0, 0.010, 2, 0.001, 0.001 };
        yield return new object[] { "edge_flange_pos_bend_outside.SLDPRT", Math.PI / 2.0, 0.010, 3, 0.001, 0.001 };
        yield return new object[] { "edge_flange_pos_bend_outside_resave.SLDPRT", Math.PI / 2.0, 0.010, 3, 0.001, 0.001 };
        yield return new object[] { "edge_flange_relief_custom.SLDPRT", Math.PI / 2.0, 0.010, 1, 0.0015, 0.0025 };
        yield return new object[] { "edge_flange_relief_custom_resave.SLDPRT", Math.PI / 2.0, 0.010, 1, 0.0015, 0.0025 };
    }

    [Theory]
    [MemberData(nameof(VariantSamples))]
    public void VariantFieldsMatchNativeSnapshots(
        string fileName, double angle, double offsetDistance, int positionType,
        double reliefWidth, double reliefDepth)
    {
        using var file = SwFile.Open(Sample(fileName), readOnly: true);
        var definition = Assert.IsType<EdgeFlangeFeatureModel>(
            file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);

        Assert.Equal(angle, definition.BendAngle!.Value, 12);
        Assert.Equal(0.002, definition.BendRadius!.Value, 12);
        Assert.Equal(1, definition.EdgeCount ?? -1);
        Assert.Equal(offsetDistance, definition.OffsetDistance!.Value, 12);
        Assert.Equal(positionType, definition.PositionType ?? -1);
        Assert.Equal(reliefWidth, definition.ReliefWidth!.Value, 12);
        Assert.Equal(reliefDepth, definition.ReliefDepth!.Value, 12);

        Assert.True(definition.ParameterResolution.BendAngle);
        Assert.True(definition.ParameterResolution.BendRadius);
        Assert.True(definition.ParameterResolution.EdgeCount);
        Assert.True(definition.ParameterResolution.OffsetDistance);
        Assert.True(definition.ParameterResolution.PositionType);
        Assert.True(definition.ParameterResolution.UsePositionOffset);
        Assert.True(definition.ParameterResolution.PositionOffsetDistance);
        Assert.True(definition.ParameterResolution.ReliefWidth);
        Assert.True(definition.ParameterResolution.ReliefDepth);

        // Values that no controlled sample varies remain unknown.
        Assert.False(definition.UsePositionOffset ?? true);
        Assert.Equal(0.010, definition.PositionOffsetDistance!.Value, 12);
        Assert.Null(definition.ReliefRatio);
        Assert.Null(definition.UseReliefRatio);
        Assert.Null(definition.AutoReliefType);
        Assert.Null(definition.ReliefTearType);
        Assert.Null(definition.UseDefaultBendRelief);
        Assert.Null(definition.OffsetType);
        Assert.Null(definition.UseDefaultBendRadius);
        Assert.Null(definition.UseDefaultBendAllowance);
        Assert.False(definition.ParameterResolution.ReliefRatio);
        Assert.False(definition.ParameterResolution.UseReliefRatio);
        Assert.False(definition.ParameterResolution.AutoReliefType);
        Assert.False(definition.ParameterResolution.ReliefTearType);
        Assert.False(definition.ParameterResolution.UseDefaultBendRelief);
        Assert.False(definition.ParameterResolution.OffsetType);
        Assert.False(definition.ParameterResolution.UseDefaultBendRadius);
        Assert.False(definition.ParameterResolution.UseDefaultBendAllowance);
    }

    [Theory]
    [InlineData("edge_flange_position_offset.SLDPRT", 0.005)]
    [InlineData("edge_flange_position_offset_resave.SLDPRT", 0.005)]
    [InlineData("edge_flange_position_offset10.SLDPRT", 0.010)]
    [InlineData("edge_flange_position_offset10_resave.SLDPRT", 0.010)]
    public void PositionOffsetSamplesMatchNativeSnapshots(string fileName, double positionOffsetDistance)
    {
        using var file = SwFile.Open(Sample(fileName), readOnly: true);
        var definition = Assert.IsType<EdgeFlangeFeatureModel>(
            file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);

        Assert.Equal(1, definition.PositionType ?? -1);
        Assert.True(definition.UsePositionOffset ?? false);
        Assert.Equal(positionOffsetDistance, definition.PositionOffsetDistance!.Value, 12);
        Assert.Equal(0.010, definition.OffsetDistance!.Value, 12);
        Assert.Equal(1, definition.EdgeCount ?? -1);
        Assert.True(definition.ParameterResolution.UsePositionOffset);
        Assert.True(definition.ParameterResolution.PositionOffsetDistance);
        Assert.Null(definition.PositionOffsetType);
        Assert.False(definition.ParameterResolution.PositionOffsetType);
    }

    /// <summary>
    /// Audit of the still-unresolved edge-flange values against all six variants
    /// and resaves. D6 is now resolved as PositionOffsetDistance (0.010 in every
    /// original sample, 0.005/0.010 in the position-offset samples) and the
    /// relief-ratio gate is 0.5 with u64=1 in every sample. The native create12.json
    /// snapshots likewise report positionOffsetDistance=0.01,
    /// reliefRatio=0.5, useReliefRatio=true, useDefaultBendRelief=false,
    /// autoReliefType=1, reliefTearType=1, offsetType=1 and
    /// useDefaultBendRadius=false in every case; no single-variable sample exists
    /// for the remaining values, so none may be promoted.
    /// </summary>
    [Fact]
    public void UnresolvedEdgeFlangeValuesHaveNoVariantEvidence()
    {
        foreach (var fileName in new[]
        {
            "edge_flange_90_len10.SLDPRT",
            "edge_flange_90_len10_resave.SLDPRT",
            "edge_flange_120_len10.SLDPRT",
            "edge_flange_120_len10_resave.SLDPRT",
            "edge_flange_len20.SLDPRT",
            "edge_flange_len20_resave.SLDPRT",
            "edge_flange_pos_outer.SLDPRT",
            "edge_flange_pos_outer_resave.SLDPRT",
            "edge_flange_pos_bend_outside.SLDPRT",
            "edge_flange_pos_bend_outside_resave.SLDPRT",
            "edge_flange_relief_custom.SLDPRT",
            "edge_flange_relief_custom_resave.SLDPRT"
        })
        {
            byte[] raw = ReadStream(fileName);
            int d6 = FindNamedParameter(raw, "D6", 0x5CDA, raw.Length);
            Assert.True(d6 > 0, fileName);
            Assert.Equal(0.010, BitConverter.ToDouble(raw, d6 + 0x1E), 12);

            int anchor = FindReliefAnchor(raw);
            Assert.True(anchor > 0, fileName);
            Assert.Equal(0.5, BitConverter.ToDouble(raw, anchor), 12);
            Assert.Equal(1UL, BitConverter.ToUInt64(raw, anchor + 24));

            using var file = SwFile.Open(Sample(fileName), readOnly: true);
            var definition = Assert.IsType<EdgeFlangeFeatureModel>(
                file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
            Assert.Equal(0.010, definition.PositionOffsetDistance!.Value, 12);
            Assert.False(definition.UsePositionOffset ?? true);
            Assert.Null(definition.ReliefRatio);
            Assert.Null(definition.UseReliefRatio);
            Assert.Null(definition.UseDefaultBendRelief);
            Assert.Null(definition.AutoReliefType);
            Assert.Null(definition.ReliefTearType);
            Assert.Null(definition.OffsetType);
            Assert.Null(definition.UseDefaultBendRadius);
            Assert.Null(definition.UseDefaultBendAllowance);
        }
    }

    private static int FindReliefAnchor(byte[] raw)
    {
        int found = -1;
        for (int p = 0; p <= raw.Length - 40; p++)
        {
            if (BitConverter.ToDouble(raw, p) != 0.5) continue;
            double width = BitConverter.ToDouble(raw, p + 8);
            double depth = BitConverter.ToDouble(raw, p + 16);
            if (!(width > 0) || !(depth > 0)) continue;
            if (BitConverter.ToUInt64(raw, p + 24) != 1UL) continue;
            uint position = BitConverter.ToUInt32(raw, p + 32);
            if (position < 1 || position > 3) continue;
            if (found >= 0) return -1;
            found = p;
        }
        return found;
    }

    [Fact]
    public void UnknownOrTruncatedEdgeFlangeLayoutIsRejected()
    {
        // Observed TypeId: recognized node, but a synthetic payload without the
        // selection/dimension layout is rejected by the strict window parser.
        var validRaw = BuildSyntheticEdgeFlangeStream(0x67);
        var validDoc = MfcDocument.Parse(validRaw);
        var valid = validDoc.AllElements.Single(e => e.ClassName == "moEdgeFlange_c");
        Assert.True(FlangeFeatureParsers.IsEdgeFlangeNode(valid));
        Assert.Null(FlangeFeatureParsers.ParseEdgeFlange(valid));
        Assert.Null(FlangeFeatureParsers.ParseEdgeFlange(validDoc, valid, 0, validRaw.Length));

        // Catalog candidate 0x63 is not the observed TypeId.
        var wrongRaw = BuildSyntheticEdgeFlangeStream(0x63);
        var wrongDoc = MfcDocument.Parse(wrongRaw);
        var wrongType = wrongDoc.AllElements.Single(e => e.ClassName == "moEdgeFlange_c");
        Assert.False(FlangeFeatureParsers.IsEdgeFlangeNode(wrongType));
        Assert.Null(FlangeFeatureParsers.ParseEdgeFlange(wrongDoc, wrongType, 0, wrongRaw.Length));

        // Truncated data cannot expose the folder header type id.
        var truncatedRaw = BuildSyntheticEdgeFlangeStream(0x67, dataLength: 4);
        var truncatedDoc = MfcDocument.Parse(truncatedRaw);
        var truncated = truncatedDoc.AllElements.Single(e => e.ClassName == "moEdgeFlange_c");
        Assert.False(FlangeFeatureParsers.IsEdgeFlangeNode(truncated));
        Assert.Null(FlangeFeatureParsers.ParseEdgeFlange(truncatedDoc, truncated, 0, truncatedRaw.Length));

        // Wrong class is rejected even with the right tag/type.
        var miter = MfcDocument.Parse(BuildSyntheticEdgeFlangeStream(0x67)).AllElements
            .Single(e => e.ClassName == "moEdgeFlange_c");
        Assert.False(FlangeFeatureParsers.IsEdgeFlangeNode(null!));
        Assert.Null(FlangeFeatureParsers.ParseMiterFlange(miter));
    }

    [Fact]
    public void TamperedBendSlotsAreRejected()
    {
        byte[] main = ReadStream("edge_flange_90_len10.SLDPRT");

        // Baseline window parse succeeds.
        var baseline = ParseWithWindow(main);
        Assert.NotNull(baseline);

        // D4 (BendAngle) zeroed.
        byte[] badAngle = (byte[])main.Clone();
        BitConverter.GetBytes(0.0).CopyTo(badAngle, 0x69DF);
        Assert.Null(ParseWithWindow(badAngle));

        // D1 (BendRadius) made negative.
        byte[] badRadius = (byte[])main.Clone();
        BitConverter.GetBytes(-0.001).CopyTo(badRadius, 0x6117);
        Assert.Null(ParseWithWindow(badRadius));

        // Wrong stream version for the established layout.
        byte[] badVersion = (byte[])main.Clone();
        BitConverter.GetBytes((ushort)0x73).CopyTo(badVersion, 0);
        Assert.Null(ParseWithWindow(badVersion));

        // The mapper must also leave FeatureData null for a tampered stream.
        var badDoc = MfcDocument.Parse(badAngle);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(badDoc, model);
        Assert.Null(model.Folders.Single(f => f.ClassName == "moEdgeFlange_c").FeatureData);
    }

    [Fact]
    public void TamperedVariantSlotsAreRejected()
    {
        byte[] main = ReadStream("edge_flange_90_len10.SLDPRT");
        Assert.NotNull(ParseWithWindow(main));

        // D7 (OffsetDistance) zeroed.
        byte[] badOffset = (byte[])main.Clone();
        BitConverter.GetBytes(0.0).CopyTo(badOffset, 0x9DD2);
        Assert.Null(ParseWithWindow(badOffset));

        // D7 made negative.
        byte[] negativeOffset = (byte[])main.Clone();
        BitConverter.GetBytes(-0.01).CopyTo(negativeOffset, 0x9DD2);
        Assert.Null(ParseWithWindow(negativeOffset));

        // D6 (position-offset candidate) missing from the observed layout.
        byte[] missingD6 = (byte[])main.Clone();
        Array.Clear(missingD6, 0x972D, 8);
        Assert.Null(ParseWithWindow(missingD6));

        // Relief block width/depth made negative.
        foreach (int slot in new[] { 0x9ACF, 0x9AD7 })
        {
            byte[] badRelief = (byte[])main.Clone();
            BitConverter.GetBytes(-0.001).CopyTo(badRelief, slot);
            Assert.Null(ParseWithWindow(badRelief));
        }

        // Relief ratio gate (0.5) and u64 flag gate (1) changed.
        byte[] badRatio = (byte[])main.Clone();
        BitConverter.GetBytes(0.4).CopyTo(badRatio, 0x9AC7);
        Assert.Null(ParseWithWindow(badRatio));

        byte[] badFlag = (byte[])main.Clone();
        BitConverter.GetBytes(0UL).CopyTo(badFlag, 0x9ADF);
        Assert.Null(ParseWithWindow(badFlag));

        // PositionType outside the observed enum range 1..3.
        foreach (uint position in new[] { 0u, 4u })
        {
            byte[] badPosition = (byte[])main.Clone();
            BitConverter.GetBytes(position).CopyTo(badPosition, 0x9AE7);
            Assert.Null(ParseWithWindow(badPosition));
        }

        // A duplicated relief/position block is ambiguous and must be rejected.
        byte[] duplicated = (byte[])main.Clone();
        Array.Copy(main, 0x9ABF, duplicated, 0x9B80, 48);
        Assert.Null(ParseWithWindow(duplicated));
    }

    [Fact]
    public void MappingDoesNotModifyAnyVariantStream()
    {
        foreach (var fileName in new[]
        {
            "edge_flange_90_len10.SLDPRT",
            "edge_flange_120_len10.SLDPRT",
            "edge_flange_len20.SLDPRT",
            "edge_flange_pos_outer.SLDPRT",
            "edge_flange_pos_bend_outside.SLDPRT",
            "edge_flange_relief_custom.SLDPRT"
        })
        {
            byte[] raw = ReadStream(fileName);
            var before = (byte[])raw.Clone();
            var doc = MfcDocument.Parse(raw);
            ResolvedFeaturesModel.Map(doc, new ResolvedFeaturesModel());
            Assert.Equal(before, doc.SourceData);
        }
    }

    [Fact]
    public void MappingDoesNotModifyTheEdgeFlangeStream()
    {
        byte[] raw = ReadStream("edge_flange_90_len10.SLDPRT");
        var before = (byte[])raw.Clone();
        var doc = MfcDocument.Parse(raw);
        ResolvedFeaturesModel.Map(doc, new ResolvedFeaturesModel());
        Assert.Equal(before, doc.SourceData);
    }
}
