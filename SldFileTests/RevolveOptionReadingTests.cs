using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading coverage for the measured revolution options:
/// thin one-direction (moRevolutionThin_c / 0x50), both directions (moRevolution_c mode 0 / Type 2),
/// and cut full revolution (moRevCut_c / 0x55 / stream 0x71).
/// </summary>
public class RevolveOptionReadingTests
{
    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "RevolveOptions", sample + ".SLDPRT");

    private static RevolveFeatureModel ReadThin(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Revolve));
        var definition = Assert.IsType<RevolveFeatureModel>(feature.Model);
        Assert.Equal("moRevolutionThin_c", feature.TypeName);
        Assert.Equal("moRevolutionThin_c", definition.ClassName);
        Assert.Equal(0x50u, definition.TypeId);
        return definition;
    }

    private static RevolveFeatureModel ReadBoth(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Revolve));
        var definition = Assert.IsType<RevolveFeatureModel>(feature.Model);
        Assert.Equal("moRevolution_c", definition.ClassName);
        Assert.Equal(0x4Eu, definition.TypeId);
        return definition;
    }

    private static RevolveFeatureModel ReadCut(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.TypeName == "moRevCut_c"));
        Assert.Equal(FeatureKind.Cut, feature.Kind);
        var definition = Assert.IsType<RevolveFeatureModel>(feature.Model);
        Assert.Equal("moRevCut_c", definition.ClassName);
        Assert.Equal(0x55u, definition.TypeId);
        return definition;
    }

    [Theory]
    [InlineData("revolve_thin_90", Math.PI / 2.0)]
    [InlineData("revolve_thin_90_resave", Math.PI / 2.0)]
    [InlineData("revolve_thin_360", 2.0 * Math.PI)]
    [InlineData("revolve_thin_360_resave", 2.0 * Math.PI)]
    public void ThinRevolveSamples_ReadThicknessAndWallType(string sample, double angle)
    {
        var definition = ReadThin(sample);

        Assert.True(definition.IsThinFeature);
        Assert.False(definition.IsCut);
        Assert.Equal(0, definition.Type);
        Assert.False(definition.BothDirections);
        Assert.False(definition.ReverseDirection);
        Assert.Equal(angle, definition.Angle, 12);
        Assert.Equal(0, definition.AngleDirection2, 12);
        Assert.Equal(0u, definition.ThinWallType);
        Assert.Equal(0.001, definition.Thickness1, 12);
        Assert.Equal(0, definition.Thickness2, 12);

        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Angle)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.BothDirections)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.IsThinFeature)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.IsCut)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.ThinWallType)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Thickness1)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Thickness2)));
        Assert.False(definition.IsResolved(nameof(RevolveFeatureModel.Merge)));
    }

    [Theory]
    [InlineData("revolve_both_directions")]
    [InlineData("revolve_both_directions_resave")]
    public void BothDirectionsRevolveSamples_ReadBothAnglesAndType(string sample)
    {
        var definition = ReadBoth(sample);

        Assert.False(definition.IsThinFeature);
        Assert.False(definition.IsCut);
        Assert.True(definition.BothDirections);
        Assert.Equal(2, definition.Type);
        Assert.False(definition.ReverseDirection);
        Assert.Equal(Math.PI / 2.0, definition.Angle, 12);
        Assert.Equal(Math.PI / 4.0, definition.AngleDirection2, 12);

        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Angle)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.AngleDirection2)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.BothDirections)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.ReverseDirection)));
    }

    [Theory]
    [InlineData("revolve_cut_360")]
    [InlineData("revolve_cut_360_resave")]
    public void CutRevolveSamples_ReadIsCutFullRevolution(string sample)
    {
        var definition = ReadCut(sample);

        Assert.True(definition.IsCut);
        Assert.False(definition.IsThinFeature);
        Assert.Equal(0, definition.Type);
        Assert.False(definition.BothDirections);
        Assert.False(definition.ReverseDirection);
        Assert.Equal(2.0 * Math.PI, definition.Angle, 12);
        Assert.Equal(0, definition.AngleDirection2, 12);
        Assert.Equal(0, definition.Thickness1, 12);
        Assert.Equal(0, definition.Thickness2, 12);

        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Angle)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.BothDirections)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.IsCut)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.IsThinFeature)));
    }

    [Theory]
    [InlineData("revolve_90", Math.PI / 2.0, false)]
    [InlineData("revolve_180", Math.PI, false)]
    [InlineData("revolve_180_reverse", Math.PI, true)]
    [InlineData("revolve_360", 2.0 * Math.PI, false)]
    [InlineData("revolve_360_resave", 2.0 * Math.PI, false)]
    public void ExistingSingleDirectionRevolveSamples_KeepMeasuredFields(string sample, double angle, bool reverse)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var definition = Assert.IsType<RevolveFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Revolve)).Model);

        Assert.Equal(angle, definition.Angle, 12);
        Assert.Equal(reverse, definition.ReverseDirection);
        Assert.Equal(0, definition.Type);
        Assert.False(definition.BothDirections);
        Assert.Equal(0, definition.AngleDirection2, 12);
        Assert.False(definition.IsCut);
        Assert.False(definition.IsThinFeature);
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.BothDirections)));
        Assert.True(definition.IsResolved(nameof(RevolveFeatureModel.ReverseDirection)));
    }

    private static RevolveFeatureModel? ParseDirect(byte[] stream, string className, uint typeId)
    {
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == className));
        return RevolveFeatureParser.Parse(doc, className, typeId, feature.TagOffset, stream.Length);
    }

    private static byte[] LoadStream(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        return (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
    }

    private static MfcElement Element(byte[] stream, string className) =>
        Assert.Single(MfcDocument.Parse(stream).AllElements.Where(e => e.ClassName == className));

    private static byte[] Rename(byte[] stream, string from, string to)
    {
        byte[] pattern = { 0xFF, 0xFE, 0xFF, (byte)from.Length };
        int index = Find(stream, pattern.Concat(System.Text.Encoding.Unicode.GetBytes(from)).ToArray(), 0);
        Assert.True(index > 0);
        byte[] result = (byte[])stream.Clone();
        byte[] replacement = System.Text.Encoding.Unicode.GetBytes(to);
        Array.Copy(replacement, 0, result, index + 4, replacement.Length);
        return result;
    }

    [Fact]
    public void ThinLayoutRequiresClassTypeVersionAndBothThicknessParameters()
    {
        byte[] stream = LoadStream("revolve_thin_90");
        Assert.Null(ParseDirect(stream, "moRevolutionThin_c", 0x4E));
        Assert.Null(ParseDirect(stream, "moRevolutionThin_c", 0x55));
        byte[] wrongVersion = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x71).CopyTo(wrongVersion, 0);
        Assert.Null(ParseDirect(wrongVersion, "moRevolutionThin_c", 0x50));
        Assert.Null(ParseDirect(Rename(stream, "D3", "D9"), "moRevolutionThin_c", 0x50));
        Assert.Null(ParseDirect(Rename(stream, "D4", "D9"), "moRevolutionThin_c", 0x50));
    }

    [Fact]
    public void ThinSpecMustBeTheMeasuredEmptyOneDirectionPayload()
    {
        byte[] stream = LoadStream("revolve_thin_90");
        var thinSpec = Element(stream, "moThinSpec_c");
        byte[] inserted = new byte[stream.Length + 1];
        Array.Copy(stream, 0, inserted, 0, thinSpec.DataStart);
        inserted[thinSpec.DataStart] = 0x01;
        Array.Copy(stream, thinSpec.DataStart, inserted, thinSpec.DataStart + 1, stream.Length - thinSpec.DataStart);
        Assert.Null(ParseDirect(inserted, "moRevolutionThin_c", 0x50));
    }

    [Fact]
    public void BothDirectionsRequiresModeZeroAndD2Angle()
    {
        byte[] stream = LoadStream("revolve_both_directions");
        var spec = Element(stream, "moRevEndSpec_c");

        byte[] modeTwo = (byte[])stream.Clone();
        BitConverter.GetBytes(2u).CopyTo(modeTwo, spec.DataStart);
        Assert.Null(ParseDirect(modeTwo, "moRevolution_c", 0x4E));

        Assert.Null(ParseDirect(Rename(stream, "D2", "D9"), "moRevolution_c", 0x4E));

        byte[] zeroD2 = (byte[])stream.Clone();
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(stream, spec.TagOffset, stream.Length, "D2");
        Assert.Single(d2);
        BitConverter.GetBytes(0.0).CopyTo(zeroD2, d2[0].Offset + 32);
        Assert.Null(ParseDirect(zeroD2, "moRevolution_c", 0x4E));
    }

    [Fact]
    public void CutLayoutRequiresCutClassVersionAndAxisHeader()
    {
        byte[] stream = LoadStream("revolve_cut_360");
        Assert.Null(ParseDirect(stream, "moRevCut_c", 0x4E));
        Assert.Null(ParseDirect(stream, "moRevCut_c", 0x50));
        byte[] wrongVersion = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x70).CopyTo(wrongVersion, 0);
        Assert.Null(ParseDirect(wrongVersion, "moRevCut_c", 0x55));

        var axis = Element(stream, "moLineRef_w");
        byte[] wrongAxis = (byte[])stream.Clone();
        wrongAxis[axis.TagOffset - 12] = 0x64; // 0x8065 -> 0x8064
        Assert.Null(ParseDirect(wrongAxis, "moRevCut_c", 0x55));
    }

    [Fact]
    public void AngleParameterNameAndValueAreValidated()
    {
        byte[] thin = LoadStream("revolve_thin_90");
        Assert.Null(ParseDirect(Rename(thin, "D1", "D9"), "moRevolutionThin_c", 0x50));

        byte[] both = LoadStream("revolve_both_directions");
        var angle = Element(both, "moAngleParameter_c");
        byte[] zeroAngle = (byte[])both.Clone();
        BitConverter.GetBytes(0.0).CopyTo(zeroAngle, angle.DataStart + 32);
        Assert.Null(ParseDirect(zeroAngle, "moRevolution_c", 0x4E));
    }

    private static int Find(byte[] data, byte[] pattern, int start)
    {
        for (int i = Math.Max(0, start); i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
