using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class UserUnitsTableSchemaMapper
{
    public static void Map(byte[] data, UserUnitsTableData model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Root: moUnitsTable_c contains u16 unit count
        var root = doc.FindFirst("moUnitsTable_c");
        if (root != null)
        {
            model.UnitCount = root.CreateReader().ReadU16();
        }

        // Parse each child unit type
        var unitEntries = new List<UserUnitTypeEntry>();
        foreach (var element in doc.AllElements)
        {
            if (string.IsNullOrEmpty(element.ClassName))
                continue;
            if (element.ClassName == "moUnitsTable_c")
                continue;

            var entry = ParseUnitEntry(element);
            unitEntries.Add(entry);
        }

        model.UnitTypes = unitEntries.AsReadOnly();
    }

    private static UserUnitTypeEntry ParseUnitEntry(MfcElement element)
    {
        var entry = new UserUnitTypeEntry
        {
            ClassName = element.ClassName,
            UnitTypeName = DeriveUnitTypeName(element.ClassName),
            RawBlock = element.RawData,
        };

        var reader = element.CreateReader();

        // Read sub-unit count
        if (!reader.TryReadU32(out uint subUnitCount))
            return entry;

        entry.SubUnitCount = (int)subUnitCount;

        // Read sub-units: each is u32 index + u32 flags + f64 factor
        var subUnits = new List<UserSubUnit>();
        for (int i = 0; i < Math.Min(subUnitCount, 20); i++)
        {
            if (reader.Remaining < 16) break;

            uint u1 = reader.ReadU32();
            uint u2 = reader.ReadU32();
            double factor = reader.ReadF64();

            // Heuristic: if first u32 looks like a small index (1-100), treat as index+flags
            if (u1 <= 100 && u2 > 100)
            {
                subUnits.Add(new UserSubUnit
                {
                    Index = u1,
                    Flags = u2,
                    ConversionFactor = factor
                });
            }
            // If both look like valid indices/flags
            else if (u1 <= 100)
            {
                subUnits.Add(new UserSubUnit
                {
                    Index = u1,
                    Flags = 0,
                    ConversionFactor = BitConverter.ToDouble(reader.Data, reader.Offset - 12)
                });
                reader.Skip(-4); // adjust for smaller entry
            }
        }
        entry.SubUnits = subUnits.AsReadOnly();

        // Read tail field (last 4 bytes of element data)
        if (element.DataLength >= 4)
        {
            entry.TailField = BitConverter.ToUInt32(element.SourceData, element.DataEnd - 4);
        }

        return entry;
    }

    private static string DeriveUnitTypeName(string className)
    {
        // moLengthUserUnits_c → Length
        // moDensityUnits_c → Density
        return className
            .Replace("mo", "")
            .Replace("UserUnits_c", "")
            .Replace("Units_c", "");
    }
}
