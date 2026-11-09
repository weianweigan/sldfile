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
/// Fixed legacy tutorial HoleWizard authoring for three single trailing-hole samples:
/// ANSI-inch CBORE #6 (box), ANSI-inch CBORE #00 (clamp2) and Drill&amp;CBore 1/4 SHCS (die insert).
/// </summary>
public class LegacyHoleWizardAuthoringTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";
    private const string Root = @"TestFiles\Features2026\HoleWizardLegacyTutorials";

    private sealed record Sample(string File, uint FolderTypeId);

    private static readonly Sample[] Samples =
    {
        new("11_box.sldprt", 0x23u),
        new("12_clamp2.sldprt", 0x158u),
        new("08_die_insert.sldprt", 0x3Cu)
    };

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private sealed record BaseFile(
        TempTestFile Temp, byte[] NativeBlock, HoleWizardFeatureModel Model) : IDisposable
    {
        public void Dispose() => Temp.Dispose();
    }

    private static BaseFile CreateBase(string file)
    {
        var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        byte[] nativeBlock;
        HoleWizardFeatureModel definition;
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            string key = StreamKey(sw);
            byte[] raw = sw.RawStreams[key];
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> fs) { foreach (var f in fs) { folders.Add(f); Walk(f.Children); } }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
            nativeBlock = raw.AsSpan(hole.TagOffset).ToArray();
            byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(key, stripped);
            sw.Save();
        }
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            // Parse the original definition from a separate read-only open.
            using var original = SwFile.Open(PathOf(file), readOnly: true);
            definition = Assert.IsType<HoleWizardFeatureModel>(
                Assert.Single(original.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        }
        return new BaseFile(temp, nativeBlock, definition);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void LegacyAuthoring_ReproducesNativeBlock(string file, uint folderTypeId)
    {
        using var baseFile = CreateBase(file);
        var template = Assert.Single(LegacyHoleWizardAuthoringTemplates.All.Where(t => t.FolderTypeId == folderTypeId));
        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(baseFile.Model);
            Assert.Equal(template.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(baseFile.Temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[StreamKey(reopened)];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var hole = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moHoleWzd_c"));
        Assert.Equal(baseFile.NativeBlock, stream.AsSpan(hole.TagOffset).ToArray());
        var read = Assert.IsType<HoleWizardFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Model);
        Assert.Equal(template.Type, read.Type);
        Assert.Equal(template.EndCondition, read.EndCondition);
        Assert.Equal(template.FastenerSize, read.FastenerSize);
    }

    [Fact]
    public void LegacyAuthoring_RejectsCustomNamesValuesAndBases()
    {
        using var baseFile = CreateBase("11_box.sldprt");
        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            string key = StreamKey(part);
            byte[] before = (byte[])part.RawStreams[key].Clone();
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(baseFile.Model, "Custom"));
            var wrongType = CreateDefinition(0x23u);
            wrongType.Type = 99;
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(wrongType));
            var wrongBore = CreateDefinition(0x23u);
            wrongBore.CounterBoreDiameter = 0.02;
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(wrongBore));
            Assert.Equal(before, part.RawStreams[key]);
        }

        using var wrongBase = CreateBase("12_clamp2.sldprt");
        using (var sw = SwFile.Open(wrongBase.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(CreateDefinition(0x23u)));
        }

        using var existing = TestFileProvider.CreateTempCopy(PathOf("11_box.sldprt"));
        using (var sw = SwFile.Open(existing.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(CreateDefinition(0x23u)));
        }
    }

    [Fact]
    public void LegacyAuthoring_RejectsFolderVersionMarkerAndBaseTamper()
    {
        using var baseFile = CreateBase("12_clamp2.sldprt");
        using var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        string streamKey = StreamKey(part);
        byte[] original = (byte[])part.RawStreams[streamKey].Clone();

        void Reject(byte[] stream)
        {
            byte[] before = (byte[])part.RawStreams[streamKey].Clone();
            part.SetPendingStream(streamKey, stream);
            Assert.ThrowsAny<Exception>(() => part.AddHoleWizard(CreateDefinition(0x158u)));
            part.SetPendingStream(streamKey, before);
        }

        byte[] wrongFolder = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(wrongFolder, 4) + 1)).CopyTo(wrongFolder, 4);
        Reject(wrongFolder);

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(wrongVersion, 0) + 1)).CopyTo(wrongVersion, 0);
        Reject(wrongVersion);

        var template = LegacyHoleWizardAuthoringTemplates.Find(0x158u)!;
        Assert.True(template.MarkerOffset >= 0);
        byte[] wrongMarker = (byte[])original.Clone();
        wrongMarker[template.MarkerOffset] ^= 0xFF;
        Reject(wrongMarker);

        byte[] wrongBase = (byte[])original.Clone();
        wrongBase[wrongBase.Length - 1] ^= 0xFF;
        Reject(wrongBase);
    }

    private static HoleWizardFeatureModel CreateDefinition(uint folderTypeId)
    {
        var t = LegacyHoleWizardAuthoringTemplates.Find(folderTypeId)!;
        return new HoleWizardFeatureModel
        {
            FolderTypeId = folderTypeId, Type = t.Type, EndCondition = t.EndCondition,
            Standard = t.Standard, FastenerType = t.FastenerType, FastenerSize = t.FastenerSize,
            ThruHoleDiameter = t.ThruHoleDiameter, ThruHoleDepth = t.ThruHoleDepth,
            CounterBoreDiameter = t.CounterBoreDiameter, CounterBoreDepth = t.CounterBoreDepth,
            CounterSinkDiameter = t.CounterSinkDiameter, CounterSinkAngle = t.CounterSinkAngle,
            ThreadDiameter = t.ThreadDiameter, ThreadDepth = t.ThreadDepth
        };
    }

    public static IEnumerable<object[]> TestCases() =>
        Samples.Select(s => new object[] { s.File, s.FolderTypeId });
}
