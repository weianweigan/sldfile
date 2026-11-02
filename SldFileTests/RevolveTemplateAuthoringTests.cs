using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Template authoring for the measured thin/both-directions/cut revolutions. Each layout appends the
/// complete native block to its exact stripped base; only measured parameter slots are patched.
/// </summary>
public class RevolveTemplateAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "RevolveOptions", sample + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, string className, out byte[] nativeStream)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            nativeStream = raw;
            var doc = MfcDocument.Parse(raw);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == className));
            int tag = feature.TagOffset;
            byte[] stripped = new byte[tag];
            Array.Copy(raw, 0, stripped, 0, tag);
            BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(stripped, 4) - 1)).CopyTo(stripped, 4);
            int marker = tag - 0x10;
            Assert.Equal(0x67, stripped[marker]);
            BitConverter.GetBytes(0u).CopyTo(stripped, marker + 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using var check = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.True(check.RawStreams[ResolvedStream].Length < nativeStream.Length);
        return temp;
    }

    private static RevolveFeatureDefinitionData Thin(double angle, double thickness = 0.001)
    {
        return new RevolveFeatureDefinitionData
        {
            IsThinFeature = true,
            ThinWallType = 0,
            Angle = angle,
            Thickness1 = thickness,
            Thickness2 = 0,
            Type = 0,
            Merge = true
        };
    }

    private static RevolveFeatureDefinitionData Both(double angle1 = Math.PI / 2.0, double angle2 = Math.PI / 4.0)
    {
        return new RevolveFeatureDefinitionData
        {
            BothDirections = true,
            Type = 2,
            Angle = angle1,
            AngleDirection2 = angle2,
            Merge = true
        };
    }

    private static RevolveFeatureDefinitionData Cut()
    {
        return new RevolveFeatureDefinitionData
        {
            IsCut = true,
            Angle = 2.0 * Math.PI,
            Type = 0,
            Merge = true
        };
    }

    [Theory]
    [InlineData("revolve_thin_90", Math.PI / 2.0)]
    [InlineData("revolve_thin_360", 2.0 * Math.PI)]
    public void ThinNativeValuesReproduceTheMeasuredFile(string sample, double angle)
    {
        using var temp = CreateStrippedBase(sample, "moRevolutionThin_c", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(Thin(angle));
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<RevolveFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Revolve)).Definition);
        Assert.True(definition.IsThinFeature);
        Assert.Equal(angle, definition.Angle, 12);
        Assert.Equal(0.001, definition.Thickness1, 12);
    }

    [Fact]
    public void ThinThicknessIsPatchedAndSurvivesResave()
    {
        using var temp = CreateStrippedBase("revolve_thin_90", "moRevolutionThin_c", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(Thin(Math.PI / 2.0, 0.002));
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<RevolveFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Revolve)).Definition);
        Assert.Equal(0.002, definition.Thickness1, 12);

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
    public void BothDirectionsReproducesNativeAndPatchesBothAngles()
    {
        using var temp = CreateStrippedBase("revolve_both_directions", "moRevolution_c", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(Both());
            part.Save();
        }
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] stream = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(stream);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moRevolution_c"));
            byte[] stripped = new byte[feature.TagOffset];
            Array.Copy(stream, 0, stripped, 0, stripped.Length);
            BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(stripped, 4) - 1)).CopyTo(stripped, 4);
            BitConverter.GetBytes(0u).CopyTo(stripped, feature.TagOffset - 0x10 + 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(Both(0.5, 0.25));
            part.Save();
        }
        using var changed = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<RevolveFeatureDefinitionData>(
            Assert.Single(changed.Features.Where(f => f.Kind == FeatureKind.Revolve)).Definition);
        Assert.Equal(0.5, definition.Angle, 12);
        Assert.Equal(0.25, definition.AngleDirection2, 12);
    }

    [Fact]
    public void CutNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("revolve_cut_360", "moRevCut_c", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(Cut());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<RevolveFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.TypeName == "moRevCut_c")).Definition);
        Assert.True(definition.IsCut);
        Assert.Equal(2.0 * Math.PI, definition.Angle, 12);
    }

    [Fact]
    public void MismatchedTemplateBasesAreRejected()
    {
        using var temp = CreateStrippedBase("revolve_thin_90", "moRevolutionThin_c", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var thin360 = Thin(2.0 * Math.PI);
        var angleError = Assert.Throws<NotSupportedException>(() => part.AddRevolve(thin360));
        Assert.Contains("signature", angleError.Message);

        var both = Both();
        Assert.Throws<NotSupportedException>(() => part.AddRevolve(both));
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void UnsupportedThinAndCutAnglesAreRejectedWithoutMutation()
    {
        using var thinTemp = CreateStrippedBase("revolve_thin_90", "moRevolutionThin_c", out _);
        using (var sw = SwFile.Open(thinTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddRevolve(Thin(Math.PI)));
            Assert.Contains("90-degree", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var cutTemp = CreateStrippedBase("revolve_cut_360", "moRevCut_c", out _);
        using (var sw = SwFile.Open(cutTemp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var cut = Cut();
            cut.Angle = Math.PI / 2.0;
            var error = Assert.Throws<NotSupportedException>(() => part.AddRevolve(cut));
            Assert.Contains("360-degree", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void CustomFeatureNamesAreRejectedBeforeMutation()
    {
        using var temp = CreateStrippedBase("revolve_both_directions", "moRevolution_c", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();
        var error = Assert.Throws<NotSupportedException>(
            () => part.AddRevolve(Both(), featureName: "CustomBoth"));
        Assert.Contains("BothDirectionsRevolve", error.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }
}
