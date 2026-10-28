using System;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Maps a CMgrHdr2 MFC document tree to a typed <see cref="CmgrHdr2Data"/> model.
/// </summary>
internal static class CmgrHdr2SchemaMapper
{
    public static void Map(byte[] data, CmgrHdr2Data model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Find dmConfigMgrHeader_c root
        var root = doc.FindFirst("dmConfigMgrHeader_c");
        if (root != null)
        {
            model.ConfigCount = root.CreateReader().ReadU16();
        }

        // Find dmConfigHeader_c
        var header = doc.FindFirst("dmConfigHeader_c");
        if (header != null)
        {
            var reader = header.CreateReader();
            model.TagCount = reader.ReadU16();
            reader.Skip(2); // skip padding u16

            // Read the WChar-encoded magic marker (2 chars = u32)
            string? wchar = reader.ReadWChar();
            if (wchar?.Length >= 2)
            {
                model.MagicMarker = BitConverter.ToUInt32(Encoding.Unicode.GetBytes(wchar), 0);
            }

            reader.Skip(4); // skip zero field
            model.TypeFlag = reader.ReadU32();
            reader.ReadWChar(); // skip second magic marker

            // Trailer fields located relative to data end:
            // FileTypeCode (u16) at data.Length - 6, Timestamp (u32) at data.Length - 4
            if (data.Length >= 6)
            {
                model.FileTypeCode = BitConverter.ToUInt16(data, data.Length - 6);
                model.Timestamp = BitConverter.ToUInt32(data, data.Length - 4);
            }
        }
    }
}
