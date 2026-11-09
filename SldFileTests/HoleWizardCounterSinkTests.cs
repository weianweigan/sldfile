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
/// Native API-created ISO flat-head countersinks (Type=44, stream 0x71):
/// M6 Ø6 through/Ø11 x 90° and M8 Ø8 through/Ø15 x 90°, plus native resaves.
/// </summary>
public class HoleWizardCounterSinkTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static HoleWizardFeatureModel Read(string name)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        return Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
    }

    [Theory]
    [InlineData("hole_countersink_iso_m6_11_90", "M6", 0.006, 0.011)]
    [InlineData("hole_countersink_iso_m6_11_90_resave", "M6", 0.006, 0.011)]
    [InlineData("hole_countersink_iso_m8_15_90", "M8", 0.008, 0.015)]
    [InlineData("hole_countersink_iso_m8_15_90_resave", "M8", 0.008, 0.015)]
    public void CounterSink_ReadsMeasuredIsoFlatHeadFields(
        string sample, string size, double thruDiameter, double csDiameter)
    {
        var d = Read(sample);
        Assert.Equal(0x52u, d.FolderTypeId);
        Assert.Equal(44, d.Type);
        Assert.Equal(1, d.EndCondition);
        Assert.Equal("ISO", d.Standard);
        Assert.Equal(8, d.Standard2);
        Assert.Equal("锥孔平头 ISO 7046-1", d.FastenerType);
        Assert.Equal(141, d.FastenerType2);
        Assert.Equal(size, d.FastenerSize);
        Assert.Equal(0.0, d.HoleDiameter!.Value, 12);
        Assert.Equal(0.0, d.Depth!.Value, 12);
        Assert.Equal(0.0, d.HoleDepth!.Value, 12);
        Assert.Equal(thruDiameter, d.ThruHoleDiameter!.Value, 12);
        Assert.Equal(0.010, d.ThruHoleDepth!.Value, 12);
        Assert.Equal(0.0, d.CounterBoreDiameter!.Value, 12);
        Assert.Equal(0.0, d.CounterBoreDepth!.Value, 12);
        Assert.Equal(csDiameter, d.CounterSinkDiameter!.Value, 12);
        Assert.Equal(Math.PI / 2.0, d.CounterSinkAngle!.Value, 12);
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
            nameof(HoleWizardFeatureModel.FastenerSize),
            nameof(HoleWizardFeatureModel.ThruHoleDiameter),
            nameof(HoleWizardFeatureModel.CounterSinkDiameter),
            nameof(HoleWizardFeatureModel.CounterSinkAngle),
            nameof(HoleWizardFeatureModel.SketchPointLocations)
        })
            Assert.True(d.IsResolved(name), name);
    }

    [Fact]
    public void CounterSinkParser_RejectsTamperedFieldsAndWrongVersion()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath("hole_countersink_iso_m6_11_90"), readOnly: true))
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
            b[body.DataStart + HoleWizardSegments.CounterSinkStandardOffset + 20] ^= 0xFF;
        });
        Tamper((b, d) =>
        {
            var body = d.AllElements.First(e => e.ClassName == "moCompSolidBody_c");
            BitConverter.GetBytes(142u).CopyTo(b, body.DataStart + HoleWizardSegments.CounterSinkFastenerType2Offset);
        });
        Tamper((b, d) =>
        {
            var angle = d.AllElements.First(e => e.ClassName == "moAngleParameter_c");
            byte[] payload = MfcPrimitives.CopyRange(b, angle.DataStart, angle.DataStart +
                HoleWizardSegments.CounterSinkAngleParameterLength);
            int offset = FindNamedValue(payload, HoleWizardSegments.CounterSinkDiameterName);
            Assert.True(offset > 0);
            BitConverter.GetBytes(0.012).CopyTo(b, angle.DataStart + offset);
        });
        Tamper((b, d) =>
        {
            var angle = d.AllElements.First(e => e.ClassName == "moAngleParameter_c");
            byte[] payload = MfcPrimitives.CopyRange(b, angle.DataStart, angle.DataStart +
                HoleWizardSegments.CounterSinkAngleParameterLength);
            int offset = FindNamedValue(payload, HoleWizardSegments.CounterSinkAngleName);
            Assert.True(offset > 0);
            BitConverter.GetBytes(Math.PI / 3.0).CopyTo(b, angle.DataStart + offset);
        });
        Tamper((b, d) =>
        {
            var feature2 = d.AllElements.First(e => e.ClassName == "moHoleWzd_c");
            b[feature2.DataStart + 2] ^= 0xFF;
        });

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)0x84).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "moHoleWzd_c");
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            wrongDoc, "moHoleWzd_c", 0x52, wrongFeature.TagOffset, wrongVersion.Length));
        Assert.Equal(original, doc.SourceData);
    }

    [Theory]
    [InlineData("hole_countersink_iso_m6_11_90", "M6", 0.006, 0.011)]
    [InlineData("hole_countersink_iso_m6_11_90_resave", "M6", 0.006, 0.011)]
    [InlineData("hole_countersink_iso_m8_15_90", "M8", 0.008, 0.015)]
    [InlineData("hole_countersink_iso_m8_15_90_resave", "M8", 0.008, 0.015)]
    public void CounterSinkAuthoring_ReproducesNativeBlock(
        string sample, string size, double thruDiameter, double csDiameter)
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
                Type = 44,
                FastenerSize = size,
                ThruHoleDiameter = thruDiameter,
                CounterSinkDiameter = csDiameter
            });
            Assert.Equal($"Hole_cs_iso_flathead_{size.ToLowerInvariant()}", feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[StreamPath];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var holeElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        Assert.Equal(nativeBlock, stream.AsSpan(holeElement.TagOffset).ToArray());
        var read = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(size, read.FastenerSize);
        Assert.Equal(csDiameter, read.CounterSinkDiameter!.Value, 12);
    }

    [Fact]
    public void CounterSinkAuthoring_RejectsMismatchedFieldsNamesAndBases()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath("hole_countersink_iso_m6_11_90"));
        Reject(temp, new HoleWizardFeatureModel { Type = 44, FastenerSize = "M6",
            ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.015 });
        Reject(temp, new HoleWizardFeatureModel { Type = 44, FastenerSize = "M8",
            ThruHoleDiameter = 0.008, CounterSinkDiameter = 0.015 });
        Reject(temp, new HoleWizardFeatureModel { Type = 44, FastenerSize = "M6",
            ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.011, CounterSinkAngle = Math.PI / 3 });
        Reject(temp, new HoleWizardFeatureModel { Type = 44, FastenerSize = "M6",
            ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.011, Standard = "ANSI" });
        Reject(temp, new HoleWizardFeatureModel { Type = 44, FastenerSize = "M6",
            ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.011 }, "Custom");

        using var legacy = TestFileProvider.CreateTempCopy(SamplePath("hole_simple_d5_throughall"));
        using (var sw = SwFile.Open(legacy.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(
                new HoleWizardFeatureModel { Type = 44, FastenerSize = "M6",
                    ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.011 }));
        }

        static void Reject(TempTestFile source, HoleWizardFeatureModel definition, string? name = null)
        {
            string copy = source.FilePath + "." + Guid.NewGuid().ToString("N") + ".SLDPRT";
            File.Copy(source.FilePath, copy, true);
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
