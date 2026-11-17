using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeBlockManager BlockManager = new NativeBlockManager();
    internal NativeLoopManager LoopManager = new NativeLoopManager();
    internal NativeSlotManager SlotManager = new NativeSlotManager();
    internal int NativeState134;
    internal NativeDrawingObjectGraph.Node? NativeOwnerProfile;

    // Common sgSketch suffix 0x4C5C2C71. Owner-profile presence is archive
    // context (+834), never inferred from the next bytes or a failed tag.
    internal void SerializeNativeCaches(MfcArchive ar, bool includesOwnerProfile)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch caches.");
        BlockManager.Serialize(ar); LoopManager.Serialize(ar); SlotManager.Serialize(ar);
        ar.I32(ref NativeState134);
        if (includesOwnerProfile) NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeOwnerProfile);
    }

    internal sealed class NativeIndexedObject
    {
        internal int Index;
        internal NativeDrawingObjectGraph.Node? Object;
        internal void Serialize(MfcArchive ar)
        {
            ar.I32(ref Index); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Object);
        }
    }

    // +580 virtual Serialize 0x4C92A9F0. All supported >=5000 versions
    // include the triple table and the nested +40 object's Serialize.
    internal sealed class NativeBlockManager
    {
        internal NativeIndexedObject[] Blocks = Array.Empty<NativeIndexedObject>();
        internal State[] States = Array.Empty<State>();
        internal HandleData[] Handles = Array.Empty<HandleData>();
        internal sealed class State
        {
            internal int Index;
            internal uint First, Second;
        }
        internal sealed class HandleData
        {
            internal NativeDrawingObjectGraph.Node? Handle, Data;
        }
        internal void Serialize(MfcArchive ar)
        {
            Rows(ar, ref Blocks, item => item.Serialize(ar));
            Rows(ar, ref States, item =>
            {
                ar.I32(ref item.Index); ar.U32(ref item.First); ar.U32(ref item.Second);
            });
            // Nested vtable 0x4D268C18 -> 0x4C92B160: tagged sgEntHandle
            // followed by tagged sgEntHandleData_c, with an i32 pair count.
            var graph = NativeDrawingModelFactory.Graph(ar);
            Rows(ar, ref Handles, item =>
            {
                graph.Object(ar, ref item.Handle); graph.Object(ar, ref item.Data);
            });
        }
    }

    // +598 sgLoopManager::Serialize 0x4C5B5FD0. Preserve every wire slot,
    // including null slots; native saving may trim trailing empty slots.
    internal sealed class NativeLoopManager
    {
        internal Loop[] Slots = Array.Empty<Loop>();
        internal sealed class Loop
        {
            internal int Presence, Index, State110, State114, State118, State11C, State130;
            internal double State138;
            internal NativeDrawingFacetModel.Pair[] Segments = Array.Empty<NativeDrawingFacetModel.Pair>();
            internal NativeDrawingObjectGraph.Node? DisplayDimension;
            internal void Serialize(MfcArchive ar)
            {
                ar.I32(ref Presence); if (Presence == 0) return;
                ar.I32(ref Index);
                Rows(ar, ref Segments, item => { ar.I32(ref item.Key); ar.I32(ref item.Value); });
                ar.I32(ref State110); ar.I32(ref State114); ar.I32(ref State118); ar.I32(ref State11C);
                if (ar.DocumentVersion > 0x17D8) { ar.I32(ref State130); ar.F64(ref State138); }
                if (ar.DocumentVersion > 0x17E3) NativeDrawingModelFactory.Graph(ar).Object(ar, ref DisplayDimension);
            }
        }
        internal void Serialize(MfcArchive ar) => Rows(ar, ref Slots, item => item.Serialize(ar));
    }

    // +588 virtual Serialize 0x4C5C3690 calls map helper 0x4C5C3A60.
    // sgSlot_c payloads use the owning graph and shared entity/handle codecs.
    internal sealed class NativeSlotManager
    {
        internal int State10;
        internal NativeIndexedObject[] Slots = Array.Empty<NativeIndexedObject>();
        internal void Serialize(MfcArchive ar)
        {
            ar.I32(ref State10); Rows(ar, ref Slots, item => item.Serialize(ar));
        }
    }

    // sgSlot_c descriptor 0x4DAFA538, vtable 0x4D2D6F98,
    // Serialize 0x4C5C3D20. Reuse the common entity and tagged handles.
    internal sealed class NativeSlot : IMfcSerializable
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal int StateC0, StateC4;
        internal NativeDrawingObjectGraph.Node?[] Handles = new NativeDrawingObjectGraph.Node?[6];
        public void Serialize(MfcArchive ar)
        {
            bool ownerIs3D = ar.Context.NativeSketchOwnerIs3D
                ?? throw new InvalidOperationException("Native slot requires its owning sketch context.");
            Entity.Serialize(ar, ownerIs3D); ar.I32(ref StateC0); ar.I32(ref StateC4);
            if (Handles.Length != 6) throw new System.IO.InvalidDataException("Native slot requires six handle positions.");
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < 6; i++) graph.Object(ar, ref Handles[i]);
        }
    }
}
