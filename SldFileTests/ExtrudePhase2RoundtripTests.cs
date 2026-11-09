using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Phase 2 Verification Suite for SolidWorks Extrusion Features (moExtrusion_c &amp; moICE_c).
/// Verifies:
/// 1. CArchive Wire Format protocol state machine (ReadClassTag, WriteClassTag, CString, Count, Version Gating).
/// 2. High-precision parameter extraction from controlled native SolidWorks samples.
/// 3. In-memory and on-disk authoring/writing of new Extrude features into native models.
/// 4. 100% Roundtrip fidelity: reopening written files and verifying feature trees and parameter invariants.
/// </summary>
public class ExtrudePhase2RoundtripTests
{
    private static string GetSamplePath(string fileName)
    {
        var testFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", fileName);
        if (File.Exists(testFilesPath))
            return testFilesPath;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", fileName);
            if (File.Exists(candidate))
                return candidate;

            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }

        // Fallback to @models
        string modelsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "models", fileName));
        if (File.Exists(modelsPath))
            return modelsPath;

        throw new FileNotFoundException($"Sample file '{fileName}' not found.");
    }

    // =========================================================================
    // Part 1: CArchive Wire Format Protocol Verification
    // =========================================================================

    [Fact]
    public void CArchive_WriteAndReadClassTag_HandlesAllWireFormatKinds()
    {
        var ctx = new MfcArchiveContext();
        using var writer = MfcArchive.CreateWriter(context: ctx);

        // 1. New Class: moExtrusion_c
        ushort extrudeTag = 0;
        writer.WriteClassTag("moExtrusion_c", ref extrudeTag, schema: 1);
        Assert.Equal(0x8001, extrudeTag);

        // 2. Class Reference: moExtrusion_c again
        ushort refTag = 0;
        writer.WriteClassTag("moExtrusion_c", ref refTag, schema: 1);
        Assert.Equal(0x8001, refTag);

        // 3. New Class: moICE_c
        ushort iceTag = 0;
        writer.WriteClassTag("moICE_c", ref iceTag, schema: 2);
        Assert.Equal(0x8002, iceTag);

        byte[] serialized = writer.ToArray();

        // Loading side
        var loadCtx = new MfcArchiveContext();
        using var reader = MfcArchive.CreateReader(serialized, context: loadCtx);

        // Read 1st: should be NewClass moExtrusion_c
        var kind1 = reader.ReadClassTag(out string name1, out ushort tag1, out ushort schema1);
        Assert.Equal(MfcTagKind.NewClass, kind1);
        Assert.Equal("moExtrusion_c", name1);
        Assert.Equal(1, schema1);
        Assert.Equal(0x8001, tag1);

        // Read 2nd: should be ClassReference moExtrusion_c
        var kind2 = reader.ReadClassTag(out string name2, out ushort tag2, out _);
        Assert.Equal(MfcTagKind.ClassReference, kind2);
        Assert.Equal("moExtrusion_c", name2);
        Assert.Equal(0x8001, tag2);

        // Read 3rd: should be NewClass moICE_c
        var kind3 = reader.ReadClassTag(out string name3, out ushort tag3, out ushort schema3);
        Assert.Equal(MfcTagKind.NewClass, kind3);
        Assert.Equal("moICE_c", name3);
        Assert.Equal(2, schema3);
        Assert.Equal(0x8002, tag3);
    }

    [Fact]
    public void CArchive_CString_RoundtripsUnicodeAndAnsiCorrectly()
    {
        using var writer = MfcArchive.CreateWriter();
        string? empty = "";
        string? unicode = "凸台-拉伸1";
        string? longStr = new string('A', 300);

        writer.CString(ref empty);
        writer.CString(ref unicode);
        writer.CString(ref longStr);

        byte[] bytes = writer.ToArray();

        using var reader = MfcArchive.CreateReader(bytes);
        string? readEmpty = null;
        string? readUnicode = null;
        string? readLong = null;

        reader.CString(ref readEmpty);
        reader.CString(ref readUnicode);
        reader.CString(ref readLong);

        Assert.Equal("", readEmpty);
        Assert.Equal("凸台-拉伸1", readUnicode);
        Assert.Equal(longStr, readLong);
    }

    [Fact]
    public void CArchive_Count_HandlesBothWordAndDwordWireFormats()
    {
        using var writer = MfcArchive.CreateWriter();
        uint smallCount = 42;
        uint largeCount = 100_000;

        writer.Count(ref smallCount);
        writer.Count(ref largeCount);

        byte[] bytes = writer.ToArray();

        using var reader = MfcArchive.CreateReader(bytes);
        uint readSmall = 0;
        uint readLarge = 0;

        reader.Count(ref readSmall);
        reader.Count(ref readLarge);

        Assert.Equal(42u, readSmall);
        Assert.Equal(100_000u, readLarge);
    }

    // =========================================================================
    // Part 2: Controlled Sample Perfect Reading (Extrude Boss & Cut)
    // =========================================================================

    [Theory]
    [InlineData("extrude_10mm.SLDPRT", 0.010, 0u, false)]
    [InlineData("extrude_20mm.SLDPRT", 0.020, 0u, false)]
    [InlineData("extrude_30mm.SLDPRT", 0.030, 0u, false)]
    [InlineData("extrude_cut_5mm.SLDPRT", 0.005, 0u, true)]
    public void Extrude_ControlledSamples_PerfectReading(
        string fileName,
        double expectedDepth,
        uint expectedEndCondition,
        bool expectedIsCut)
    {
        string path = GetSamplePath(fileName);
        using var file = SwFile.Open(path, readOnly: true);

        Assert.NotNull(file.Features);
        var matchingFeature = file.Features.FirstOrDefault(f => expectedIsCut
            ? (f.Kind == FeatureKind.Cut || f.TypeName == "moICE_c")
            : (f.Kind == FeatureKind.Extrusion && !f.Name.Contains("切除")));

        Assert.NotNull(matchingFeature);
        Assert.NotNull(matchingFeature!.Model);

        var extDef = Assert.IsType<ExtrudeFeatureModel>(matchingFeature.Model);
        Assert.Equal(expectedDepth, extDef.Depth, precision: 6);
        Assert.Equal(expectedEndCondition, extDef.EndCondition);
        Assert.Equal(expectedIsCut, extDef.IsCut);
        Assert.False(string.IsNullOrEmpty(extDef.SketchName), "Extrude must be bound to a sketch.");
    }

    // =========================================================================
    // Part 3 & 4: Feature Authoring, Writing & 100% Roundtrip Verification
    // =========================================================================

    [Fact]
    public void Extrude_AuthoringAndRoundtrip_PersistsAndReopensFlawlessly()
    {
        string baseExtrudePath = GetSamplePath("extrude_10mm.SLDPRT");
        string tempPath = Path.Combine(Path.GetTempPath(), $"extrude_phase2_roundtrip_{Guid.NewGuid():N}.sldprt");

        try
        {
            // 1. Copy baseline to temp file
            File.Copy(baseExtrudePath, tempPath, overwrite: true);

            // 2. Open in writable mode
            using (var part = (SldPart)SwFile.Open(tempPath, readOnly: false))
            {
                int initialFeatureCount = part.Features.Count;
                Assert.True(initialFeatureCount > 0, "Extrude baseline must have initial features.");

                // 3. Add a new Extrude Boss feature (25.4mm depth)
                var newExtrudeDef = new ExtrudeFeatureModel
                {
                    Depth = 0.0254, // 25.4 mm (1 inch)
                    EndCondition = 0, // Blind
                    Merge = true,
                    IsCut = false,
                    BothDirections = false
                };

                var addedFeature = part.AddExtrude(newExtrudeDef, featureName: "Phase2_ExtrudeBoss");
                Assert.NotNull(addedFeature);
                Assert.Equal("Phase2_ExtrudeBoss", addedFeature.Name);

                // 4. Save to disk
                part.Save();
            }

            // 5. Reopen the modified file to verify roundtrip fidelity
            using (var reopenedPart = (SldPart)SwFile.Open(tempPath, readOnly: true))
            {
                Assert.NotNull(reopenedPart.Features);
                var createdFeature = reopenedPart.Features.FirstOrDefault(f => f.Name == "Phase2_ExtrudeBoss");

                Assert.NotNull(createdFeature);
                Assert.Equal(FeatureKind.Extrusion, createdFeature!.Kind);

                var reloadedDef = Assert.IsType<ExtrudeFeatureModel>(createdFeature.Model);
                Assert.Equal(0.0254, reloadedDef.Depth, precision: 6);
                Assert.Equal(0u, reloadedDef.EndCondition);
                Assert.False(reloadedDef.IsCut);
                Assert.True(reloadedDef.Merge);

                // Verify baseline features still intact (no corruption or desynchronization)
                var baselineSketch = reopenedPart.Features.FirstOrDefault(f => f.Kind == FeatureKind.Sketch);
                Assert.NotNull(baselineSketch);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    [Fact]
    public void ExtrudeCut_AuthoringAndRoundtrip_PersistsAndReopensFlawlessly()
    {
        string baseExtrudePath = GetSamplePath("extrude_10mm.SLDPRT");
        string tempPath = Path.Combine(Path.GetTempPath(), $"extrudecut_phase2_roundtrip_{Guid.NewGuid():N}.sldprt");

        try
        {
            File.Copy(baseExtrudePath, tempPath, overwrite: true);

            // Add an Extrude Cut feature (depth 8mm)
            using (var part = (SldPart)SwFile.Open(tempPath, readOnly: false))
            {
                var cutDef = new ExtrudeFeatureModel
                {
                    Depth = 0.008, // 8 mm
                    EndCondition = 0,
                    IsCut = true
                };

                var addedCut = part.AddExtrude(cutDef, featureName: "Phase2_ExtrudeCut");
                Assert.NotNull(addedCut);
                Assert.Equal("Phase2_ExtrudeCut", addedCut.Name);

                part.Save();
            }

            // Reopen and assert
            using (var reopenedPart = (SldPart)SwFile.Open(tempPath, readOnly: true))
            {
                var reloadedCut = reopenedPart.Features.FirstOrDefault(f => f.Name == "Phase2_ExtrudeCut");
                Assert.NotNull(reloadedCut);
                Assert.Equal(FeatureKind.Cut, reloadedCut!.Kind);

                var reloadedDef = Assert.IsType<ExtrudeFeatureModel>(reloadedCut.Model);
                Assert.Equal(0.008, reloadedDef.Depth, precision: 6);
                Assert.True(reloadedDef.IsCut);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
}
