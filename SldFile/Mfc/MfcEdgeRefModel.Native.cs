using System;
using System.IO;

namespace SldFile.Mfc;

public sealed partial class MfcEdgeRefModel
{
    // moEdgeRef descriptor 0x4DADD0C0 -> constructor 0x4B79F320,
    // vtable 0x4CE9CEE0 -> shared moBEntRef serializer 0x4BBF2BD0.
    internal sealed class NativeState
    {
        internal uint State60, State64, FlagsE8, State70, State48;
        internal double State28, State30;
        internal NativeDrawingObjectGraph.Node? Vertex;
        internal NativeDrawingObjectGraph.Node?[] EntityIds = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal TimePair[] Times = Array.Empty<TimePair>();
        internal int StateEC, HasCache;
        internal ReferenceCache Cache = new ReferenceCache();

        internal void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native edge reference.");
            ar.U32(ref State60); ar.U32(ref State64);
            uint count = checked((uint)EntityIds.Length); ar.U32(ref count);
            if (count > int.MaxValue) throw new InvalidDataException("Native entity ID count exceeds the supported size.");
            ar.U32(ref FlagsE8); ar.U32(ref State70); ar.F64(ref State28); ar.F64(ref State30);
            var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Vertex);
            if (ar.IsLoading && count > ar.Remaining / 2) throw new InvalidDataException("Native entity IDs exceed the archive.");
            if (ar.IsLoading) EntityIds = new NativeDrawingObjectGraph.Node?[count];
            // moEntityId 0x4BBF8020 has only a tagged moEntityIdRep pointer
            // in the supported versions. Each array item is inline, not tagged.
            for (int i = 0; i < EntityIds.Length; i++) graph.Object(ar, ref EntityIds[i]);
            ar.U32(ref State48); // >=0x542 retains the count but writes no old ID array.
            if (ar.DocumentVersion > 0x23BB)
            {
                int n = Times.Length; ar.I32(ref n); CheckCount(ar, n, 8);
                if (ar.IsLoading) Times = new TimePair[n];
                for (int i = 0; i < n; i++)
                {
                    if (ar.IsLoading) Times[i] = new TimePair();
                    Times[i].Serialize(ar);
                }
            }
            if (ar.DocumentVersion > 0x332C)
            {
                ar.I32(ref StateEC); ar.I32(ref HasCache);
                if (HasCache != 0) Cache.Serialize(ar);
            }
        }
    }

    // 0x4B5F71B0 / 0x4B5F71F0: two su_CTime values, each a u32 on wire.
    internal sealed class TimePair
    {
        internal uint First, Second;
        internal void Serialize(MfcArchive ar) { ar.U32(ref First); ar.U32(ref Second); }
    }

    // Inline moBEntRef cache, constructor 0x4B59F3D0, vtable
    // 0x4CE2EEC8 -> serializer 0x4B59F780. Preserve all filed rows,
    // including vector zeros that native loading discards at runtime.
    internal sealed class ReferenceCache
    {
        internal Pair[] Mapping = Array.Empty<Pair>();
        internal int[] Values = Array.Empty<int>();
        internal int State38, State40, State44;
        internal void Serialize(MfcArchive ar)
        {
            int count = Mapping.Length; ar.I32(ref count); CheckCount(ar, count, 8);
            if (ar.IsLoading) Mapping = new Pair[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Mapping[i] = new Pair();
                ar.I32(ref Mapping[i].Key); ar.I32(ref Mapping[i].Value);
            }
            count = Values.Length; ar.I32(ref count); CheckCount(ar, count, 4);
            if (ar.IsLoading) Values = new int[count];
            for (int i = 0; i < count; i++) ar.I32(ref Values[i]);
            ar.I32(ref State38);
            if (ar.DocumentVersion > 0x3358) { ar.I32(ref State40); ar.I32(ref State44); }
        }
    }

    internal sealed class Pair { internal int Key, Value; }
    private static void CheckCount(MfcArchive ar, int count, int minimumSize)
    {
        if (count < 0 || (ar.IsLoading && count > ar.Remaining / minimumSize))
            throw new InvalidDataException("Invalid native edge-reference collection count.");
    }
}
