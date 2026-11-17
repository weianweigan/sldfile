using System;
using System.IO;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData : IMfcSerializable
{
    public void Serialize(MfcArchive ar)
    {
        bool is3D = ar.Context.NativeSketchOwnerIs3D
            ?? throw new InvalidOperationException("Native sketch requires its owner dimensionality.");
        bool ownerProfile = ar.Context.NativeSketchIncludesOwnerProfile
            ?? throw new InvalidOperationException("Native sketch requires explicit archive owner-profile context.");
        SerializeNativeOrdinary(ar, is3D, ownerProfile);
    }

    internal NativeSilhouette[] NativeSilhouettes = Array.Empty<NativeSilhouette>();

    internal void SerializeNativeSilhouettes(MfcArchive ar)
    {
        if (ar.DocumentVersion <= 0x2F50) return;
        if (NativeSilhouettes.Length > ushort.MaxValue) throw new InvalidDataException("Too many native silhouettes.");
        ushort count = (ushort)NativeSilhouettes.Length; ar.U16(ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 2) throw new InvalidDataException("Invalid native silhouette count.");
            NativeSilhouettes = new NativeSilhouette[count];
        }
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeSilhouettes[i] = new NativeSilhouette();
            NativeSilhouettes[i].Serialize(ar);
        }
    }

    // sgSilhouette_c 0x4C5B9E30 in an ordinary tagged-object archive.
    internal sealed class NativeSilhouette
    {
        internal ushort State28, State30;
        internal int State2C;
        internal NativeDrawingObjectGraph.Node? Objects, Plane;
        internal NativeDrawingObjectGraph.Node?[] Handles = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDynamicI32Array Indices = new NativeDynamicI32Array();
        internal Entity[] Entities = Array.Empty<Entity>();
        internal void Serialize(MfcArchive ar)
        {
            ar.U16(ref State28); if (State28 != 0) return;
            int count = Handles.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            ar.I32(ref State2C); ar.U16(ref State30);
            var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Objects);
            if (ar.IsLoading) Handles = new NativeDrawingObjectGraph.Node?[count];
            for (int i = 0; i < count; i++) graph.Object(ar, ref Handles[i]);
            Indices.Serialize(ar);
            if (ar.IsLoading) Entities = new Entity[count];
            if (Entities.Length != count) throw new InvalidDataException("Native silhouette handle/entity counts differ.");
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Entities[i] = new Entity();
                Entities[i].Serialize(ar);
            }
            if (ar.DocumentVersion > 0x2F5A) graph.Object(ar, ref Plane);
        }

        // Inline entity 0x4C5ABCE0. Native loading tests State20 twice,
        // whereas storing tests the derived cache status for State38. Until
        // that nonzero status is understood, reject it explicitly rather
        // than introduce an asymmetric codec or guess a payload boundary.
        internal sealed class Entity
        {
            internal ushort State20, State38;
            internal NativeDrawingObjectGraph.Node? ExternalReference, Handle;
            internal double State28, State30;
            internal void Serialize(MfcArchive ar)
            {
                ar.U16(ref State20); if (State20 != 0) return;
                ar.U16(ref State38);
                if (State38 != 0) throw new NotSupportedException("Native silhouette entity cache-status branch is not decoded.");
                var graph = NativeDrawingModelFactory.Graph(ar);
                graph.Object(ar, ref ExternalReference); graph.Object(ar, ref Handle);
                ar.F64(ref State28); ar.F64(ref State30);
            }
        }
    }

    // Explicit ordinary archive entry on the existing sgSketch data type.
    // Kernel/specialized entities and unsupported tagged children fail at
    // their exact boundary; this is not a registration of a Definition root.
    internal void SerializeNativeOrdinary(MfcArchive ar, bool ownerIs3D, bool includesOwnerProfile)
    {
        bool? previousOwner = ar.Context.NativeSketchOwnerIs3D;
        ar.Context.NativeSketchOwnerIs3D = ownerIs3D;
        try
        {
            SerializeNativeEntityTables(ar, ownerIs3D); SerializeNativeRelations(ar);
            SerializeNativePostRelationTables(ar); SerializeNativeSettingsPrefix(ar);
            SerializeNativeVisualAndTools(ar); SerializeNativeDisplayGroups(ar, ownerIs3D);
            SerializeNativeSilhouettes(ar); SerializeNativeCaches(ar, includesOwnerProfile);
        }
        finally { ar.Context.NativeSketchOwnerIs3D = previousOwner; }
    }
}
