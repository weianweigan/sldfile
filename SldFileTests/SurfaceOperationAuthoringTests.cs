using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured authoring tests for knit/extend/offset/fill. Blocks are re-emitted
/// verbatim on their exact stripped bases; Fill ResolutionControl is the only
/// parameterized option slot (1/2).
/// </summary>
public class SurfaceOperationAuthoringTests
{
    [Theory]
    [InlineData("surf_knit_two", "Primary", "moSewRefSurface_c")]
    [InlineData("surf_knit_two_resave", "Resave", "moSewRefSurface_c")]
    [InlineData("surf_extend_5mm", "Primary", "moExtendRefSurface_c")]
    [InlineData("surf_extend_5mm_resave", "Resave", "moExtendRefSurface_c")]
    [InlineData("surf_offset_5mm", "Primary", "moOffsetRefSurface_c")]
    [InlineData("surf_offset_5mm_resave", "Resave", "moOffsetRefSurface_c")]
    [InlineData("surf_fill_region", "Primary", "moFillRefSurface_c")]
    [InlineData("surf_fill_region_resave", "Resave", "moFillRefSurface_c")]
    public void SurfaceOperationAuthoring_ReEmitsMeasuredBlock(string sample, string variant, string className)
    {
        var template = Template(className, variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = AddMeasured(part, className);
            byte[] authored = part.GetStreamBytes(streamPath)!;

            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            for (int i = 0; i < stripped.Length; i++)
            {
                if (i == 4 || i == 5) continue;
                Assert.Equal(stripped[i], authored[i]);
            }
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            AssertDefinition(feature, className);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        AssertDefinition(reFeature, className);
    }

    [Fact]
    public void SurfaceOperationAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        var template = Template("moSewRefSurface_c", "Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_knit_two"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSurfaceKnit(featureName: "MyKnit"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddSurfaceKnit());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void SurfaceOperationAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddFillSurface());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    [Theory]
    [InlineData("surf_fill_region", "Primary", 1)]
    [InlineData("surf_fill_region", "Primary", 2)]
    [InlineData("surf_fill_region_resave", "Resave", 1)]
    [InlineData("surf_fill_region_resave", "Resave", 2)]
    [InlineData("surf_fill_res2", "Res2Primary", 1)]
    [InlineData("surf_fill_res2", "Res2Primary", 2)]
    [InlineData("surf_fill_res2_resave", "Res2Resave", 1)]
    [InlineData("surf_fill_res2_resave", "Res2Resave", 2)]
    public void FillAuthoring_ParameterizesResolutionControl(
        string sample, string variant, int resolutionControl)
    {
        var template = Template("moFillRefSurface_c", variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddFillSurface(resolutionControl);
            byte[] authored = part.GetStreamBytes(streamPath)!;

            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            for (int i = 0; i < stripped.Length; i++)
            {
                if (i == 4 || i == 5) continue;
                Assert.Equal(stripped[i], authored[i]);
            }
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));

            byte[] block = authored.Skip(stripped.Length).ToArray();
            int slot = FindFillResolutionSlot(block);
            Assert.Equal(resolutionControl == 1 ? 0x41 : 0x49, block[slot]);
            for (int i = 0; i < template.Block.Length; i++)
            {
                if (i == slot) continue;
                Assert.Equal(template.Block[i], block[i]);
            }

            var definition = Assert.IsType<FillSurfaceFeatureDefinitionData>(feature.Definition);
            Assert.Equal(resolutionControl, definition.ResolutionControl);
            Assert.True(definition.Resolution.ResolutionControl);
            Assert.Null(definition.Merge);
            Assert.Null(definition.OptimizeSurface);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        var reDefinition = Assert.IsType<FillSurfaceFeatureDefinitionData>(reFeature.Definition);
        Assert.Equal(resolutionControl, reDefinition.ResolutionControl);
        Assert.Null(reDefinition.Merge);
        Assert.Null(reDefinition.PatchBoundaryCount);
        Assert.Null(reDefinition.BoundaryCurvatureControls);
    }

    [Fact]
    public void FillAuthoring_RejectsInvalidControlWrongBaseAndCustomName_WithoutChangingStream()
    {
        var template = Template("moFillRefSurface_c", "Primary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;

            Assert.Throws<ArgumentOutOfRangeException>(() => part.AddFillSurface(0));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<ArgumentOutOfRangeException>(() => part.AddFillSurface(3));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() =>
                part.AddFillSurface(1, featureName: "MyFill"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }

        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region")))
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] original = (byte[])part.GetStreamBytes(streamPath)!.Clone();
            Assert.Throws<NotSupportedException>(() => part.AddFillSurface(2));
            Assert.Equal(original, part.GetStreamBytes(streamPath));
        }

        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            stripped[stripped.Length / 2] ^= 0x01;
            part.SetPendingStream(streamPath, stripped);
            Assert.Throws<NotSupportedException>(() => part.AddFillSurface(2));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }

        using var readOnlyTemp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region"));
        using var readOnly = new SldPart(readOnlyTemp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => readOnly.AddFillSurface(1));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int FindFillResolutionSlot(byte[] block)
    {
        const string className = "moFillRefSurface_c";
        int dataStart = 6 + className.Length;
        int limit = Math.Min(block.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !SldFile.Mfc.MfcPrimitives.IsWCharPrefix(block, offset))
            offset++;
        int cursor = offset;
        Assert.NotNull(SldFile.Mfc.MfcPrimitives.ReadWChar(block, ref cursor, block.Length));
        int header = cursor;
        Assert.Equal(0x00, block[header + 0x300]);
        Assert.Equal(0xF0, block[header + 0x301]);
        Assert.Equal(0xBF, block[header + 0x302]);
        Assert.Equal(0x82, block[header + 0x308]);
        return header + 0x307;
    }

    private static SurfaceOperationAuthoringTemplate Template(string className, string variant) =>
        SurfaceOperationAuthoringTemplates.All.Concat(FillSurfaceAuthoringTemplates.All).Single(t =>
            string.Equals(t.ClassName, className, StringComparison.Ordinal) &&
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, SurfaceOperationAuthoringTemplate template)
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

    private static Feature AddMeasured(SldPart part, string className) => className switch
    {
        "moSewRefSurface_c" => part.AddSurfaceKnit(),
        "moExtendRefSurface_c" => part.AddSurfaceExtend(),
        "moOffsetRefSurface_c" => part.AddSurfaceOffset(),
        "moFillRefSurface_c" => part.AddFillSurface(),
        _ => throw new InvalidOperationException(className)
    };

    private static void AssertDefinition(Feature feature, string className)
    {
        Assert.Equal(className, feature.Definition!.ClassName);
        switch (className)
        {
            case "moSewRefSurface_c":
                Assert.Single(Assert.IsType<SurfaceKnitFeatureDefinitionData>(feature.Definition).Entities!);
                break;
            case "moExtendRefSurface_c":
                Assert.Single(Assert.IsType<SurfaceExtendFeatureDefinitionData>(feature.Definition).Items!);
                break;
            case "moOffsetRefSurface_c":
                Assert.Single(Assert.IsType<SurfaceOffsetFeatureDefinitionData>(feature.Definition).Entities!);
                break;
            case "moFillRefSurface_c":
                Assert.IsType<FillSurfaceFeatureDefinitionData>(feature.Definition);
                break;
        }
    }
}
