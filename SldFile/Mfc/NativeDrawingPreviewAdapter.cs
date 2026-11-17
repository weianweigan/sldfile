using System;
using System.Linq;

namespace SldFile.Mfc;

// Presentation adapter only. NativeDrawingDisplayItemModel owns the wire layout.
internal static class NativeDrawingPreviewAdapter
{
    internal static DrawingPreviewRecordModel? Read(
        NativeDrawingDisplayItemModel item,
        bool includeTransformed = false,
        NativeDrawingTransformModel? parentTransform = null)
    {
        var points = item.Points.ToArray();
        var activeTransform = item.Transform ?? parentTransform;
        if (includeTransformed && activeTransform != null && points.Length > 0)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] = activeTransform.TransformPoint(points[i]);
        }

        var r = new DrawingPreviewRecordModel { Id = item.Offset.ToString("X8"), DeclaredSize = item.MemoryFootprint,
            CommandType = (uint)item.Type, Points = points };
        switch (item.Type)
        {
            case 9:
                // uoDisplayPen ctor 0x3CB972F0: COLORREF at +8,
                // layer at +4, native LineStyles at +0xC. Its enum is retained
                // in the wire model; it has no verified named style mapping yet.
                r.Color = (uint)item.Integers[2]; r.Layer = item.Integers[1];
                r.Width = item.LineWidth.Width; break;
            case 2:
            case 101:
                r.CommandType = 2;
                r.Points = points.Take(3).ToArray(); r.Normal = item.Points[3]; r.ArcFlags = (uint)item.Integers[0]; break;
            case 4: r.Flag = item.Bytes[0]; break;
            case 5:
                if ((!includeTransformed && activeTransform != null) || points.Length < 2) return null;
                r.CommandType = 4; break;
            case 100: r.CommandType = 4; break;
            case 6: r.TriangleFlags = (uint)item.Integers[0]; break;
            case 10:
                r.Height = item.Scalars[0]; r.FontPointSize = (uint)item.Integers[0];
                long attributes = BitConverter.DoubleToInt64Bits(item.Scalars[1]);
                r.FontAttributes = (uint)attributes; r.FontReserved = (uint)(attributes >> 32);
                r.FontMetric = item.Scalars[2]; r.FontFlags = (uint)item.Integers[1];
                r.Value = item.Strings[0]; r.WidthFactor = item.Scalars[3]; r.FontSpacing = item.Scalars[4]; break;
            case 12:
                r.Color = (uint)item.Integers[0]; r.Reserved16 = (ushort)item.LineWidth.Type; r.Width = item.LineWidth.Width;
                r.Layer = item.Integers[1]; r.Reserved32 = item.Integers[2]; r.Value = item.Strings[0]; break;
            case 7:
            case 15:
                if (!includeTransformed || item.Strings.Count == 0 || points.Length == 0) return null;
                r.CommandType = 18;
                r.Value = item.Strings[0];
                r.Angle = activeTransform != null ? activeTransform.GetRotationAngle() : 0.0;
                break;
            case 18:
                // Transform-bearing text needs a domain transform mapping; retain it
                // structurally unless transformed extraction is enabled.
                if (item.Transform != null && !includeTransformed) return null;
                r.Angle = item.Scalars[0] + (includeTransformed && activeTransform != null ? activeTransform.GetRotationAngle() : 0.0);
                r.TextReserved = (uint)item.Integers[1]; r.Value = item.Strings[0];
                r.TextFlags = item.Shorts[0]; r.CharacterWidths = item.Floats.ToArray(); break;
            default: return null;
        }
        return r;
    }

    internal static void WriteGeometry(DrawingPreviewRecordModel r, NativeDrawingDisplayItemModel item)
    {
        if (item.Type != 2 && item.Type != 4 && item.Type != 5 && item.Type != 6 && item.Type != 18 && item.Type != 100 && item.Type != 101) return;
        item.Points.Clear(); item.Points.AddRange(r.Points);
        if (item.Type == 2 || item.Type == 101) item.Points.Add(r.Normal);
        if (item.Type == 18) { item.Strings[0] = r.Value; item.Scalars[0] = r.Angle; }
    }
}
