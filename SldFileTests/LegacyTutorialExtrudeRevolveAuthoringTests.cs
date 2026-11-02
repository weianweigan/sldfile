using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed legacy tutorial Extrude/Revolve authoring for the four single-tail samples:
/// 01_pin (Boss-Extrude2, UpToSurface), 03_circlip (Base-Extrude, UpToSurface),
/// 05_s_632_1 (Boss-Revolve1) and 09_strain_relief_straight (Cut-Revolve1, body scope).
/// </summary>
public class LegacyTutorialExtrudeRevolveAuthoringTests
{
    private const string Root = @"TestFiles\Features2026\TutorialExtrudeRevolveAuthoring";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private static LegacyTutorialFeatureAuthoringTemplate TemplateFor(string file) =>
        Assert.Single(LegacyTutorialFeatureAuthoringTemplates.All.Where(t =>
            string.Equals(t.Sample, file, StringComparison.OrdinalIgnoreCase)));

    private static ExtrudeFeatureDefinitionData ExtrudeDefinition(string file) => file switch
    {
        "01_pin.sldprt" => new ExtrudeFeatureDefinitionData
        {
            EndCondition = 4, ReverseDirection = true, FeatureScope = true, AutoSelect = false
        },
        "03_circlip.sldprt" => new ExtrudeFeatureDefinitionData
        {
            EndCondition = 4, ReverseDirection = false, FeatureScope = false, AutoSelect = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    private static RevolveFeatureDefinitionData RevolveDefinition(string file) => file switch
    {
        "05_s_632_1.sldprt" => new RevolveFeatureDefinitionData
        {
            IsCut = false, ReverseDirection = false
        },
        "09_strain_relief_straight.sldprt" => new RevolveFeatureDefinitionData
        {
            IsCut = true, ReverseDirection = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    private static Feature AddFeature(
        SldPart part, string file, string? featureName,
        ExtrudeFeatureDefinitionData? extrude = null,
        RevolveFeatureDefinitionData? revolve = null)
    {
        if (file.StartsWith("05_", StringComparison.Ordinal) ||
            file.StartsWith("09_", StringComparison.Ordinal))
            return part.AddRevolve(revolve ?? RevolveDefinition(file), featureName: featureName);
        return part.AddExtrude(extrude ?? ExtrudeDefinition(file), featureName: featureName);
    }

    private sealed record BaseFile(
        TempTestFile Temp, byte[] OriginalStream, byte[] NativeBlock, byte[] StrippedStream) : IDisposable
    {
        public void Dispose() => Temp.Dispose();
    }

    private static BaseFile CreateBase(string file)
    {
        var template = TemplateFor(file);
        var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            string key = StreamKey(sw);
            byte[] raw = sw.RawStreams[key];
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> fs) { foreach (var f in fs) { folders.Add(f); Walk(f.Children); } }
            Walk(sw.ResolvedFeatures.Folders);
            var target = folders.Last(f => f.ClassName == template.ClassName);
            Assert.Equal(folders.Max(f => f.TagOffset), target.TagOffset);
            byte[] nativeBlock = raw.AsSpan(target.TagOffset).ToArray();
            Assert.Equal(1, ExtrudeFeatureBuilder.CountOccurrences(nativeBlock, template.ClassName));
            byte[] stripped = raw.AsSpan(0, target.TagOffset).ToArray();
            ushort count = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(key, stripped);
            sw.Save();
            return new BaseFile(temp, raw, nativeBlock, stripped);
        }
    }

    private static readonly string[] Files =
    {
        "01_pin.sldprt",
        "03_circlip.sldprt",
        "05_s_632_1.sldprt",
        "09_strain_relief_straight.sldprt"
    };

    public static IEnumerable<object[]> Samples => Files.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_ReproducesOriginalStreamAndNativeBlock(string file)
    {
        var template = TemplateFor(file);
        using var baseFile = CreateBase(file);

        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = AddFeature(part, file, null);
            Assert.Equal(template.FeatureName, feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(baseFile.Temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[StreamKey(reopened)];
        Assert.Equal(baseFile.OriginalStream, stream);

        var document = MfcDocument.Parse((byte[])stream.Clone());
        var element = Assert.Single(document.AllElements.Where(e => e.ClassName == template.ClassName));
        Assert.Equal(baseFile.NativeBlock, stream.AsSpan(element.TagOffset).ToArray());
        Assert.Contains(reopened.Features, f => f.Name == template.FeatureName);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_RejectsCustomNameAndWrongDefinitionValues(string file)
    {
        var template = TemplateFor(file);
        using var baseFile = CreateBase(file);
        using var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        string key = StreamKey(part);
        byte[] before = (byte[])part.RawStreams[key].Clone();

        Assert.Throws<NotSupportedException>(() => AddFeature(part, file, "Custom"));
        Assert.Equal(before, part.RawStreams[key]);

        if (file.StartsWith("05_", StringComparison.Ordinal) ||
            file.StartsWith("09_", StringComparison.Ordinal))
        {
            var wrongCut = RevolveDefinition(file);
            wrongCut.IsCut = !wrongCut.IsCut;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, revolve: wrongCut));

            var wrongDirection = RevolveDefinition(file);
            wrongDirection.ReverseDirection = !wrongDirection.ReverseDirection;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, revolve: wrongDirection));

            var both = RevolveDefinition(file);
            both.BothDirections = true;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, revolve: both));
        }
        else
        {
            var wrongEndCondition = ExtrudeDefinition(file);
            wrongEndCondition.EndCondition = 0;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, extrude: wrongEndCondition));

            var wrongAutoSelect = ExtrudeDefinition(file);
            wrongAutoSelect.AutoSelect = !wrongAutoSelect.AutoSelect;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, extrude: wrongAutoSelect));

            var wrongScope = ExtrudeDefinition(file);
            wrongScope.FeatureScope = !wrongScope.FeatureScope;
            Assert.Throws<NotSupportedException>(() => AddFeature(part, file, null, extrude: wrongScope));
        }

        Assert.Equal(before, part.RawStreams[key]);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_RejectsFolderVersionMarkerSignatureClassAndNameTampering(string file)
    {
        var template = TemplateFor(file);
        using var baseFile = CreateBase(file);
        string className = template.ClassName;

        byte[] changedFolder = (byte[])baseFile.StrippedStream.Clone();
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(changedFolder, 4);
        Assert.Contains("folder count",
            Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                changedFolder, template, template.FeatureName)).Message,
            StringComparison.OrdinalIgnoreCase);

        byte[] changedVersion = (byte[])baseFile.StrippedStream.Clone();
        changedVersion[0] ^= 0xFF;
        Assert.Contains("first stream",
            Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                changedVersion, template, template.FeatureName)).Message,
            StringComparison.OrdinalIgnoreCase);

        if (template.MarkerOffset >= 0)
        {
            byte[] changedMarker = (byte[])baseFile.StrippedStream.Clone();
            changedMarker[template.MarkerOffset + 4] ^= 0xFF;
            Assert.Contains("marker",
                Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                    changedMarker, template, template.FeatureName)).Message,
                StringComparison.OrdinalIgnoreCase);
        }

        byte[] changedSignature = (byte[])baseFile.StrippedStream.Clone();
        changedSignature[template.BaseLength - 1] ^= 0xFF;
        Assert.Contains("normalized signature",
            Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                changedSignature, template, template.FeatureName)).Message,
            StringComparison.OrdinalIgnoreCase);

        byte[] existingClass = (byte[])baseFile.StrippedStream.Clone();
        byte[] classBytes = System.Text.Encoding.ASCII.GetBytes(className);
        Array.Copy(classBytes, 0, existingClass, 64, classBytes.Length);
        Assert.Contains("existing",
            Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                existingClass, template, template.FeatureName)).Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("measured feature name",
            Assert.Throws<NotSupportedException>(() => LegacyTutorialFeatureBuilder.Build(
                baseFile.StrippedStream, template, "Custom")).Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(template.BaseLength, baseFile.StrippedStream.Length);
        Assert.DoesNotContain(className,
            System.Text.Encoding.ASCII.GetString(baseFile.StrippedStream), StringComparison.Ordinal);
    }
}
