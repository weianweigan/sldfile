using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Serializable sub-unit entry within a unit type (e.g. inches or millimeters within length).
/// </summary>
internal sealed class UserSubUnitModel
{
    public uint Index { get; set; }
    public uint Flags { get; set; }
    public double ConversionFactor { get; set; }
}

/// <summary>
/// Serializable unit entry in the SolidWorks unit system table (Contents/User Units Table__ZLB).
/// Corresponds to MFC classes such as moLengthUserUnits_c, moAngleUserUnits_c, moDensityUnits_c, etc.
/// </summary>
internal sealed class UserUnitTypeModel : IMfcSerializable
{
    public string ClassName { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
    public int SubUnitCount { get; set; }
    public List<UserSubUnitModel> SubUnits { get; } = new();
    public uint TailField { get; set; }
    public byte[] RawBlock { get; set; } = Array.Empty<byte>();

    public UserUnitTypeModel() { }

    public UserUnitTypeModel(string className, byte[] rawBlock)
    {
        ClassName = className ?? string.Empty;
        UnitTypeName = DeriveUnitTypeName(ClassName);
        RawBlock = rawBlock ?? Array.Empty<byte>();
        ParseFromRawBlock();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort tag = 0;
            ar.ClassTagOrRef(ClassName, ref tag);
            ar.WriteBytes(RawBlock);
        }
        else
        {
            ar.ReadClassTag(out string className, out _, out _);
            ClassName = className;
            UnitTypeName = DeriveUnitTypeName(ClassName);
        }
    }

    public static string DeriveUnitTypeName(string className)
    {
        if (string.IsNullOrEmpty(className)) return string.Empty;
        string name = className;
        if (name.StartsWith("mo", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(2);
        if (name.EndsWith("_c", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 2);
        if (name.EndsWith("UserUnits", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 9);
        else if (name.EndsWith("Units", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 5);
        return name;
    }

    private void ParseFromRawBlock()
    {
        if (RawBlock == null || RawBlock.Length < 4) return;
        int offset = 0;
        if (!MfcPrimitives.TryReadU32(RawBlock, ref offset, out uint subUnitCount))
            return;
        SubUnitCount = (int)subUnitCount;

        SubUnits.Clear();
        for (int i = 0; i < Math.Min(subUnitCount, 20); i++)
        {
            if (offset + 16 > RawBlock.Length) break;
            uint u1 = BitConverter.ToUInt32(RawBlock, offset);
            uint u2 = BitConverter.ToUInt32(RawBlock, offset + 4);
            double factor = BitConverter.ToDouble(RawBlock, offset + 8);
            offset += 16;

            if (u1 <= 100 && u2 > 100)
            {
                SubUnits.Add(new UserSubUnitModel
                {
                    Index = u1,
                    Flags = u2,
                    ConversionFactor = factor
                });
            }
            else if (u1 <= 100)
            {
                SubUnits.Add(new UserSubUnitModel
                {
                    Index = u1,
                    Flags = 0,
                    ConversionFactor = BitConverter.ToDouble(RawBlock, offset - 12)
                });
                offset -= 4;
            }
        }

        if (RawBlock.Length >= 4)
        {
            TailField = BitConverter.ToUInt32(RawBlock, RawBlock.Length - 4);
        }
    }
}

/// <summary>
/// Unified model for Contents/User Units Table__ZLB stream using MFC CArchive serialization.
/// Symmetrically reads and writes moUnitsTable_c and its collection of unit type definitions.
/// </summary>
internal sealed class UserUnitsTableModel : IMfcSerializable
{
    public const string RootClassName = "moUnitsTable_c";

    public List<UserUnitTypeModel> Units { get; } = new();

    public int UnitCount => Units.Count;
    public IReadOnlyList<UserUnitTypeModel> UnitTypes => Units;
    public byte[]? RawData { get; set; }

    public UserUnitsTableModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }
            ushort rootTag = 0;
            ar.ClassTagOrRef(RootClassName, ref rootTag);
            ushort count = (ushort)Units.Count;
            ar.U16(ref count);

            foreach (var unit in Units)
            {
                unit.Serialize(ar);
            }
        }
        else
        {
            RawData = ar.ToArray();
            Units.Clear();
            if (ar.Remaining < 2) return;

            ar.ReadClassTag(out string rootClass, out _, out _);
            if (rootClass != RootClassName && !rootClass.StartsWith("moUnitsTable"))
            {
                return;
            }

            if (ar.Remaining < 2) return;
            ushort count = 0;
            ar.U16(ref count);
            if (count == 0 || count > 1000) return;

            byte[] remaining = ar.ReadRemainingBytes();
            int offset = 0;

            for (int i = 0; i < count && offset < remaining.Length; i++)
            {
                // Align to next MFC tag
                if (!MfcPrimitives.IsMfcTag(remaining, offset))
                {
                    int next = -1;
                    for (int j = offset; j <= remaining.Length - 4; j++)
                    {
                        if (MfcPrimitives.IsMfcTag(remaining, j))
                        {
                            next = j;
                            break;
                        }
                    }
                    if (next < 0) break;
                    offset = next;
                }

                // Read tag header
                int tagStart = offset;
                var tag = MfcPrimitives.ReadMfcTag(remaining, ref offset);
                if (tag == null || string.IsNullOrEmpty(tag.ClassName))
                    break;

                int dataStart = offset;
                int dataEnd = remaining.Length;
                for (int j = dataStart; j <= remaining.Length - 4; j++)
                {
                    if (MfcPrimitives.IsMfcTag(remaining, j))
                    {
                        dataEnd = j;
                        break;
                    }
                }

                int dataLen = dataEnd - dataStart;
                byte[] rawBlock = new byte[dataLen];
                Array.Copy(remaining, dataStart, rawBlock, 0, dataLen);
                offset = dataEnd;

                var unitModel = new UserUnitTypeModel(tag.ClassName, rawBlock);
                Units.Add(unitModel);
            }
        }
    }
}
