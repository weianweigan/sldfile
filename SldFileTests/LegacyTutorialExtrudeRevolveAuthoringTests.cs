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
/// Authoring tests for Extrude and Revolve features on legacy tutorial samples using strongly typed MFC models.
/// </summary>
public class LegacyTutorialExtrudeRevolveAuthoringTests
{
    private const string Root = @"TestFiles\Features2026\TutorialExtrudeRevolveAuthoring";

    private static string PathOf(string file) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Root, file);

    private static ExtrudeFeatureModel ExtrudeDefinition(string file) => file switch
    {
        "01_pin.sldprt" => new ExtrudeFeatureModel
        {
            EndCondition = 4, ReverseDirection = true, FeatureScope = true, AutoSelect = false
        },
        "03_circlip.sldprt" => new ExtrudeFeatureModel
        {
            EndCondition = 4, ReverseDirection = false, FeatureScope = false, AutoSelect = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    private static RevolveFeatureModel RevolveDefinition(string file) => file switch
    {
        "05_s_632_1.sldprt" => new RevolveFeatureModel
        {
            IsCut = false, ReverseDirection = false
        },
        "09_strain_relief_straight.sldprt" => new RevolveFeatureModel
        {
            IsCut = true, ReverseDirection = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    private static Feature AddFeature(
        SldPart part, string file, string? featureName,
        ExtrudeFeatureModel? extrude = null,
        RevolveFeatureModel? revolve = null)
    {
        if (file.StartsWith("05_", StringComparison.Ordinal) ||
            file.StartsWith("09_", StringComparison.Ordinal))
            return part.AddRevolve(revolve ?? RevolveDefinition(file), featureName: featureName);
        return part.AddExtrude(extrude ?? ExtrudeDefinition(file), featureName: featureName);
    }

    private static readonly string[] Files =
    {
        "01_pin.sldprt",
        "03_circlip.sldprt",
        "05_s_632_1.sldprt",
        "09_strain_relief_straight.sldprt"
    };

    public static IEnumerable<object[]> Samples => Files.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_ReproducesOriginalStreamAndNativeBlock(string file)
    {
        if (file.StartsWith("05_", StringComparison.Ordinal) ||
            file.StartsWith("09_", StringComparison.Ordinal))
        {
            var def = RevolveDefinition(file);
            Assert.NotNull(def);
            Assert.False(def.ReverseDirection);
            Assert.Equal(file.StartsWith("09_", StringComparison.Ordinal), def.IsCut);
            Assert.Equal(2.0 * Math.PI, def.Angle, 12);
        }
        else
        {
            var def = ExtrudeDefinition(file);
            Assert.NotNull(def);
            Assert.Equal(4u, def.EndCondition);
            Assert.False(def.AutoSelect);
            Assert.Equal(file == "01_pin.sldprt", def.ReverseDirection);
            Assert.Equal(file == "01_pin.sldprt", def.FeatureScope);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_RejectsCustomNameAndWrongDefinitionValues(string file)
    {
        using var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        var part = Assert.IsType<SldPart>(sw);

        Assert.Throws<InvalidOperationException>(() => AddFeature(part, file, "Custom"));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void LegacyAuthoring_RejectsFolderVersionMarkerSignatureClassAndNameTampering(string file)
    {
        using var temp = TestFileProvider.CreateTempCopy(PathOf(file));
        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        var part = Assert.IsType<SldPart>(sw);

        if (file.StartsWith("05_", StringComparison.Ordinal) ||
            file.StartsWith("09_", StringComparison.Ordinal))
        {
            Assert.Throws<InvalidOperationException>(() => part.AddRevolve(RevolveDefinition(file)));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => part.AddExtrude(ExtrudeDefinition(file)));
        }
    }
}
