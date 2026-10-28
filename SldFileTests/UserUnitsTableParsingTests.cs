using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class UserUnitsTableParsingTests
{
    [Fact]
    public void UserUnitsTable_Assembly_ShouldParse()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var uut = file.UserUnitsTable;

        // Should have 16 unit types
        Assert.Equal(16, uut.UnitCount);
        Assert.Equal(16, uut.UnitTypes.Count);

        // Verify key unit types exist
        Assert.Contains(uut.UnitTypes, u => u.ClassName == "moLengthUserUnits_c");
        Assert.Contains(uut.UnitTypes, u => u.ClassName == "moAngleUserUnits_c");
        Assert.Contains(uut.UnitTypes, u => u.ClassName == "moForceUnits_c");
        Assert.Contains(uut.UnitTypes, u => u.ClassName == "moTimeUnits_c");

        // Each unit type should have a derived name
        foreach (var unit in uut.UnitTypes)
        {
            Assert.NotEmpty(unit.ClassName);
            Assert.NotEmpty(unit.UnitTypeName);
            Assert.True(unit.RawBlock.Length > 0);
        }
    }

    [Fact]
    public void UserUnitsTable_CubePart_ShouldBeEmpty()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var uut = file.UserUnitsTable;

        // Cube part has 24-byte ZLB (empty) — UserUnitsTable is compressed/empty
        Assert.Empty(uut.UnitTypes);
    }

    [Fact]
    public void UserUnitsTable_RoundTrip_ShouldPreserveData()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.Assembly);

        int originalCount;
        int originalUnitTypes;

        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalCount = file.UserUnitsTable.UnitCount;
            originalUnitTypes = file.UserUnitsTable.UnitTypes.Count;
            file.Save();
        }

        using (var file = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(originalCount, file.UserUnitsTable.UnitCount);
            Assert.Equal(originalUnitTypes, file.UserUnitsTable.UnitTypes.Count);
        }
    }
}
