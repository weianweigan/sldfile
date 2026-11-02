using System;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured single-edge persistent reference payload used by Fillet_c,
/// VarFillet_c and Chamfer_c. Only the measured header/record layout is accepted; the opaque
/// persistent id and archive GUID are exposed verbatim. Unknown layouts return null.
/// </summary>
internal static class PersistentEdgeReferenceParser
{
    internal static readonly string ExpectedArchiveGuid =
        "7dc39425ad49b2547dc39425ad49b254";

    internal static FeatureReferenceData? ParseSingle(byte[] payload)
    {
        if (payload == null) return null;
        if (payload.Length != 146 && payload.Length != 148 && payload.Length != 152) return null;
        if (BitConverter.ToUInt32(payload, 0) != 1 || BitConverter.ToUInt32(payload, 4) != 0 ||
            BitConverter.ToUInt32(payload, 8) != 4) return null;
        uint classWord = BitConverter.ToUInt32(payload, 12);
        if (classWord != 0u && classWord != 0x200u) return null;
        uint persistentId = BitConverter.ToUInt32(payload, 16);

        string guid = BitConverter.ToString(payload, 20, 16).Replace("-", string.Empty).ToLowerInvariant();
        if (guid != ExpectedArchiveGuid) return null;

        // Two measured record orders occur in the controlled samples:
        // A (fillet_5mm/var/chamfer): F1/id1, F1/id4, E8/id1, E8/id0;
        // B (fillet_2mm):             E8/id1, F1/id2, F1/id1, F1/id3.
        // Record spacing varies by payload length, so each 20-byte record is located in order.
        int record = 0x26;
        bool layoutA = TryFindRecord(payload, ref record, 0x80F1, 1) &&
                       TryFindRecord(payload, ref record, 0x80F1, 4) &&
                       TryFindRecord(payload, ref record, 0x80E8, 1) &&
                       TryFindRecord(payload, ref record, 0x80E8, 0);
        int recordB = 0x26;
        bool layoutB = TryFindRecord(payload, ref recordB, 0x80E8, 1) &&
                       TryFindRecord(payload, ref recordB, 0x80F1, 2) &&
                       TryFindRecord(payload, ref recordB, 0x80F1, 1) &&
                       TryFindRecord(payload, ref recordB, 0x80F1, 3);
        if (!layoutA && !layoutB) return null;

        return new FeatureReferenceData
        {
            EntityType = SelectionEntityType.Edge,
            EntityId = 4,
            PersistentId = persistentId,
            ClassWord = classWord,
            ArchiveGuid = guid,
            RecordCount = 4,
            ReferenceName = "PersistentEdgeRef"
        };
    }

    private static bool TryFindRecord(
        byte[] payload, ref int cursor, ushort firstClass, uint id)
    {
        for (int i = cursor; i + 20 <= payload.Length; i++)
        {
            if (!Record(payload, i, firstClass, id)) continue;
            cursor = i + 2;
            return true;
        }
        return false;
    }

    private static bool Record(byte[] payload, int offset, ushort firstClass, uint id)
    {
        if (offset < 0 || offset + 20 > payload.Length) return false;
        if (BitConverter.ToUInt16(payload, offset) != firstClass ||
            BitConverter.ToUInt16(payload, offset + 2) != 0 ||
            BitConverter.ToUInt16(payload, offset + 4) != 0x80EA ||
            BitConverter.ToUInt16(payload, offset + 6) != 0x00ED ||
            BitConverter.ToUInt16(payload, offset + 8) != 0x004F ||
            BitConverter.ToUInt16(payload, offset + 10) != 0) return false;
        uint serial = BitConverter.ToUInt32(payload, offset + 12);
        return serial != 0 && BitConverter.ToUInt32(payload, offset + 16) == id;
    }
}
