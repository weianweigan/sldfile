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
/// Read and fixed authoring for the two native Hole Wizard variants measured in
/// agent1-holewizard-blind-two-point-20261031:
/// - blind ISO M6 socket-head-cap counterbore (EndCondition=Blind, ThruHoleDepth 0.010/0.008);
/// - two-point ISO M6 socket-head-cap counterbore (HoleWizard5 with two EXTSKETCHPOINTs).
/// </summary>
public class HoleWizardBlindTwoPointAuthoringTests
{
    private const string Root = @"TestFiles\Features2026\HoleWizardBlindTwoPoint";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private sealed record BaseFile(TempTestFile Temp, byte[] OriginalStream, HoleWizardFeatureModel Model) : IDisposable
    {
        public void Dispose() => Temp.Dispose();
    }

    private static BaseFile CreateBase(string file)
    {
        var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        byte[] native;
        using (var source = SwFile.Open(PathOf(file), readOnly: true))
        {
            var part = Assert.IsType<SldPart>(source);
            native = part.RawStreams[StreamKey(part)];
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = sw.RawStreams[StreamKey(sw)];
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> fs) { foreach (var f in fs) { folders.Add(f); Walk(f.Children); } }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.Last(f => f.ClassName == "moHoleWzd_c");
            byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(StreamKey(sw), stripped);
            sw.Save();
        }
        using var original = SwFile.Open(PathOf(file), readOnly: true);
        var definition = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(original.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        return new BaseFile(temp, native, definition);
    }

    private static readonly string[] Sources =
    {
        "blind_cbore_thru_depth.SLDPRT",
        "blind_cbore_thru_depth8.SLDPRT",
        "two_point_sketch_points_ext.SLDPRT"
    };

    private static readonly string[] Resaves =
    {
        "blind_cbore_thru_depth_resave.SLDPRT",
        "blind_cbore_thru_depth8_resave.SLDPRT",
        "two_point_sketch_points_ext_resave.SLDPRT"
    };

    public static IEnumerable<object[]> ReadSamples =>
        Sources.Concat(Resaves).Select(f => new object[] { f });

    public static IEnumerable<object[]> SourceSamples =>
        Sources.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(ReadSamples))]
    public void ReadsBlindAndTwoPointLayouts(string file)
    {
        using var sw = SwFile.Open(PathOf(file), readOnly: true);
        Assert.Equal(0x71, BitConverter.ToUInt16(sw.RawStreams[StreamKey(sw)], 0));
        var feature = Assert.Single(sw.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        var definition = Assert.IsType<HoleWizardFeatureModel>(feature.Model);
        Assert.Equal(14, definition.Type);
        Assert.Equal("ISO", definition.Standard);
        Assert.Equal(8, definition.Standard2);
        Assert.Equal(139, definition.FastenerType2);
        Assert.Equal("M6", definition.FastenerSize);
        Assert.Equal(0.006, definition.ThruHoleDiameter!.Value, 12);
        Assert.Equal(0.011, definition.CounterBoreDiameter!.Value, 12);
        Assert.Equal(0.006, definition.CounterBoreDepth!.Value, 12);
        Assert.Equal(0.0, definition.CounterSinkDiameter!.Value, 12);
        if (file.StartsWith("two_point", StringComparison.Ordinal))
        {
            Assert.Equal(1, definition.EndCondition);
            Assert.Equal(0.010, definition.ThruHoleDepth!.Value, 12);
            Assert.Equal(0x53u, definition.FolderTypeId);
            Assert.Equal(2, definition.GetSketchPointCount());
            Assert.NotNull(definition.SketchPointLocations);
            Assert.Equal(2, definition.SketchPointLocations!.Count);
            Assert.Equal(0.015, definition.SketchPointLocations[0].X, 12);
            Assert.Equal(0.015, definition.SketchPointLocations[0].Y, 12);
            Assert.Equal(0.005, definition.SketchPointLocations[1].X, 12);
            Assert.Equal(0.005, definition.SketchPointLocations[1].Y, 12);
        }
        else
        {
            Assert.Equal(0, definition.EndCondition);
            Assert.Equal(0x52u, definition.FolderTypeId);
            Assert.Equal(file.Contains("depth8", StringComparison.Ordinal) ? 0.008 : 0.010,
                definition.ThruHoleDepth!.Value, 12);
            Assert.Equal(1, definition.GetSketchPointCount());
            Assert.Single(definition.SketchPointLocations!);
            Assert.Equal(0.009986487374987973, definition.SketchPointLocations![0].X, 12);
            Assert.Equal(0.010014305598872397, definition.SketchPointLocations[0].Y, 12);
        }
    }

    [Theory]
    [MemberData(nameof(ReadSamples))]
    public void RejectsTamperedBlindAndTwoPointLayouts(string file)
    {
        using var sw = SwFile.Open(PathOf(file), readOnly: true);
        byte[] raw = sw.RawStreams[StreamKey(sw)];
        bool twoPoint = file.StartsWith("two_point", StringComparison.Ordinal);
        uint typeId = twoPoint ? 0x53u : 0x52u;
        var doc = MfcDocument.Parse((byte[])raw.Clone());

        byte[] tampered = (byte[])raw.Clone();
        if (twoPoint)
        {
            var profile = doc.AllElements.Single(e => e.ClassName == "mo3DProfileFeature_c");
            BitConverter.GetBytes(99.0).CopyTo(tampered, profile.DataStart + 0xFF);
        }
        else
        {
            var body = doc.AllElements.Single(e => e.ClassName == "moCompSolidBody_c");
            BitConverter.GetBytes(1).CopyTo(tampered, body.DataStart + HoleWizardSegments.BlindEndConditionOffset);
        }
        Assert.Null(ParseDefinition(tampered, typeId));

        byte[] flagTampered = (byte[])raw.Clone();
        if (twoPoint)
        {
            var backup = doc.AllElements.Single(e => e.ClassName == "moPointBackedUpData_c");
            BitConverter.GetBytes(99.0).CopyTo(flagTampered, backup.DataStart + 0xF1);
        }
        else
        {
            var body = doc.AllElements.Single(e => e.ClassName == "moCompSolidBody_c");
            BitConverter.GetBytes(0).CopyTo(flagTampered, body.DataStart + HoleWizardSegments.BlindFlagOffset);
        }
        Assert.Null(ParseDefinition(flagTampered, typeId));
    }

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void FixedAuthoringReproducesOriginalStream(string file)
    {
        using var baseFile = CreateBase(file);
        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = CloneDefinition(baseFile.Model);
            var feature = part.AddHoleWizard(definition);
            Assert.Equal(ExpectedName(file), feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(baseFile.Temp.FilePath, readOnly: true);
        Assert.Equal(baseFile.OriginalStream, reopened.RawStreams[StreamKey(reopened)]);
        var read = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(baseFile.Model.EndCondition, read.EndCondition);
        Assert.Equal(baseFile.Model.ThruHoleDepth, read.ThruHoleDepth);
        Assert.Equal(baseFile.Model.GetSketchPointCount(), read.GetSketchPointCount());
    }

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void RejectsCustomNamesValuesAndTamperedBases(string file)
    {
        using var baseFile = CreateBase(file);
        using var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        string key = StreamKey(part);
        byte[] before = (byte[])part.RawStreams[key].Clone();

        Assert.Throws<NotSupportedException>(() =>
            part.AddHoleWizard(CloneDefinition(baseFile.Model), "Custom"));
        Assert.Equal(before, part.RawStreams[key]);

        var wrongDepth = CloneDefinition(baseFile.Model);
        wrongDepth.ThruHoleDepth = 0.009;
        Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(wrongDepth));
        Assert.Equal(before, part.RawStreams[key]);

        if (file.StartsWith("two_point", StringComparison.Ordinal))
        {
            var wrongPoint = CloneDefinition(baseFile.Model);
            wrongPoint.SketchPointLocations = new[]
            {
                new HoleWizardSketchPointLocation { X = 0.001, Y = 0.001 },
                new HoleWizardSketchPointLocation { X = 0.002, Y = 0.002 }
            };
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(wrongPoint));
            Assert.Equal(before, part.RawStreams[key]);
        }

        Assert.Equal(before, part.RawStreams[key]);
    }

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void RejectsFolderVersionAndShaTampering(string file)
    {
        using var baseFile = CreateBase(file);
        var definition = CloneDefinition(baseFile.Model);
        string name = ExpectedName(file);
        byte[] stripped = ReadStripped(baseFile.Temp.FilePath);

        byte[] wrongFolder = (byte[])stripped.Clone();
        BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(wrongFolder, 4) + 1)).CopyTo(wrongFolder, 4);
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.BuildHoleWizardStream(wrongFolder, definition, name));

        byte[] wrongVersion = (byte[])stripped.Clone();
        wrongVersion[0] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.BuildHoleWizardStream(wrongVersion, definition, name));

        byte[] wrongSha = (byte[])stripped.Clone();
        wrongSha[wrongSha.Length - 1] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.BuildHoleWizardStream(wrongSha, definition, name));

        Assert.Throws<NotSupportedException>(() =>
            HoleWizardFeatureBuilder.BuildHoleWizardStream(stripped, definition, "Custom"));
    }

    private static byte[] ReadStripped(string path)
    {
        using var sw = SwFile.Open(path, readOnly: true);
        return (byte[])sw.RawStreams[StreamKey(sw)].Clone();
    }

    private static string ExpectedName(string file)
    {
        if (file.StartsWith("two_point", StringComparison.Ordinal))
            return HoleWizardTwoPointCounterBoreTemplates.Source.FeatureName;
        return HoleWizardBlindCounterBoreTemplates.FindByDepth(
            file.Contains("depth8", StringComparison.Ordinal) ? 0.008 : 0.010)!.FeatureName;
    }

    private static HoleWizardFeatureModel CloneDefinition(HoleWizardFeatureModel source)
    {
        var clone = new HoleWizardFeatureModel
        {
            FolderTypeId = source.FolderTypeId,
            Type = source.Type,
            EndCondition = source.EndCondition,
            Standard = source.Standard,
            Standard2 = source.Standard2,
            FastenerType = source.FastenerType,
            FastenerType2 = source.FastenerType2,
            FastenerSize = source.FastenerSize,
            HoleDiameter = source.HoleDiameter,
            Depth = source.Depth,
            HoleDepth = source.HoleDepth,
            ThruHoleDiameter = source.ThruHoleDiameter,
            ThruHoleDepth = source.ThruHoleDepth,
            CounterBoreDiameter = source.CounterBoreDiameter,
            CounterBoreDepth = source.CounterBoreDepth,
            CounterSinkDiameter = source.CounterSinkDiameter,
            CounterSinkAngle = source.CounterSinkAngle,
            ThreadDiameter = source.ThreadDiameter,
            ThreadDepth = source.ThreadDepth,
            SketchPoints = source.SketchPoints,
            SketchPointLocations = source.SketchPointLocations
        };
        return clone;
    }

    private static HoleWizardFeatureModel? ParseDefinition(byte[] raw, uint typeId)
    {
        var doc = MfcDocument.Parse((byte[])raw.Clone());
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        return HoleWizardFeatureParser.ParseHoleWizard(doc, "moHoleWzd_c", typeId,
            element.TagOffset, raw.Length);
    }
}
