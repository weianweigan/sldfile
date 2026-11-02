using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Task 2.3 lofted-bend reading from the controlled non-formed 0x72 specimen
/// (LoftedBendOptions/lofted_bend_2lines.SLDPRT, ResolvedFeatures version 0x74).
/// </summary>
public class LoftedBendFeatureTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "LoftedBendOptions", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles",
                "Features2026", "LoftedBendOptions", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"LoftedBendOptions/{fileName} not found");
    }

    public static IEnumerable<object[]> NativeSamples()
    {
        yield return new object[] { "lofted_bend_2lines.SLDPRT" };
        yield return new object[] { "lofted_bend_2lines_resave.SLDPRT" };
    }

    [Theory]
    [MemberData(nameof(NativeSamples))]
    public void NativeLoftedBendMatchesSnapshot(string fileName)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        Assert.Equal((ushort)0x74,
            BitConverter.ToUInt16(file.RawStreams[ResolvedStream], 0));

        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moLoftedBend_c");
        Assert.Equal(0x72u, folder.Header.TypeId);
        Assert.Equal("LoftedBend", folder.DisplayName);

        var definition = Assert.IsType<LoftedBendFeatureDefinitionData>(folder.FeatureData);
        Assert.Equal(0.001, definition.Thickness!.Value, 12);
        Assert.Equal(2, definition.NumberOfBendLines ?? -1);
        Assert.True(definition.ParameterResolution.Thickness);
        Assert.True(definition.ParameterResolution.NumberOfBendLines);

        // Everything below is unresolved: no varying sample or reliable slot.
        Assert.Null(definition.Direction);
        Assert.Null(definition.BendLineControlOption);
        Assert.Null(definition.MaximumDeviation);
        Assert.Null(definition.ReferToEndPoint);
        Assert.Null(definition.FacetingOption);
        Assert.Null(definition.FacetValue);
        Assert.Null(definition.FormedMethod);
        Assert.Null(definition.SymmetricThickness);
        Assert.Null(definition.Profiles);
        Assert.Null(definition.ProfileCount);
        Assert.False(definition.ParameterResolution.Direction);
        Assert.False(definition.ParameterResolution.BendLineControlOption);
        Assert.False(definition.ParameterResolution.MaximumDeviation);
        Assert.False(definition.ParameterResolution.ReferToEndPoint);
        Assert.False(definition.ParameterResolution.FacetingOption);
        Assert.False(definition.ParameterResolution.FacetValue);
        Assert.False(definition.ParameterResolution.FormedMethod);
        Assert.False(definition.ParameterResolution.SymmetricThickness);
        Assert.False(definition.ParameterResolution.Profiles);
        Assert.False(definition.ParameterResolution.ProfileCount);
    }

    [Fact]
    public void LoftedBendFolderMapsToLoftedBendFeatureKind()
    {
        using var file = SwFile.Open(SamplePath("lofted_bend_2lines.SLDPRT"), readOnly: true);
        var part = Assert.IsType<SldPart>(file);
        var feature = Assert.Single(part.Features, f => f.Name == "LoftedBend");
        Assert.Equal(FeatureKind.LoftedBend, feature.Kind);
        Assert.Equal("moLoftedBend_c", feature.TypeName);
        Assert.IsType<LoftedBendFeatureDefinitionData>(feature.Definition);
    }

    [Fact]
    public void MappingDoesNotModifyLoftedBendStreams()
    {
        foreach (var fileName in new[] { "lofted_bend_2lines.SLDPRT", "lofted_bend_2lines_resave.SLDPRT" })
        {
            using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
            byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            ResolvedFeaturesSchemaMapper.Map(doc, new ResolvedFeaturesData());
            Assert.Equal(raw, doc.SourceData);
        }
    }

    [Fact]
    public void UnknownLayoutsReturnNull()
    {
        using var file = SwFile.Open(SamplePath("lofted_bend_2lines.SLDPRT"), readOnly: true);
        byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
        var element = Assert.Single(MfcDocument.Parse(raw).AllElements,
            e => e.ClassName == "moLoftedBend_c");

        // Wrong stream version.
        byte[] wrongVersion = (byte[])raw.Clone();
        BitConverter.GetBytes((ushort)0x73).CopyTo(wrongVersion, 0);
        var wrongVersionDoc = MfcDocument.Parse(wrongVersion);
        var wrongVersionElement = Assert.Single(wrongVersionDoc.AllElements,
            e => e.ClassName == "moLoftedBend_c");
        Assert.Null(SheetMetalFormingParsers.ParseLoftedBend(
            wrongVersionDoc, wrongVersionElement, wrongVersion.Length));

        // Non-integer NumberOfBendLines (D14 value; 3-char WChar name, so the
        // scalar descriptor starts at name + 0x0A and the value at +0x20).
        int d14 = FindD14NameOffset(raw, element.TagOffset);
        Assert.True(d14 > 0);
        byte[] fractional = (byte[])raw.Clone();
        BitConverter.GetBytes(2.5).CopyTo(fractional, d14 + 0x20);
        var fractionalDoc = MfcDocument.Parse(fractional);
        var fractionalElement = Assert.Single(fractionalDoc.AllElements,
            e => e.ClassName == "moLoftedBend_c");
        Assert.Null(SheetMetalFormingParsers.ParseLoftedBend(
            fractionalDoc, fractionalElement, fractional.Length));

        // Broken thickness gate (the two 1.0 companions before the slot).
        byte[] brokenGate = (byte[])raw.Clone();
        BitConverter.GetBytes(2.0).CopyTo(brokenGate, d14 - 0xBC5);
        var brokenGateDoc = MfcDocument.Parse(brokenGate);
        var brokenGateElement = Assert.Single(brokenGateDoc.AllElements,
            e => e.ClassName == "moLoftedBend_c");
        Assert.Null(SheetMetalFormingParsers.ParseLoftedBend(
            brokenGateDoc, brokenGateElement, brokenGate.Length));

        // Non-positive Thickness at D14 name - 0xBB4.
        byte[] zeroThickness = (byte[])raw.Clone();
        BitConverter.GetBytes(0.0).CopyTo(zeroThickness, d14 - 0xBB4);
        var zeroThicknessDoc = MfcDocument.Parse(zeroThickness);
        var zeroThicknessElement = Assert.Single(zeroThicknessDoc.AllElements,
            e => e.ClassName == "moLoftedBend_c");
        Assert.Null(SheetMetalFormingParsers.ParseLoftedBend(
            zeroThicknessDoc, zeroThicknessElement, zeroThickness.Length));
    }

    private static int FindD14NameOffset(byte[] raw, int start)
    {
        for (int p = start; p <= raw.Length - 8; p++)
        {
            if (raw[p] == 0xFF && raw[p + 1] == 0xFE && raw[p + 2] == 0xFF &&
                raw[p + 3] == 3 && raw[p + 4] == (byte)'D' && raw[p + 5] == 0 &&
                raw[p + 6] == (byte)'1' && raw[p + 7] == 0 &&
                p + 9 <= raw.Length && raw[p + 8] == (byte)'4' && raw[p + 9] == 0)
                return p;
        }
        return -1;
    }
}
