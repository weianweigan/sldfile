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
/// Strict read-only coverage for the legacy tutorial HoleWizard families. Each family is
/// matched by version/folder count/FolderHeader type/rest length, and every exposed field is
/// read from an exact class-relative slot whose expected value came from the native API
/// snapshot. No legacy authoring exists.
/// </summary>
public class LegacyHoleWizardTutorialTests
{
    private const string Root = @"TestFiles\Features2026\HoleWizardLegacyTutorials";

    private sealed record Expectation(string File, int? Type, int? EndCondition);

    private static readonly Expectation[] Expectations =
    {
        new("01_b_plate_pcs2b.sldprt", 0, 0),
        new("02_ejector_plate_pcs2b.sldprt", 0, 1),
        new("03_support_plate_pcs2b.sldprt", 0, 0),
        new("04_Straight_solid_coupling.SLDPRT", 46, 0),
        new("05_roller_hanger.sldprt", 25, 1),
        new("06_strap_hanger.sldprt", 22, 4),
        new("07_balloon_series_f_valves_w_hwheel.sldprt", 48, 2),
        new("08_die_insert.sldprt", 14, 1),
        new("09_Complex_Sheet_Metal_Part.SLDPRT", 25, 2),
        new("10_connector.sldprt", 0, 0),
        new("11_box.sldprt", 18, 1),
        new("12_clamp2.sldprt", 14, 1),
        new("13_cover_datum.sldprt", 44, 1),
        new("14_cover_position.sldprt", 22, 0),
        new("15_maingrip.sldprt", null, null),
        new("16_mount_plate.sldprt", 48, 1),
        new("17_nist_ftc_08_asme1_rc_sw1702_ra.SLDPRT", 25, 1),
        new("18_head.sldprt", 14, 1),
        new("19_table.sldprt", 46, 0),
        new("20_flange.sldprt", 25, 1),
        new("21_baseplate.sldprt", 14, 1),
        new("22_slider.sldprt", null, null),
        new("23_top_plate.sldprt", 52, 1)
    };

    public static IEnumerable<object[]> Families() =>
        Expectations.Select(e => new object[] { e.File, e.Type!, e.EndCondition! });

    private static string SamplePath(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static (Hit Hit, LegacyHoleWizardFamily? Family, int Start) Read(string file)
    {
        using var sw = SwFile.Open(SamplePath(file), readOnly: true);
        byte[] raw = sw.RawStreams.Keys
            .Where(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase))
            .Select(k => sw.RawStreams[k]).First();
        var doc = MfcDocument.Parse((byte[])raw.Clone());
        var folders = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> fs) { foreach (var f in fs) { folders.Add(f); Walk(f.Children); } }
        Walk(sw.ResolvedFeatures.Folders);
        var hole = folders.FirstOrDefault(f => f.ClassName == "moHoleWzd_c");
        if (hole == null) return (new Hit(null, null), null, 0);
        int end = folders.Where(f => f.TagOffset > hole.TagOffset)
            .Select(f => f.TagOffset).DefaultIfEmpty(raw.Length).Min();
        end = Math.Min(end, raw.Length);
        var family = LegacyHoleWizardTutorialParser.MatchFamily(doc, hole.TagOffset, end);
        var definition = HoleWizardFeatureParser.ParseHoleWizard(
            doc, "moHoleWzd_c", 0, hole.TagOffset, end);
        return (new Hit(definition, raw, hole.TagOffset, end), family, hole.TagOffset);
    }

    private sealed record Hit(HoleWizardFeatureModel? Model, byte[]? Raw, int Start = 0, int End = 0);

    [Theory]
    [MemberData(nameof(Families))]
    public void LegacyFamily_ReadsProvableFields(string file, int? type, int? endCondition)
    {
        var (hit, family, _) = Read(file);
        if (type == null)
        {
            Assert.Null(hit.Model);
            Assert.Null(family);
            return;
        }
        var definition = Assert.IsType<HoleWizardFeatureModel>(hit.Model);
        Assert.NotNull(family);
        Assert.Equal(type, definition.Type);
        Assert.Equal(endCondition, definition.EndCondition);
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.Type)));
        Assert.True(definition.IsResolved(nameof(HoleWizardFeatureModel.EndCondition)));
        foreach (var field in family!.Fields)
        {
            var property = typeof(HoleWizardFeatureModel).GetProperty(field.Property);
            Assert.NotNull(property);
            object? value = property!.GetValue(definition);
            if (field.IsString)
                Assert.Equal(field.ExpectedString, value);
            else
                Assert.Equal(field.ExpectedDouble, Assert.IsType<double>(value), 15);
            Assert.True(definition.IsResolved(field.Property), field.Property);
        }
    }

    [Fact]
    public void LegacyFamily_FieldOrHeaderTamperingReturnsNull()
    {
        foreach (var expected in Expectations)
        {
            if (expected.Type == null) continue;
            using var sw = SwFile.Open(SamplePath(expected.File), readOnly: true);
            byte[] raw = sw.RawStreams.Keys
                .Where(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase))
                .Select(k => sw.RawStreams[k]).First();
            var doc = MfcDocument.Parse((byte[])raw.Clone());
            var folders = new List<FeatureFolder>();
            void Walk(IEnumerable<FeatureFolder> fs) { foreach (var f in fs) { folders.Add(f); Walk(f.Children); } }
            Walk(sw.ResolvedFeatures.Folders);
            var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
            int end = folders.Where(f => f.TagOffset > hole.TagOffset)
                .Select(f => f.TagOffset).DefaultIfEmpty(raw.Length).Min();
            end = Math.Min(end, raw.Length);
            var family = LegacyHoleWizardTutorialParser.MatchFamily(doc, hole.TagOffset, end);
            Assert.NotNull(family);
            byte[] copy = (byte[])raw.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            if (family!.Fields.Length > 0)
            {
                var field = family.Fields[0];
                int tagOffset = hole.TagOffset + field.ClassTagOffset;
                var element = copyDoc.AllElements.First(e =>
                    e.TagOffset == tagOffset && e.ClassName == field.ClassName);
                copy[element.DataStart + field.Offset] ^= 0xFF;
            }
            else
            {
                copy[4] ^= 0xFF;
            }
            var tampered = MfcDocument.Parse(copy);
            var tamperedHole = tampered.AllElements.First(e =>
                e.ClassName == "moHoleWzd_c" && e.TagOffset == hole.TagOffset);
            Assert.Null(HoleWizardFeatureParser.ParseHoleWizard(
                tampered, "moHoleWzd_c", 0, tamperedHole.TagOffset, end));
        }
    }
}
