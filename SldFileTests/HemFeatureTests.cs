using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Task 2.3 hem reading and fixed-combination authoring, validated against the
/// controlled native open/closed samples (ResolvedFeatures version 0x73).
/// </summary>
public class HemFeatureTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x4D35;

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalFormingOptions", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles",
                "Features2026", "SheetMetalFormingOptions", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"SheetMetalFormingOptions/{fileName} not found");
    }

    private static HemFeatureDefinitionData ReadDefinition(string fileName)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders, f => f.ClassName == "moHem_c");
        return Assert.IsType<HemFeatureDefinitionData>(folder.FeatureData);
    }

    public static IEnumerable<object[]> NativeSamples()
    {
        yield return new object[] { "sheetmetal_hem_open.SLDPRT", 0, 0.0005 };
        yield return new object[] { "sheetmetal_hem_closed.SLDPRT", 1, 0.0001 };
        yield return new object[] { "sheetmetal_hem_open_resave.SLDPRT", 0, 0.0005 };
        yield return new object[] { "sheetmetal_hem_closed_resave.SLDPRT", 1, 0.0001 };
    }

    [Theory]
    [MemberData(nameof(NativeSamples))]
    public void NativeHemFieldsMatchSnapshots(string fileName, int type, double gap)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders, f => f.ClassName == "moHem_c");
        Assert.Equal(0x67u, folder.Header.TypeId);
        var definition = Assert.IsType<HemFeatureDefinitionData>(folder.FeatureData);

        Assert.Equal(type, definition.Type ?? -1);
        Assert.Equal(0.005, definition.Length!.Value, 12);
        Assert.Equal(gap, definition.GapDistance!.Value, 12);
        Assert.Equal(3.1415926535898, definition.Angle!.Value, 12);
        Assert.Equal(0.001, definition.Radius!.Value, 12);
        Assert.Equal(0.0005, definition.MiterGap!.Value, 12);
        Assert.Equal(1, definition.EdgeCount ?? -1);

        Assert.True(definition.ParameterResolution.Type);
        Assert.True(definition.ParameterResolution.Length);
        Assert.True(definition.ParameterResolution.GapDistance);
        Assert.True(definition.ParameterResolution.Angle);
        Assert.True(definition.ParameterResolution.Radius);
        Assert.True(definition.ParameterResolution.MiterGap);
        Assert.True(definition.ParameterResolution.EdgeCount);

        Assert.Null(definition.BendPosition);
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.UseDefaultBendAllowance);
        Assert.Null(definition.Edges);
        Assert.Null(definition.CustomBendAllowance);
        Assert.False(definition.ParameterResolution.BendPosition);
        Assert.False(definition.ParameterResolution.ReverseDirection);
        Assert.False(definition.ParameterResolution.UseDefaultBendAllowance);
        Assert.False(definition.ParameterResolution.Edges);
        Assert.False(definition.ParameterResolution.CustomBendAllowance);
    }

    [Theory]
    [InlineData("sheetmetal_hem_open.SLDPRT", "Hem_open")]
    [InlineData("sheetmetal_hem_closed.SLDPRT", "Hem_closed")]
    public void HemFolderMapsToHemFeatureKind(string fileName, string featureName)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        var part = Assert.IsType<SldPart>(file);
        var feature = Assert.Single(part.Features, f => f.Name == featureName);
        Assert.Equal(FeatureKind.Hem, feature.Kind);
        Assert.IsType<HemFeatureDefinitionData>(feature.Definition);
    }

    [Fact]
    public void OnlyGapAndTypeVaryBetweenOpenAndClosed()
    {
        // Parameterization audit: the two controlled samples vary only Type and
        // GapDistance together. Length, Radius, Angle and MiterGap are constant,
        // so there is no single-variable evidence for their authoring slots or
        // for decoupling Gap from Type. The fixed templates must stay fixed until
        // Agent 5 provides the one-variable sample matrix.
        var open = ReadDefinition("sheetmetal_hem_open.SLDPRT");
        var closed = ReadDefinition("sheetmetal_hem_closed.SLDPRT");

        Assert.NotEqual(open.Type, closed.Type);
        Assert.NotEqual(open.GapDistance, closed.GapDistance);
        Assert.Equal(open.Length, closed.Length);
        Assert.Equal(open.Radius, closed.Radius);
        Assert.Equal(open.Angle, closed.Angle);
        Assert.Equal(open.MiterGap, closed.MiterGap);
        Assert.Equal(0.005, open.Length!.Value, 12);
        Assert.Equal(0.001, open.Radius!.Value, 12);
        Assert.Equal(3.1415926535898, open.Angle!.Value, 12);
        Assert.Equal(0.0005, open.MiterGap!.Value, 12);
    }

    public static IEnumerable<object[]> HemVariants()
    {
        yield return new object[] { "hem_open_len10.SLDPRT", 0.010, 0.0005, 0.001 };
        yield return new object[] { "hem_open_len10_resave.SLDPRT", 0.010, 0.0005, 0.001 };
        yield return new object[] { "hem_open_gap1.SLDPRT", 0.005, 0.001, 0.001 };
        yield return new object[] { "hem_open_gap1_resave.SLDPRT", 0.005, 0.001, 0.001 };
        yield return new object[] { "hem_open_rad2.SLDPRT", 0.005, 0.0005, 0.002 };
        yield return new object[] { "hem_open_rad2_resave.SLDPRT", 0.005, 0.0005, 0.002 };
        yield return new object[] { "hem_open_angle135.SLDPRT", 0.005, 0.0005, 0.001 };
        yield return new object[] { "hem_open_angle135_resave.SLDPRT", 0.005, 0.0005, 0.001 };
    }

    [Theory]
    [MemberData(nameof(HemVariants))]
    public void HemVariantFieldsMatchSnapshots(
        string fileName, double length, double gap, double radius)
    {
        var definition = ReadDefinition(fileName);
        Assert.Equal(0, definition.Type ?? -1);
        Assert.Equal(length, definition.Length!.Value, 12);
        Assert.Equal(gap, definition.GapDistance!.Value, 12);
        Assert.Equal(radius, definition.Radius!.Value, 12);
        // angle135 requested 3π/4 but the native readback stays π (not persisted).
        Assert.Equal(3.1415926535898, definition.Angle!.Value, 12);
        Assert.Equal(0.0005, definition.MiterGap!.Value, 12);
        Assert.True(definition.ParameterResolution.Length);
        Assert.True(definition.ParameterResolution.GapDistance);
        Assert.True(definition.ParameterResolution.Radius);
        Assert.True(definition.ParameterResolution.Angle);
    }

    public static IEnumerable<object[]> HemExtraVariants()
    {
        yield return new object[] { "hem_closed_len10.SLDPRT", 1, 0.010, 0.0001, 0.001, 0.0005 };
        yield return new object[] { "hem_closed_len10_resave.SLDPRT", 1, 0.010, 0.0001, 0.001, 0.0005 };
        yield return new object[] { "hem_open_mitergap1.SLDPRT", 0, 0.005, 0.0005, 0.001, 0.001 };
        yield return new object[] { "hem_open_mitergap1_resave.SLDPRT", 0, 0.005, 0.0005, 0.001, 0.001 };
    }

    [Theory]
    [MemberData(nameof(HemExtraVariants))]
    public void HemExtraVariantsMatchSnapshots(
        string fileName, int type, double length, double gap, double radius, double miterGap)
    {
        var definition = ReadDefinition(fileName);
        Assert.Equal(type, definition.Type ?? -1);
        Assert.Equal(length, definition.Length!.Value, 12);
        Assert.Equal(gap, definition.GapDistance!.Value, 12);
        Assert.Equal(radius, definition.Radius!.Value, 12);
        Assert.Equal(3.1415926535898, definition.Angle!.Value, 12);
        Assert.Equal(miterGap, definition.MiterGap!.Value, 12);
        Assert.True(definition.ParameterResolution.Type);
        Assert.True(definition.ParameterResolution.Length);
        Assert.True(definition.ParameterResolution.GapDistance);
        Assert.True(definition.ParameterResolution.MiterGap);
    }

    [Theory]
    [InlineData("hem_closed_len10.SLDPRT", 1, 0.010, 0.0001, 0.0005)]
    [InlineData("hem_open_mitergap1.SLDPRT", 0, 0.005, 0.0005, 0.001)]
    public void AuthoringReconstructsExtraHemTemplates(
        string baseFile, int type, double length, double gap, double miterGap)
    {
        var definition = Definition(type);
        definition.Length = length;
        definition.GapDistance = gap;
        definition.MiterGap = miterGap;

        using var temp = CreateStrippedBase(baseFile);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHem(definition);
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(baseFile), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);

        using var parsedFile = SwFile.Open(temp.FilePath, readOnly: true);
        var parsed = Assert.IsType<HemFeatureDefinitionData>(
            parsedFile.ResolvedFeatures.Folders.Single(f => f.ClassName == "moHem_c").FeatureData);
        Assert.Equal(type, parsed.Type ?? -1);
        Assert.Equal(length, parsed.Length!.Value, 12);
        Assert.Equal(gap, parsed.GapDistance!.Value, 12);
        Assert.Equal(miterGap, parsed.MiterGap!.Value, 12);
    }

    [Theory]
    [InlineData("hem_open_len10.SLDPRT", 0.010, 0.0005)]
    [InlineData("hem_open_gap1.SLDPRT", 0.005, 0.001)]
    public void AuthoringReconstructsLengthGapVariantTemplates(
        string baseFile, double length, double gap)
    {
        var definition = Definition(0);
        definition.Length = length;
        definition.GapDistance = gap;

        using var temp = CreateStrippedBase(baseFile);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHem(definition);
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(baseFile), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);

        using var parsedFile = SwFile.Open(temp.FilePath, readOnly: true);
        var parsed = Assert.IsType<HemFeatureDefinitionData>(
            parsedFile.ResolvedFeatures.Folders.Single(f => f.ClassName == "moHem_c").FeatureData);
        Assert.Equal(length, parsed.Length!.Value, 12);
        Assert.Equal(gap, parsed.GapDistance!.Value, 12);
    }

    [Fact]
    public void HemLengthGapBoundariesAndUnmeasuredCombinationsAreRejected()
    {
        foreach (var (length, gap) in new[]
        {
            (0.006, 0.0005), (0.010, 0.001), (0.005, 0.0002), (0.004, 0.0005)
        })
        {
            using var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT");
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var definition = Definition(0);
            definition.Length = length;
            definition.GapDistance = gap;
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(definition));
            Assert.Contains("Length", error.Message);
        }

        // Closed Gap=0.0005 is blocked by native readback (still 0.0001), and
        // MiterGap/combined variants not present in the samples are rejected.
        using (var temp = CreateStrippedBase("hem_closed_len10.SLDPRT"))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var definition = Definition(1);
            definition.Length = 0.010;
            definition.GapDistance = 0.0005;
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(definition));
            Assert.Contains("GapDistance", error.Message);
        }

        foreach (var (type, length, gap, miter, baseFile) in new[]
        {
            (0, 0.005, 0.0005, 0.002, "sheetmetal_hem_open.SLDPRT"),
            (0, 0.010, 0.0005, 0.001, "hem_open_len10.SLDPRT"),
            (0, 0.005, 0.001, 0.001, "hem_open_gap1.SLDPRT"),
            (1, 0.010, 0.0001, 0.001, "hem_closed_len10.SLDPRT")
        })
        {
            using var temp = CreateStrippedBase(baseFile);
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var definition = Definition(type);
            definition.Length = length;
            definition.GapDistance = gap;
            definition.MiterGap = miter;
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(definition));
            Assert.Contains("MiterGap", error.Message);
        }

        // Radius and Angle remain callout/not-persisted and cannot be authored.
        using (var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT"))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var radiusDef = Definition(0);
            radiusDef.Radius = 0.002;
            var radiusError = Assert.Throws<NotSupportedException>(() => part.AddHem(radiusDef));
            Assert.Contains("Radius", radiusError.Message);

            var angleDef = Definition(0);
            angleDef.Angle = 3.0 * Math.PI / 4.0;
            var angleError = Assert.Throws<NotSupportedException>(() => part.AddHem(angleDef));
            Assert.Contains("Angle", angleError.Message);
        }
    }

    [Fact]
    public void MappingDoesNotModifyHemStreams()
    {
        foreach (var fileName in new[] { "sheetmetal_hem_open.SLDPRT", "sheetmetal_hem_closed.SLDPRT" })
        {
            using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
            byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
            var doc = SldFile.Mfc.MfcDocument.Parse(raw);
            ResolvedFeaturesSchemaMapper.Map(doc, new ResolvedFeaturesData());
            Assert.Equal(raw, doc.SourceData);
        }
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

    private static HemFeatureDefinitionData Definition(int type)
        => new()
        {
            Type = type,
            Length = 0.005,
            GapDistance = type == 0 ? 0.0005 : 0.0001,
            Angle = 3.1415926535898,
            Radius = 0.001,
            MiterGap = 0.0005,
            EdgeCount = 1
        };

    private static HemFeatureDefinitionData AuthorAndReopen(
        string baseFile, HemFeatureDefinitionData definition, out string featureName)
    {
        using var temp = CreateStrippedBase(baseFile);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHem(definition);
            featureName = feature.Name;
            Assert.Equal(FeatureKind.Hem, feature.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders, f => f.ClassName == "moHem_c");
        return Assert.IsType<HemFeatureDefinitionData>(folder.FeatureData);
    }

    [Theory]
    [InlineData("sheetmetal_hem_open.SLDPRT", 0, "Hem_open", 0.0005)]
    [InlineData("sheetmetal_hem_closed.SLDPRT", 1, "Hem_closed", 0.0001)]
    public void AuthoringReconstructsNativeTemplate(
        string baseFile, int type, string expectedName, double gap)
    {
        var parsed = AuthorAndReopen(baseFile, Definition(type), out string featureName);

        Assert.Equal(expectedName, featureName);
        Assert.Equal(type, parsed.Type ?? -1);
        Assert.Equal(0.005, parsed.Length!.Value, 12);
        Assert.Equal(gap, parsed.GapDistance!.Value, 12);
        Assert.Equal(0.001, parsed.Radius!.Value, 12);
        Assert.Equal(0.0005, parsed.MiterGap!.Value, 12);
        Assert.Equal(1, parsed.EdgeCount ?? -1);
        Assert.True(parsed.ParameterResolution.Type);
        Assert.True(parsed.ParameterResolution.GapDistance);
    }

    [Theory]
    [InlineData("sheetmetal_hem_open.SLDPRT", 0)]
    [InlineData("sheetmetal_hem_closed.SLDPRT", 1)]
    public void AuthoredStreamMatchesNativeBytes(string baseFile, int type)
    {
        using var temp = CreateStrippedBase(baseFile);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHem(Definition(type));
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(baseFile), readOnly: true);
        byte[] native = (byte[])original.RawStreams[ResolvedStream].Clone();
        Assert.Equal(native, authored);
    }

    [Fact]
    public void ResavePreservesAuthoredHem()
    {
        using var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddHem(Definition(0));
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
        var definition = Assert.IsType<HemFeatureDefinitionData>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moHem_c").FeatureData);
        Assert.Equal(0, definition.Type ?? -1);
        Assert.Equal(0.0005, definition.GapDistance!.Value, 12);
    }

    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        HemFeatureDefinitionData Base() => Definition(0);

        var teardrop = Base(); teardrop.Type = 2;
        yield return new object[] { teardrop, "Type" };
        var nullType = Base(); nullType.Type = null;
        yield return new object[] { nullType, "Type" };
        var length = Base(); length.Length = 0.01;
        yield return new object[] { length, "Length" };
        var gap = Base(); gap.GapDistance = 0.0001;
        yield return new object[] { gap, "GapDistance" };
        var angle = Base(); angle.Angle = Math.PI / 2.0;
        yield return new object[] { angle, "Angle" };
        var radius = Base(); radius.Radius = 0.002;
        yield return new object[] { radius, "Radius" };
        var miter = Base(); miter.MiterGap = 0.001;
        yield return new object[] { miter, "MiterGap" };
        var position = Base(); position.BendPosition = 1;
        yield return new object[] { position, "BendPosition" };
        var reverse = Base(); reverse.ReverseDirection = true;
        yield return new object[] { reverse, "ReverseDirection" };
        var allowance = Base(); allowance.UseDefaultBendAllowance = false;
        yield return new object[] { allowance, "UseDefaultBendAllowance" };
        var edges = Base(); edges.EdgeCount = 2;
        yield return new object[] { edges, "EdgeCount" };
        var edgeRef = Base(); edgeRef.Edges = new[] { new EntityReference { EntityId = 1 } };
        yield return new object[] { edgeRef, "Edges" };
        var cba = Base(); cba.CustomBendAllowance = new FlangeCustomBendAllowanceData { Type = 2 };
        yield return new object[] { cba, "CustomBendAllowance" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedFieldsAreRejectedWithoutModifyingStream(
        HemFeatureDefinitionData definition, string property)
    {
        using var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT");
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.ThrowsAny<Exception>(() => part.AddHem(definition));
            Assert.Contains(property, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void WrongTemplatePrefixAndCustomNameAreRejected()
    {
        // Closed template into the open stripped base: prefix hash must reject.
        using (var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(Definition(1)));
            Assert.Contains("SHA-256", error.Message);
        }

        using (var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddHem(Definition(0), featureName: "Hem2"));
            Assert.Contains("display name", error.Message);
        }
    }

    [Fact]
    public void WrongVersionFullDocumentAndTamperedPrefixAreRejected()
    {
        using (var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT", versionOverride: 0x74))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(Definition(0)));
            Assert.Contains("0x73", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("sheetmetal_hem_open.SLDPRT")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddHem(Definition(0)));
            Assert.Contains("stripped", error.Message);
        }

        using (var temp = CreateStrippedBase("sheetmetal_hem_open.SLDPRT"))
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
                var error = Assert.Throws<NotSupportedException>(() => part.AddHem(Definition(0)));
                Assert.Contains("SHA-256", error.Message);
            }
        }
    }
}
