using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading and fixed-authoring tests for the measured 2026 360-degree surface revolve.
/// Only angle and type code are decoded; partial angles/type/reverse have no samples.
/// </summary>
public class SurfaceRevolveFeatureTests
{
    [Theory]
    [InlineData("surf_revolve_360")]
    [InlineData("surf_revolve_360_resave")]
    public void SurfaceRevolveSample_DecodesAngle(string name)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "SurfaceRevolve360"));
        Assert.Equal(FeatureKind.SurfaceRevolve, feature.Kind);
        var definition = Assert.IsType<SurfaceRevolveFeatureModel>(feature.Model);

        Assert.Equal("moRevolvRefSurf_c", definition.ClassName);
        Assert.Equal(0x4Eu, definition.SerializedTypeId);
        Assert.Equal(2 * Math.PI, definition.Angle!.Value, 12);
        Assert.Null(definition.Type);
        Assert.Null(definition.ReverseDirection);
        Assert.True(definition.Resolution.Angle);
        Assert.False(definition.Resolution.Type);
        Assert.False(definition.Resolution.ReverseDirection);
    }

    [Fact]
    public void SurfaceRevolveParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildSurfaceRevolveStream(new byte[64]);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal("moRevolvRefSurf_c", folder.ClassName);
        Assert.Null(folder.FeatureData);

        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moRevolvRefSurf_c");
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void SurfaceRevolveParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("surf_revolve_360"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moRevolvRefSurf_c");

        Assert.NotNull(SurfaceRevolveFeatureParser.Parse(element));
        Assert.Equal(before, stream);
    }

    [Theory]
    [InlineData("surf_revolve_360", "Primary")]
    [InlineData("surf_revolve_360_resave", "Resave")]
    public void SurfaceRevolveAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        var template = Template(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSurfaceRevolve();
            byte[] authored = part.GetStreamBytes(streamPath)!;

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
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            AssertDefinition(feature);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "SurfaceRevolve360"));
        Assert.Equal(FeatureKind.SurfaceRevolve, reFeature.Kind);
        AssertDefinition(reFeature);
    }

    [Fact]
    public void SurfaceRevolveAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_revolve_360"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSurfaceRevolve(featureName: "MyRevolve"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddSurfaceRevolve());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void SurfaceRevolveAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_revolve_360"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSurfaceRevolve());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SurfaceRevolveAuthoringTemplate Template(string variant) =>
        SurfaceRevolveAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, SurfaceRevolveAuthoringTemplate template)
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

    private static void AssertDefinition(Feature feature)
    {
        var definition = Assert.IsType<SurfaceRevolveFeatureModel>(feature.Model);
        Assert.Equal("moRevolvRefSurf_c", definition.ClassName);
        Assert.Equal(0x4Eu, definition.SerializedTypeId);
        Assert.Equal(2 * Math.PI, definition.Angle!.Value, 12);
        Assert.Null(definition.Type);
        Assert.Null(definition.ReverseDirection);
        Assert.True(definition.Resolution.Angle);
    }

    private static byte[] BuildSurfaceRevolveStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moRevolvRefSurf_c");
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x11 });
        bytes.AddRange(Encoding.Unicode.GetBytes("SurfaceRevolve360"));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
