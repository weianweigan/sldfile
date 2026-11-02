using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured Shell authoring tests. The block is re-emitted verbatim; only the
/// six validated thickness slots, folder count and feature presence change.
/// </summary>
public class ShellAuthoringTests
{
    [Theory]
    [InlineData("shell_t2_remove_top", "Primary", 0.002)]
    [InlineData("shell_t2_remove_top_resave", "Resave", 0.002)]
    [InlineData("shell_t3_remove_top", "Primary", 0.003)]
    [InlineData("shell_t3_remove_top_resave", "Resave", 0.003)]
    public void ShellAuthoring_ReEmitsMeasuredBlockAndReReads(string sample, string variant, double thickness)
    {
        var template = FindTemplate(thickness, variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddShell(thickness);
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertShellBlock(stripped, authored, template, thickness);
            var definition = Assert.IsType<ShellFeatureDefinitionData>(feature.Definition);
            AssertShellDefinition(definition, thickness);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "Shell"));
        Assert.Equal(FeatureKind.Shell, reFeature.Kind);
        AssertShellDefinition(Assert.IsType<ShellFeatureDefinitionData>(reFeature.Definition), thickness);
    }

    [Fact]
    public void ShellAuthoring_ParameterizesThicknessBeyondMeasuredSamples()
    {
        var template = FindTemplate(0.002, "Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            part.AddShell(0.004);
            AssertShellBlock(stripped, part.GetStreamBytes(streamPath)!, template, 0.004);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<ShellFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Name == "Shell")).Definition);
        AssertShellDefinition(definition, 0.004);
    }

    [Fact]
    public void ShellAuthoring_RejectsWrongBaseThicknessAndName_WithoutChangingStream()
    {
        var template = FindTemplate(0.002, "Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddShell(0.002, featureName: "MyShell"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddShell(0.0));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddShell(0.2));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddShell(0.002));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void ShellAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddShell(0.002));
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void AuthoredShell_SurvivesResave()
    {
        var template = FindTemplate(0.002, "Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddShell(0.002);
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertShellDefinition(Assert.IsType<ShellFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Name == "Shell")).Definition), 0.002);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static ShellAuthoringTemplate FindTemplate(double thickness, string variant) =>
        ShellAuthoringTemplates.All.Single(t =>
            Math.Abs(t.MeasuredThickness - thickness) < 1e-12 &&
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/ShellOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, ShellAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        BitConverter.GetBytes((ushort)template.StrippedFolderCount).CopyTo(stripped, 4);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static void AssertShellBlock(
        byte[] stripped, byte[] authored, ShellAuthoringTemplate template, double thickness)
    {
        Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
        for (int i = 0; i < stripped.Length; i++)
        {
            if (i == 4 || i == 5) continue;
            Assert.Equal(stripped[i], authored[i]);
        }
        Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));

        byte[] block = authored.Skip(stripped.Length).ToArray();
        var slots = new HashSet<int>();
        foreach (int slot in ShellFeatureParser.ThicknessSlotOffsets)
        {
            for (int j = 0; j < 8; j++) slots.Add(slot + j);
        }
        for (int i = 0; i < block.Length; i++)
        {
            if (slots.Contains(i)) continue;
            Assert.True(template.Block[i] == block[i],
                $"Shell block mismatch at 0x{i:X}: template=0x{template.Block[i]:X2} authored=0x{block[i]:X2}");
        }
        foreach (int slot in ShellFeatureParser.ThicknessSlotOffsets)
            Assert.Equal(thickness, BitConverter.ToDouble(block, slot), 12);
    }

    private static void AssertShellDefinition(ShellFeatureDefinitionData definition, double thickness)
    {
        Assert.Equal("moShell_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
        Assert.Equal(1, definition.FacesRemovedCount);
        Assert.Equal(SelectionEntityType.Face, definition.FacesRemoved![0].EntityType);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.Resolution.FacesRemoved);
        Assert.False(definition.Resolution.Direction);
    }
}
