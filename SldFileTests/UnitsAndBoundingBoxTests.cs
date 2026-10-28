using System;
using SldFile;
using Xunit;

namespace SldFileTests;

public class UnitsAndBoundingBoxTests
{
    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    public void DocumentUnits_2012Sample_WithoutUnitProperties_ReturnsNullWithoutThrowing(string propertyName)
    {
        string filePath = ResolvePath(propertyName);
        using var file = SwFile.Open(filePath, readOnly: true);

        // 2012 sample parts do not declare SW_Unit* system properties; should gracefully return null
        Assert.Null(file.Metadata.Units);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart2026))]
    [InlineData(nameof(TestFileProvider.CylinderPart2026))]
    public void DocumentUnits_2026Sample_HasExpectedUnits(string propertyName)
    {
        string filePath = ResolvePath(propertyName);
        using var file = SwFile.Open(filePath, readOnly: true);

        Assert.NotNull(file.Metadata.Units);
        var units = file.Metadata.Units!;

        Assert.Equal(UnitSystem.Mmgs, units.System);
        Assert.Equal(LengthUnit.Millimeter, units.Length);
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart2026))]
    public void BoundingBox_2026CubePart_HasValidExtents(string propertyName)
    {
        string filePath = ResolvePath(propertyName);
        using var file = SwFile.Open(filePath, readOnly: true);

        // Cube is 100mm = 0.1m, centered or starting at origin
        if (file.Metadata.BoundingBox != null)
        {
            var bb = file.Metadata.BoundingBox;
            Assert.True(bb.MaxX > bb.MinX, $"MaxX ({bb.MaxX}) should be > MinX ({bb.MinX})");
            Assert.True(bb.MaxY > bb.MinY, $"MaxY ({bb.MaxY}) should be > MinY ({bb.MinY})");
            Assert.True(bb.MaxZ > bb.MinZ, $"MaxZ ({bb.MaxZ}) should be > MinZ ({bb.MinZ})");
        }
    }

    private static string ResolvePath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);
        return (string)prop!.GetValue(null)!;
    }
}
