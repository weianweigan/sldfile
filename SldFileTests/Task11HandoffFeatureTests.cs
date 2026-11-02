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
/// Agent 5 Task 1.1/1.2 handoff: DraftOutward cut, UpToVertex (blocked), thin CapEnds,
/// FromOffsetReverse cut, thin AutoFilletCorners and revolve axis=model edge.
/// </summary>
public class Task11HandoffFeatureTests
{
    private const string Root = @"TestFiles\Features2026\Task11Handoff";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private static Task11FeatureAuthoringTemplate TemplateFor(string file) =>
        Assert.Single(Task11FeatureAuthoringTemplates.All.Where(t => t.Sample == file));

    private static readonly (string File, string Name)[] ReadSamples =
    {
        ("task11_extrude_draft_outward_true.SLDPRT", "DraftOutward"),
        ("task11_extrude_draft_outward_true_resave.SLDPRT", "DraftOutward"),
        ("task11_extrude_thin_cap_ends.SLDPRT", "Thin_0_0.001"),
        ("task11_extrude_thin_cap_ends_resave.SLDPRT", "Thin_0_0.001"),
        ("task11_extrude_from_offset_reverse.SLDPRT", "FromOffsetReverseBoss"),
        ("task11_extrude_from_offset_reverse_resave.SLDPRT", "FromOffsetReverseBoss"),
        ("task11_extrude_auto_fillet.SLDPRT", "AutoFilletThin"),
        ("task11_extrude_auto_fillet_resave.SLDPRT", "AutoFilletThin"),
        ("task11_revolve_axis_model_edge.SLDPRT", "RevolveAxisEdge"),
        ("task11_revolve_axis_model_edge_resave.SLDPRT", "RevolveAxisEdge")
    };

    public static IEnumerable<object[]> ReadCases =>
        ReadSamples.Select(s => new object[] { s.File, s.Name });

    [Theory]
    [MemberData(nameof(ReadCases))]
    public void ReadsNewFields(string file, string name)
    {
        using var sw = SwFile.Open(PathOf(file), readOnly: true);
        var feature = Assert.Single(sw.Features.Where(f => f.Name == name));
        if (file.Contains("revolve", StringComparison.Ordinal))
        {
            var d = Assert.IsType<RevolveFeatureDefinitionData>(feature.Definition);
            Assert.Equal(2 * Math.PI, d.Angle, 12);
            Assert.Equal(1, d.AxisType);
            Assert.NotNull(d.AxisReference);
            Assert.Equal(SelectionEntityType.Edge, d.AxisReference!.EntityType);
            Assert.Equal("moCompEdge_c", d.AxisReference.ReferenceName);
            Assert.Equal(0x55u, d.FolderTypeId);
            Assert.False(d.IsCut);
            Assert.False(d.IsThinFeature);
            Assert.True(d.IsResolved(nameof(RevolveFeatureDefinitionData.AxisType)));
            Assert.True(d.IsResolved(nameof(RevolveFeatureDefinitionData.AxisReference)));
            Assert.True(d.IsResolved(nameof(RevolveFeatureDefinitionData.Angle)));
            return;
        }

        var def = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        if (file.Contains("draft_outward", StringComparison.Ordinal))
        {
            Assert.True(def.IsCut);
            Assert.Equal(0.010, def.Depth, 12);
            Assert.True(def.DraftWhileExtruding);
            Assert.Equal(5.0 * Math.PI / 180.0, def.DraftAngle, 12);
            Assert.True(def.DraftOutward);
            Assert.True(def.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftOutward)));
        }
        else if (file.Contains("thin_cap_ends", StringComparison.Ordinal))
        {
            Assert.True(def.IsThinFeature);
            Assert.Equal(0.010, def.Depth, 12);
            Assert.Equal(0.001, def.WallThickness, 12);
            Assert.Equal(0u, def.ThinWallType);
            Assert.True(def.CapEnds);
            Assert.False(def.AutoFilletCorners);
        }
        else if (file.Contains("auto_fillet", StringComparison.Ordinal))
        {
            Assert.True(def.IsThinFeature);
            Assert.Equal(0.010, def.Depth, 12);
            Assert.Equal(0.001, def.WallThickness, 12);
            Assert.True(def.AutoFilletCorners);
            Assert.Equal(0.001, def.AutoFilletRadius, 12);
            Assert.False(def.CapEnds);
            Assert.True(def.IsResolved(nameof(ExtrudeFeatureDefinitionData.AutoFilletCorners)));
            Assert.True(def.IsResolved(nameof(ExtrudeFeatureDefinitionData.AutoFilletRadius)));
        }
        else if (file.Contains("from_offset_reverse", StringComparison.Ordinal))
        {
            Assert.True(def.IsCut);
            Assert.Equal(0.005, def.Depth, 12);
            Assert.Equal(3u, def.FromType);
            Assert.Equal(0.005, def.FromOffsetDistance, 12);
            Assert.True(def.FromOffsetReverse);
            Assert.True(def.IsResolved(nameof(ExtrudeFeatureDefinitionData.FromType)));
            Assert.True(def.IsResolved(nameof(ExtrudeFeatureDefinitionData.FromOffsetReverse)));
        }
    }

    [Fact]
    public void UpToVertex_RemainsBlocked_NestedFeatureNotExposed()
    {
        // Stream 0x72 stores UpToVertexBoss as a nested inline record (name at 0x60FC inside
        // moCompFace_c); there is no top-level folder tag to strip and the existing parser
        // returns no definition. This guard keeps the field unresolved rather than guessing.
        using var sw = SwFile.Open(PathOf("task11_extrude_upto_vertex.SLDPRT"), readOnly: true);
        Assert.DoesNotContain(sw.Features, f => f.Name == "UpToVertexBoss");
    }

    private sealed record BaseFile(TempTestFile Temp, byte[] OriginalStream, Task11FeatureAuthoringTemplate Template) : IDisposable
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
                var target = folders.Last(f => f.ClassName == template.ClassName);
                byte[] stripped = raw.AsSpan(0, target.TagOffset).ToArray();
                ushort count = BitConverter.ToUInt16(stripped, 4);
                BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
                sw.SetPendingStream(StreamKey(sw), stripped);
                sw.Save();
            }
            return new BaseFile(temp, original, template);
        }
    }

    private static ExtrudeFeatureDefinitionData ExtrudeDefinition(string file) => file switch
    {
        "task11_extrude_draft_outward_true.SLDPRT" => new ExtrudeFeatureDefinitionData
        {
            IsCut = true, Depth = 0.010, DraftWhileExtruding = true,
            DraftAngle = 5.0 * Math.PI / 180.0, DraftOutward = true
        },
        "task11_extrude_thin_cap_ends.SLDPRT" => new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010, IsThinFeature = true, ThinWallType = 0, WallThickness = 0.001,
            CapEnds = true
        },
        "task11_extrude_from_offset_reverse.SLDPRT" => new ExtrudeFeatureDefinitionData
        {
            IsCut = true, Depth = 0.005, FromType = 3, FromOffsetDistance = 0.005,
            FromOffsetReverse = true
        },
        "task11_extrude_auto_fillet.SLDPRT" => new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010, IsThinFeature = true, ThinWallType = 0, WallThickness = 0.001,
            AutoFilletCorners = true, AutoFilletRadius = 0.001
        },
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    private static RevolveFeatureDefinitionData RevolveDefinition() => new()
    {
        Angle = 2 * Math.PI,
        AxisType = 1,
        AxisReference = new FeatureReferenceData { EntityType = SelectionEntityType.Edge, ReferenceName = "moCompEdge_c" }
    };

    private static readonly string[] AuthoringSamples =
    {
        "task11_extrude_draft_outward_true.SLDPRT",
        "task11_extrude_thin_cap_ends.SLDPRT",
        "task11_extrude_from_offset_reverse.SLDPRT",
        "task11_extrude_auto_fillet.SLDPRT",
        "task11_revolve_axis_model_edge.SLDPRT"
    };

    public static IEnumerable<object[]> AuthoringCases =>
        AuthoringSamples.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(AuthoringCases))]
    public void FixedAuthoringReproducesOriginalStream(string file)
    {
        using var baseFile = CreateBase(file);
        using (var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            Feature feature = baseFile.Template.IsRevolve
                ? part.AddRevolve(RevolveDefinition())
                : part.AddExtrude(ExtrudeDefinition(file));
            Assert.Equal(baseFile.Template.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(baseFile.Temp.FilePath, readOnly: true);
        Assert.Equal(baseFile.OriginalStream, reopened.RawStreams[StreamKey(reopened)]);
        Assert.Contains(reopened.Features, f => f.Name == baseFile.Template.FeatureName);
    }

    [Theory]
    [MemberData(nameof(AuthoringCases))]
    public void RejectsCustomNamesWrongFieldsAndTamperedBases(string file)
    {
        using var baseFile = CreateBase(file);
        using var sw = SwFile.Open(baseFile.Temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        string key = StreamKey(part);
        byte[] before = (byte[])part.RawStreams[key].Clone();

        if (baseFile.Template.IsRevolve)
        {
            Assert.Throws<NotSupportedException>(() => part.AddRevolve(RevolveDefinition(), featureName: "Custom"));
            var wrongAngle = RevolveDefinition();
            wrongAngle.Angle = Math.PI;
            Assert.Throws<NotSupportedException>(() => part.AddRevolve(wrongAngle));
            var wrongAxis = RevolveDefinition();
            wrongAxis.AxisType = 2;
            Assert.Throws<NotSupportedException>(() => part.AddRevolve(wrongAxis));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(ExtrudeDefinition(file), featureName: "Custom"));
            var wrong = ExtrudeDefinition(file);
            switch (file)
            {
                case "task11_extrude_draft_outward_true.SLDPRT": wrong.DraftOutward = false; break;
                case "task11_extrude_thin_cap_ends.SLDPRT": wrong.CapEnds = false; break;
                case "task11_extrude_from_offset_reverse.SLDPRT": wrong.FromOffsetReverse = false; break;
                case "task11_extrude_auto_fillet.SLDPRT": wrong.AutoFilletRadius = 0.002; break;
            }
            Assert.Throws<NotSupportedException>(() => part.AddExtrude(wrong));
        }
        Assert.Equal(before, part.RawStreams[key]);

        byte[] stripped = part.RawStreams[key];
        var template = baseFile.Template;
        byte[] wrongFolder = (byte[])stripped.Clone();
        BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(wrongFolder, 4) + 1)).CopyTo(wrongFolder, 4);
        Assert.Throws<NotSupportedException>(() => Build(template, wrongFolder, out _, out _));
        byte[] wrongVersion = (byte[])stripped.Clone();
        wrongVersion[0] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() => Build(template, wrongVersion, out _, out _));
        byte[] wrongSha = (byte[])stripped.Clone();
        wrongSha[wrongSha.Length - 1] ^= 0xFF;
        Assert.Throws<NotSupportedException>(() => Build(template, wrongSha, out _, out _));
        Assert.Throws<NotSupportedException>(() => Build(template, stripped, out _, out _, "Custom"));
    }

    private static void Build(
        Task11FeatureAuthoringTemplate template, byte[] stream, out byte[] result, out Feature? feature,
        string? name = null)
    {
        string resolved = name ?? template.FeatureName;
        if (template.IsRevolve)
            result = Task11FeatureAuthoringBuilder.BuildRevolve(stream, template, RevolveDefinition(), resolved);
        else
            result = Task11FeatureAuthoringBuilder.BuildExtrude(stream, template,
                ExtrudeDefinition(template.Sample), resolved);
        feature = null;
    }
}
