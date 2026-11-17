using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// sldmfcu.dll gcBucketPoly_c restore 0x3CAB8820 / save 0x3CABB040.
// A polygon is inline data, not an MFC object, and its flags are not a type tag.
internal sealed class NativeDrawingBucketPolyModel : IMfcSerializable
{
    internal int SourceOffset;
    internal int ComponentId, EntityId;
    internal uint Flags;
    internal int PointCount;
    internal double[] Coordinates = Array.Empty<double>();
    internal byte HasLineVisual;
    internal string? LineStyle;
    internal NativeDrawingLineWidthModel LineWidth = new NativeDrawingLineWidthModel { RestoreType = 3 };
    internal int LineColor, LineState, ParameterType, State40, State44;
    internal double[] Parameters = Array.Empty<double>();
    internal int ComponentId2, EntityId2, State6C;
    internal DrawingPreviewPoint ArcStart, ArcEnd, ArcCenter;
    internal byte ArcState68, ArcState6C, ArcState74;
    internal int ArcState70, ArcState78;
    internal uint State8C, State90, State94;
    internal int StateA0;
    internal double ScalarA8, ScalarB0;
    // The containing gcBucket owns this mode, determining 2D/3D wire coordinates.
    internal bool TwoDimensional;
    internal int LegacyType, LegacyVisible, LegacyState, LegacyOverride;
    internal int[] PreBitfieldWords = new int[11];
    internal int PreBitfieldArcFlag, PreBitfieldTailWord;
    internal float[] LegacyCoordinates = Array.Empty<float>();
    internal bool HasArcData => (Flags & 0x20) != 0 || PreBitfieldArcFlag != 0;

    internal DrawingPreviewPoint[] GetPoints()
    {
        if (PointCount <= 0) return Array.Empty<DrawingPreviewPoint>();
        int dim = TwoDimensional ? 2 : 3;
        if (Coordinates != null && Coordinates.Length >= PointCount * dim)
        {
            var pts = new DrawingPreviewPoint[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                double x = Coordinates[i * dim];
                double y = Coordinates[i * dim + 1];
                double z = dim == 3 ? Coordinates[i * dim + 2] : 0.0;
                pts[i] = new DrawingPreviewPoint(x, y, z);
            }
            return pts;
        }
        if (LegacyCoordinates != null && LegacyCoordinates.Length >= PointCount * dim)
        {
            var pts = new DrawingPreviewPoint[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                double x = LegacyCoordinates[i * dim];
                double y = LegacyCoordinates[i * dim + 1];
                double z = dim == 3 ? LegacyCoordinates[i * dim + 2] : 0.0;
                pts[i] = new DrawingPreviewPoint(x, y, z);
            }
            return pts;
        }
        return Array.Empty<DrawingPreviewPoint>();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsLoading) SourceOffset = ar.Position;
        if (ar.DocumentVersion == 5000) { SerializeLegacy(ar); return; }
        ar.I32(ref ComponentId); ar.I32(ref EntityId);
        ar.U32(ref Flags); ar.I32(ref PointCount);
        if (PointCount < 0) throw new InvalidDataException("Negative native polygon point count.");
        Values(ar, ref Coordinates, checked(PointCount * (TwoDimensional ? 2 : 3)));
        if (PointCount == 0) return;
        ar.Byte(ref HasLineVisual);
        if (HasLineVisual != 0)
        {
            DrawingPreviewRecordModel.String(ar, ref LineStyle); LineWidth.Serialize(ar);
            ar.I32(ref LineColor); ar.I32(ref LineState);
        }
        ar.I32(ref ParameterType);
        if (ParameterType == 1 || ParameterType == 2 || ParameterType == 3)
        {
            int count = Parameters.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            Values(ar, ref Parameters, count);
        }
        if (ar.DocumentVersion < 0x41A1)
        {
            for (int i = 0; i < PreBitfieldWords.Length; i++) ar.I32(ref PreBitfieldWords[i]);
            ar.I32(ref PreBitfieldArcFlag);
            if (PreBitfieldArcFlag != 0)
            {
                DrawingPreviewRecordModel.Point(ar, ref ArcStart);
                DrawingPreviewRecordModel.Point(ar, ref ArcEnd);
                DrawingPreviewRecordModel.Point(ar, ref ArcCenter);
                ar.Byte(ref ArcState68);
                if (ar.DocumentVersion >= 0x3724) ar.Byte(ref ArcState6C);
                ar.I32(ref ArcState70); ar.Byte(ref ArcState74); ar.I32(ref ArcState78);
            }
            ar.I32(ref PreBitfieldTailWord);
        }
        else
        {
            ar.I32(ref State40); ar.I32(ref State44);
            ar.I32(ref ComponentId2); ar.I32(ref EntityId2); ar.I32(ref State6C);
            if ((Flags & 0x20) != 0)
            {
                DrawingPreviewRecordModel.Point(ar, ref ArcStart);
                DrawingPreviewRecordModel.Point(ar, ref ArcEnd);
                DrawingPreviewRecordModel.Point(ar, ref ArcCenter);
                ar.Byte(ref ArcState68);
                if (ar.DocumentVersion >= 0x3724) ar.Byte(ref ArcState6C);
                ar.I32(ref ArcState70); ar.Byte(ref ArcState74); ar.I32(ref ArcState78);
            }
        }
        ar.U32(ref State8C); ar.U32(ref State90); ar.U32(ref State94);
        ar.I32(ref StateA0); ar.F64(ref ScalarA8); ar.F64(ref ScalarB0);
    }

    // Version 5000 stores float coordinates and separate state words, not
    // the later polygon bitfield. Do not normalize away those legacy values.
    private void SerializeLegacy(MfcArchive ar)
    {
        ar.I32(ref ComponentId); ar.I32(ref EntityId); ar.I32(ref LegacyType); ar.I32(ref PointCount);
        int values = checked(PointCount * (TwoDimensional ? 2 : 3));
        if (values < 0 || ar.IsLoading && values > ar.Remaining / 4) throw new InvalidDataException("Invalid legacy polygon count.");
        if (ar.IsLoading) LegacyCoordinates = new float[values];
        for (int i = 0; i < values; i++) ar.F32(ref LegacyCoordinates[i]);
        if (PointCount == 0) return;
        ar.Byte(ref HasLineVisual);
        if (HasLineVisual != 0)
        {
            DrawingPreviewRecordModel.String(ar, ref LineStyle); LineWidth.Serialize(ar);
            ar.I32(ref LineColor); ar.I32(ref LineState);
        }
        ar.I32(ref ParameterType);
        if (ParameterType == 1 || ParameterType == 2 || ParameterType == 3)
        {
            int count = Parameters.Length; NativeDrawingDisplayArchive.Count(ar, ref count); Values(ar, ref Parameters, count);
        }
        ar.I32(ref LegacyVisible); ar.I32(ref State40); ar.I32(ref LegacyState);
        ar.I32(ref State44); ar.I32(ref LegacyOverride);
    }

    internal static void Values(MfcArchive ar, ref double[] values, int count)
    {
        if (ar.IsLoading)
        {
            if (count < 0 || count > ar.Remaining / 8) throw new InvalidDataException("Invalid native double-array count.");
            values = new double[count];
        }
        if (values.Length != count) throw new InvalidDataException("Native coordinate count does not match polygon dimension.");
        for (int i = 0; i < values.Length; i++) ar.F64(ref values[i]);
    }
}
