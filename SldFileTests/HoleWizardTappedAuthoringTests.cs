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
/// Controlled tapped M6 Hole Wizard authoring on the measured hole_tapped_m6 stripped base.
/// ThreadDiameter is fixed at the measured 0.006 m and ThreadDepth at 0.010 m; null uses those
/// measured values. Other values are rejected because native verification showed they only change
/// the callout/definition, not the rebuilt geometry or volume.
/// </summary>
public class HoleWizardTappedAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string SampleName = "hole_tapped_m6";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedTappedBase(out int holeTagOffset)
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

    private static HoleWizardFeatureDefinitionData Definition(double diameter, double depth)
    {
        return new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = 0x52u,
            Type = 48,
            EndCondition = 1,
            Standard = "ISO",
            Standard2 = 8,
            FastenerType = "螺纹孔",
            FastenerType2 = 147,
            FastenerSize = "M6",
            ThreadDiameter = diameter,
            ThreadDepth = depth,
            CosmeticThreadType = 1
        };
    }

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Fact]
    public void TappedAuthoring_RoundTripsThreadDiameterAndDepth()
    {
        using var temp = CreateStrippedTappedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddHoleWizard(Definition(0.006, 0.01), "TappedAgent1");
            Assert.Equal(FeatureKind.HoleWizard, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        Assert.Equal("moHoleWzd_c", feature.TypeName);
        Assert.Equal(0x52, feature.Id);
        Assert.Equal("TappedAgent1", feature.Name);
        var definition = Assert.IsType<HoleWizardFeatureDefinitionData>(feature.Definition);
        Assert.Equal(0x52u, definition.FolderTypeId);
        Assert.Equal(48, definition.Type);
        Assert.Equal(1, definition.EndCondition);
        Assert.Equal("ISO", definition.Standard);
        Assert.Equal(8, definition.Standard2);
        Assert.Equal("螺纹孔", definition.FastenerType);
        Assert.Equal(147, definition.FastenerType2);
        Assert.Equal("M6", definition.FastenerSize);
        Assert.Equal(0.006, definition.ThreadDiameter!.Value, 12);
        Assert.Equal(0.01, definition.ThreadDepth!.Value, 12);
        Assert.Equal(1, definition.CosmeticThreadType);
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);
        Assert.Equal((ushort)21, BitConverter.ToUInt16(ReadStream(temp.FilePath), 4));
    }

    [Fact]
    public void TappedAuthoring_MinimalDefinitionUsesMeasuredDefaults()
    {
        using var temp = CreateStrippedTappedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(new HoleWizardFeatureDefinitionData
            {
                FolderTypeId = 0x52u
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<HoleWizardFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Definition);
        Assert.Equal(0.006, definition.ThreadDiameter!.Value, 12);
        Assert.Equal(0.01, definition.ThreadDepth!.Value, 12);
        Assert.Equal("ISO", definition.Standard);
        Assert.Equal("M6", definition.FastenerSize);
    }

    [Fact]
    public void TappedAuthoring_WithNativeValuesDiffersFromNativeOnlyAtTheDisplayCopy()
    {
        byte[] native = ReadStream(SamplePath(SampleName));
        var nativeDoc = MfcDocument.Parse(native);
        var nativeHole = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] nativeBlock = MfcPrimitives.CopyRange(native, nativeHole.TagOffset, native.Length);

        using var temp = CreateStrippedTappedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(0.005999999999999975, 0.01), "M6 螺纹孔1");
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredHole = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        byte[] authoredBlock = MfcPrimitives.CopyRange(authored, authoredHole.TagOffset, authored.Length);
        Assert.Equal(nativeBlock.Length, authoredBlock.Length);
        var diffs = Enumerable.Range(0, nativeBlock.Length)
            .Where(i => nativeBlock[i] != authoredBlock[i])
            .ToList();
        Assert.NotEmpty(diffs);
        // The native ThreeDdiameterDim value is 0.005999999999999975 while the
        // moCThreadData display copy is the rounded 0.006. Authoring writes the requested
        // parameter to both, so only that 8-byte display copy differs.
        Assert.All(diffs, i => Assert.InRange(i, 31588, 31595));
    }

    [Fact]
    public void TappedAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedTappedBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHoleWizard(Definition(0.006, 0.01));
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
        Assert.Equal(0.006, definition.ThreadDiameter!.Value, 12);
        Assert.Equal(0.01, definition.ThreadDepth!.Value, 12);
    }

    [Fact]
    public void TappedAuthoring_RejectsWrongBaseExistingHoleAndVersion()
    {
        using (var temp = CreateStrippedTappedBase(out _))
        {
            RewriteStream(temp, raw => raw[7000] ^= 0xFF);
            AssertTapAddRejected(temp, Definition(0.006, 0.01), "base signature");
        }

        using (var full = TestFileProvider.CreateTempCopy(SamplePath(SampleName)))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(Definition(0.006, 0.01)));
            Assert.Contains("existing hole", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedTappedBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x71).CopyTo(raw, 0));
            AssertTapAddRejected(temp, Definition(0.006, 0.01), "0x84");
        }

        using (var temp = CreateStrippedTappedBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)21).CopyTo(raw, 4));
            AssertTapAddRejected(temp, Definition(0.006, 0.01), "folder count");
        }

        using (var legacy = TestFileProvider.CreateTempCopy(
                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", "hole_simple_d5_throughall.SLDPRT")))
        {
            using var sw = SwFile.Open(legacy.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(Definition(0.006, 0.01)));
            Assert.Contains("0x84", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void TappedAuthoring_RejectsUnverifiedValuesAndFields()
    {
        using var temp = CreateStrippedTappedBase(out _);

        void Reject(HoleWizardFeatureDefinitionData definition, string message)
        {
            var error = Assert.Throws<NotSupportedException>(() =>
                HoleWizardFeatureBuilder.ValidateDefinition(definition));
            Assert.Contains(message, error.Message);
        }

        var type = Definition(0.006, 0.01); type.Type = 0; Reject(type, "Type = 48");
        var folder = Definition(0.006, 0.01); folder.FolderTypeId = 0x54u; Reject(folder, "FolderTypeId 0x52");
        var end = Definition(0.006, 0.01); end.EndCondition = 0; Reject(end, "EndCondition = 1");
        var standard = Definition(0.006, 0.01); standard.Standard = "ANSI"; Reject(standard, "Standard");
        var standard2 = Definition(0.006, 0.01); standard2.Standard2 = 9; Reject(standard2, "Standard2");
        var fastener = Definition(0.006, 0.01); fastener.FastenerType2 = 148; Reject(fastener, "FastenerType2");
        var size = Definition(0.006, 0.01); size.FastenerSize = "M8"; Reject(size, "FastenerSize");
        var cosmetic = Definition(0.006, 0.01); cosmetic.CosmeticThreadType = 0; Reject(cosmetic, "CosmeticThreadType");
        var diameter = Definition(0.006, 0.01); diameter.Diameter = 0.008; Reject(diameter, "Diameter");
        var depth = Definition(0.006, 0.01); depth.Depth = 0.01; Reject(depth, "Depth");
        var face = Definition(0.006, 0.01); face.Face = new FeatureReferenceData(); Reject(face, "Face");
        var points = Definition(0.006, 0.01);
        points.SketchPoints = new[] { new FeatureReferenceData() }; Reject(points, "SketchPoints");
        var counter = Definition(0.006, 0.01); counter.CounterBoreDiameter = 0.008; Reject(counter, "CounterBoreDiameter");
        var scope = Definition(0.006, 0.01); scope.FeatureScope = false; Reject(scope, "FeatureScope");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(double.NaN, 0.01)));
        var depthError = Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.006, 0.012)));
        Assert.Contains("ThreadDepth", depthError.Message);
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.006, 0.0)));
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.006, double.PositiveInfinity)));

        // ThreadDiameter is fixed at the measured 0.006 m or null: other callout diameters do not
        // change native geometry/volume and are rejected explicitly.
        var threadDiameterError = Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.008, 0.01)));
        Assert.Contains("ThreadDiameter", threadDiameterError.Message);
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.006 + 1e-9, 0.01)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(0.0, 0.01)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(double.NaN, 0.01)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HoleWizardFeatureBuilder.ValidateDefinition(Definition(double.PositiveInfinity, 0.01)));

        AssertTapAddRejected(temp, new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = 0x52u, ThreadDiameter = 0.006, ThreadDepth = 0.01, Face = new FeatureReferenceData()
        }, "Face");
    }

    private static void AssertTapAddRejected(
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
