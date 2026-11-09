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
/// Fixed option-template base-flange authoring from the 2026-10-31 retry group
/// (ResolvedFeatures version 0x73, controlled open-L sketch, stripped base ends
/// at the moSMBaseFlange_c tag, 0x3EF1 bytes). Each template reconstructs its
/// native source ResolvedFeatures stream byte-for-byte.
/// </summary>
public class SheetMetalBaseFlangeOptionAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x3EF1;

    private const string GaugePath =
        "C:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS\\lang\\chinese-simplified\\" +
        "Sheet Metal Gauge Tables\\k-factor mm sample.xlsx";

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalOptions", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles",
                "Features2026", "SheetMetalOptions", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"SheetMetalOptions/{fileName} not found");
    }

    private static TempTestFile CreateStrippedBase(string fileName, ushort? versionOverride = null)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(fileName));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[PrefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        if (versionOverride.HasValue)
            BitConverter.GetBytes(versionOverride.Value).CopyTo(stripped, 0);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static SheetMetalBaseFlangeModel Definition(string template) => template switch
    {
        "KFactor" => new SheetMetalBaseFlangeModel
        {
            Thickness = 0.001, BendRadius = 0.002, Depth = 0.010,
            BendAllowanceType = 2, KFactor = 0.3
        },
        "Direct" => new SheetMetalBaseFlangeModel
        {
            Thickness = 0.001, BendRadius = 0.002, Depth = 0.010,
            BendAllowanceType = 3, BendAllowance = 0.001
        },
        "Deduction" => new SheetMetalBaseFlangeModel
        {
            Thickness = 0.001, BendRadius = 0.002, Depth = 0.010,
            BendAllowanceType = 4, BendDeduction = 0.001
        },
        "GaugeTable" => new SheetMetalBaseFlangeModel
        {
            Thickness = 0.001, BendRadius = 0.001, Depth = 0.010,
            UseGaugeTable = true, GaugeTablePath = GaugePath,
            ThicknessTableName = "规格表"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static string SampleFor(string template) => template switch
    {
        "KFactor" => "sm_l_allowance_kfactor_persisted.SLDPRT",
        "Direct" => "sm_l_allowance_direct_persisted.SLDPRT",
        "Deduction" => "sm_l_allowance_deduction_persisted.SLDPRT",
        "GaugeTable" => "sm_l_gauge_table.SLDPRT",
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    public static IEnumerable<object[]> Templates()
    {
        yield return new object[] { "KFactor" };
        yield return new object[] { "Direct" };
        yield return new object[] { "Deduction" };
        yield return new object[] { "GaugeTable" };
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void AuthoredStreamMatchesNativeSource(string template)
    {
        using var temp = CreateStrippedBase(SampleFor(template));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition(template));
            Assert.Equal("基体-法兰1", feature.Name);
            Assert.Equal(FeatureKind.Extrusion, feature.Kind);
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(SampleFor(template)), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void AuthoredTemplateReadsMeasuredOption(string template)
    {
        using var temp = CreateStrippedBase(SampleFor(template));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition(template));
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.Equal(0.001, definition.Thickness, precision: 12);
        Assert.Equal(0.010, definition.Depth, precision: 12);

        switch (template)
        {
            case "KFactor":
                Assert.Equal(2u, definition.BendAllowanceType);
                Assert.Equal(0.3, definition.KFactor, precision: 12);
                Assert.False(definition.UseGaugeTable);
                break;
            case "Direct":
                Assert.Equal(3u, definition.BendAllowanceType);
                Assert.Equal(0.001, definition.BendAllowance!.Value, precision: 12);
                Assert.False(definition.UseGaugeTable);
                break;
            case "Deduction":
                Assert.Equal(4u, definition.BendAllowanceType);
                Assert.Equal(0.001, definition.BendDeduction!.Value, precision: 12);
                Assert.False(definition.UseGaugeTable);
                break;
            case "GaugeTable":
                Assert.True(definition.UseGaugeTable);
                Assert.Equal("规格表", definition.ThicknessTableName);
                Assert.Contains("k-factor mm sample.xlsx", definition.GaugeTablePath);
                break;
        }
    }

    [Fact]
    public void StrippedBaseReadsAsIntermediateWithoutBaseFlange()
    {
        using var temp = CreateStrippedBase(SampleFor("KFactor"));
        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Empty(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moTemplateSheetMetal_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moProfileFeature_c"));
        Assert.Equal((ushort)0x73, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 0));
        Assert.Equal((ushort)22, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 4));
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void ResavePreservesAuthoredOptionTemplate(string template)
    {
        using var temp = CreateStrippedBase(SampleFor(template));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition(template));
            part.Save();
        }

        byte[] before;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStream].Clone();
            second.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        Assert.Single(reopened.ResolvedFeatures.Folders, f => f.ClassName == "moSMBaseFlange_c");
    }

    [Fact]
    public void WrongTemplateBaseAndValuesAreRejectedWithoutChanges()
    {
        // KFactor definition against the deduction template base.
        using (var temp = CreateStrippedBase(SampleFor("Deduction")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
            Assert.Contains("SHA-256", error.Message);
        }

        using (var temp = CreateStrippedBase(SampleFor("KFactor")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(
                    Definition("KFactor"), featureName: "BaseFlange2"));
            Assert.Contains("display name", error.Message);

            var badK = Definition("KFactor");
            badK.KFactor = 0.4;
            Assert.Contains("KFactor", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(badK)).Message);
        }

        using (var temp = CreateStrippedBase(SampleFor("Direct")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var bad = Definition("Direct");
            bad.BendAllowance = 0.002;
            Assert.Contains("BendAllowance", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(bad)).Message);

            var badRadius = Definition("Direct");
            badRadius.BendRadius = 0.003;
            Assert.Contains("BendRadius", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(badRadius)).Message);
        }

        using (var temp = CreateStrippedBase(SampleFor("Deduction")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var bad = Definition("Deduction");
            bad.BendDeduction = 0.002;
            Assert.Contains("BendDeduction", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(bad)).Message);
        }

        using (var temp = CreateStrippedBase(SampleFor("GaugeTable")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var badName = Definition("GaugeTable");
            badName.ThicknessTableName = "other";
            Assert.Contains("ThicknessTableName", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(badName)).Message);

            var badPath = Definition("GaugeTable");
            badPath.GaugeTablePath = "C:\\tables\\other.xlsx";
            Assert.Contains("GaugeTablePath", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(badPath)).Message);

            var unsupported = new SheetMetalBaseFlangeModel
            {
                Thickness = 0.001, BendRadius = 0.001, Depth = 0.010
            };
            Assert.Contains("supports only", Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(unsupported)).Message);
        }
    }

    [Fact]
    public void WrongVersionFullDocumentAndTamperedPrefixAreRejected()
    {
        using (var temp = CreateStrippedBase(SampleFor("KFactor"), versionOverride: 0x74))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
            Assert.Contains("0x73", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath(SampleFor("KFactor"))))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
            Assert.Contains("stripped template base", error.Message);
        }

        using (var temp = CreateStrippedBase(SampleFor("KFactor")))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x100] ^= 0xFF;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
                Assert.Contains("SHA-256", error.Message);
            }
        }

        using (var temp = CreateStrippedBase(SampleFor("KFactor")))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x1B70] ^= 0xFF;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
                Assert.Contains("0x67 marker", error.Message);
            }
        }

        using (var temp = CreateStrippedBase(SampleFor("KFactor")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<InvalidOperationException>(() =>
                part.AddSheetMetalBaseFlangeFromOptionTemplate(Definition("KFactor")));
        }
    }
}
