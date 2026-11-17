using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

internal sealed partial class DrViewMfcModel
{
    internal NativeState? NativeView;

    // moAbsoluteView descriptor 0x4DB072D0, vtable 0x4CE9ABD0,
    // Serialize 0x4C96ADC0 -> shared moDrView 0x4C977BE0.
    // The graph owns all tags. This ordinary-file context excludes the
    // condition(1) body-export protocol and DB/pointer-export archives.
    internal sealed class NativeState
    {
        internal bool Unfolded;
        internal bool Detail;
        internal NativeDrawingObjectGraph.Node? ParentViewFE0;
        internal ushort UnfoldedStateFE8;
        internal string? UnfoldedNameFF0;
        internal int UnfoldedStateFF8, UnfoldedState1008;
        internal NativeDrawingTransformModel UnfoldedTransform1000 = new NativeDrawingTransformModel();
        internal NativeDrawingObjectGraph.Node?[] ObjectsD00 = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
        internal NativeDrawingTransformModel Transform5B8 = new NativeDrawingTransformModel(), Transform690 = new NativeDrawingTransformModel(), Transform698 = new NativeDrawingTransformModel();
        internal NativeDrawingTransformModel Transform5C0 = new NativeDrawingTransformModel(), Transform5C8 = new NativeDrawingTransformModel();
        internal double[] Bounds6B0 = new double[4], Bounds6F0 = new double[4], Bounds9C0 = new double[4], Bounds850 = new double[4], Bounds710 = new double[4];
        internal double[] Box5D8 = new double[10], Box5E0 = new double[10];
        internal DrawingPreviewPoint Point750, Direction6A8, Point620, PointE48;
        internal double State758, State760, StateB40, State688, State3E0, State3E8, State3F0;
        internal byte State770, State774, HasEdge6A0, HasProfile7A0, HasProfile8B8, HasProfileDB0, HasOverridesB28, State8C0, StateD6C, HasPointE48;
        internal ushort State784, State794, State780, State798, State788, State78C, State8C4, HasAssocNote;
        internal NativeDrawingObjectGraph.Node? Edge6A0, Profile7A0, Profile8B8, ProfileDB0, Reference, Alignment, ExplodeId, DetailFolder, FeatureReference, AssocNote, DetailCircle, LayerMask, LinkedDisplay;
        internal string? StateE00, State538, State540, StateFC8;
        internal int StateE08, State8C8, StateB4C, HasPoint620, StateE10, StateD88, State404, StateE5C;
        internal uint Flags5E8, Flags5EC, StateE64;
        internal int State530, StateF74, StateF78, StateF7C, State5F0, StateF84, State548, State54C, StateF8C, State580, State584, State5F4, State778, State598;
        internal NativeDrawingObjectListModel Objects8D8 = new NativeDrawingObjectListModel();
        internal NativeDrawingObjectListModel[] Lists = CreateLists();
        internal DrSheetMfcModel.NativeState.OverrideEntry[] Overrides = Array.Empty<DrSheetMfcModel.NativeState.OverrideEntry>();
        internal DrawingPreviewPoint[] Vectors = Array.Empty<DrawingPreviewPoint>();
        internal EdgeEntry[] Edges = Array.Empty<EdgeEntry>(), SilhouetteEdges = Array.Empty<EdgeEntry>();
        internal NativeDrawingObjectArray.Property[] ComponentProperties = Array.Empty<NativeDrawingObjectArray.Property>();
        internal NativeDrawingFacetModel.Pair[] ComponentStates = Array.Empty<NativeDrawingFacetModel.Pair>();
        internal NativeDrawingGeometryFeatureModel.ObjectNumber[] ObjectStates = Array.Empty<NativeDrawingGeometryFeatureModel.ObjectNumber>();
        internal ushort StateFE0;
        internal string? StateFE8;
        internal byte StateFF0;
        internal int StateFF8, StateFFC, State1000;

        private static NativeDrawingObjectListModel[] CreateLists()
        {
            var lists = new NativeDrawingObjectListModel[9];
            for (int i = 0; i < lists.Length; i++) lists[i] = new NativeDrawingObjectListModel();
            return lists;
        }

        // Inline edge records have no class tag. 0x4C983680 reuses the
        // exact sketch moLineViz helper and appends one i32.
        internal sealed class EdgeEntry
        {
            internal NativeDrawingObjectGraph.Node? Edge;
            internal int State, State20, VisualState;
            internal byte HasVisual;
            internal SldFile.Binary.SketchData.NativeLineVisual Visual = new SldFile.Binary.SketchData.NativeLineVisual();
            internal void Serialize(MfcArchive ar, bool silhouette)
            {
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref Edge); ar.I32(ref State); ar.Byte(ref HasVisual);
                if (HasVisual != 0) { Visual.Serialize(ar); ar.I32(ref VisualState); }
                if (silhouette) ar.I32(ref State20);
            }
        }

        internal void Serialize(MfcArchive ar)
        {
            uint previous = ar.Context.NativeDrawingArchiveConditions;
            if ((previous & ~0x40u) != 0)
                throw new NotSupportedException("Drawing-view export/partition archive conditions are not decoded.");
            bool? previousOwner = ar.Context.NativeDrawingOwnerIsDrawing;
            ar.Context.NativeDrawingArchiveConditions = previous | 0x40;
            ar.Context.NativeDrawingOwnerIsDrawing = true;
            try { SerializeCore(ar, previous); SerializeDerived(ar); }
            finally
            {
                ar.Context.NativeDrawingArchiveConditions = previous;
                ar.Context.NativeDrawingOwnerIsDrawing = previousOwner;
            }
        }

        private void SerializeCore(MfcArchive ar, uint parentConditions)
        {
            int version = ar.DocumentVersion;
            if (version < 5000) throw new NotSupportedException("Unverified legacy drawing-view layout.");
            var graph = NativeDrawingModelFactory.Graph(ar);
            NativeDrawingObjectArray.Serialize(ar, ref ObjectsD00);
            Feature.Serialize(ar);
            Transform5B8.Serialize(ar); Transform690.Serialize(ar); Transform698.Serialize(ar);
            Doubles(ar, Bounds6B0); Doubles(ar, Bounds6F0); DrawingPreviewRecordModel.Point(ar, ref Point750);
            ar.F64(ref State758); ar.F64(ref State760); ar.Byte(ref State770);
            if (State770 != 0) { Transform5C0.Serialize(ar); Transform5C8.Serialize(ar); }
            ar.Byte(ref State774); DrawingPreviewRecordModel.String(ar, ref StateE00); ar.I32(ref StateE08);
            ar.Byte(ref HasEdge6A0);
            if (HasEdge6A0 != 0) { graph.Object(ar, ref Edge6A0); DrawingPreviewRecordModel.Point(ar, ref Direction6A8); }
            ar.Byte(ref HasProfile7A0); if (HasProfile7A0 != 0) graph.Object(ar, ref Profile7A0);
            ar.Byte(ref HasProfile8B8); if (HasProfile8B8 != 0) graph.Object(ar, ref Profile8B8);
            ar.Byte(ref HasProfileDB0); if (HasProfileDB0 != 0) graph.Object(ar, ref ProfileDB0);
            graph.Object(ar, ref Reference); graph.Object(ar, ref Alignment);
            ar.U16(ref State784); ar.U16(ref State794); ar.U16(ref State780); ar.U16(ref State798);
            ar.U16(ref State788); ar.U16(ref State78C); ar.U16(ref State8C4); ar.I32(ref State8C8);
            graph.Object(ar, ref ExplodeId); graph.Object(ar, ref DetailFolder); graph.Object(ar, ref FeatureReference);
            ar.U16(ref HasAssocNote); if (HasAssocNote != 0) graph.Object(ar, ref AssocNote);
            EdgeEntries(ar, ref Edges, false); Doubles(ar, Bounds9C0);
            ar.U32(ref Flags5E8); ar.U32(ref Flags5EC); Objects8D8.Serialize(ar); Doubles(ar, Box5D8);
            ar.Byte(ref HasOverridesB28);
            if (HasOverridesB28 != 0)
            {
                int count = Overrides.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 25) throw new InvalidDataException("Invalid view line-override count.");
                    Overrides = new DrSheetMfcModel.NativeState.OverrideEntry[count];
                }
                for (int i = 0; i < count; i++)
                {
                    if (ar.IsLoading) Overrides[i] = new DrSheetMfcModel.NativeState.OverrideEntry();
                    Overrides[i].Serialize(ar);
                }
            }
            int vectors = Vectors.Length; NativeDrawingDisplayArchive.Count(ar, ref vectors);
            if (ar.IsLoading)
            {
                if (vectors > ar.Remaining / 24) throw new InvalidDataException("Invalid view vector count.");
                Vectors = new DrawingPreviewPoint[vectors];
            }
            for (int i = 0; i < vectors; i++) DrawingPreviewRecordModel.Point(ar, ref Vectors[i]);
            EdgeEntries(ar, ref SilhouetteEdges, true); ar.F64(ref StateB40); ar.I32(ref StateB4C);
            Lists[0].Serialize(ar); ar.I32(ref HasPoint620);
            if (HasPoint620 != 0) DrawingPreviewRecordModel.Point(ar, ref Point620);
            graph.Object(ar, ref DetailCircle); graph.Object(ar, ref LayerMask);
            for (int i = 1; i <= 4; i++) Lists[i].Serialize(ar); // +9F8, A40, A88, AD0
            if ((Flags5E8 & 1) != 0) Doubles(ar, Bounds850);
            ar.Byte(ref State8C0); ar.F64(ref State688);
            Lists[5].Serialize(ar); Lists[6].Serialize(ar); ar.Byte(ref StateD6C); Lists[7].Serialize(ar);
            ar.I32(ref StateE10); ar.I32(ref StateD88); ar.I32(ref State404);
            ar.F64(ref State3E0); ar.F64(ref State3E8); ar.Byte(ref HasPointE48);
            if (HasPointE48 != 0) DrawingPreviewRecordModel.Point(ar, ref PointE48);
            Lists[8].Serialize(ar); ar.I32(ref StateE5C); ar.U32(ref StateE64);
            NativeDrawingObjectArray.Properties(ar, ref ComponentProperties);
            ObjectStateMap(ar); IntegerStateMap(ar);
            ar.I32(ref State530); ar.I32(ref StateF74); ar.I32(ref StateF78); ar.I32(ref StateF7C);
            if (version > 0x1423) ar.I32(ref State5F0);
            if (version > 0x146D) ar.I32(ref StateF84);
            if (version > 0x1BD2) { ar.I32(ref State548); DrawingPreviewRecordModel.String(ar, ref State538); }
            if (version >= 0x1C39) graph.Object(ar, ref LinkedDisplay);
            if (version > 0x277A) DrawingPreviewRecordModel.String(ar, ref State540);
            // Native moDrView removes its temporary 0x40 condition at
            // 0x4C97B8FC before these final scalar fields and the derived
            // view tail. Keep recursive derived references in parent scope.
            ar.Context.NativeDrawingArchiveConditions = parentConditions;
            if (version >= 0x1FB0) Doubles(ar, Box5E0);
            if (version >= 0x1FC0) Doubles(ar, Bounds710);
            if (version >= 0x1FCA) ar.I32(ref State54C);
            if (version >= 0x1FCB) ar.I32(ref StateF8C);
            if (version >= 0x23A3) { ar.I32(ref State580); ar.I32(ref State584); }
            if (version >= 0x23AE) ar.F64(ref State3F0);
            if (version >= 0x375B) DrawingPreviewRecordModel.String(ar, ref StateFC8);
            if (version >= 0x375F) ar.I32(ref State5F4);
            if (version > 0x3B14) ar.I32(ref State778);
            if (version > 0x42F3) ar.I32(ref State598);
        }

        private void SerializeDerived(MfcArchive ar)
        {
            // moDetailView 0x4C96F540, vtable 0x4CF08110. All
            // obsolete extra migration fields precede the supported 5000.
            // Its ordinary derived tail is just the same +FE0 parent ref.
            if (Detail)
            {
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref ParentViewFE0);
                return;
            }
            // moUnfoldedView 0x4C9830F0, final vtable 0x4D212740.
            // Common moDrView is identical; only this derived tail differs.
            if (Unfolded)
            {
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref ParentViewFE0);
                ar.U16(ref UnfoldedStateFE8); DrawingPreviewRecordModel.String(ar, ref UnfoldedNameFF0);
                ar.I32(ref UnfoldedStateFF8); UnfoldedTransform1000.Serialize(ar); ar.I32(ref UnfoldedState1008);
                return;
            }
            // moAbsoluteView-only tail 0x4C96ADC0. All gates precede 5000.
            ar.U16(ref StateFE0); DrawingPreviewRecordModel.String(ar, ref StateFE8);
            ar.Byte(ref StateFF0); ar.I32(ref StateFF8); ar.I32(ref StateFFC); ar.I32(ref State1000);
        }

        private void ObjectStateMap(MfcArchive ar)
        {
            // ctor +F40 is su_CMapObToInt, native CArchive::Count.
            uint count = checked((uint)ObjectStates.Length); ar.Count(ref count);
            if (ar.IsLoading)
            {
                if (count > int.MaxValue || count > (uint)ar.Remaining / 6) throw new InvalidDataException("Invalid view object-state map.");
                ObjectStates = new NativeDrawingGeometryFeatureModel.ObjectNumber[count];
            }
            for (int i = 0; i < ObjectStates.Length; i++)
            {
                if (ar.IsLoading) ObjectStates[i] = new NativeDrawingGeometryFeatureModel.ObjectNumber();
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref ObjectStates[i].Object); ar.U32(ref ObjectStates[i].Number);
            }
        }
        private void IntegerStateMap(MfcArchive ar)
        {
            int count = ComponentStates.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading)
            {
                if (count > ar.Remaining / 8) throw new InvalidDataException("Invalid view component-state map.");
                ComponentStates = new NativeDrawingFacetModel.Pair[count];
            }
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) ComponentStates[i] = new NativeDrawingFacetModel.Pair();
                ar.I32(ref ComponentStates[i].Key); ar.I32(ref ComponentStates[i].Value);
            }
        }
        private static void EdgeEntries(MfcArchive ar, ref EdgeEntry[] entries, bool silhouette)
        {
            if (entries.Length > ushort.MaxValue) throw new InvalidDataException("Too many view edge records.");
            ushort count = (ushort)entries.Length; ar.U16(ref count);
            if (ar.IsLoading)
            {
                if (count > ar.Remaining / (silhouette ? 11 : 7)) throw new InvalidDataException("Invalid view edge-record count.");
                entries = new EdgeEntry[count];
            }
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) entries[i] = new EdgeEntry();
                entries[i].Serialize(ar, silhouette);
            }
        }
        private static void Doubles(MfcArchive ar, double[] values)
        {
            for (int i = 0; i < values.Length; i++) ar.F64(ref values[i]);
        }
    }
}
