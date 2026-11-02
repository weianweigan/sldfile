using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed-measurement variable-radius fillet authoring: the complete measured VarFillet_c block
/// (one edge, D0=0.002 m, D01=0.004 m) on the exact source or resave stripped base.
/// </summary>
public class VariableFilletAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "FilletOptions", sample + ".SLDPRT");

    private static VariableFilletFeatureDefinitionData Definition(double first = 0.002, double second = 0.004)
    {
        var definition = new VariableFilletFeatureDefinitionData();
        definition.SetControlPointRadii(new[] { first, second });
        return definition;
    }

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeStream)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            nativeStream = raw;
            var doc = MfcDocument.Parse(raw);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "VarFillet_c"));
            int tag = feature.TagOffset;
            byte[] stripped = new byte[tag];
            Array.Copy(raw, 0, stripped, 0, tag);
            BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(stripped, 4) - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using var check = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.True(check.RawStreams[ResolvedStream].Length < nativeStream.Length);
        return temp;
    }

    private static VariableFilletFeatureDefinitionData ReadFillet(SwFile file)
    {
        var feature = Assert.Single(file.Features.Where(f => f.TypeName == "VarFillet_c"));
        Assert.Equal(FeatureKind.Fillet, feature.Kind);
        return Assert.IsType<VariableFilletFeatureDefinitionData>(feature.Definition);
    }

    [Theory]
    [InlineData("fillet_variable_2_4mm")]
    [InlineData("fillet_variable_2_4mm_resave")]
    public void NativeValuesReproduceTheMeasuredFile(string sample)
    {
        using var temp = CreateStrippedBase(sample, out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddVariableFillet(Definition());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = ReadFillet(reopened);
        Assert.Equal(2, definition.ControlPointCount);
        Assert.Equal(0.002, definition.GetControlPointRadiusAtIndex(0)!.Value, 12);
        Assert.Equal(0.004, definition.GetControlPointRadiusAtIndex(1)!.Value, 12);

        byte[] before;
        using (var resave = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])resave.RawStreams[ResolvedStream].Clone();
            resave.Save();
        }
        using var after = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, after.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void RadiusListsMustMatchTheMeasuredTwoFourMillimetreControl()
    {
        using var temp = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var wrong = Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition(0.001, 0.003)));
        Assert.Contains("2/4", wrong.Message);

        var one = new VariableFilletFeatureDefinitionData();
        one.SetControlPointRadii(new[] { 0.002 });
        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(one));

        var three = new VariableFilletFeatureDefinitionData();
        three.SetControlPointRadii(new[] { 0.002, 0.004, 0.006 });
        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(three));
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void EdgeVertexAndTransitionOverridesAreRejected()
    {
        using var temp = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var edges = Definition();
        edges.FilletEdgeCount = 2;
        Assert.Contains("single edge",
            Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(edges)).Message);

        var edgeRefs = Definition();
        edgeRefs.Edges = new[] { new FeatureReferenceData() };
        Assert.Contains("edge overrides",
            Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(edgeRefs)).Message);

        var defaults = Definition();
        defaults.DefaultRadius = 0.001;
        Assert.Contains("default",
            Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(defaults)).Message);

        var transition = Definition();
        transition.TransitionType = 1;
        Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(transition));

        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void CustomNameWrongBaseAndTamperedSignatureAreRejected()
    {
        using var temp = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var nameError = Assert.Throws<NotSupportedException>(
                () => part.AddVariableFillet(Definition(), featureName: "CustomVarFillet"));
            Assert.Contains("VariableFillet", nameError.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var other = TestFileProvider.CreateTempCopy(
            Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT"));
        using (var sw = SwFile.Open(other.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition()));
            Assert.Contains("stripped base length", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var modified = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using (var sw = SwFile.Open(modified.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            raw[100] ^= 0xFF;
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using (var sw = SwFile.Open(modified.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition()));
            Assert.Contains("signature", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void WrongFolderCountOrMarkerIsRejectedBeforeMutation()
    {
        using var temp = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            BitConverter.GetBytes((ushort)21).CopyTo(raw, 4);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition()));
            Assert.Contains("folder count", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var markerTemp = CreateStrippedBase("fillet_variable_2_4mm", out _);
        using (var sw = SwFile.Open(markerTemp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            int marker = -1;
            for (int i = 0; i <= raw.Length - 8; i++)
            {
                if (raw[i] == 0x67 && raw[i + 1] == 0 && raw[i + 2] == 0 && raw[i + 3] == 0 &&
                    BitConverter.ToUInt32(raw, i + 4) == 1)
                {
                    marker = i;
                    break;
                }
            }
            Assert.True(marker > 0);
            BitConverter.GetBytes(2u).CopyTo(raw, marker + 4);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using (var sw = SwFile.Open(markerTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddVariableFillet(Definition()));
            Assert.Contains("0x67", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }
}
