using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for SheetPreviews/SheetNames binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes sheet count and WChar sheet names.
/// </summary>
internal sealed class SheetNamesModel : IMfcSerializable
{
    public List<string> Names { get; } = new();
    public byte[]? RawData { get; set; }

    public SheetNamesModel() { }



    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort count = (ushort)Names.Count;
            ar.U16(ref count);
            foreach (var name in Names)
            {
                string? n = name;
                ar.WChar(ref n);
            }
        }
        else
        {
            Names.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length < 2) return;

            int offset = 0;
            ushort count = BitConverter.ToUInt16(data, offset);
            offset += 2;

            for (int i = 0; i < count && offset < data.Length; i++)
            {
                if (MfcPrimitives.IsWCharPrefix(data, offset))
                {
                    var name = MfcPrimitives.ReadWChar(data, ref offset);
                    if (name != null)
                        Names.Add(name);
                }
                else
                {
                    break;
                }
            }
        }
    }
}
