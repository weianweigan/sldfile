using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed-measurement UpToSurface cut authoring on the source and resave stripped bases.
/// The target face/body scope are intentionally bound to the measured block; no parameter is varied.
/// </summary>
public class ExtrudeUpToSurfaceAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const uint BodyId = 2479;

    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeStream)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            nativeStream = raw;
            var doc = MfcDocument.Parse(raw);
            var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moICE_c"));
            int tag = feature.TagOffset;
            byte[] stripped = new byte[tag];
            Array.Copy(raw, 0, stripped, 0, tag);
            BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(stripped, 4) - 1)).CopyTo(stripped, 4);
            int marker = tag - 0x10;
            Assert.Equal(0x67, stripped[marker]);
            BitConverter.GetBytes(0u).CopyTo(stripped, marker + 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        using var check = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.True(check.RawStreams[ResolvedStream].Length < nativeStream.Length);
        return temp;
    }

    private static ExtrudeFeatureModel Definition(uint? faceId = null, uint? bodyId = null)
    {
        var definition = new ExtrudeFeatureModel
        {
            IsCut = true,
            EndCondition = 4,
            Depth = 0,
            Merge = true,
            FeatureScope = true,
            AutoSelect = false
        };
        if (faceId.HasValue)
        {
            definition.EndConditionEntity = new EndConditionEntityReference
            {
                EntityType = SelectionEntityType.Face,
                EntityId = faceId.Value,
                FaceCount = 1,
                SurfaceName = $"Face_0x{faceId.Value:X}"
            };
        }
        if (bodyId.HasValue)
        {
            definition.BodyScope = new BodyScopeData
            {
                AutoSelect = false,
                Flags = 0,
                BodyIds = new[] { bodyId.Value },
                Bodies = new[]
                {
                    new EntityReference { EntityId = bodyId.Value, EntityType = SelectionEntityType.Body }
                }
            };
        }
        return definition;
    }

    private static ExtrudeFeatureModel ReadCut(SwFile file)
    {
        var feature = Assert.Single(file.Features.Where(f => f.TypeName == "moICE_c" && f.Model is ExtrudeFeatureModel));
        return Assert.IsType<ExtrudeFeatureModel>(feature.Model);
    }

    [Fact]
    public void SourceNativeValuesReproduceTheMeasuredFile()
    {
        using var temp = CreateStrippedBase("extrude_upto_surface_modify", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddExtrude(Definition(0x7CB, BodyId));
            Assert.Equal(FeatureKind.Cut, created.Kind);
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = ReadCut(reopened);
        Assert.True(definition.IsCut);
        Assert.Equal(4u, definition.EndCondition);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.EndCondition)));
        Assert.Equal(0d, definition.Depth);
        Assert.Equal(SelectionEntityType.Face, definition.EndConditionEntity!.EntityType);
        Assert.Equal(0x7CBu, definition.EndConditionEntity.EntityId);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.EndConditionEntity)));
        Assert.False(definition.BodyScope!.AutoSelect);
        Assert.Equal(new[] { BodyId }, definition.BodyScope.BodyIds.ToArray());
        Assert.True(definition.FeatureScope);
        Assert.False(definition.AutoSelect);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.BodyScope)));

        byte[] before;
        using (var resave = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])resave.RawStreams[ResolvedStream].Clone();
            resave.Save();
        }
        using var after = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, after.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ResaveNativeValuesReproduceTheMeasuredResaveFile()
    {
        using var temp = CreateStrippedBase("extrude_upto_surface_modify_resave", out byte[] nativeStream);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(0x0AFA, BodyId));
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(nativeStream, reopened.RawStreams[ResolvedStream]);
        var definition = ReadCut(reopened);
        Assert.Equal(4u, definition.EndCondition);
        Assert.Equal(0x0AFAu, definition.EndConditionEntity!.EntityId);
        Assert.Equal(new[] { BodyId }, definition.BodyScope!.BodyIds.ToArray());
    }

    [Fact]
    public void MeasuredDefaultsCanBeSuppliedByTheTemplate()
    {
        using var temp = CreateStrippedBase("extrude_upto_surface_modify", out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition());
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = ReadCut(reopened);
        Assert.Equal(4u, definition.EndCondition);
        Assert.Equal(0x7CBu, definition.EndConditionEntity!.EntityId);
        Assert.Equal(new[] { BodyId }, definition.BodyScope!.BodyIds.ToArray());
    }

    [Fact]
    public void FaceAndBodyBindingsMustMatchTheSelectedBase()
    {
        using var source = CreateStrippedBase("extrude_upto_surface_modify", out _);
        using (var sw = SwFile.Open(source.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            Assert.Contains("target face",
                Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(0x0AFA, BodyId))).Message);
            Assert.Contains("body scope",
                Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(0x7CB, 9999))).Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void WrongDepthConditionOrBaseIsRejectedBeforeMutation()
    {
        using var source = CreateStrippedBase("extrude_upto_surface_modify", out _);
        using (var sw = SwFile.Open(source.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();

            var depth = Definition(0x7CB, BodyId);
            depth.Depth = 0.010;
            Assert.Contains("Depth", Assert.Throws<NotSupportedException>(() => part.AddExtrude(depth)).Message);

            var offset = Definition(0x7CB, BodyId);
            offset.EndCondition = 5;
            offset.Depth = 0.010;
            Assert.Contains("EndCondition", Assert.Throws<NotSupportedException>(() => part.AddExtrude(offset)).Message);

            var vertex = Definition(0x7CB, BodyId);
            vertex.EndCondition = 3;
            vertex.Depth = 0.010;
            Assert.Contains("EndCondition", Assert.Throws<NotSupportedException>(() => part.AddExtrude(vertex)).Message);

            var name = Assert.Throws<NotSupportedException>(
                () => part.AddExtrude(Definition(0x7CB, BodyId), featureName: "CustomUpToSurface"));
            Assert.Contains("UpToSurfaceModify", name.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using var draft = CreateStrippedBase("extrude_draft_dir2_3deg", out _);
        using (var sw = SwFile.Open(draft.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(0x7CB, BodyId)));
            Assert.Contains("stripped base length", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }
}
