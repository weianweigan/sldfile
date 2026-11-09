using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled legacy Hole Wizard authoring on the measured through-all and blind-10 bases.
/// </summary>
public class HoleWizardAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string ThroughAllSample = "hole_simple_d5_throughall";
    private const string BlindSample = "hole_simple_d5_blind10";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedHoleBase(string sampleName, out int holeTagOffset)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleName));
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

    private static HoleWizardFeatureModel Definition(int endCondition, double diameter, double? depth = null)
    {
        return new HoleWizardFeatureModel
        {
            FolderTypeId = 0x54u,
            Type = 0,
            EndCondition = endCondition,
            Diameter = diameter,
            Depth = depth
        };
    }

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Fact]
    public void ThroughAllHoleAuthoring_RoundTripsDiameterEndConditionAndPoint()
    {
        using var temp = CreateStrippedHoleBase(ThroughAllSample, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddHoleWizard(Definition(1, 0.006), "HoleAgent1");
            Assert.Equal(FeatureKind.HoleWizard, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        Assert.Equal("moHoleWzd_c", feature.TypeName);
        Assert.Equal(0x54, feature.Id);
        Assert.Equal("HoleAgent1", feature.Name);
        var definition = Assert.IsType<HoleWizardFeatureModel>(feature.Model);
        Assert.Equal(0x54u, definition.FolderTypeId);
        Assert.Equal(0, definition.Type);
        Assert.Equal(1, definition.EndCondition);
        Assert.Equal(0.006, definition.Diameter!.Value, 12);
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);
        Assert.Equal(1, definition.GetSketchPointCount());
        Assert.Equal((ushort)21, BitConverter.ToUInt16(ReadStream(temp.FilePath), 4));
    }

    [Fact]
    public void BlindHoleAuthoring_RoundTripsMeasuredTemplate()
    {
        using var temp = CreateStrippedHoleBase(BlindSample, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(0, 0.0045, 0.010), "HoleBlindAgent1");
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(0, definition.Type);
        Assert.Equal(0, definition.EndCondition);
        Assert.Equal(0.0045, definition.Diameter!.Value, 12);
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);
    }

    [Theory]
    [InlineData(ThroughAllSample, 1.0, "孔1")]
    [InlineData(BlindSample, 0.0, "孔1")]
    public void HoleWizardAuthoring_WithNativeValuesReproducesNativeFeatureBlock(
        string sampleName, double endCondition, string nativeName)
    {
        byte[] native = ReadStream(SamplePath(sampleName));
        var nativeDoc = MfcDocument.Parse(native);
        var nativeHole = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] nativeBlock = MfcPrimitives.CopyRange(native, nativeHole.TagOffset, native.Length);

        using var temp = CreateStrippedHoleBase(sampleName, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition((int)endCondition, 0.005), nativeName);
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredHole = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] authoredBlock = MfcPrimitives.CopyRange(authored, authoredHole.TagOffset, authored.Length);
        Assert.Equal(nativeBlock, authoredBlock);
    }

    [Fact]
    public void HoleWizardAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedHoleBase(ThroughAllSample, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(1, 0.007));
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
        var definition = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(0.007, definition.Diameter!.Value, 12);
        Assert.Equal(1, definition.EndCondition);
    }

    [Fact]
    public void HoleWizardAuthoring_RejectsWrongBaseExistingHoleAndVersion()
    {
        using (var temp = CreateStrippedHoleBase(ThroughAllSample, out _))
            AssertHoleAddRejected(temp, Definition(0, 0.005), "blind10");

        using (var temp = CreateStrippedHoleBase(BlindSample, out _))
            AssertHoleAddRejected(temp, Definition(1, 0.005), "throughall");

        using (var temp = CreateStrippedHoleBase(ThroughAllSample, out _))
        {
            RewriteStream(temp, raw => raw[7000] ^= 0xFF);
            AssertHoleAddRejected(temp, Definition(1, 0.005), "base signature");
        }

        using (var full = TestFileProvider.CreateTempCopy(SamplePath(ThroughAllSample)))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(Definition(1, 0.005)));
            Assert.Contains("existing hole", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedHoleBase(ThroughAllSample, out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x84).CopyTo(raw, 0));
            AssertHoleAddRejected(temp, Definition(1, 0.005), "0x71");
        }

        using (var temp = CreateStrippedHoleBase(ThroughAllSample, out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)21).CopyTo(raw, 4));
            AssertHoleAddRejected(temp, Definition(1, 0.005), "folder count");
        }
    }

    [Fact]
    public void HoleWizardAuthoring_RejectsUnverifiedFieldsAndValues()
    {
        using var temp = CreateStrippedHoleBase(ThroughAllSample, out _);

        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(2, 0.005)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(1, 0.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(1, double.NaN)));
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(1, 0.005, 0.020)));

        void Reject(HoleWizardFeatureModel definition)
        {
            Assert.Throws<NotSupportedException>(() => HoleWizardFeatureBuilder.ValidateDefinition(definition));
        }

        // A legacy definition carrying tapped markers is classified as tapped and fails the
        // tapped required-thread validation; the exact exception type depends on the missing field.
        Assert.ThrowsAny<Exception>(() => HoleWizardFeatureBuilder.ValidateDefinition(
            new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, FolderTypeId = 0x52 }));
        Assert.ThrowsAny<Exception>(() => HoleWizardFeatureBuilder.ValidateDefinition(
            new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, Type = 48 }));
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, CounterBoreDiameter = 0.008 });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, ThreadDiameter = 0.006 });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, Face = new FeatureReferenceData() });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, SketchPoints = new[] { new FeatureReferenceData() } });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, SketchPointLocations = new[] { new HoleWizardSketchPointLocation() } });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, FeatureScope = false });
        Reject(new HoleWizardFeatureModel { EndCondition = 1, Diameter = 0.005, ReverseDirection = false });

        AssertHoleAddRejected(temp, new HoleWizardFeatureModel
        {
            EndCondition = 1, Diameter = 0.005, CounterSinkAngle = 1.5
        }, "CounterSinkAngle");
    }

    private static void AssertHoleAddRejected(
        TempTestFile temp, HoleWizardFeatureModel definition, string expectedMessage)
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
