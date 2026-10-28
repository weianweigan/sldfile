using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SldFile.Ole;

internal class OlePropertySet
{
    public Guid Clsid { get; set; }
    public uint OSVersion { get; set; } = 2;
    public int CodePage { get; set; } = 1200;

    public Guid FmtId0 { get; set; }
    public Guid? FmtId1 { get; set; }

    public List<OlePropertyEntry> Properties { get; } = new();
    public List<OlePropertyEntry> UserDefinedProperties { get; } = new();

}

internal class OlePropertyEntry
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public object? Value { get; set; }
    public OleVtType VtType { get; set; }
}

internal static class OlePropertySetSerializer
{
    public static OlePropertySet Parse(byte[] bytes)
    {
        var ps = new OlePropertySet();
        if (bytes.Length < 28) return ps;

        ps.Clsid = ReadGuid(bytes, 8);
        ps.OSVersion = BitConverter.ToUInt32(bytes, 4);

        int numFmtIds = BitConverter.ToInt32(bytes, 24);
        if (numFmtIds < 1) return ps;

        ps.FmtId0 = ReadGuid(bytes, 28);
        int section0Offset = BitConverter.ToInt32(bytes, 44);
        ParseSection(bytes, section0Offset, ps, isUserDefined: false);

        if (numFmtIds >= 2 && bytes.Length >= 68)
        {
            ps.FmtId1 = ReadGuid(bytes, 48);
            int section1Offset = BitConverter.ToInt32(bytes, 64);
            if (section1Offset > 0 && section1Offset < bytes.Length)
                ParseSection(bytes, section1Offset, ps, isUserDefined: true);
        }

        return ps;
    }

    private static void ParseSection(byte[] bytes, int sectionOffset, OlePropertySet ps, bool isUserDefined)
    {
        if (sectionOffset + 8 > bytes.Length) return;

        int sectionSize = BitConverter.ToInt32(bytes, sectionOffset);
        int numProps = BitConverter.ToInt32(bytes, sectionOffset + 4);
        if (numProps <= 0 || sectionOffset + 8 + numProps * 8 > bytes.Length) return;

        var propOffsets = new Dictionary<uint, int>();
        for (int i = 0; i < numProps; i++)
        {
            int entryBase = sectionOffset + 8 + i * 8;
            if (entryBase + 8 > bytes.Length) break;
            uint id = BitConverter.ToUInt32(bytes, entryBase);
            int off = BitConverter.ToInt32(bytes, entryBase + 4);
            int absOff = sectionOffset + off;
            if (absOff >= 0 && absOff < bytes.Length)
                propOffsets[id] = absOff;
        }

        int codepage = 1200;
        if (propOffsets.TryGetValue(1, out int cpOff))
            codepage = ReadCodePage(bytes, cpOff);

        if (propOffsets.TryGetValue(0, out int dictOff))
            ps.CodePage = codepage;

        var dict = ReadDictionary(bytes, propOffsets, codepage);

        var targetList = isUserDefined ? ps.UserDefinedProperties : ps.Properties;
        foreach (var kvp in propOffsets)
        {
            if (kvp.Key == 0 || kvp.Key == 1) continue;

            var entry = ReadProperty(bytes, kvp.Value, kvp.Key, dict);
            if (entry != null)
                targetList.Add(entry);
        }
    }

    private static int ReadCodePage(byte[] bytes, int offset)
    {
        if (offset + 8 > bytes.Length) return 1200;
        short cp = BitConverter.ToInt16(bytes, offset + 4);
        return cp > 0 ? cp : 1200;
    }

    private static Dictionary<uint, string> ReadDictionary(byte[] bytes, Dictionary<uint, int> propOffsets, int codepage)
    {
        var dict = new Dictionary<uint, string>();
        if (!propOffsets.TryGetValue(0, out int dictOff) || dictOff + 4 > bytes.Length)
            return dict;

        int count = BitConverter.ToInt32(bytes, dictOff);
        int cursor = dictOff + 4;

        for (int i = 0; i < count; i++)
        {
            if (cursor + 8 > bytes.Length) break;
            uint propId = BitConverter.ToUInt32(bytes, cursor);
            int nameLen = BitConverter.ToInt32(bytes, cursor + 4);
            cursor += 8;
            if (nameLen <= 0 || cursor + nameLen > bytes.Length) break;

            string name;
            if (nameLen > 1)
                name = codepage == 1200
                    ? Encoding.Unicode.GetString(bytes, cursor, nameLen - 1)
                    : Encoding.UTF8.GetString(bytes, cursor, nameLen - 1);
            else
                name = "";

            if (!dict.ContainsKey(propId))
                dict[propId] = name;

            cursor += nameLen;
        }

        return dict;
    }

    private static OlePropertyEntry? ReadProperty(byte[] bytes, int offset, uint id, Dictionary<uint, string> dict)
    {
        if (offset + 4 > bytes.Length) return null;
        int vtType = BitConverter.ToInt32(bytes, offset);

        dict.TryGetValue(id, out var name);

        switch (vtType)
        {
            case 30: // VT_LPSTR
                if (offset + 8 > bytes.Length) return null;
                int strLen = BitConverter.ToInt32(bytes, offset + 4);
                if (strLen <= 0) return new OlePropertyEntry { Id = id, Name = name, Value = "", VtType = OleVtType.VT_LPSTR };
                int readLen = strLen > 0 ? strLen - 1 : 0;
                if (offset + 8 + readLen > bytes.Length) return null;
                var strVal = Encoding.UTF8.GetString(bytes, offset + 8, readLen);
                return new OlePropertyEntry { Id = id, Name = name, Value = strVal, VtType = OleVtType.VT_LPSTR };

            case 3: // VT_I4
                if (offset + 8 > bytes.Length) return null;
                return new OlePropertyEntry { Id = id, Name = name, Value = BitConverter.ToInt32(bytes, offset + 4), VtType = OleVtType.VT_I4 };

            case 64: // VT_FILETIME
                if (offset + 12 > bytes.Length) return null;
                long fileTime = BitConverter.ToInt64(bytes, offset + 4);
                var dt = fileTime != 0 ? DateTime.FromFileTime(fileTime) : DateTime.MinValue;
                return new OlePropertyEntry { Id = id, Name = name, Value = dt, VtType = OleVtType.VT_FILETIME };

            case 11: // VT_BOOL
                if (offset + 8 > bytes.Length) return null;
                return new OlePropertyEntry { Id = id, Name = name, Value = BitConverter.ToInt32(bytes, offset + 4) != 0, VtType = OleVtType.VT_BOOL };

            default:
                return null;
        }
    }

    private static Guid ReadGuid(byte[] bytes, int offset)
    {
        var guidBytes = new byte[16];
        Array.Copy(bytes, offset, guidBytes, 0, 16);
        return new Guid(guidBytes);
    }
}
