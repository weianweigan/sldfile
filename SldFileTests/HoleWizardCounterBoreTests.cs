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
/// Native API-created ISO M6 socket-head-cap counterbore (Ø6 through, Ø11 x 6 counterbore,
/// through-all). This is a real persistent counterbore feature, not the earlier drill-only
/// legacy counterbore attempts.
/// </summary>
public class HoleWizardCounterBoreTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";
    private const string Sample = "hole_counterbore_iso_m6_11_6";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static HoleWizardFeatureModel ReadDefinition(string name)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        return Assert.IsType<HoleWizardFeatureModel>(feature.Model);
    }

    [Fact]
    public void CounterBore_ReadsMeasuredIsoM6Fields()
    {
        var d = ReadDefinition(Sample);
        Assert.Equal(0x52u, d.FolderTypeId);
        Assert.Equal(14, d.Type);
        Assert.Equal(1, d.EndCondition);
        Assert.Equal("ISO", d.Standard);
        Assert.Equal(8, d.Standard2);
        Assert.Equal("六角凹头 ISO 4762", d.FastenerType);
        Assert.Equal(139, d.FastenerType2);
        Assert.Equal("M6", d.FastenerSize);
        Assert.Equal(0.0, d.HoleDiameter!.Value, 12);
        Assert.Equal(0.0, d.Depth!.Value, 12);
        Assert.Equal(0.0, d.HoleDepth!.Value, 12);
        Assert.Equal(0.006, d.ThruHoleDiameter!.Value, 12);
        Assert.Equal(0.010, d.ThruHoleDepth!.Value, 12);
        Assert.Equal(0.011, d.CounterBoreDiameter!.Value, 12);
        Assert.Equal(0.006, d.CounterBoreDepth!.Value, 12);
        Assert.Equal(0.0, d.CounterSinkDiameter!.Value, 12);
        Assert.Equal(0.0, d.CounterSinkAngle!.Value, 12);
        Assert.Equal(0.0, d.ThreadDiameter!.Value, 12);
        Assert.Equal(0.0, d.ThreadDepth!.Value, 12);
        Assert.Equal(1, d.GetSketchPointCount());
        var point = Assert.Single(d.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureModel.Type),
            nameof(HoleWizardFeatureModel.Standard),
            nameof(HoleWizardFeatureModel.FastenerType),
            nameof(HoleWizardFeatureModel.ThruHoleDiameter),
            nameof(HoleWizardFeatureModel.ThruHoleDepth),
            nameof(HoleWizardFeatureModel.CounterBoreDiameter),
            nameof(HoleWizardFeatureModel.CounterBoreDepth),
            nameof(HoleWizardFeatureModel.SketchPointLocations)
        })
            Assert.True(d.IsResolved(name), name);
    }

    [Fact]
    public void CounterBoreParser_RejectsTamperingAndWrongVersion()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath(Sample), readOnly: true))
            original = (byte[])file.RawStreams[StreamPath].Clone();
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "moHoleWzd_c");
        Assert.NotNull(HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWzd_c", 0x52, feature.TagOffset, original.Length));

        void Tamper(Action<byte[], MfcDocument> mutate)
        {
            byte[] copy = (byte[])original.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            mutate(copy, copyDoc);
            var tampered = MfcDocument.Parse(copy);
            var f = tampered.AllElements.First(e => e.ClassName == "moHoleWzd_c");
            Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
                tampered, "moHoleWzd_c", 0x52, f.TagOffset, copy.Length));
        }

        Tamper((b, d) =>
        {
            var body = d.AllElements.First(e => e.ClassName == "moCompSolidBody_c");
            b[body.DataStart + HoleWizardSegments.CounterBoreStandardOffset + 20] ^= 0xFF;
        });
        Tamper((b, d) =>
        {
            var body = d.AllElements.First(e => e.ClassName == "moCompSolidBody_c");
            BitConverter.GetBytes(9u).CopyTo(b, body.DataStart + HoleWizardSegments.CounterBoreStandard2Offset);
        });
        Tamper((b, d) =>
        {
            var body = d.AllElements.First(e => e.ClassName == "moCompSolidBody_c");
            BitConverter.GetBytes(140u).CopyTo(b, body.DataStart + HoleWizardSegments.CounterBoreFastenerType2Offset);
        });
        Tamper((b, d) =>
        {
            var angle = d.AllElements.First(e => e.ClassName == "moAngleParameter_c");
            byte[] payload = MfcPrimitives.CopyRange(b, angle.DataStart, angle.DataStart +
                HoleWizardSegments.CounterBoreAngleParameterLength);
            int valueOffset = FindNamedValue(payload, HoleWizardSegments.CounterBoreDiameterName);
            Assert.True(valueOffset > 0);
            BitConverter.GetBytes(0.012).CopyTo(b, angle.DataStart + valueOffset);
        });
        Tamper((b, d) =>
        {
            var feature2 = d.AllElements.First(e => e.ClassName == "moHoleWzd_c");
            b[feature2.DataStart + 2] ^= 0xFF; // schema
        });

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)0x84).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "moHoleWzd_c");
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            wrongDoc, "moHoleWzd_c", 0x52, wrongFeature.TagOffset, wrongVersion.Length));
    }

    [Fact]
    public void CounterBoreAuthoring_ReproducesNativeBlockAndRoundTrips()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath(Sample));
        byte[] nativeBlock;
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = sw.ResolvedFeatures.RawData!;
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> current)
            {
                foreach (var folder in current) { folders.Add(folder); Walk(folder.Children); }
            }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
            nativeBlock = raw.AsSpan(hole.TagOffset).ToArray();
            byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(StreamPath, stripped);
            sw.Save();
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(new HoleWizardFeatureModel
            {
                Type = 14,
                CounterBoreDiameter = 0.011,
                CounterBoreDepth = 0.006
            });
            Assert.Equal(HoleWizardCounterBoreAuthoringTemplates.FeatureName, feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[StreamPath];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var holeElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        Assert.Equal(nativeBlock, stream.AsSpan(holeElement.TagOffset).ToArray());
        var read = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(0.011, read.CounterBoreDiameter!.Value, 12);
        Assert.Equal(139, read.FastenerType2);
    }

    [Fact]
    public void CounterBoreAuthoring_RejectsMismatchedFieldsAndBases()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath(Sample));
        Reject(temp, new HoleWizardFeatureModel { Type = 14, CounterBoreDiameter = 0.012 });
        Reject(temp, new HoleWizardFeatureModel { Type = 14, CounterBoreDepth = 0.005 });
        Reject(temp, new HoleWizardFeatureModel { Type = 14, Standard = "ANSI" });
        Reject(temp, new HoleWizardFeatureModel { Type = 14, FastenerSize = "M8" });
        Reject(temp, new HoleWizardFeatureModel { Type = 14, CounterBoreDiameter = 0.011,
            CounterBoreDepth = 0.006, Standard = "ISO" }, "Custom");
        Reject(temp, new HoleWizardFeatureModel { Type = 14, ThreadDiameter = 0.006 });

        using var legacy = TestFileProvider.CreateTempCopy(SamplePath("hole_simple_d5_throughall"));
        using (var sw = SwFile.Open(legacy.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(
                new HoleWizardFeatureModel { Type = 14, CounterBoreDiameter = 0.011 }));
        }

        static void Reject(TempTestFile temp, HoleWizardFeatureModel definition, string? name = null)
        {
            // Use a fresh stripped copy per rejection so earlier failures cannot mutate the base.
            string copy = temp.FilePath + "." + Guid.NewGuid().ToString("N") + ".SLDPRT";
            File.Copy(temp.FilePath, copy, true);
            try
            {
                using (var sw = SwFile.Open(copy, readOnly: false))
                {
                    byte[] raw = sw.ResolvedFeatures.RawData!;
                    var folders = new List<FeatureFolder>();
                    void Walk(IEnumerable<FeatureFolder> current)
                    {
                        foreach (var folder in current) { folders.Add(folder); Walk(folder.Children); }
                    }
                    Walk(sw.ResolvedFeatures.Folders);
                    var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
                    byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
                    ushort count = BitConverter.ToUInt16(stripped, 4);
                    BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
                    sw.SetPendingStream(StreamPath, stripped);
                    sw.Save();
                }
                using (var sw = SwFile.Open(copy, readOnly: false))
                {
                    var part = Assert.IsType<SldPart>(sw);
                    byte[] before = (byte[])part.RawStreams[StreamPath].Clone();
                    Assert.ThrowsAny<Exception>(() => part.AddHoleWizard(definition, name));
                    Assert.Equal(before, part.RawStreams[StreamPath]);
                }
            }
            finally { try { File.Delete(copy); } catch { } }
        }
    }

    [Theory]
    [InlineData("hole_counterbore_ansi_metric_m6_11_6")]
    [InlineData("hole_counterbore_ansi_metric_m6_11_6_resave")]
    public void AnsiCounterBore_ReadsSecondMeasuredTemplate(string sample)
    {
        var d = ReadDefinition(sample);
        Assert.Equal(0x52u, d.FolderTypeId);
        Assert.Equal(14, d.Type);
        Assert.Equal(1, d.EndCondition);
        Assert.Equal("ANSI Metric", d.Standard);
        Assert.Equal(1, d.Standard2);
        Assert.Equal("凹头盖螺钉 - ANSI B18.3.1M", d.FastenerType);
        Assert.Equal(33, d.FastenerType2);
        Assert.Equal("M6", d.FastenerSize);
        Assert.Equal(0.006, d.ThruHoleDiameter!.Value, 12);
        Assert.Equal(0.010, d.ThruHoleDepth!.Value, 12);
        Assert.Equal(0.011, d.CounterBoreDiameter!.Value, 12);
        Assert.Equal(0.006, d.CounterBoreDepth!.Value, 12);
        Assert.Equal(0.0, d.CounterSinkDiameter!.Value, 12);
    }

    [Theory]
    [InlineData("hole_counterbore_ansi_metric_m6_11_6")]
    [InlineData("hole_counterbore_ansi_metric_m6_11_6_resave")]
    public void AnsiCounterBoreAuthoring_ReproducesNativeBlock(string sample)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        byte[] nativeBlock;
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = sw.ResolvedFeatures.RawData!;
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> current)
            {
                foreach (var folder in current) { folders.Add(folder); Walk(folder.Children); }
            }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
            nativeBlock = raw.AsSpan(hole.TagOffset).ToArray();
            byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(StreamPath, stripped);
            sw.Save();
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(new HoleWizardFeatureModel
            {
                Type = 14,
                Standard = "ANSI Metric",
                Standard2 = 1,
                FastenerType = "凹头盖螺钉 - ANSI B18.3.1M",
                FastenerType2 = 33,
                FastenerSize = "M6",
                ThruHoleDiameter = 0.006,
                ThruHoleDepth = 0.010,
                CounterBoreDiameter = 0.011,
                CounterBoreDepth = 0.006
            });
            Assert.Equal(HoleWizardCounterBoreAnsiAuthoringTemplates.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[StreamPath];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var holeElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        Assert.Equal(nativeBlock, stream.AsSpan(holeElement.TagOffset).ToArray());
        var read = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(33, read.FastenerType2);
        Assert.Equal(0.011, read.CounterBoreDiameter!.Value, 12);
    }

    [Fact]
    public void AnsiCounterBoreAuthoring_RejectsWrongStandardOnBase()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath("hole_counterbore_ansi_metric_m6_11_6"));
        // Strip first.
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = sw.ResolvedFeatures.RawData!;
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> current)
            {
                foreach (var folder in current) { folders.Add(folder); Walk(folder.Children); }
            }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
            byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(StreamPath, stripped);
            sw.Save();
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            byte[] before = (byte[])part.RawStreams[StreamPath].Clone();
            Assert.ThrowsAny<Exception>(() => part.AddHoleWizard(new HoleWizardFeatureModel
            {
                Type = 14,
                Standard = "ISO",
                Standard2 = 8,
                FastenerType2 = 139,
                CounterBoreDiameter = 0.011,
                CounterBoreDepth = 0.006
            }));
            Assert.Equal(before, part.RawStreams[StreamPath]);
        }
    }

    private static int FindNamedValue(byte[] payload, string name)
    {
        for (int i = 0; i + 6 <= payload.Length; i++)
        {
            if (payload[i] != 0x04 || payload[i + 1] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(payload, i + 2)) continue;
            int pos = i + 2;
            if (!string.Equals(MfcPrimitives.ReadWChar(payload, ref pos), name, StringComparison.Ordinal))
                continue;
            if (pos + 30 > payload.Length || BitConverter.ToDouble(payload, pos) != 2.0) continue;
            return pos + 22;
        }
        return -1;
    }
}
