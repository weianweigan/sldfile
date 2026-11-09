using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the measured 2026 Shell samples. Only thickness, one removed-face
/// record and the type code are decoded; direction/multi-thickness remain unresolved.
/// </summary>
public class ShellFeatureReadingTests
{
    [Theory]
    [InlineData("shell_t2_remove_top", 0.002)]
    [InlineData("shell_t2_remove_top_resave", 0.002)]
    [InlineData("shell_t3_remove_top", 0.003)]
    [InlineData("shell_t3_remove_top_resave", 0.003)]
    public void ShellSample_DecodesThicknessAndRemovedFace(string name, double thickness)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "Shell"));
        Assert.Equal(FeatureKind.Shell, feature.Kind);
        var definition = Assert.IsType<ShellFeatureModel>(feature.Model);

        Assert.Equal("moShell_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(0x51u, definition.TypeId);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
        Assert.Null(definition.Direction);
        Assert.Null(definition.MultipleThicknessFaces);
        Assert.Null(definition.MultipleThicknessValues);

        var face = Assert.Single(definition.FacesRemoved!);
        Assert.Equal(SelectionEntityType.Face, face.EntityType);
        Assert.True(face.EntityId > 0);
        Assert.Equal(1, definition.FacesRemovedCount);
        Assert.StartsWith("moCompFace_c:", face.Name);

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.Resolution.FacesRemoved);
        Assert.False(definition.Resolution.Direction);
        Assert.False(definition.Resolution.MultipleThicknessFaces);
        Assert.False(definition.Resolution.MultipleThicknessValues);
    }

    [Fact]
    public void ShellParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildShellStream(new byte[64]);
        var document = MfcDocument.Parse(stream);
        var element = Assert.Single(document.AllElements.Where(e => e.ClassName == "moShell_c"));

        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);
        var folder = Assert.Single(model.Folders);
        Assert.Equal("moShell_c", folder.ClassName);
        Assert.Null(folder.FeatureData);
    }

    [Fact]
    public void ShellParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("shell_t2_remove_top"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var document = MfcDocument.Parse(stream);
        var element = document.AllElements.First(e => e.ClassName == "moShell_c");

        Assert.NotNull(ShellFeatureModel.Parse(element));
        Assert.Equal(before, stream);
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/ShellOptions", name + ".SLDPRT");

    private static byte[] BuildShellStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moShell_c");
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x15, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x05 });
        bytes.AddRange(Encoding.Unicode.GetBytes("Shell"));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
