using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled UpToNext legacy Hole Wizard authoring on the measured hole_up_to_next base.
/// </summary>
public class HoleWizardUpToNextAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string SampleName = "hole_up_to_next";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(out int holeTagOffset)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(SampleName));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var hole = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
            holeTagOffset = hole.TagOffset;
            byte[] stripped = new byte[holeTagOffset];
            Array.Copy(raw, 0, stripped, 0, stripped.Length);
            ushort folderCount = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using (var check = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.DoesNotContain(check.Features, f => f.Kind == FeatureKind.HoleWizard);
            Assert.Equal((ushort)20, BitConverter.ToUInt16(check.RawStreams[ResolvedStream], 4));
        }
        return temp;
    }

    private static HoleWizardFeatureDefinitionData Definition(double diameter, int endCondition = 11)
    {
        return new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = 0x54u,
            Type = 0,
            EndCondition = endCondition,
            Diameter = diameter
        };
    }

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Fact]
    public void UpToNextAuthoring_RoundTripsEndConditionDiameterAndPoint()
    {
        using var temp = CreateStrippedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddHoleWizard(Definition(0.006), "UpToNextAgent1");
            Assert.Equal(FeatureKind.HoleWizard, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        Assert.Equal("moHoleWzd_c", feature.TypeName);
        Assert.Equal(0x54, feature.Id);
        Assert.Equal("UpToNextAgent1", feature.Name);
        var definition = Assert.IsType<HoleWizardFeatureDefinitionData>(feature.Definition);
        Assert.Equal(0x54u, definition.FolderTypeId);
        Assert.Equal(0, definition.Type);
        Assert.Equal(11, definition.EndCondition);
        Assert.Equal(0.006, definition.Diameter!.Value, 12);
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);
        Assert.Equal(1, definition.GetSketchPointCount());
        Assert.Equal((ushort)21, BitConverter.ToUInt16(ReadStream(temp.FilePath), 4));
    }

    [Fact]
    public void UpToNextAuthoring_WithNativeValuesReproducesNativeFeatureBlock()
    {
        byte[] native = ReadStream(SamplePath(SampleName));
        var nativeDoc = MfcDocument.Parse(native);
        var nativeHole = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] nativeBlock = MfcPrimitives.CopyRange(native, nativeHole.TagOffset, native.Length);

        using var temp = CreateStrippedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(0.005), "孔1");
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredHole = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] authoredBlock = MfcPrimitives.CopyRange(authored, authoredHole.TagOffset, authored.Length);
        Assert.Equal(nativeBlock, authoredBlock);
    }

    [Fact]
    public void UpToNextAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(0.007));
            part.Save();
        }

        byte[] before;
        using (var first = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])first.RawStreams[ResolvedStream].Clone();
            first.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<HoleWizardFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Definition);
        Assert.Equal(11, definition.EndCondition);
        Assert.Equal(0.007, definition.Diameter!.Value, 12);
    }

    [Fact]
    public void UpToNextAuthoring_RejectsWrongBaseExistingHoleAndVersion()
    {
        var throughAllBase = TestFileProvider.CreateTempCopy(SamplePath("hole_simple_d5_throughall"));
        using (throughAllBase)
        {
            using (var sw = SwFile.Open(throughAllBase.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                var doc = MfcDocument.Parse(raw);
                var hole = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
                byte[] stripped = new byte[hole.TagOffset];
                Array.Copy(raw, 0, stripped, 0, stripped.Length);
                ushort fc = BitConverter.ToUInt16(stripped, 4);
                BitConverter.GetBytes((ushort)(fc - 1)).CopyTo(stripped, 4);
                sw.SetPendingStream(ResolvedStream, stripped);
                sw.Save();
            }
            AssertUpToNextRejected(throughAllBase, Definition(0.005), "hole_up_to_next");
        }

        using (var temp = CreateStrippedBase(out _))
            AssertUpToNextRejected(temp, Definition(0.005, 1), "hole_simple_d5_throughall");

        using (var temp = CreateStrippedBase(out _))
        {
            RewriteStream(temp, raw => raw[7000] ^= 0xFF);
            AssertUpToNextRejected(temp, Definition(0.005), "base signature");
        }

        using (var full = TestFileProvider.CreateTempCopy(SamplePath(SampleName)))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(Definition(0.005)));
            Assert.Contains("existing hole", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x84).CopyTo(raw, 0));
            AssertUpToNextRejected(temp, Definition(0.005), "0x71");
        }

        using (var temp = CreateStrippedBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)21).CopyTo(raw, 4));
            AssertUpToNextRejected(temp, Definition(0.005), "folder count");
        }
    }

    [Fact]
    public void UpToNextAuthoring_RejectsUnverifiedFields()
    {
        using var temp = CreateStrippedBase(out _);
        AssertUpToNextRejected(temp, new HoleWizardFeatureDefinitionData
        {
            EndCondition = 11, Diameter = 0.005, CounterSinkDiameter = 0.008
        }, "CounterSinkDiameter");
        AssertUpToNextRejected(temp, new HoleWizardFeatureDefinitionData
        {
            EndCondition = 11, Diameter = 0.005, Depth = 0.020
        }, "template depth");
        AssertUpToNextRejected(temp, new HoleWizardFeatureDefinitionData
        {
            EndCondition = 11, Diameter = 0.005, Face = new FeatureReferenceData()
        }, "Face");
    }

    private static void AssertUpToNextRejected(
        TempTestFile temp, HoleWizardFeatureDefinitionData definition, string expectedMessage)
    {
        byte[] before = ReadStream(temp.FilePath);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.ThrowsAny<Exception>(() => part.AddHoleWizard(definition));
            Assert.True(error is NotSupportedException || error is ArgumentOutOfRangeException,
                "Unexpected exception type: " + error.GetType().Name);
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }
        Assert.Equal(before, ReadStream(temp.FilePath));
    }

    private static void RewriteStream(TempTestFile temp, Action<byte[]> mutate)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        mutate(raw);
        sw.SetPendingStream(ResolvedStream, raw);
        sw.Save();
    }
}
