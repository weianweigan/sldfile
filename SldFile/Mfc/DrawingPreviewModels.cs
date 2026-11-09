using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Drawing;

namespace SldFile.Mfc;

// DisplayLists is a command archive, not a sequence of model-feature headers.
// Evidence: D8001507933 and the 2012/2026 cube drawings. uiLayerMgr_c and
// uiViewBucket_c are verified in tools/output/mfc_classes.json (slddrwu.dll).
// Keep unverified fields and commands verbatim; never fabricate MFC class tags.
internal sealed class DrawingPreviewRecordModel : IMfcSerializable
{
    internal uint DeclaredSize, CommandType;
    internal uint Color;
    internal ushort Reserved16;
    internal float Width;
    internal int Layer, Reserved32;
    internal string? Value;
    internal DrawingPreviewPoint[] Points = Array.Empty<DrawingPreviewPoint>();
    internal DrawingPreviewPoint Normal;
    internal byte Flag;
    internal uint ArcFlags, TriangleFlags;
    internal double Height, Angle, FontMetric, WidthFactor, FontSpacing;
    internal uint FontFlags, FontReserved;
    internal uint FontPointSize, FontAttributes, TextAlignment, TextReserved;
    internal ushort TextFlags;
    internal float[] CharacterWidths = Array.Empty<float>();
    internal double TextSpacing, TextScale;
    internal string Id = string.Empty;
    internal uint[] GroupFields = new uint[9];
    internal string?[] GroupStrings = new string?[4];
    internal byte[] ContextPayload = new byte[34];
    internal ulong PointFlags;

    internal static bool IsHeader(uint size, uint type) =>
        (type == 12 && size >= 58 && size <= 131130) ||
        (type == 4 && size == 80) || (type == 2 && size == 128) ||
        (type == 6 && size >= 120 && size <= 2400048) ||
        (type == 10 && size >= 98 && size <= 131170) ||
        (type == 18 && size >= 114 && size <= 10000146) ||
        (type == 30 && size == 56) || (type == 9 && (size == 60 || size == 56)) ||
        (type == 1 && size == 56) || (type == 29 && size >= 66 && size <= 524346);

    public void Serialize(MfcArchive ar)
    {
        ar.U32(ref DeclaredSize);
        ar.U32(ref CommandType);
        if (!IsHeader(DeclaredSize, CommandType)) throw new InvalidDataException("Unsupported display command.");
        switch (CommandType)
        {
            case 30:
                var direction = Normal; Point(ar, ref direction); Normal = direction; ar.U32(ref ArcFlags);
                break;
            case 9:
                int contextLength = DeclaredSize == 56 ? 30 : 34;
                if (ar.IsLoading)
                {
                    ContextPayload = ar.ReadBytes(contextLength);
                    if (ContextPayload.Length != contextLength) throw new EndOfStreamException();
                }
                else ar.WriteBytes(ContextPayload);
                break;
            case 1:
                PointArray(ar, 1); ar.U64(ref PointFlags);
                break;
            case 29:
                for (int i = 0; i < GroupFields.Length; i++) ar.U32(ref GroupFields[i]);
                for (int i = 0; i < GroupStrings.Length; i++) String(ar, ref GroupStrings[i]);
                break;
            case 12:
                ar.U32(ref Color); ar.U16(ref Reserved16); ar.F32(ref Width);
                ar.I32(ref Layer); ar.I32(ref Reserved32); String(ar, ref Value);
                break;
            case 4:
                PointArray(ar, 2); ar.Byte(ref Flag);
                break;
            case 2:
                PointArray(ar, 3); var normal = Normal; Point(ar, ref normal); Normal = normal;
                ar.U32(ref ArcFlags);
                break;
            case 6:
                ar.U32(ref TriangleFlags);
                uint count = (uint)Points.Length; ar.U32(ref count);
                if (count < 3 || count > 100000 || (ar.IsLoading && count > ar.Remaining / 24))
                    throw new InvalidDataException("Invalid polygon point count.");
                PointArray(ar, (int)count);
                break;
            case 10:
                ar.F64(ref Height); ar.U32(ref FontPointSize); ar.U32(ref FontAttributes); ar.U32(ref FontReserved);
                ar.F64(ref FontMetric); ar.U32(ref FontFlags); String(ar, ref Value);
                ar.F64(ref WidthFactor); ar.F64(ref FontSpacing);
                break;
            case 18:
                PointArray(ar, 1); ar.U32(ref TextAlignment); ar.F64(ref Angle); ar.U32(ref TextReserved);
                String(ar, ref Value); ar.U16(ref TextFlags);
                uint widths = (uint)CharacterWidths.Length; ar.U32(ref widths);
                if (widths > 1000000 || (ar.IsLoading && widths > ar.Remaining / 4))
                    throw new InvalidDataException("Invalid character width count.");
                if (ar.IsLoading) CharacterWidths = new float[widths];
                for (int i = 0; i < CharacterWidths.Length; i++) ar.F32(ref CharacterWidths[i]);
                // The 2012 layout has no trailing reserved word / scale; the newer
                // native command advertises 32 additional in-memory bytes in its declaration.
                if (DeclaredSize == 146 + 10 * (Value?.Length ?? 0))
                {
                    ar.U32(ref FontReserved); ar.F64(ref TextScale);
                }
                break;
        }
        Validate();
    }

    private void Validate()
    {
        uint expectedSize = CommandType == 12 ? checked((uint)(58 + 2 * (Value?.Length ?? 0))) :
            CommandType == 10 ? checked((uint)(98 + 2 * (Value?.Length ?? 0))) :
            CommandType == 18 ? checked((uint)((DeclaredSize == 114 + 10 * (Value?.Length ?? 0) ? 114 : 146) + 10 * (Value?.Length ?? 0))) :
            CommandType == 6 ? checked((uint)(48 + 24 * Points.Length)) : DeclaredSize;
        if (DeclaredSize != expectedSize) throw new InvalidDataException("Unverified display command layout.");
        foreach (var p in Points)
            if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw new InvalidDataException("Nonfinite preview point.");
        if (CommandType == 2 && (!Finite(Normal.X) || !Finite(Normal.Y) || !Finite(Normal.Z) ||
            Math.Abs(Normal.X * Normal.X + Normal.Y * Normal.Y + Normal.Z * Normal.Z - 1) > 1e-8))
            throw new InvalidDataException("Invalid arc normal.");
        if (CommandType == 18 && (CharacterWidths.Length != (Value?.Length ?? 0) || CharacterWidths.Any(w => !Finite(w) || w < 0)))
            throw new InvalidDataException("Invalid native glyph advances.");
        if (!Finite(Height) || !Finite(Angle) || !Finite(WidthFactor)) throw new InvalidDataException("Nonfinite text geometry.");
        if ((CommandType == 10) && ((Height <= 0 && Height != -1) || WidthFactor <= 0))
            throw new InvalidDataException("Invalid font metrics.");
    }

    private void PointArray(MfcArchive ar, int count)
    {
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 24) throw new EndOfStreamException();
            Points = new DrawingPreviewPoint[count];
        }
        if (Points.Length != count) throw new InvalidDataException("Incorrect preview point count.");
        for (int i = 0; i < count; i++) { var p = Points[i]; Point(ar, ref p); Points[i] = p; }
    }

    internal static void Point(MfcArchive ar, ref DrawingPreviewPoint p)
    {
        double x = p.X, y = p.Y, z = p.Z;
        ar.F64(ref x); ar.F64(ref y); ar.F64(ref z);
        p = new DrawingPreviewPoint(x, y, z);
    }

    internal static void String(MfcArchive ar, ref string? value)
    {
        // MfcArchive.WChar is tolerant for legacy callers; this wire codec is strict.
        if (ar.IsLoading)
        {
            int start = ar.Position;
            byte[] prefix = ar.ReadBytes(4);
            if (prefix.Length != 4 || prefix[0] != 255 || prefix[1] != 254 || prefix[2] != 255)
                throw new InvalidDataException("Missing CString marker.");
            long count = prefix[3];
            if (count == 255) { ushort n = 0; ar.U16(ref n); count = n; if (n == 65535) { uint n32 = 0; ar.U32(ref n32); count = n32; } }
            if (count > ar.Remaining / 2) throw new EndOfStreamException();
            ar.Position = start;
        }
        ar.WChar(ref value);
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}

internal sealed class DrawingPreviewStreamModel : IMfcSerializable
{
    internal sealed class Segment
    {
        internal byte[] Raw = Array.Empty<byte>();
        internal DrawingPreviewRecordModel? Record;
    }
    internal readonly List<Segment> Segments = new List<Segment>();
    internal bool DecodeCommands;
    internal int Length;

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            foreach (var segment in Segments)
                if (segment.Record != null) segment.Record.Serialize(ar);
                else ar.WriteBytes(segment.Raw);
            return;
        }
        Segments.Clear(); Length = ar.Remaining;
        var bytes = ar.ReadRemainingBytes();
        int offset = 0, rawStart = 0;
        while (DecodeCommands && offset <= bytes.Length - 8)
        {
            uint op = BitConverter.ToUInt32(bytes, offset), signature = BitConverter.ToUInt32(bytes, offset + 4);
            if (DrawingPreviewRecordModel.IsHeader(op, signature))
            {
                try
                {
                    using var recordArchive = MfcArchive.CreateReader(bytes, offset);
                    var record = new DrawingPreviewRecordModel { Id = offset.ToString("X8") };
                    record.Serialize(recordArchive);
                    int consumed = recordArchive.Position;
                    // A serializable record is accepted only if its canonical form preserves every byte.
                    // Noncanonical strings and unverified variants remain opaque instead of being normalized.
                    byte[] roundtrip = MfcArchive.Serialize(record);
                    if (consumed != roundtrip.Length || !bytes.AsSpan(offset, consumed).SequenceEqual(roundtrip))
                        throw new InvalidDataException("Unverified display command variant.");
                    if (offset > rawStart) AddRaw(bytes, rawStart, offset - rawStart);
                    Segments.Add(new Segment { Record = record });
                    offset += consumed; rawStart = offset;
                    continue;
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is OverflowException) { }
            }
            offset++;
        }
        if (rawStart < bytes.Length) AddRaw(bytes, rawStart, bytes.Length - rawStart);
    }

    private void AddRaw(byte[] bytes, int offset, int length)
    {
        var raw = new byte[length]; Array.Copy(bytes, offset, raw, 0, length);
        Segments.Add(new Segment { Raw = raw });
    }

    internal DrawingPreviewDefinition ToDefinition(int sheetIndex, int sheetCount)
    {
        var result = new DrawingPreviewDefinition { SheetIndex = sheetIndex,
            PreservedByteCount = Segments.Sum(s => s.Raw.Length),
            DecodedByteCount = Segments.Where(s => s.Record != null).Sum(s => MfcArchive.Serialize(s.Record!).Length) };
        // The observed native drawing streams contain document-wide display commands.
        // Page ownership must be established before any of these commands is shown on a multi-sheet document.
        if (sheetIndex != 0 || sheetCount != 1)
        {
            result.ResolutionMessage = "Native display commands have unresolved sheet ownership.";
            return result;
        }
        var primitives = new List<DrawingPreviewPrimitive>();
        DrawingPreviewRecordModel? style = null, font = null;
        foreach (var segment in Segments)
        {
            var r = segment.Record;
            if (r == null) continue;
            if (r.CommandType == 12) { style = r; continue; }
            if (r.CommandType == 10) { font = r; continue; }
            if (r.CommandType == 1 || r.CommandType == 9 || r.CommandType == 29 || r.CommandType == 30) continue;
            if (style == null) continue;
            if (r.CommandType == 18 && font == null) continue;
            var p = new DrawingPreviewPrimitive
            {
                Id = r.Id, Points = r.Points.ToArray(), Normal = r.Normal,
                ColorRgb = ((style.Color & 255) << 16) | (style.Color & 0xFF00) | ((style.Color >> 16) & 255),
                LayerId = style.Layer, LineStyle = style.Value ?? string.Empty,
                LineWidth = style.Width > 0 ? style.Width : 0.00025,
                Kind = r.CommandType == 4 ? DrawingPreviewPrimitiveKind.Line : r.CommandType == 2 ? DrawingPreviewPrimitiveKind.Arc
                    : r.CommandType == 6 ? DrawingPreviewPrimitiveKind.Triangle : DrawingPreviewPrimitiveKind.Text,
                Text = r.CommandType == 18 ? r.Value ?? string.Empty : string.Empty,
                FontName = font?.Value ?? "Arial", TextHeight = font?.Height > 0 ? font.Height : 0.0025,
                Angle = r.CommandType == 18 ? r.Angle : 0, WidthFactor = font?.WidthFactor ?? 1
            };
            if (font?.Height == -1 && font.FontPointSize > 0)
                p.TextHeight = font.FontPointSize * 0.0254 / 72;
            p.IsBold = ((font?.FontFlags ?? 0) & 8) != 0;
            if (r.CommandType == 18) p.CharacterAdvances = r.CharacterWidths.Select(w => (double)w).ToArray();
            primitives.Add(p);
        }
        result.Primitives = primitives.AsReadOnly();
        result.IsResolved = result.PreservedByteCount == 0;
        result.ResolutionMessage = result.IsResolved ? string.Empty : "Unverified native commands are preserved; raster preview remains available.";
        return result;
    }

    internal void FromDefinition(DrawingPreviewDefinition definition)
    {
        if (definition.SheetIndex != 0) throw new NotSupportedException("Unresolved native sheet ownership.");
        var originals = ToDefinition(0, 1).Primitives.ToDictionary(p => p.Id);
        if (definition.Primitives.Count != originals.Count || definition.Primitives.Select(p => p.Id).Distinct().Count() != originals.Count)
            throw new NotSupportedException("Adding or removing native display commands is not yet verified.");
        var records = Segments.Where(s => s.Record != null).ToDictionary(s => s.Record!.Id, s => s.Record!);
        foreach (var p in definition.Primitives)
        {
            if (!originals.TryGetValue(p.Id, out var original) || p.Kind != original.Kind)
                throw new ArgumentException("Preview record identity or kind changed.", nameof(definition));
            // State records may be shared by many primitives. Reject unsupported state edits rather than changing unrelated entities.
            if (p.ColorRgb != original.ColorRgb || p.LineStyle != original.LineStyle || p.LayerId != original.LayerId ||
                p.LineWidth != original.LineWidth || p.FontName != original.FontName || p.TextHeight != original.TextHeight ||
                p.WidthFactor != original.WidthFactor || p.IsBold != original.IsBold || !p.CharacterAdvances.SequenceEqual(original.CharacterAdvances))
                throw new NotSupportedException("Editing shared native style/font state is not yet verified.");
            var r = records[p.Id]; r.Points = p.Points.ToArray(); r.Normal = p.Normal;
            if (p.Kind == DrawingPreviewPrimitiveKind.Text)
            {
                if (p.Text.Length != original.Text.Length) throw new NotSupportedException("Text edits must preserve native glyph count.");
                r.Value = p.Text; r.Angle = p.Angle;
            }
            // Validate before replacing the live document's stream.
            MfcArchive.Serialize(r);
        }
    }
}
