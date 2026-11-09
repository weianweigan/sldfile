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
/// Tests that the 15 tutorial HoleWizard models are cleanly opened and their hole features
/// are present and typed as FeatureKind.HoleWizard.
/// </summary>
public class LegacyHoleWizardTailAuthoringTests
{
    private const string Root = @"TestFiles\Features2026\HoleWizardLegacyTailTutorials";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    public static readonly (string File, string ExpectedName)[] SampleData =
    {
        ("01_b_plate_tap1.sldprt", "Tap1"),
        ("02_bracket_m5_clearance.sldprt", "M5 Clearance Hole1"),
        ("03_plate_ads_csk.sldprt", "CSK for  Flat Head Machine Screw1"),
        ("04_bracket_ads_cbore_m35.sldprt", "CBORE for M3.5 Pan Head Machine Screw1"),
        ("05_base_cbore_panhead6.sldprt", "CBORE for #6 Pan Head Machine Screw1"),
        ("06_bracket_auto_38_clearance.sldprt", "3/8 Clearance Hole1"),
        ("07_rocker_support_d50.sldprt", "#50 (0.07) Diameter Hole1"),
        ("08_maingrip_00_clearance.sldprt", "#00 Clearance Hole1"),
        ("09_spring_cylinder_d50.sldprt", "#50 (0.07) Diameter Hole1"),
        ("10_pin_m5_clearance.sldprt", "M5 Clearance Hole1"),
        ("11_table_m8_tapped.sldprt", "M8x1.0 Tapped Hole1"),
        ("12_mount_plate_m10_tapped.sldprt", "M10x1.5 Tapped Hole1"),
        ("13_straight_solid_npsm.SLDPRT", "3/4-14 NPSM Tapped Hole1"),
        ("14_regulator_12_20_tapped.SLDPRT", "1/2-20 Tapped Hole1"),
        ("15_filter_12_20_tapped.sldprt", "1/2-20 Tapped Hole1")
    };

    public static IEnumerable<object[]> SourceSamples =>
        SampleData.Select(s => new object[] { s.File, s.ExpectedName });

    [Theory]
    [MemberData(nameof(SourceSamples))]
    public void OpenFile_ContainsExpectedHoleWizardFeature(string file, string expectedName)
    {
        string path = PathOf(file);
        using var sw = SwFile.Open(path, readOnly: true);
        var part = Assert.IsType<SldPart>(sw);

        var match = part.Features.FirstOrDefault(f => f.Name == expectedName);
        Assert.NotNull(match);
        Assert.Equal(FeatureKind.HoleWizard, match!.Kind);
    }
}
