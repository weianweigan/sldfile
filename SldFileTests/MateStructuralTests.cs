using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Structural tests for the MatesList stream. The three assembly samples establish
/// serialized class labels, display names and reference-wrapper kinds only; mate
/// type, alignment, entity topology and numeric parameters must stay unparsed.
/// </summary>
public class MateStructuralTests
{
    // ── Native samples ────────────────────────────────────────────────

    [Fact]
    public void Assembly2012_Mates_ExposeObservedClassLabelsAndReferences()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        Assert.Equal(4, assembly.Mates.Count);
        Assert.All(assembly.Mates, mate =>
        {
            Assert.True(mate.Resolution.ClassName);
            Assert.True(mate.Resolution.DisplayName);
            Assert.True(mate.Resolution.ReferenceStructure);
            Assert.True(mate.Resolution.MateType); // 2012 classes map to native types
            Assert.False(mate.Resolution.MateAlignment);
            Assert.False(mate.Resolution.MateParameters);
        });

        var pointMate = Assert.Single(assembly.Mates.Where(m => m.SerializedClassName == "moMateCoordinateM"));
        Assert.Equal("Coincident3", pointMate.DisplayName);
        Assert.Equal(AssemblyMateType.Coordinate, pointMate.MateType);
        Assert.Equal(20u, pointMate.MateTypeId);
        Assert.False(pointMate.Resolution.EntityReferences);
        var pointRef = Assert.Single(pointMate.References);
        Assert.Equal("moPointRef_w", pointRef.WrapperClassName);
        Assert.Equal(MateReferenceKind.Point, pointRef.Kind);
        Assert.Contains("moCompSketchEntHandle_c", pointRef.EntityClassNames);

        // Coincident4 has an inline class tag; Coincident5/6 are serialized with
        // the EA 80 4D 80 MFC class reference and are now recovered too.
        var planeMates = assembly.Mates
            .Where(m => m.SerializedClassName == "moMateCoincidentM")
            .ToArray();
        Assert.Equal(3, planeMates.Length);
        var planeMate = Assert.Single(planeMates.Where(m => m.DisplayName == "Coincident4"));
        Assert.Equal(AssemblyMateType.Coincident, planeMate.MateType);
        Assert.Equal(0u, planeMate.MateTypeId);
        var planeRef = Assert.Single(planeMate.References);
        Assert.Equal("moPlaneRef_w", planeRef.WrapperClassName);
        Assert.Equal(MateReferenceKind.Plane, planeRef.Kind);
        Assert.Contains("moCompFace_c", planeRef.EntityClassNames);

        // Two legacy face records: the tagged 0x192D and the F2 80 class-reference
        // instance 0x263. Component names are not serialized for these faces.
        Assert.Equal(2, planeMate.Entities.Count);
        Assert.Equal(
            new uint?[] { 0x192Du, 0x263u },
            planeMate.Entities.Select(e => e.PersistentId).ToArray());
        Assert.All(planeMate.Entities, e =>
        {
            Assert.Equal(MateReferenceKind.Plane, e.Kind);
            Assert.Empty(e.ComponentInstanceName);
        });

        var classRefPlaneMates = planeMates.Where(m => m.DisplayName != "Coincident4").ToArray();
        Assert.Equal(
            new[] { "Coincident5", "Coincident6" },
            classRefPlaneMates.Select(m => m.DisplayName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.All(classRefPlaneMates, m =>
        {
            Assert.Equal(AssemblyMateType.Coincident, m.MateType);
            Assert.False(m.Resolution.EntityReferences);
        });
    }

    [Fact]
    public void Assembly2026_Mates_ExposeVersionSpecificClassLabels()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        Assert.Equal(4, assembly.Mates.Count);
        var pointMate = Assert.Single(assembly.Mates.Where(m => m.SerializedClassName == "moMateCoordinate"));
        Assert.Equal("Coincident3", pointMate.DisplayName);
        Assert.Equal(MateReferenceKind.Point, Assert.Single(pointMate.References).Kind);

        // Coincident4 is the inline deferred-face record; Coincident5/6 use the
        // C1 80 class reference and decode as controlled coincident mates now.
        var planeMates = assembly.Mates
            .Where(m => m.SerializedClassName == "moMateCoincident")
            .ToArray();
        Assert.Equal(3, planeMates.Length);
        var planeMate = Assert.Single(planeMates.Where(m => m.DisplayName == "Coincident4"));
        var planeRef = Assert.Single(planeMate.References);
        Assert.Equal(MateReferenceKind.Plane, planeRef.Kind);
        // Observed 2026 layout difference: the plane wrapper's direct child is an
        // unnamed serialized tag (face path stored elsewhere in the stream), so no
        // direct entity class name is claimed. This must not be guessed from 2012.
        Assert.Empty(planeRef.EntityClassNames);

        var classRefMates = planeMates.Where(m => m.DisplayName != "Coincident4").ToArray();
        Assert.Equal(2, classRefMates.Length);
        Assert.All(classRefMates, mate =>
        {
            Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
            Assert.Equal(2, mate.Entities.Count);
            Assert.True(mate.Resolution.EntityReferences);
        });
    }

    // ── Unknown / reject paths ────────────────────────────────────────

    [Fact]
    public void UnknownMateLayout_IsRecognizedButNotGuessed()
    {
        byte[] stream = BuildMatesListStream(
            MfcTag("moMateDistance", 0x4D, 0x80),
            WChar("Distance1"));

        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);

        var entry = Assert.Single(model.Mates);
        Assert.Equal("moMateDistanceM", entry.ClassName);
        Assert.Equal("Distance1", entry.DisplayName);
        Assert.Empty(entry.References);

        var info = new AssemblyMateInfo(entry);
        Assert.Empty(info.References);
        Assert.True(info.Resolution.ClassName);
        Assert.True(info.Resolution.DisplayName);
        Assert.False(info.Resolution.ReferenceStructure);
        Assert.False(info.Resolution.MateType);
        Assert.False(info.Resolution.MateParameters);
    }

    [Fact]
    public void UnknownReferenceWrapper_MapsToUnknownKind()
    {
        var entry = new MateEntry
        {
            ClassName = "moMateDistance",
            DisplayName = "Distance1",
            References = new[]
            {
                new MateReferenceEntry
                {
                    WrapperClassName = "moCylinderRef_w",
                    EntityClassNames = Array.Empty<string>()
                }
            }
        };

        var info = new AssemblyMateInfo(entry);
        var reference = Assert.Single(info.References);
        Assert.Equal(MateReferenceKind.Unknown, reference.Kind);
        Assert.True(info.Resolution.ReferenceStructure);
        Assert.False(info.Resolution.EntityReferences);
        Assert.False(info.Resolution.MateParameters);
    }

    [Fact]
    public void MaterialClass_IsNotRecognizedAsMate()
    {
        byte[] stream = BuildMatesListStream(
            MfcTag("moMaterial_c"),
            WChar("Material <not specified>"));

        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);

        Assert.Empty(model.Mates);
    }

    [Fact]
    public void MappingMatesList_DoesNotModifyInputStream()
    {
        byte[] stream = BuildMatesListStream(
            MfcTag("moMateCoincident", 0x4D, 0x80),
            WChar("Coincident9"));
        byte[] before = (byte[])stream.Clone();

        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);

        Assert.Equal(before, stream);
        Assert.Single(model.Mates);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static byte[] BuildMatesListStream(params byte[][] parts)
    {
        var bytes = new List<byte> { 0x04, 0x00 }; // MatesList stream version
        foreach (byte[] part in parts)
            bytes.AddRange(part);
        bytes.AddRange(new byte[48]);              // header padding for u32/u16 reads
        return bytes.ToArray();
    }

    private static byte[] MfcTag(string className, params byte[] suffix)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) };
        bytes.AddRange(ascii);
        bytes.AddRange(suffix);
        return bytes.ToArray();
    }

    private static byte[] WChar(string value)
    {
        var bytes = new List<byte> { 0xFF, 0xFE, 0xFF, (byte)value.Length };
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        return bytes.ToArray();
    }
}
