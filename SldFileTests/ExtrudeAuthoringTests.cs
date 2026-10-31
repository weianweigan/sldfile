using System;
using System.IO;
using System.Linq;
using Xunit;
using SldFile;

namespace SldFileTests;

public class ExtrudeAuthoringTests
{
    [Fact]
    public void ExtrudeFeatureDefinitionData_FullPropertyMapping_RoundTrip()
    {
        var data = new ExtrudeFeatureDefinitionData();

        // 1. Depth & EndCondition
        data.SetDepth(true, 0.025);
        data.SetDepth(false, 0.015);
        Assert.Equal(0.025, data.GetDepth(true));
        Assert.Equal(0.015, data.GetDepth(false));
        Assert.Equal(0.025, data.Depth);
        Assert.Equal(0.015, data.DepthDirection2);

        data.SetEndCondition(true, 6); // MidPlane
        data.SetEndCondition(false, 0); // Blind
        Assert.Equal(6u, data.GetEndCondition(true));
        Assert.Equal(0u, data.GetEndCondition(false));
        Assert.Equal(6u, data.EndCondition);
        Assert.Equal(0u, data.EndConditionDirection2);

        // 2. Draft
        data.SetDraftAngle(true, 2.5);
        data.SetDraftAngle(false, 1.5);
        Assert.Equal(2.5, data.GetDraftAngle(true));
        Assert.Equal(1.5, data.GetDraftAngle(false));

        data.SetDraftOutward(true, true);
        data.SetDraftOutward(false, false);
        Assert.True(data.GetDraftOutward(true));
        Assert.False(data.GetDraftOutward(false));

        data.SetDraftWhileExtruding(true, true);
        data.SetDraftWhileExtruding(false, false);
        Assert.True(data.GetDraftWhileExtruding(true));
        Assert.False(data.GetDraftWhileExtruding(false));

        // 3. Wall thickness & Thin feature
        data.SetWallThickness(true, 0.002);
        data.SetWallThickness(false, 0.001);
        Assert.Equal(0.002, data.GetWallThickness(true));
        Assert.Equal(0.001, data.GetWallThickness(false));

        // 4. Offset & Surface options
        data.SetReverseOffset(true, true);
        data.SetReverseOffset(false, false);
        Assert.True(data.GetReverseOffset(true));
        Assert.False(data.GetReverseOffset(false));

        data.SetTranslateSurface(true, true);
        data.SetTranslateSurface(false, false);
        Assert.True(data.GetTranslateSurface(true));
        Assert.False(data.GetTranslateSurface(false));

        // 5. Boss vs Cut
        data.IsCut = false;
        Assert.True(data.IsBossFeature());
        data.IsCut = true;
        Assert.False(data.IsBossFeature());

        // 6. Advanced options
        data.LinkToThickness = true;
        Assert.True(data.LinkToThickness);
        data.AutoFilletCorners = true;
        data.AutoFilletRadius = 0.003;
        Assert.True(data.AutoFilletCorners);
        Assert.Equal(0.003, data.AutoFilletRadius);
    }

    [Fact]
    public void SldPart_AddExtrude_CreatesFeatureAndInjectsStream()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(extPath);

        // 1. Strip the original extrusion to produce a sketch-only baseline
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = sw.ResolvedFeatures.RawData!;
            byte[] sketchOnly = new byte[0x22E0];
            Array.Copy(raw, 0, sketchOnly, 0, 0x22E0);

            ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
            sketchOnly[0x22D4] = 0x00;

            sw.SetPendingStream("Contents/Config-0-ResolvedFeatures", sketchOnly);
            sw.Save();
        }

        // 2. Open via SldPart and add new 35mm extrusion
        double newDepth = 0.035;
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feat = part.AddExtrude(newDepth);
            Assert.NotNull(feat);
            Assert.Equal("凸台-拉伸1", feat.Name);
            Assert.Equal(FeatureKind.Extrusion, feat.Kind);
            part.Save();
        }

        // 3. Re-open and assert SldFile parses the injected feature with 35mm depth
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var feat = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
            Assert.Equal("凸台-拉伸1", feat.Name);
            var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feat.Definition);
            Assert.Equal(0u, def.EndCondition);
            Assert.False(def.IsCut);
            Assert.True(def.IsBossFeature());
        }
    }

    [Fact]
    public void CompareSynthesizedBlockToOriginalSample()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        using var sw = SwFile.Open(extPath, readOnly: true);
        var raw = sw.ResolvedFeatures.RawData!;

        // The original extrusion starts at 0x22E0
        byte[] originalExtrude = new byte[raw.Length - 0x22E0];
        Array.Copy(raw, 0x22E0, originalExtrude, 0, originalExtrude.Length);

        var def = new ExtrudeFeatureDefinitionData { Depth = 0.010, EndCondition = 0, BothDirections = false };
        byte[] synthesized = SldFile.Binary.FeatureData.ExtrudeFeatureBuilder.BuildBossExtrudeBlock(def, "凸台-拉伸1", 4);

        Assert.Equal(originalExtrude.Length, synthesized.Length);
        Assert.Equal(originalExtrude, synthesized);
    }
}
