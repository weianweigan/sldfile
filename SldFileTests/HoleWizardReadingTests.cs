using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled reading coverage for the SW2026 Hole Wizard samples:
/// legacy simple hole (moHoleWzd_c / 0x54 / stream 0x71) and tapped hole
/// (moHoleWzd_c / 0x52 / stream 0x84).
/// </summary>
public class HoleWizardReadingTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static Hit Read(string name)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
        Assert.Equal("moHoleWzd_c", feature.TypeName);
        return new Hit(feature, Assert.IsType<HoleWizardFeatureModel>(feature.Model));
    }

    private sealed record Hit(Feature Feature, HoleWizardFeatureModel Model);

    [Theory]
    [InlineData("hole_simple_d5_throughall", 1, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_simple_d5_throughall_resave", 1, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_simple_d5_blind10", 0, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_simple_d5_blind10_resave", 0, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_up_to_next", 11, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_up_to_next_resave", 11, 0.005, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_on_face_point", 1, 0.005, 0.002985353949839009, 0.007005198384888445)]
    [InlineData("hole_on_face_point_resave", 1, 0.005, 0.002985353949839009, 0.007005198384888445)]
    [InlineData("hole_counterbore_d8_d4_depth5", 1, 0.004, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_counterbore_d8_d4_depth5_resave", 1, 0.004, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_countersink_d8_d4_90", 1, 0.004, 0.009986487374987973, 0.010014305598872397)]
    [InlineData("hole_countersink_d8_d4_90_resave", 1, 0.004, 0.009986487374987973, 0.010014305598872397)]
    public void LegacyHole_ParsesTypeEndConditionDiameterAndPoint(
        string sample, int endCondition, double diameter, double x, double y)
    {
        var hit = Read(sample);
        var definition = hit.Model;
        Assert.Equal(0x54, hit.Feature.Id);
        Assert.Equal(0x54u, definition.FolderTypeId);
        Assert.Equal(0, definition.Type);
        Assert.Equal(endCondition, definition.EndCondition);
        Assert.Equal(diameter, definition.Diameter!.Value, 12);
        Assert.Equal(1, definition.GetSketchPointCount());
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(x, point.X, 12);
        Assert.Equal(y, point.Y, 12);

        // Not decoded without controlled variants.
        Assert.Null(definition.Depth);
        Assert.Null(definition.CounterBoreDiameter);
        Assert.Null(definition.CounterBoreDepth);
        Assert.Null(definition.CounterSinkDiameter);
        Assert.Null(definition.CounterSinkAngle);
        var face = Assert.IsType<FeatureReferenceData>(definition.Face);
        Assert.Equal(SelectionEntityType.Face, face.EntityType);
        Assert.Equal(6u, face.EntityId);
        Assert.Equal(0x200u, face.ClassWord);
        Assert.Equal(HoleWizardSegments.FaceArchiveGuid, face.ArchiveGuid);
        Assert.Equal("HoleFaceRef", face.ReferenceName);
        Assert.True(face.PersistentId.HasValue && face.PersistentId.Value != 0);
        var iFace = Assert.IsType<FeatureReferenceData>(definition.IFace);
        Assert.Equal(SelectionEntityType.Face, iFace.EntityType);
        Assert.Equal(6u, iFace.EntityId);
        Assert.Equal(0x200u, iFace.ClassWord);
        Assert.Equal(HoleWizardSegments.FaceArchiveGuid, iFace.ArchiveGuid);
        Assert.Equal("HoleCompFace", iFace.ReferenceName);
        Assert.True(iFace.PersistentId.HasValue && iFace.PersistentId.Value != 0);
        Assert.NotEqual(face.PersistentId, iFace.PersistentId);
        var pointRef = Assert.Single(definition.SketchPoints!);
        Assert.Equal(SelectionEntityType.Sketch, pointRef.EntityType);
        Assert.Equal("HoleSketchPoint", pointRef.ReferenceName);
        // moCompSolidBody_c carries no body id/class word in any legacy sample; its final u32
        // is the EndCondition, so no body terminator is claimed even for UpToNext.
        Assert.Null(definition.EndConditionReference);
        Assert.False(definition.IsResolved(nameof(HoleWizardFeatureModel.EndConditionReference)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.Face)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.IFace)));

        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.EndCondition)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.Diameter)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.SketchPointLocations)));
    }

    [Theory]
    [InlineData("hole_simple_d5_throughall")]
    [InlineData("hole_simple_d5_blind10")]
    [InlineData("hole_up_to_next")]
    [InlineData("hole_on_face_point")]
    [InlineData("hole_counterbore_d8_d4_depth5")]
    [InlineData("hole_countersink_d8_d4_90")]
    public void LegacyHole_FaceReferencesAreStableAcrossResave(string main)
    {
        var mainDefinition = Read(main).Model;
        var resaveDefinition = Read(main + "_resave").Model;
        foreach (var pair in new[]
                 {
                     (mainDefinition.Face, resaveDefinition.Face, "HoleFaceRef"),
                     (mainDefinition.IFace, resaveDefinition.IFace, "HoleCompFace")
                 })
        {
            var first = Assert.IsType<FeatureReferenceData>(pair.Item1);
            var second = Assert.IsType<FeatureReferenceData>(pair.Item2);
            Assert.Equal(SelectionEntityType.Face, first.EntityType);
            Assert.Equal(6u, first.EntityId);
            Assert.Equal(0x200u, first.ClassWord);
            Assert.Equal(HoleWizardSegments.FaceArchiveGuid, first.ArchiveGuid);
            Assert.Equal(pair.Item3, first.ReferenceName);
            Assert.True(first.PersistentId.HasValue && first.PersistentId.Value != 0);
            Assert.Equal(first.EntityId, second.EntityId);
            Assert.Equal(first.ClassWord, second.ClassWord);
            Assert.Equal(first.PersistentId, second.PersistentId);
            Assert.Equal(first.ArchiveGuid, second.ArchiveGuid);
        }
        Assert.NotEqual(mainDefinition.Face!.PersistentId, mainDefinition.IFace!.PersistentId);
        Assert.Null(mainDefinition.EndConditionReference);
        Assert.False(mainDefinition.IsResolved(nameof(HoleWizardFeatureModel.EndConditionReference)));
    }

    [Theory]
    [InlineData("hole_tapped_m6")]
    [InlineData("hole_tapped_m6_resave")]
    public void TappedHole_ParsesIsoM6ThreadFields(string sample)
    {
        var hit = Read(sample);
        var definition = hit.Model;
        Assert.Equal(0x52, hit.Feature.Id);
        Assert.Equal(0x52u, definition.FolderTypeId);
        Assert.Equal(48, definition.Type);
        Assert.Equal(1, definition.EndCondition);
        Assert.Equal("ISO", definition.Standard);
        Assert.Equal(8, definition.Standard2);
        Assert.Equal("螺纹孔", definition.FastenerType);
        Assert.Equal(147, definition.FastenerType2);
        Assert.Equal("M6", definition.FastenerSize);
        Assert.Equal(0.005999999999999975, definition.ThreadDiameter!.Value, 15);
        Assert.Equal(0.01, definition.ThreadDepth!.Value, 12);
        Assert.Equal(1, definition.CosmeticThreadType);
        Assert.Equal(1, definition.GetSketchPointCount());
        var point = Assert.Single(definition.SketchPointLocations!);
        Assert.Equal(0.009986487374987973, point.X, 12);
        Assert.Equal(0.010014305598872397, point.Y, 12);

        Assert.Null(definition.Diameter);
        Assert.Null(definition.Depth);
        Assert.Null(definition.ThreadClass);
        Assert.Null(definition.ThreadAngle);
        Assert.Null(definition.TapDrillDiameter);
        Assert.Null(definition.Face);
        Assert.Null(definition.IFace);
        Assert.Null(definition.EndConditionReference);
        Assert.False(definition.IsResolved(nameof(HoleWizardFeatureModel.Face)));
        Assert.False(definition.IsResolved(nameof(HoleWizardFeatureModel.IFace)));
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureModel.Type),
            nameof(HoleWizardFeatureModel.EndCondition),
            nameof(HoleWizardFeatureModel.Standard),
            nameof(HoleWizardFeatureModel.Standard2),
            nameof(HoleWizardFeatureModel.FastenerType),
            nameof(HoleWizardFeatureModel.FastenerType2),
            nameof(HoleWizardFeatureModel.FastenerSize),
            nameof(HoleWizardFeatureModel.ThreadDiameter),
            nameof(HoleWizardFeatureModel.ThreadDepth),
            nameof(HoleWizardFeatureModel.CosmeticThreadType)
        })
        {
            Assert.True(definition.IsResolved(name));
        }
    }

    [Fact]
    public void HoleWizardParser_RejectsTamperedLayoutsAndWrongGuards()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath("hole_simple_d5_throughall"), readOnly: true))
            original = (byte[])file.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "moHoleWzd_c");
        Assert.NotNull(HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWzd_c", 0x54, feature.TagOffset, original.Length));

        void Tamper(string className, int payloadOffset)
        {
            byte[] copy = (byte[])original.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            var element = copyDoc.AllElements.First(e => e.ClassName == className);
            copy[element.DataStart + payloadOffset] ^= 0xFF;
            var tampered = MfcDocument.Parse(copy);
            var tamperedFeature = tampered.AllElements.First(e => e.ClassName == "moHoleWzd_c");
            Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
                tampered, "moHoleWzd_c", 0x54, tamperedFeature.TagOffset, copy.Length));
        }

        Tamper("sgLLDist", 45);          // diameter high byte
        Tamper("moSkDimHandleValG2_c", 460); // diameter copy high byte
        Tamper("moCompSolidBody_c", 347);    // EndCondition
        Tamper("moHoleWzd_c", 298);          // sketch point X high byte (out of the controlled box)
        Tamper("moCompFace_c", 0x56 + HoleWizardSegments.FaceRecordClassWordOffset); // IFace class word
        Tamper("moSingleFaceRef_w", 0x58 + HoleWizardSegments.FaceRecordGuidOffset); // Face archive GUID
        Tamper("moCompFace_c", 0x56 + HoleWizardSegments.FaceRecordTailOffset);      // IFace record tail

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)0x70).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "moHoleWzd_c");
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            wrongDoc, "moHoleWzd_c", 0x54, wrongFeature.TagOffset, wrongVersion.Length));
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWzd_c", 0x52, feature.TagOffset, original.Length));
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWzd_c", 0x54, feature.TagOffset, feature.TagOffset + 100));
        Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWizard_c", 0x54, feature.TagOffset, original.Length));

        Assert.Equal(original, doc.SourceData);
    }

    [Fact]
    public void TappedHoleParser_RejectsTamperedThreadFields()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath("hole_tapped_m6"), readOnly: true))
            original = (byte[])file.RawStreams[ResolvedStream].Clone();

        void Tamper(string className, int payloadOffset)
        {
            byte[] copy = (byte[])original.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            var element = copyDoc.AllElements.First(e => e.ClassName == className);
            copy[element.DataStart + payloadOffset] ^= 0xFF;
            var tampered = MfcDocument.Parse(copy);
            var tamperedFeature = tampered.AllElements.First(e => e.ClassName == "moHoleWzd_c");
            Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
                tampered, "moHoleWzd_c", 0x52, tamperedFeature.TagOffset, copy.Length));
        }

        Tamper("ThreeDdiameterDim_c", 45);   // thread diameter high byte
        Tamper("moCThreadData_c", 36);       // thread diameter copy high byte
        Tamper("moCThreadData_c", 72);       // thread depth high byte
        Tamper("moAngleParameter_c", 14811); // Type = 48
        Tamper("moSingleFaceRef_w", 432);    // standard ISO
        Tamper("moSingleFaceRef_w", 452);    // fastener size M6
        Tamper("moSingleFaceRef_w", 532);    // Standard2 = 8
        Tamper("moCompSolidBody_c", 649);    // EndCondition
    }

    [Fact]
    public void HoleWizardMappingDoesNotModifyTheStream()
    {
        string path = SamplePath("hole_tapped_m6");
        byte[] before;
        using (var file = SwFile.Open(path, readOnly: true))
        {
            before = (byte[])file.RawStreams[ResolvedStream].Clone();
            var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.HoleWizard));
            Assert.IsType<HoleWizardFeatureModel>(feature.Model);
            Assert.Equal(before, file.RawStreams[ResolvedStream]);
        }

        using var reopened = SwFile.Open(path, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }
}
