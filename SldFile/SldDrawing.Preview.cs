using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Binary;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile;

public partial class SldDrawing
{
    /// <summary>Gets a detached public definition of the saved native preview, in metres/radians.</summary>
    public DrawingPreviewDefinition GetDrawingPreview(int sheetIndex = 0) =>
        GetDrawingPreview(sheetIndex, includeTransformedItems: false);

    /// <summary>
    /// Gets a detached public definition of the saved native preview, optionally applying local transforms
    /// to symbol/text display items.
    /// </summary>
    public DrawingPreviewDefinition GetDrawingPreview(int sheetIndex, bool includeTransformedItems)
    {
        var sheets = Sheets;
        if (sheetIndex < 0 || sheetIndex >= sheets.Count) throw new ArgumentOutOfRangeException(nameof(sheetIndex));
        if (!includeTransformedItems) return sheets[sheetIndex].Preview.Clone();
        var stream = ReadPreviewModel("Contents/DisplayLists", includeTransformedItems);
        var result = stream.ToDefinition(sheetIndex, sheets.Count);
        result.PaperWidth = sheets[sheetIndex].Preview.PaperWidth;
        result.PaperHeight = sheets[sheetIndex].Preview.PaperHeight;
        if (sheets.Count == 1 && stream.NativeArchive != null)
        {
            var names = sheets[sheetIndex].Preview.Views.Select(v => v.Name).ToArray();
            var cache = DrawingViewCacheArchive.Read(GetDrawingPreviewStreamBytes("Contents/VBLists"), false, names,
                GetNativeDrawingDocumentVersion(), true);
            DrawingPreviewComposer.AppendViews(result, stream.NativeArchive, cache, names);
        }
        return result;
    }

    /// <summary>
    /// Serializes all three source streams with the same models used for reading.
    /// Unknown records are losslessly retained. Returned keys are the original CFB paths,
    /// including __ZLB when present; unmodified compressed envelopes are retained verbatim.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> SerializeDrawingPreviewStreams()
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var names = Sheets.SelectMany(s => s.Views).Select(v => v.Name).Distinct().ToArray();
        foreach (var name in new[] { "Contents/Definition", "Contents/VBLists", "Contents/DisplayLists" })
        {
            string path = GetStreamBytes(name) != null ? name : name + "__ZLB";
            byte[]? original = GetStreamBytes(path);
            if (original == null) continue;
            IMfcSerializable source = name == "Contents/DisplayLists" ? ReadPreviewModel(name) :
                DrawingViewCacheArchive.Read(GetDrawingPreviewStreamBytes(name), name == "Contents/Definition", names, GetNativeDrawingDocumentVersion());
            byte[] bytes = MfcArchive.Serialize(source);
            byte[]? payload = GetDrawingPreviewStreamBytes(name);
            if (payload != null && bytes.SequenceEqual(payload)) result[path] = (byte[])original.Clone();
            else result[path] = EncodePreviewStream(path, original, bytes);
        }
        return result;
    }

    /// <summary>
    /// Updates verified command geometry or same-length text, retaining native identities and unknown records.
    /// Shared font/style state, topology changes and unresolved page ownership are rejected explicitly.
    /// Call Save() to persist the queued stream.
    /// </summary>
    public void SetDrawingPreview(DrawingPreviewDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (IsReadOnly) throw new InvalidOperationException("Drawing is opened read-only.");
        var sheets = Sheets;
        if (sheets.Count != 1 || definition.SheetIndex != 0) throw new NotSupportedException("Unresolved native sheet ownership.");
        var current = GetDrawingPreview(0);
        if (definition.PaperWidth != current.PaperWidth || definition.PaperHeight != current.PaperHeight)
            throw new NotSupportedException("Paper dimensions must be edited in the drawing definition, not its display cache.");
        var source = ReadPreviewModel("Contents/DisplayLists");
        // Work on a detached model: a validation failure must leave queued streams untouched.
        var cacheOriginals = current.Primitives.Where(p => p.Id.StartsWith("VB:", StringComparison.Ordinal)).ToDictionary(p => p.Id);
        var cacheEdits = definition.Primitives.Where(p => p.Id.StartsWith("VB:", StringComparison.Ordinal)).ToList();
        if (cacheEdits.Count != cacheOriginals.Count || cacheEdits.Any(p => !cacheOriginals.TryGetValue(p.Id, out var original) ||
            !p.Points.SequenceEqual(original.Points) || p.Kind != original.Kind || !p.Normal.Equals(original.Normal) ||
            p.ViewName != original.ViewName || p.LineStyle != original.LineStyle || p.ColorRgb != original.ColorRgb || p.LineWidth != original.LineWidth))
            throw new NotSupportedException("Edit native model-space edge records rather than projected/cropped sheet segments.");
        source.FromDefinition(new DrawingPreviewDefinition { SheetIndex = definition.SheetIndex,
            Primitives = definition.Primitives.Where(p => !p.Id.StartsWith("VB:", StringComparison.Ordinal)).ToArray() });
        byte[] payload = MfcArchive.Serialize(source);
        const string name = "Contents/DisplayLists";
        string path = GetStreamBytes(name) != null ? name : name + "__ZLB";
        byte[] original = GetStreamBytes(path) ?? throw new InvalidDataException("Drawing has no saved display command stream.");
        SetPendingStream(path, EncodePreviewStream(path, original, payload));
        _sheets = null;
    }

    private DrawingPreviewStreamModel ReadPreviewModel(string name, bool includeTransformedItems = false)
    {
        var model = new DrawingPreviewStreamModel { DecodeCommands = name == "Contents/DisplayLists", IncludeTransformedItems = includeTransformedItems };
        using var ar = MfcArchive.CreateReader(GetDrawingPreviewStreamBytes(name) ?? Array.Empty<byte>());
        ar.DocumentVersion = GetNativeDrawingDocumentVersion();
        model.Serialize(ar);
        return model;
    }

    private static byte[] EncodePreviewStream(string path, byte[] original, byte[] payload)
    {
        if (!path.EndsWith("__ZLB", StringComparison.OrdinalIgnoreCase)) return payload;
        var zlb = new ZlbData(); ZlbStreamHandler.Decompress(original, zlb);
        zlb.RawData = payload;
        return ZlbStreamHandler.Serialize(zlb);
    }
}
