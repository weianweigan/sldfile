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
/// Native variable-fillet pairs 1/3, 2/4 and 3/5 mm, source and resave. The measured blocks
/// differ in derived control-point/session fields, so authoring carries an exact native block
/// per base signature rather than scaling only the radius values.
/// </summary>
public class NativeVariableFilletAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "FilletOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeBlock)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = sw.ResolvedFeatures.RawData!;
        var folders = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current)
            {
                folders.Add(folder);
                Walk(folder.Children);
            }
        }
        Walk(sw.ResolvedFeatures.Folders);
        var feature = folders.First(f => f.ClassName == "VarFillet_c");
        nativeBlock = raw.AsSpan(feature.TagOffset).ToArray();
        byte[] stripped = raw.AsSpan(0, feature.TagOffset).ToArray();
        ushort count = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static VariableFilletFeatureModel Definition(double radius0, double radius1)
    {
        var d = new VariableFilletFeatureModel();
        d.SetControlPointRadii(new[] { radius0, radius1 });
        return d;
    }

    [Theory]
    [InlineData("fillet_variable_native_1_3", 0.001, 0.003)]
    [InlineData("fillet_variable_native_1_3_resave", 0.001, 0.003)]
    [InlineData("fillet_variable_native_2_4", 0.002, 0.004)]
    [InlineData("fillet_variable_native_2_4_resave", 0.002, 0.004)]
    [InlineData("fillet_variable_native_3_5", 0.003, 0.005)]
    [InlineData("fillet_variable_native_3_5_resave", 0.003, 0.005)]
    public void NativeVariableFillet_AuthoringReproducesNativeBlock(
        string sample, double radius0, double radius1)
    {
        using var temp = CreateStrippedBase(sample, out byte[] nativeBlock);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddVariableFillet(Definition(radius0, radius1));
            Assert.Equal("VariableFillet", feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[ResolvedStream];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var featureElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "VarFillet_c"));
        Assert.Equal(nativeBlock, stream.AsSpan(featureElement.TagOffset).ToArray());

        var definition = Assert.IsType<VariableFilletFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet)).Model);
        Assert.Equal(new[] { radius0, radius1 }, definition.ControlPointRadii!.ToArray());
        Assert.True(definition.IsResolved(nameof(VariableFilletFeatureModel.ControlPointRadii)));
    }

    [Theory]
    [InlineData("fillet_variable_native_1_3", 0.001, 0.003)]
    [InlineData("fillet_variable_native_1_3_resave", 0.001, 0.003)]
    [InlineData("fillet_variable_native_2_4", 0.002, 0.004)]
    [InlineData("fillet_variable_native_2_4_resave", 0.002, 0.004)]
    [InlineData("fillet_variable_native_3_5", 0.003, 0.005)]
    [InlineData("fillet_variable_native_3_5_resave", 0.003, 0.005)]
    public void NativeVariableFillet_ReadingPairs(
        string sample, double radius0, double radius1)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var definition = Assert.IsType<VariableFilletFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet)).Model);
        Assert.Equal(new[] { radius0, radius1 }, definition.ControlPointRadii!.ToArray());
        Assert.Equal(2, definition.ControlPointCount);
        Assert.True(definition.IsResolved(nameof(VariableFilletFeatureModel.ControlPointRadii)));
    }

    [Fact]
    public void NativeVariableFillet_RejectsMismatchedPairBaseAndName()
    {
        using var temp = CreateStrippedBase("fillet_variable_native_1_3", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        byte[] before = (byte[])part.RawStreams[ResolvedStream].Clone();

        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition(0.002, 0.004)));
        Assert.Equal(before, part.RawStreams[ResolvedStream]);
        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition(0.001, 0.004)));
        Assert.Throws<NotSupportedException>(() =>
            part.AddVariableFillet(Definition(0.001, 0.003), "Custom"));
        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(
            new VariableFilletFeatureModel { DefaultRadius = 0.002 }));
        Assert.Equal(before, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void NativeVariableFillet_RejectsWrongFolderMarkerAndSignature()
    {
        using var temp = CreateStrippedBase("fillet_variable_native_3_5", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] before = (byte[])sw.RawStreams[ResolvedStream].Clone();

        byte[] wrongFolder = (byte[])before.Clone();
        BitConverter.GetBytes((ushort)19).CopyTo(wrongFolder, 4);
        sw.SetPendingStream(ResolvedStream, wrongFolder);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddVariableFillet(Definition(0.003, 0.005)));

        byte[] wrongMarker = (byte[])before.Clone();
        int marker = FindMarker(wrongMarker);
        Assert.True(marker > 0);
        BitConverter.GetBytes(0u).CopyTo(wrongMarker, marker + 4);
        sw.SetPendingStream(ResolvedStream, wrongMarker);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddVariableFillet(Definition(0.003, 0.005)));

        byte[] wrongBase = (byte[])before.Clone();
        wrongBase[wrongBase.Length - 1] ^= 0xFF;
        sw.SetPendingStream(ResolvedStream, wrongBase);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddVariableFillet(Definition(0.003, 0.005)));
    }

    private static int FindMarker(byte[] stream)
    {
        for (int i = 0; i <= stream.Length - 8; i++)
            if (stream[i] == 0x67 && stream[i + 1] == 0 && stream[i + 2] == 0 &&
                stream[i + 3] == 0 && BitConverter.ToUInt32(stream, i + 4) == 1)
                return i;
        return -1;
    }
}
