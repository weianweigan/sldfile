using System;
using System.IO;

namespace SldFile.Mfc;

// swccu su_CMapStringToString::Serialize 0x31DA76D0 delegates in an
// ordinary archive to MFC ordinal 12804 (mfc140u 0x180233F30).
// CArchive::Count followed by key/value CStrings. Retain serialized order
// rather than rebuilding a dictionary and changing its hash iteration order.
internal sealed class NativeDrawingStringMapModel : IMfcSerializable
{
    internal sealed class Entry { internal string? Key, Value; }
    internal Entry[] Entries = Array.Empty<Entry>();
    public void Serialize(MfcArchive ar)
    {
        uint count = checked((uint)Entries.Length); ar.Count(ref count);
        if (ar.IsLoading)
        {
            if (count > int.MaxValue || count > (uint)ar.Remaining / 2)
                throw new InvalidDataException("Invalid native string-map count.");
            Entries = new Entry[count];
        }
        for (int i = 0; i < Entries.Length; i++)
        {
            if (ar.IsLoading) Entries[i] = new Entry();
            DrawingPreviewRecordModel.String(ar, ref Entries[i].Key);
            DrawingPreviewRecordModel.String(ar, ref Entries[i].Value);
        }
    }
}
