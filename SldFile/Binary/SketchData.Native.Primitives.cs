using System;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeParabola[] NativeParabolas = Array.Empty<NativeParabola>();
    internal NativePlane[] NativePlanes = Array.Empty<NativePlane>();
    internal NativeCylinder[] NativeCylinders = Array.Empty<NativeCylinder>();
    internal NativeSphere[] NativeSpheres = Array.Empty<NativeSphere>();
    internal NativeCone[] NativeCones = Array.Empty<NativeCone>();
    internal NativeParametricSurface[] NativeParametricSurfaces = Array.Empty<NativeParametricSurface>();

    internal void SerializeNativeParabolaTable(MfcArchive ar, bool ownerIs3D)
    {
        Rows(ar, ref NativeParabolas, row => row.Serialize(ar, ownerIs3D));
        if (ar.IsLoading)
            foreach (var row in NativeParabolas)
            {
                row.Geometry.StartPoint = PointAt(row.Geometry.StartPointIndex);
                row.Geometry.EndPoint = PointAt(row.Geometry.EndPointIndex);
            }
    }

    // sgSketch 0x4C5BA790, entity tables 9..13 at native +E0..+100.
    // No class tags or signatures occur between these inline table counts.
    internal void SerializeNativePrimitiveTables(MfcArchive ar, bool ownerIs3D)
    {
        Rows(ar, ref NativePlanes, row => row.Serialize(ar, ownerIs3D));
        Rows(ar, ref NativeCylinders, row => row.Serialize(ar, ownerIs3D));
        Rows(ar, ref NativeSpheres, row => row.Serialize(ar, ownerIs3D));
        Rows(ar, ref NativeCones, row => row.Serialize(ar, ownerIs3D));
        Rows(ar, ref NativeParametricSurfaces, row => row.Serialize(ar, ownerIs3D));
    }

    private static void Rows<T>(MfcArchive ar, ref T[] rows, Action<T> serialize) where T : new()
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch entity table.");
        int count = rows.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) rows = new T[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) rows[i] = new T();
            serialize(rows[i]);
        }
    }

    internal sealed class NativeParabola
    {
        internal ushort KindFlags, StateF8;
        internal NativeLine Base = new NativeLine();
        internal SketchSegmentModel Geometry => Base.Geometry;
        internal int StateFC, State100, State104, State108;
        internal double StateE8, StateF0;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // Table 7, shared payload 0x4C5AF330. Normal sgParabola
            // constructor 0x4C491A30 installs vtable 0x4D1C77B8.
            ar.U16(ref KindFlags); Base.SerializeBase(ar, ownerIs3D);
            ar.I32(ref StateFC); ar.I32(ref State100);
            if (ar.DocumentVersion > 0x13EC) { ar.I32(ref State104); ar.I32(ref State108); }
            ar.F64(ref StateE8); ar.F64(ref StateF0); ar.U16(ref StateF8);
            Geometry.Kind = KindFlags == 0 ? SketchSegmentKind.Parabola : SketchSegmentKind.Unknown;
        }
    }

    internal sealed class NativePlane
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal DrawingPreviewPoint Origin, Axis1, Axis2, OptionalPoint;
        internal double State120, State128, State130, State138, State190;
        internal double[] Bounds = new double[4];
        internal byte State180, HasString, HasPoint;
        internal string? Name;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // sg3DPlane, ctor 0x4C41CF40, Serialize 0x4C5AD860.
            Entity.Serialize(ar, ownerIs3D); DrawingPreviewRecordModel.Point(ar, ref Origin);
            DrawingPreviewRecordModel.Point(ar, ref Axis1); DrawingPreviewRecordModel.Point(ar, ref Axis2);
            ar.F64(ref State120); ar.F64(ref State128); ar.F64(ref State130); ar.F64(ref State138);
            for (int i = 0; i < 4; i++) ar.F64(ref Bounds[i]);
            ar.Byte(ref State180); ar.Byte(ref HasString);
            if (HasString != 0) DrawingPreviewRecordModel.String(ar, ref Name);
            ar.F64(ref State190); ar.Byte(ref HasPoint);
            if (HasPoint != 0) DrawingPreviewRecordModel.Point(ar, ref OptionalPoint);
        }
    }

    internal sealed class NativeCylinder
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal DrawingPreviewPoint Direction, Position;
        internal double StateF0;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // sg3DCylinder, vtable 0x4D1B6598; inline payload in
            // sgSketch at 0x4C5C01FF..0x4C5C0369.
            Entity.Serialize(ar, ownerIs3D); DrawingPreviewRecordModel.Point(ar, ref Direction);
            DrawingPreviewRecordModel.Point(ar, ref Position); ar.F64(ref StateF0);
        }
    }

    internal sealed class NativeSphere
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal DrawingPreviewPoint Position;
        internal double StateD8;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // sg3DSphere, vtable 0x4D1B6738; inline payload at
            // 0x4C5C057F..0x4C5C0677.
            Entity.Serialize(ar, ownerIs3D); DrawingPreviewRecordModel.Point(ar, ref Position); ar.F64(ref StateD8);
        }
    }

    internal sealed class NativeCone
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal DrawingPreviewPoint Direction, Position;
        internal double StateF0, StateF8, State100;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // sg3DCone vtable 0x4D1B68D8, payload 0x4C5AD460.
            Entity.Serialize(ar, ownerIs3D); DrawingPreviewRecordModel.Point(ar, ref Direction);
            DrawingPreviewRecordModel.Point(ar, ref Position); ar.F64(ref StateF0); ar.F64(ref StateF8); ar.F64(ref State100);
        }
    }

    internal sealed class NativeParametricSurface
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        // sg3DParametricSurface, vtable 0x4CF8CED8, shared entity only.
        internal void Serialize(MfcArchive ar, bool ownerIs3D) => Entity.Serialize(ar, ownerIs3D);
    }
}
