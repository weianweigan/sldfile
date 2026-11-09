using System;
using System.IO;
using System.Numerics;
using System.Text;
using SldFile;
using Xunit;

namespace SldFileTests;

public class MassPropertiesAndCutListTests
{
    [Fact]
    public void MassProperties_TryParse_Valid14DoubleString_ParsedAccurately()
    {
        // 14 doubles separated by commas:
        // CoG: 0.015, 0.025, 0.035
        // Vol: 0.00125
        // Surf: 0.075
        // Mass: 3.375
        // Moments: 0.005, 0.006, 0.007
        // Products: 0.0001, 0.0002, 0.0003
        string raw = "0.015, 0.025, 0.035, 0.00125, 0.075, 3.375, 0.005, 0.006, 0.007, 0.0001, 0.0002, 0.0003, 0.0, 0.0";

        bool success = MassProperties.TryParse(raw, out var mp);
        Assert.True(success);
        Assert.NotNull(mp);

        Assert.Equal(0.015f, mp.CenterOfGravity.X, 4);
        Assert.Equal(0.025f, mp.CenterOfGravity.Y, 4);
        Assert.Equal(0.035f, mp.CenterOfGravity.Z, 4);

        Assert.Equal(0.00125, mp.Volume, 6);
        Assert.Equal(0.075, mp.SurfaceArea, 6);
        Assert.Equal(3.375, mp.Mass, 6);

        Assert.Equal(0.005f, mp.MomentsOfInertia.X, 4);
        Assert.Equal(0.006f, mp.MomentsOfInertia.Y, 4);
        Assert.Equal(0.007f, mp.MomentsOfInertia.Z, 4);

        Assert.Equal(0.0001f, mp.ProductsOfInertia.X, 4);
        Assert.Equal(0.0002f, mp.ProductsOfInertia.Y, 4);
        Assert.Equal(0.0003f, mp.ProductsOfInertia.Z, 4);
    }

    [Fact]
    public void MassProperties_TryParse_InvalidInput_ReturnsFalseSafely()
    {
        Assert.False(MassProperties.TryParse(null, out _));
        Assert.False(MassProperties.TryParse("", out _));
        Assert.False(MassProperties.TryParse("1.0, 2.0", out _)); // Less than 12
        Assert.False(MassProperties.TryParse("abc, def, ghi", out _));
    }

    [Fact]
    public void CutListReader_Read_ParsesFeaturesAndProperties()
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Configuration Name=""Default"">
  <Feature Name=""Structural Tube 50x50x3&lt;1&gt;"">
    <Quantity>4</Quantity>
    <CustomProperty Name=""MATERIAL"">Steel</CustomProperty>
    <CustomProperty Name=""LENGTH"">1250.0</CustomProperty>
  </Feature>
  <Feature Name=""Base Plate&lt;1&gt;"">
    <CustomProperty Name=""QUANTITY"">2</CustomProperty>
    <CustomProperty Name=""THICKNESS"">10.0</CustomProperty>
  </Feature>
</Configuration>";

        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        var items = CutListReader.Read(bytes);

        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var tube = items[0];
        Assert.Equal("Structural Tube 50x50x3<1>", tube.Name);
        Assert.Equal(4, tube.Quantity);
        Assert.Equal("Steel", tube.Properties["MATERIAL"]);
        Assert.Equal("1250.0", tube.Properties["LENGTH"]);

        var plate = items[1];
        Assert.Equal("Base Plate<1>", plate.Name);
        Assert.Equal(2, plate.Quantity);
        Assert.Equal("10.0", plate.Properties["THICKNESS"]);
    }

    [Fact]
    public void CutListReader_Read_EmptyOrMalformedXml_ReturnsEmptySafely()
    {
        Assert.Empty(CutListReader.Read(null!));
        Assert.Empty(CutListReader.Read(Array.Empty<byte>()));
        Assert.Empty(CutListReader.Read(Encoding.UTF8.GetBytes("not xml")));
    }
}
