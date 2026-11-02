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
/// Fixed authoring for the 15 newly measured tutorial HoleWizard families whose hole folder
/// is the last folder in the ResolvedFeatures stream: NPSM pipe tapped, ANSI-inch/ANSI-metric
/// clearance and drill-size holes, M3.5/#6 counterbores, M2 countersink, M8/M10/1/2-20 tapped,
/// 3/8 clearance and the legacy blind Tap family.
/// </summary>
public class LegacyHoleWizardTailAuthoringTests
{
    private const string Root = @"TestFiles\Features2026\HoleWizardLegacyTailTutorials";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private static LegacyHoleWizardTailAuthoringTemplate TemplateFor(string file) =>
        Assert.Single(LegacyHoleWizardTailAuthoringTemplates.All.Where(t => t.Sample == file));

    private sealed record BaseFile(
        TempTestFile Temp, byte[] OriginalStream, LegacyHoleWizardTailAuthoringTemplate Template) : IDisposable
    {
        public void Dispose() => Temp.Dispose();
    }

    private static BaseFile CreateBase(string file)
    {
        var template = TemplateFor(file);
        var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        using (var source = SwFile.Open(PathOf(file), readOnly: true))
        {
            byte[] original = source.RawStreams[StreamKey(source)];
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
            return new BaseFile(temp, original, template);
        }
    }

    public static IEnumerable<object[]> SourceSamples =>
        LegacyHoleWizardTailAuthoringTemplates.All.Select(t => new object[] { t.Sample });

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void FixedAuthoringReproducesOriginalStream(string file)
    {
        using var baseFile = CreateBase(file);
        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(new HoleWizardFeatureDefinitionData());
            Assert.Equal(baseFile.Template.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(baseFile.Temp.FilePath, readOnly: true);
        Assert.Equal(baseFile.OriginalStream, reopened.RawStreams[StreamKey(reopened)]);
        Assert.Contains(reopened.Features, f => f.Name == baseFile.Template.FeatureName);
    }

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void RejectsCustomNamesWrongDefinitionsAndTamperedBases(string file)
    {
        var template = TemplateFor(file);
        using var baseFile = CreateBase(file);
        using var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        string key = StreamKey(part);
        byte[] before = (byte[])part.RawStreams[key].Clone();

        Assert.Throws<NotSupportedException>(() =>
            part.AddHoleWizard(new HoleWizardFeatureDefinitionData(), "Custom"));
        Assert.Equal(before, part.RawStreams[key]);

        Assert.Throws<NotSupportedException>(() =>
            part.AddHoleWizard(new HoleWizardFeatureDefinitionData { Standard = "Bogus" }));
        Assert.Equal(before, part.RawStreams[key]);

        if (template.Type.HasValue)
        {
            Assert.Throws<NotSupportedException>(() =>
                part.AddHoleWizard(new HoleWizardFeatureDefinitionData { Type = template.Type.Value + 1 }));
            Assert.Equal(before, part.RawStreams[key]);
        }

        byte[] stripped = part.RawStreams[key];
        var definition = new HoleWizardFeatureDefinitionData();
        byte[] wrongFolder = (byte[])stripped.Clone();
        BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(wrongFolder, 4) + 1)).CopyTo(wrongFolder, 4);
        Assert.Throws<NotSupportedException>(() =>
            LegacyHoleWizardTailBuilder.Build(wrongFolder, template, definition, template.FeatureName));

        byte[] wrongVersion = (byte[])stripped.Clone();
        wrongVersion[0] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() =>
            LegacyHoleWizardTailBuilder.Build(wrongVersion, template, definition, template.FeatureName));

        byte[] wrongSha = (byte[])stripped.Clone();
        wrongSha[wrongSha.Length - 1] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() =>
            LegacyHoleWizardTailBuilder.Build(wrongSha, template, definition, template.FeatureName));

        Assert.Throws<NotSupportedException>(() =>
            LegacyHoleWizardTailBuilder.Build(baseFile.OriginalStream, template, definition, template.FeatureName));

        Assert.Throws<NotSupportedException>(() =>
            LegacyHoleWizardTailBuilder.Build(stripped, template, definition, "Custom"));
    }
}
