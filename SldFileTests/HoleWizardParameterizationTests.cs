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
/// Single-variable parameterization of the measured ISO M6 counterbore and countersink
/// authoring blocks. The slot map was derived from native same-document ModifyDefinition
/// experiments: through Ø6/Ø8/Ø10, counterbore Ø11/Ø13, counterbore depth 4/6/8 mm,
/// countersink Ø11/Ø13/Ø17 and 90/100/120 degrees.
/// </summary>
public class HoleWizardParameterizationTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";
    private const string CounterBoreSample = "hole_counterbore_iso_m6_11_6";
    private const string CounterSinkSample = "hole_countersink_iso_m6_11_90";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "HoleWizardOptions", name + ".SLDPRT");

    private static string StreamKey(SwFile file) =>
        file.RawStreams.Keys.First(k => k.Contains("ResolvedFeatures", StringComparison.OrdinalIgnoreCase));

    private static TempTestFile CreateBase(string sample)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = sw.RawStreams[StreamKey(sw)];
        var folders = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current) { folders.Add(folder); Walk(folder.Children); }
        }
        Walk(sw.ResolvedFeatures.Folders);
        var hole = folders.First(f => f.ClassName == "moHoleWzd_c");
        byte[] stripped = raw.AsSpan(0, hole.TagOffset).ToArray();
        ushort count = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
        sw.SetPendingStream(StreamKey(sw), stripped);
        sw.Save();
        return temp;
    }

    public static IEnumerable<object[]> CounterBoreCases =>
        new (double Thru, double Bore, double Depth)[]
        {
            (0.008, 0.011, 0.006),
            (0.010, 0.011, 0.006),
            (0.006, 0.013, 0.006),
            (0.006, 0.011, 0.004),
            (0.006, 0.011, 0.008),
            (0.008, 0.013, 0.004),
            (0.010, 0.013, 0.008)
        }.Select(v => new object[] { v.Thru, v.Bore, v.Depth });

    public static IEnumerable<object[]> CounterSinkCases =>
        new (double Diameter, double Angle)[]
        {
            (0.011, Math.PI / 2.0),
            (0.013, Math.PI / 2.0),
            (0.017, Math.PI / 2.0),
            (0.013, 100.0 * Math.PI / 180.0),
            (0.017, 120.0 * Math.PI / 180.0)
        }.Select(v => new object[] { v.Diameter, v.Angle });

    [Theory]
    [MemberData(nameof(CounterBoreCases))]
    public void CounterBoreParameterization_RoundTripsValues(double thru, double bore, double depth)
    {
        using var temp = CreateBase(CounterBoreSample);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(new HoleWizardFeatureDefinitionData
            {
                Type = 14,
                ThruHoleDiameter = thru,
                CounterBoreDiameter = bore,
                CounterBoreDepth = depth
            });
            Assert.Equal(HoleWizardCounterBoreAuthoringTemplates.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var read = Assert.IsType<HoleWizardFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Definition);
        Assert.Equal(thru, read.ThruHoleDiameter!.Value, 12);
        Assert.Equal(bore, read.CounterBoreDiameter!.Value, 12);
        Assert.Equal(depth, read.CounterBoreDepth!.Value, 12);
        Assert.Equal(0.010, read.ThruHoleDepth!.Value, 12);
    }

    [Theory]
    [MemberData(nameof(CounterSinkCases))]
    public void CounterSinkParameterization_RoundTripsValues(double diameter, double angle)
    {
        using var temp = CreateBase(CounterSinkSample);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddHoleWizard(new HoleWizardFeatureDefinitionData
            {
                Type = 44,
                FastenerSize = "M6",
                ThruHoleDiameter = 0.006,
                CounterSinkDiameter = diameter,
                CounterSinkAngle = angle
            });
            Assert.Equal(HoleWizardCounterSinkAuthoringTemplates.M6.FeatureName, feature.Name);
            part.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var read = Assert.IsType<HoleWizardFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.HoleWizard)).Definition);
        Assert.Equal(diameter, read.CounterSinkDiameter!.Value, 12);
        Assert.Equal(angle, read.CounterSinkAngle!.Value, 12);
        Assert.Equal(0.006, read.ThruHoleDiameter!.Value, 12);
    }

    [Fact]
    public void CounterBoreParameterization_RejectsUnmeasuredValues()
    {
        using var temp = CreateBase(CounterBoreSample);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        byte[] before = (byte[])part.RawStreams[StreamKey(part)].Clone();
        foreach (var definition in new[]
                 {
                     new HoleWizardFeatureDefinitionData { Type = 14, ThruHoleDiameter = 0.007 },
                     new HoleWizardFeatureDefinitionData { Type = 14, ThruHoleDiameter = 0.012 },
                     new HoleWizardFeatureDefinitionData { Type = 14, CounterBoreDiameter = 0.012 },
                     new HoleWizardFeatureDefinitionData { Type = 14, CounterBoreDiameter = 0.014 },
                     new HoleWizardFeatureDefinitionData { Type = 14, CounterBoreDepth = 0.005 },
                     new HoleWizardFeatureDefinitionData { Type = 14, CounterBoreDepth = 0.009 },
                     new HoleWizardFeatureDefinitionData { Type = 14, ThruHoleDepth = 0.012 }
                 })
        {
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(definition));
            Assert.Equal(before, part.RawStreams[StreamKey(part)]);
        }
    }

    [Fact]
    public void CounterSinkParameterization_RejectsUnmeasuredValues()
    {
        using var temp = CreateBase(CounterSinkSample);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        byte[] before = (byte[])part.RawStreams[StreamKey(part)].Clone();
        foreach (var definition in new[]
                 {
                     new HoleWizardFeatureDefinitionData { Type = 44, FastenerSize = "M6",
                         ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.012 },
                     new HoleWizardFeatureDefinitionData { Type = 44, FastenerSize = "M6",
                         ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.014 },
                     new HoleWizardFeatureDefinitionData { Type = 44, FastenerSize = "M6",
                         ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.019 },
                     new HoleWizardFeatureDefinitionData { Type = 44, FastenerSize = "M6",
                         ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.013,
                         CounterSinkAngle = 95.0 * Math.PI / 180.0 },
                     new HoleWizardFeatureDefinitionData { Type = 44, FastenerSize = "M6",
                         ThruHoleDiameter = 0.006, CounterSinkDiameter = 0.013,
                         CounterSinkAngle = 130.0 * Math.PI / 180.0 }
                 })
        {
            Assert.Throws<NotSupportedException>(() => part.AddHoleWizard(definition));
            Assert.Equal(before, part.RawStreams[StreamKey(part)]);
        }
    }
}
