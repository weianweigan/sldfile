using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

public class ExtrudeAndSheetMetalFeatureTests
{
    private static string GetSamplePath(string fileName)
    {
        // 1. Standard build output path (copied by SldFileTests.csproj, used in CI/GitHub Actions)
        var testFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", fileName);
        if (File.Exists(testFilesPath))
            return testFilesPath;

        // 2. Search upwards for SldFileTests/TestFiles/Features2026 or reverse_workspace
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", fileName);
            if (File.Exists(candidate))
                return candidate;

            var revCandidate = Path.Combine(dir, "reverse_workspace", "experiments", "feature-matrix-20261028", "samples", fileName);
            if (File.Exists(revCandidate))
                return revCandidate;

            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }

        throw new FileNotFoundException($"Controlled sample file '{fileName}' not found in TestFiles/Features2026 or reverse_workspace.");
    }

    private static List<FeatureFolder> FlattenFolders(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<FeatureFolder>();
        foreach (var folder in folders)
        {
            result.Add(folder);
            result.AddRange(FlattenFolders(folder.Children));
        }
        return result;
    }

    [Theory]
    [InlineData("extrude_10mm.SLDPRT", 0.010, 0u, false)]
    [InlineData("extrude_20mm.SLDPRT", 0.020, 0u, false)]
    [InlineData("extrude_30mm.SLDPRT", 0.030, 0u, false)]
    [InlineData("extrude_midplane.SLDPRT", 0.010, 6u, false)]
    [InlineData("extrude_cut_5mm.SLDPRT", 0.005, 0u, true)]
    public void Extrude_ShouldExtractExactDepthsAndConditions_AcrossControlledSamples(
        string fileName,
        double expectedDepth,
        uint expectedEndCondition,
        bool expectedIsCut)
    {
        var path = GetSamplePath(fileName);
        using var file = SwFile.Open(path, readOnly: true);
        var rf = file.ResolvedFeatures;

        var allFolders = FlattenFolders(rf.Folders);
        var extFolder = allFolders.FirstOrDefault(f => expectedIsCut
            ? (f.ClassName == "moICE_c" || f.DisplayName.Contains("切除"))
            : (f.ClassName == "moExtrusion_c" || f.ClassName == "moExtrusion_ct"));

        Assert.NotNull(extFolder);
        Assert.NotNull(extFolder!.FeatureData);

        var extData = Assert.IsType<ExtrudeFeatureDefinitionData>(extFolder.FeatureData);
        Assert.Equal(expectedDepth, extData.Depth, precision: 6);
        Assert.Equal(expectedEndCondition, extData.EndCondition);
        Assert.Equal(expectedIsCut, extData.IsCut);

        // High-level SwFile.Features integration check
        var features = file.Features;
        var matchingFeat = features.FirstOrDefault(f => expectedIsCut
            ? f.Kind == FeatureKind.Cut
            : (f.Kind == FeatureKind.Extrusion && !f.Name.Contains("切除")));
        Assert.NotNull(matchingFeat);

        if (expectedIsCut)
        {
            Assert.Equal(FeatureKind.Cut, matchingFeat!.Kind);
        }

        var depthDim = matchingFeat!.Dimensions.FirstOrDefault(d => d.Name == "Depth");
        Assert.NotNull(depthDim);
        Assert.Contains((expectedDepth * 1000).ToString("F2"), depthDim!.Value);
    }

    [Theory]
    [InlineData("sm_base_flange_10mm.SLDPRT", 0.001, 0.002)]
    [InlineData("sm_base_flange_thick2.SLDPRT", 0.002, 0.002)]
    [InlineData("sm_base_flange_rad3.SLDPRT", 0.001, 0.003)]
    public void SheetMetalBaseFlange_ShouldExtractThicknessAndRadius_AcrossControlledSamples(
        string fileName,
        double expectedThickness,
        double expectedBendRadius)
    {
        var path = GetSamplePath(fileName);
        using var file = SwFile.Open(path, readOnly: true);
        var rf = file.ResolvedFeatures;

        var allFolders = FlattenFolders(rf.Folders);
        var smFolder = allFolders.FirstOrDefault(f => f.ClassName == "moSMBaseFlange_c");

        Assert.NotNull(smFolder);
        Assert.NotNull(smFolder!.FeatureData);

        var smData = Assert.IsType<SheetMetalBaseFlangeDefinitionData>(smFolder.FeatureData);
        Assert.Equal(expectedThickness, smData.Thickness, precision: 6);
        Assert.Equal(expectedBendRadius, smData.BendRadius, precision: 6);

        // High-level SwFile.Features integration check
        var features = file.Features;
        var matchingFeat = features.FirstOrDefault(f => f.Name.Contains("基体-法兰") || f.Name.Contains("BaseFlange") || f.Kind == FeatureKind.Extrusion);
        Assert.NotNull(matchingFeat);

        var thickDim = matchingFeat!.Dimensions.FirstOrDefault(d => d.Name == "Thickness");
        Assert.NotNull(thickDim);
        Assert.Contains((expectedThickness * 1000).ToString("F2"), thickDim!.Value);

        var radDim = matchingFeat.Dimensions.FirstOrDefault(d => d.Name == "BendRadius");
        Assert.NotNull(radDim);
        Assert.Contains((expectedBendRadius * 1000).ToString("F2"), radDim!.Value);
    }
}
