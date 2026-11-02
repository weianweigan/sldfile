using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured surface-extrude authoring tests. Only the two depth slots are patched;
/// the rest of the measured block is re-emitted verbatim.
/// </summary>
public class SurfaceExtrudeAuthoringTests
{
    [Theory]
    [InlineData("surf_extrude_10mm", "Primary")]
    [InlineData("surf_extrude_10mm_resave", "Resave")]
    public void SurfaceExtrudeAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        var template = Template(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSurfaceExtrude(0.010);
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertBlock(stripped, authored, template, 0.010);
            AssertDefinition(feature, 0.010);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "SurfaceExtrude"));
        Assert.Equal(FeatureKind.SurfaceExtrude, reFeature.Kind);
        AssertDefinition(reFeature, 0.010);
    }

    [Fact]
    public void SurfaceExtrudeAuthoring_ParameterizesDepth()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_extrude_10mm"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            part.AddSurfaceExtrude(0.020);
            AssertBlock(stripped, part.GetStreamBytes(streamPath)!, template, 0.020);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertDefinition(Assert.Single(reopened.Features.Where(f => f.Name == "SurfaceExtrude")), 0.020);
    }

    [Fact]
    public void AuthoredSurfaceExtrude_SurvivesResave()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_extrude_10mm"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddSurfaceExtrude(0.010);
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertDefinition(Assert.Single(reopened.Features.Where(f => f.Name == "SurfaceExtrude")), 0.010);
    }

    [Fact]
    public void SurfaceExtrudeAuthoring_RejectsWrongBaseDepthAndName_WithoutChangingStream()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_extrude_10mm"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSurfaceExtrude(0.010, featureName: "MySurface"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddSurfaceExtrude(0.0));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddSurfaceExtrude(2.0));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddSurfaceExtrude(0.010));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void SurfaceExtrudeAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_extrude_10mm"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSurfaceExtrude(0.010));
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SurfaceExtrudeAuthoringTemplate Template(string variant) =>
        SurfaceExtrudeAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, SurfaceExtrudeAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        BitConverter.GetBytes((ushort)template.StrippedFolderCount).CopyTo(stripped, 4);
        BitConverter.GetBytes(0u).CopyTo(stripped, template.MarkerOffset + 4);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static void AssertBlock(
        byte[] stripped, byte[] authored, SurfaceExtrudeAuthoringTemplate template, double depth)
    {
        Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
        for (int i = 0; i < stripped.Length; i++)
        {
            if (i == 4 || i == 5 ||
                (i >= template.MarkerOffset + 4 && i < template.MarkerOffset + 8))
                continue;
            Assert.Equal(stripped[i], authored[i]);
        }
        Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
        Assert.Equal(template.MarkerValue, BitConverter.ToUInt32(authored, template.MarkerOffset + 4));

        byte[] block = authored.Skip(stripped.Length).ToArray();
        var slots = new HashSet<int>();
        foreach (int slot in new[] { template.DepthSlot1, template.DepthSlot2 })
        {
            for (int j = 0; j < 8; j++) slots.Add(slot + j);
        }
        for (int i = 0; i < block.Length; i++)
        {
            if (slots.Contains(i)) continue;
            Assert.True(template.Block[i] == block[i],
                $"Surface extrude block mismatch at 0x{i:X}: template=0x{template.Block[i]:X2} authored=0x{block[i]:X2}");
        }
        Assert.Equal(depth, BitConverter.ToDouble(block, template.DepthSlot1), 12);
        Assert.Equal(depth, BitConverter.ToDouble(block, template.DepthSlot2), 12);
    }

    private static void AssertDefinition(Feature feature, double depth)
    {
        var definition = Assert.IsType<SurfaceExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moExtruRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Fu, definition.SerializedTypeId);
        Assert.Equal(depth, definition.Depth!.Value, 12);
        Assert.Null(definition.EndCondition);
        Assert.Null(definition.BothDirections);
        Assert.True(definition.Resolution.Depth);
        Assert.False(definition.Resolution.EndCondition);
    }
}
