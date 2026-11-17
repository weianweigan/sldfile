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
        if (!TrySerialize(ar)) throw new InvalidDataException("Invalid or unsupported display command.");
    }

    internal bool TrySerialize(MfcArchive ar)
    {
        if (ar.IsLoading && ar.Remaining < 8) return false;
        ar.U32(ref DeclaredSize);
        ar.U32(ref CommandType);
        if (!IsHeader(DeclaredSize, CommandType)) return false;
        int minimum = CommandType switch { 30 => 28, 9 => DeclaredSize == 56 ? 30 : 34,
            1 => 32, 29 => 36, 12 => 18, 4 => 49, 2 => 100, 6 => 8, 10 => 32, 18 => 40, _ => 0 };
        if (ar.IsLoading && ar.Remaining < minimum) return false;
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
                for (int i = 0; i < GroupStrings.Length; i++)
                    if (!TryString(ar, ref GroupStrings[i])) return false;
                break;
            case 12:
                ar.U32(ref Color); ar.U16(ref Reserved16); ar.F32(ref Width);
                ar.I32(ref Layer); ar.I32(ref Reserved32);
                if (!TryString(ar, ref Value)) return false;
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
                    return false;
                PointArray(ar, (int)count);
                break;
            case 10:
                ar.F64(ref Height); ar.U32(ref FontPointSize); ar.U32(ref FontAttributes); ar.U32(ref FontReserved);
                ar.F64(ref FontMetric); ar.U32(ref FontFlags);
                if (!TryString(ar, ref Value) || ar.IsLoading && ar.Remaining < 16) return false;
                ar.F64(ref WidthFactor); ar.F64(ref FontSpacing);
                break;
            case 18:
                PointArray(ar, 1); ar.U32(ref TextAlignment); ar.F64(ref Angle); ar.U32(ref TextReserved);
                if (!TryString(ar, ref Value) || ar.IsLoading && ar.Remaining < 6) return false;
                ar.U16(ref TextFlags);
                uint widths = (uint)CharacterWidths.Length; ar.U32(ref widths);
                if (widths > 1000000 || (ar.IsLoading && widths > ar.Remaining / 4))
                    return false;
                if (ar.IsLoading) CharacterWidths = new float[widths];
                for (int i = 0; i < CharacterWidths.Length; i++) ar.F32(ref CharacterWidths[i]);
                // The 2012 layout has no trailing reserved word / scale; the newer
                // native command advertises 32 additional in-memory bytes in its declaration.
                if (DeclaredSize == 146 + 10 * (Value?.Length ?? 0))
                {
                    if (ar.IsLoading && ar.Remaining < 12) return false;
                    ar.U32(ref FontReserved); ar.F64(ref TextScale);
                }
                break;
        }
        return Valid();
    }

    private bool Valid()
    {
        uint expectedSize = CommandType == 12 ? checked((uint)(58 + 2 * (Value?.Length ?? 0))) :
            CommandType == 10 ? checked((uint)(98 + 2 * (Value?.Length ?? 0))) :
            CommandType == 18 ? checked((uint)((DeclaredSize == 114 + 10 * (Value?.Length ?? 0) ? 114 : 146) + 10 * (Value?.Length ?? 0))) :
            CommandType == 6 ? checked((uint)(48 + 24 * Points.Length)) : DeclaredSize;
        if (DeclaredSize != expectedSize) return false;
        foreach (var p in Points)
            if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) return false;
        if (CommandType == 2 && (!Finite(Normal.X) || !Finite(Normal.Y) || !Finite(Normal.Z) ||
            Math.Abs(Normal.X * Normal.X + Normal.Y * Normal.Y + Normal.Z * Normal.Z - 1) > 1e-8))
            return false;
        if (CommandType == 18 && (CharacterWidths.Length != (Value?.Length ?? 0) || CharacterWidths.Any(w => !Finite(w) || w < 0)))
            return false;
        if (!Finite(Height) || !Finite(Angle) || !Finite(WidthFactor)) return false;
        if ((CommandType == 10) && ((Height <= 0 && Height != -1) || WidthFactor <= 0))
            return false;
        return true;
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
        if (!TryString(ar, ref value)) throw new InvalidDataException("Invalid preview CString.");
    }

    internal static bool TryString(MfcArchive ar, ref string? value)
    {
        // MfcArchive.WChar is tolerant for legacy callers; this wire codec is strict.
        if (ar.IsLoading)
        {
            int start = ar.Position;
            if (ar.Remaining < 4) return false;
            byte[] prefix = ar.ReadBytes(4);
            if (prefix.Length != 4 || prefix[0] != 255 || prefix[1] != 254 || prefix[2] != 255)
                return false;
            long count = prefix[3];
            if (count == 255)
            {
                if (ar.Remaining < 2) return false;
                ushort n = 0; ar.U16(ref n); count = n;
                if (n == 65535)
                {
                    if (ar.Remaining < 4) return false;
                    uint n32 = 0; ar.U32(ref n32); count = n32;
                }
            }
            if (count > ar.Remaining / 2) return false;
            ar.Position = start;
        }
        ar.WChar(ref value);
        return true;
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}

internal sealed class DrawingPreviewStreamModel : IMfcSerializable
{
    internal sealed class Segment
    {
        internal byte[] Raw = Array.Empty<byte>();
        internal DrawingPreviewRecordModel? Record;
        internal NativeDrawingDisplayItemModel? NativeRecord;
        internal int Scope;
        internal bool IsSheetFormat;
    }
    internal readonly List<Segment> Segments = new List<Segment>();
    internal bool DecodeCommands;
    internal bool IncludeTransformedItems;
    internal int Length;
    private NativeDrawingDisplayArchive? _native;
    private byte[] _suffix = Array.Empty<byte>();
    private int _decodedLength;
    internal string? DecodeFailure;
    internal int FormatBucketCount => _native?.Formats.Length ?? 0;
    internal NativeDrawingDisplayArchive? NativeArchive => _native;

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (_native != null)
            {
                foreach (var segment in Segments)
                    if (segment.Record != null && segment.NativeRecord != null)
                        NativeDrawingPreviewAdapter.WriteGeometry(segment.Record, segment.NativeRecord);
                _native.Serialize(ar); ar.WriteBytes(_suffix);
                return;
            }
            foreach (var segment in Segments)
                if (segment.Record != null) segment.Record.Serialize(ar);
                else ar.WriteBytes(segment.Raw);
            return;
        }
        Segments.Clear(); Length = ar.Remaining; _native = null; _decodedLength = 0; DecodeFailure = null;
        var bytes = ar.ReadRemainingBytes();
        if (!DecodeCommands || bytes.Length == 0) { AddRaw(bytes, 0, bytes.Length); return; }
        // The global _MO_VERSION value selects the actual native serializers.
        // Never scan for plausible command headers or infer a version from size.
        if (ar.DocumentVersion < 5000)
        {
            DecodeFailure = "A verified native drawing document version is required.";
            AddRaw(bytes, 0, bytes.Length); return;
        }
        try
        {
            using var input = MfcArchive.CreateReader(bytes);
            input.DocumentVersion = ar.DocumentVersion;
            var native = new NativeDrawingDisplayArchive(); native.Serialize(input);
            _decodedLength = input.Position;
            _suffix = input.ReadRemainingBytes();
            _native = native;
            foreach (var format in native.Formats) AddItems(format.Items, isSheetFormat: true);
            foreach (var view in native.Views) { AddItems(view.Items); AddItems(view.DetailItems); }
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is OverflowException || ex is NotSupportedException)
        {
            DecodeFailure = ex.Message; _native = null; _decodedLength = 0;
            Segments.Clear(); AddRaw(bytes, 0, bytes.Length);
        }
    }

    private void AddItems(NativeDrawingDisplayItemModel[] items, NativeDrawingTransformModel? parentTransform = null, bool isSheetFormat = false)
    {
        // Each list is a separate display state scope; don't inherit style/font
        // from the preceding format or view bucket.
        Segments.Add(new Segment());
        foreach (var item in items)
            AddItem(item, string.Empty, parentTransform, isSheetFormat);
    }

    private void AddItem(NativeDrawingDisplayItemModel item, string owner, NativeDrawingTransformModel? parentTransform = null, bool isSheetFormat = false)
    {
        if (item.Type == 19)
        {
            // uoDisplaySimpleSymbol ctor -> 0x3CB9E530 clones the supplied
            // display recording using 0x3CB9EE50, preserving its coordinates.
            // The two-dimensional cache already contains placed geometry; its
            // origin/angle/height must not be applied again unless IncludeTransformedItems is enabled.
            if (item.Transform != null && !IncludeTransformedItems)
            {
                Segments.Add(new Segment { NativeRecord = item, IsSheetFormat = isSheetFormat });
                return;
            }
            Segments.Add(new Segment { Scope = 1 });
            string path = owner + "SYM:" + item.Offset.ToString("X8") + "/";
            var childTransform = item.Transform ?? parentTransform;
            foreach (var child in item.Children) AddItem(child, path, childTransform, isSheetFormat);
            Segments.Add(new Segment { Scope = 2 });
            return;
        }
        var record = NativeDrawingPreviewAdapter.Read(item, IncludeTransformedItems, parentTransform);
        if (record == null)
        {
            if (item.Type != 0) Segments.Add(new Segment { NativeRecord = item, IsSheetFormat = isSheetFormat });
            return;
        }
        record.Id = owner + record.Id;
        Segments.Add(new Segment { Record = record, NativeRecord = item, IsSheetFormat = isSheetFormat });
    }

    private void AddRaw(byte[] bytes, int offset, int length)
    {
        var raw = new byte[length]; Array.Copy(bytes, offset, raw, 0, length);
        Segments.Add(new Segment { Raw = raw });
    }

    internal DrawingPreviewDefinition ToDefinition(int sheetIndex, int sheetCount)
    {
        var result = new DrawingPreviewDefinition { SheetIndex = sheetIndex,
            IsResolved = true,
            PreservedByteCount = _native == null ? Length : _suffix.Length,
            DecodedByteCount = _decodedLength };
        if (_native != null)
            result.NativeLineStyles = Array.AsReadOnly(_native.LineStyles.Select(n => n?.Payload)
                .OfType<NativeDrawingLineStyleModel>().Select(s => new DrawingPreviewLineStyle(s.Name ?? string.Empty,
                    Array.AsReadOnly(s.Segments.ToArray()), s.Flags)).ToArray());
        if (DecodeFailure != null) result.AddIssue("DisplayDecode", DecodeFailure, "Contents/DisplayLists");
        else if (_native == null && Length > 0) result.AddIssue("OpaqueDisplay", "Display commands were not decoded.", "Contents/DisplayLists");
        if (_suffix.Length > 0) result.AddIssue("DisplaySuffix", $"{_suffix.Length} trailing bytes are preserved without a presentation mapping.", "Contents/DisplayLists");
        // The observed native drawing streams contain document-wide display commands.
        // Page ownership must be established before any of these commands is shown on a multi-sheet document.
        if (sheetIndex < 0 || sheetIndex >= Math.Max(1, sheetCount) ||
            (sheetCount > 1 && (_native == null || _native.Formats.Length != sheetCount)))
        {
            result.AddIssue("SheetOwnership", "Native display commands have unresolved sheet ownership.", "Contents/DisplayLists");
            return result;
        }
        IEnumerable<Segment> sourceSegments = Segments;
        if (sheetCount > 1 && _native != null && sheetIndex < _native.Formats.Length)
        {
            var sheetSegments = new List<Segment>();
            void AppendSheetItems(IEnumerable<NativeDrawingDisplayItemModel> list, string owner, NativeDrawingTransformModel? tf)
            {
                foreach (var item in list)
                {
                    if (item.Type == 19)
                    {
                        if (item.Transform != null && !IncludeTransformedItems)
                        {
                            sheetSegments.Add(new Segment { NativeRecord = item });
                            continue;
                        }
                        sheetSegments.Add(new Segment { Scope = 1 });
                        string path = owner + "SYM:" + item.Offset.ToString("X8") + "/";
                        AppendSheetItems(item.Children, path, item.Transform ?? tf);
                        sheetSegments.Add(new Segment { Scope = 2 });
                        continue;
                    }
                    var rec = NativeDrawingPreviewAdapter.Read(item, IncludeTransformedItems, tf);
                    if (rec == null)
                    {
                        if (item.Type != 0) sheetSegments.Add(new Segment { NativeRecord = item });
                        continue;
                    }
                    rec.Id = owner + rec.Id;
                    sheetSegments.Add(new Segment { Record = rec, NativeRecord = item, IsSheetFormat = true });
                }
            }
            sheetSegments.Add(new Segment());
            AppendSheetItems(_native.Formats[sheetIndex].Items, string.Empty, null);
            sourceSegments = sheetSegments;
            if (_native.Views.Length > 0) result.AddIssue("ViewSheetOwnership", "View-to-sheet ownership is not mapped.", "Contents/DisplayLists");
        }
        var primitives = new List<DrawingPreviewPrimitive>();
        DrawingPreviewRecordModel? style = null, font = null;
        var nativeStyles = Segments.Where(s => s.Record != null && s.NativeRecord?.Type == 9)
            .GroupBy(s => s.Record!.Id).ToDictionary(g => g.Key, g => g.First().NativeRecord!);
        var scopes = new Stack<(DrawingPreviewRecordModel? Style, DrawingPreviewRecordModel? Font)>();
        foreach (var segment in sourceSegments)
        {
            if (segment.Scope == 1) { scopes.Push((style, font)); continue; }
            if (segment.Scope == 2) { (style, font) = scopes.Pop(); continue; }
            var r = segment.Record;
            if (r == null)
            {
                if (segment.NativeRecord is { } skipped)
                    result.AddIssue("DisplayCommand", $"Command {skipped.Type} has no verified presentation mapping.",
                        "Contents/DisplayLists", skipped.Type, skipped.Offset);
                else { style = null; font = null; }
                continue;
            }
            if (r.CommandType == 12 || r.CommandType == 9) { style = r; continue; }
            if (r.CommandType == 10) { font = r; continue; }
            if (r.CommandType == 1 || r.CommandType == 9 || r.CommandType == 29 || r.CommandType == 30) continue;
            if (style == null || (r.CommandType == 18 && font == null))
            {
                result.AddIssue("MissingDisplayState", "Geometry has no decoded pen or text font.", "Contents/DisplayLists",
                    (int)r.CommandType, segment.NativeRecord?.Offset);
                continue;
            }
            var p = new DrawingPreviewPrimitive
            {
                Id = r.Id, Points = r.Points.ToArray(), Normal = r.Normal,
                IsSheetLevelNote = segment.IsSheetFormat,
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
            if (nativeStyles.TryGetValue(style.Id, out var nativePen))
            {
                int styleId = nativePen.Integers[3];
                p.NativeLineStyleId = styleId;
                result.AddIssue("PenLineStyle", $"Native pen style IDs require a verified named-style mapping (first ID {styleId}).",
                    "Contents/DisplayLists", 9, nativePen.Offset);
            }
            if (p.Kind == DrawingPreviewPrimitiveKind.Text && ((font?.FontFlags ?? 0) & ~8u) != 0)
                result.AddIssue("FontFlags", "Additional font flags are preserved without a presentation mapping.", "Contents/DisplayLists", 10);
            primitives.Add(p);
        }
        result.Primitives = primitives.AsReadOnly();
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
            if (p.Points.Count != original.Points.Count || p.Points.Any(point =>
                    double.IsNaN(point.X) || double.IsInfinity(point.X) || double.IsNaN(point.Y) || double.IsInfinity(point.Y) || double.IsNaN(point.Z) || double.IsInfinity(point.Z)) ||
                double.IsNaN(p.Normal.X) || double.IsInfinity(p.Normal.X) || double.IsNaN(p.Normal.Y) || double.IsInfinity(p.Normal.Y) || double.IsNaN(p.Normal.Z) || double.IsInfinity(p.Normal.Z) ||
                double.IsNaN(p.Angle) || double.IsInfinity(p.Angle))
                throw new InvalidDataException("Invalid native preview geometry.");
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
            var segment = Segments.Single(s => ReferenceEquals(s.Record, r));
            if (segment.NativeRecord != null)
            {
                NativeDrawingPreviewAdapter.WriteGeometry(r, segment.NativeRecord);
                using var validation = MfcArchive.CreateWriter(); validation.DocumentVersion = _native!.DocumentVersion;
                segment.NativeRecord.Serialize(validation);
            }
            else MfcArchive.Serialize(r);
        }
    }
}
