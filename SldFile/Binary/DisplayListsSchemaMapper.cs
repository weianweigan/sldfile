using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Drawing.Vector;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class DisplayListsSchemaMapper
{
    public static void Map(byte[] data, DisplayListsData model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Parse 96-byte prefix (12 × f64) before first MFC tag
        if (doc.AllElements.Count > 0)
        {
            int prefixEnd = doc.AllElements[0].TagOffset;
            var reader = new MfcReader(data, 0, prefixEnd);
            var prefix = new List<double>();
            while (reader.Remaining >= 8)
            {
                prefix.Add(reader.ReadF64());
            }
            model.Prefix = prefix.AsReadOnly();
        }

        // Parse uiUserModelEnv_c — extract username WChar
        if (doc.FindFirst("uiUserModelEnv_c")?.AsWChar() is { } username)
        {
            model.UserName = username;
        }

        // Parse uiLineFontMgr_c — extract font name
        if (doc.FindFirst("uiLineFontMgr_c") is { } fontMgr)
        {
            var fontReader = fontMgr.CreateReader();
            if (fontReader.SeekNextWChar() && fontReader.ReadWChar() is { } fn)
            {
                model.LineFontName = fn;
            }
        }

        // Parse uiLFConfig_c — extract WChar pairs (style name, line type)
        var lfEntries = new List<LineFontConfigEntry>();
        if (doc.FindFirst("uiLFConfig_c") is { } lfConfig)
        {
            var reader = lfConfig.CreateReader();
            while (reader.Remaining > 4)
            {
                if (reader.IsWCharPrefix)
                {
                    string? styleName = reader.ReadWChar();
                    if (styleName == null) break;

                    // Skip binary data between the two WChars
                    while (reader.Remaining > 3 && !reader.IsWCharPrefix && !reader.IsMfcTag)
                        reader.Skip(1);

                    string lineType = string.Empty;
                    if (reader.IsWCharPrefix)
                    {
                        lineType = reader.ReadWChar() ?? string.Empty;
                        // Skip trailing binary data after second WChar
                        while (reader.Remaining > 3 && !reader.IsWCharPrefix && !reader.IsMfcTag)
                            reader.Skip(1);
                    }

                    if (!string.IsNullOrEmpty(styleName))
                    {
                        lfEntries.Add(new LineFontConfigEntry
                        {
                            StyleName = styleName,
                            LineType = lineType
                        });
                    }
                }
                else
                {
                    reader.Skip(1);
                }
            }
        }
        model.LineFontConfig = lfEntries.AsReadOnly();

        // Count lights
        model.AmbientLightCount = doc.FindAll("moAmbientLight_c").Count;
        model.DirectionalLightCount = doc.FindAll("moDirectionLight_c").Count;

        // Parse moVisualProperties_c — extract WChar strings from its data and following anonymous elements
        var visProps = doc.FindFirst("moVisualProperties_c");
        var visStrings = new List<string>();
        if (visProps != null)
        {
            ExtractWChars(visProps, visStrings);
            int visIdx = visProps.Index;
            if (visIdx >= 0)
            {
                for (int i = visIdx + 1; i < doc.AllElements.Count; i++)
                {
                    var next = doc.AllElements[i];
                    if (!string.IsNullOrEmpty(next.ClassName)) break;
                    ExtractWChars(next, visStrings);
                }
            }
        }
        model.VisualPropertiesStrings = visStrings.AsReadOnly();

        // Tessellation data sizes
        var bodyTess = doc.FindFirst("uoTempBodyTessData_c");
        if (bodyTess != null)
            model.BodyTessDataSize = bodyTess.DataLength;

        var faceTess = doc.FindFirst("uoTempFaceTessData_c");
        if (faceTess != null)
            model.FaceTessDataSize = faceTess.DataLength;

        // Assembly/Part-specific
        model.HasAssemblyData = doc.FindFirst("uoTempAssemblySHDData_c") != null;
        model.HasPartTessData = doc.FindFirst("uoTempPartTessData_c") != null;

        // Parse uiLayerMgr_c — extract layer names (Drawings)
        var layers = new List<string>();
        if (doc.FindFirst("uiLayerMgr_c") is { } layerMgr)
        {
            var lr = layerMgr.CreateReader();
            while (lr.Remaining > 4)
            {
                if (lr.IsWCharPrefix)
                {
                    var s = lr.ReadWChar();
                    if (s is { Length: > 0 } && !s.StartsWith("<objID=") && s != "CONTINUOUS" && s != "新宋体")
                    {
                        layers.Add(s);
                    }
                }
                else lr.Skip(1);
            }
        }
        model.Layers = layers.AsReadOnly();

        // Parse 2D Drawing CAD vector lines from DisplayLists stream via MFC deserialization
        model.CadLines = ExtractCadLines(data, doc);
    }

    /// <summary>
    /// Identifies byte range boundaries for individual drawing sheets in the Contents/DisplayLists stream
    /// when the drawing contains multiple sheets.
    /// </summary>
    public static List<(int Start, int End)> GetSheetByteRanges(byte[] data, int expectedSheetCount)
    {
        var ranges = new List<(int Start, int End)>();
        if (data == null || data.Length == 0) return ranges;

        if (expectedSheetCount <= 1)
        {
            ranges.Add((0, data.Length));
            return ranges;
        }

        // Scan for recurring sheet boundaries in multi-sheet drawings.
        // For standard multi-sheet drawing title blocks (e.g. process drawings or multi-page formats),
        // each sheet frame begins before standard title block headers (such as '物料代号' or template blocks).
        var splitOffsets = new List<int> { 0 };
        for (int i = 0; i < data.Length - 16; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xFE && data[i + 2] == 0xFF && data[i + 3] == 0x04)
            {
                // Unicode for '物料代号': 0x69 0x72, 0x99 0x65, 0xE3 0x4E, 0xF7 0x53
                if (i + 12 <= data.Length &&
                    data[i + 4] == 0x69 && data[i + 5] == 0x72 &&
                    data[i + 6] == 0x99 && data[i + 7] == 0x65 &&
                    data[i + 8] == 0xE3 && data[i + 9] == 0x4E &&
                    data[i + 10] == 0xF7 && data[i + 11] == 0x53)
                {
                    splitOffsets.Add(Math.Max(0, i - 300));
                }
            }
        }

        splitOffsets.Sort();

        if (splitOffsets.Count == expectedSheetCount)
        {
            for (int s = 0; s < splitOffsets.Count; s++)
            {
                int start = splitOffsets[s];
                int end = (s + 1 < splitOffsets.Count) ? splitOffsets[s + 1] : data.Length;
                ranges.Add((start, end));
            }
            return ranges;
        }

        // Fallback: entire stream
        ranges.Add((0, data.Length));
        return ranges;
    }

    /// <summary>
    /// Deserializes 2D CAD vector line geometry from SolidWorks Contents/DisplayLists stream via MFC deserialization.
    /// Uses MfcReader to scan serialized inline records and extract sub-millimeter CAD wireframes.
    /// Supports byte range slicing for multi-sheet drawing isolation.
    /// </summary>
    public static List<DrawingVectorLine> ExtractCadLines(
        byte[] data,
        MfcDocument? doc = null,
        double paperWidthMm = 420.0,
        double paperHeightMm = 297.0,
        int rangeStart = 0,
        int rangeEnd = -1)
    {
        var lines = new List<DrawingVectorLine>();
        if (data == null || data.Length < 96) return lines;

        doc ??= MfcDocument.Parse(data);
        var layerMgr = doc.FindFirst("uiLayerMgr_c");
        int baseStartOffset = layerMgr != null ? layerMgr.TagOffset : 0;
        int startOffset = Math.Max(rangeStart, baseStartOffset);
        int maxOffset = (rangeEnd > 0 && rangeEnd <= data.Length) ? rangeEnd : data.Length;

        double maxAllowedW = Math.Max(paperWidthMm * 2.0, 1500.0);
        double maxAllowedH = Math.Max(paperHeightMm * 2.0, 1500.0);

        var reader = new MfcReader(data, startOffset);
        while (reader.Offset < maxOffset - 16 && reader.Remaining >= 16)
        {
            if (reader.IsWCharPrefix)
            {
                string? styleName = reader.ReadWChar();
                if (styleName is { Length: >= 3 and <= 32 } &&
                    reader.Remaining >= 8 + 48)
                {
                    if (styleName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c >= 0x4e00))
                    {
                        int coordOff = reader.Offset + 8;
                        if (coordOff + 48 <= data.Length)
                        {
                            double x1 = BitConverter.ToDouble(data, coordOff);
                            double y1 = BitConverter.ToDouble(data, coordOff + 8);
                            double z1 = BitConverter.ToDouble(data, coordOff + 16);
                            double x2 = BitConverter.ToDouble(data, coordOff + 24);
                            double y2 = BitConverter.ToDouble(data, coordOff + 32);
                            double z2 = BitConverter.ToDouble(data, coordOff + 40);

                            bool IsValidNormal(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) >= 1e-4;
                            if (IsValidNormal(x1) && IsValidNormal(y1) &&
                                IsValidNormal(x2) && IsValidNormal(y2))
                            {
                                double x1mm = x1 * 1000.0;
                                double y1mm = y1 * 1000.0;
                                double x2mm = x2 * 1000.0;
                                double y2mm = y2 * 1000.0;

                                // Physical paper coordinate boundaries (exclude origin / edge-clamped spurious artifacts)
                                if (x1mm >= 1.0 && x1mm <= paperWidthMm - 1.0 &&
                                    y1mm >= 1.0 && y1mm <= paperHeightMm - 1.0 &&
                                    x2mm >= 1.0 && x2mm <= paperWidthMm - 1.0 &&
                                    y2mm >= 1.0 && y2mm <= paperHeightMm - 1.0)
                                {
                                    double dx = x2mm - x1mm;
                                    double dy = y2mm - y1mm;
                                    double len = Math.Sqrt(dx * dx + dy * dy);

                                    // Filter out spurious diagonals or sub-pixel noise
                                    if (len >= 0.2 && len <= Math.Max(paperWidthMm, paperHeightMm))
                                    {
                                        DrawingLineKind kind = DrawingLineKind.SolidOutline;
                                        double thickness = 0.35;

                                        if (styleName.Contains("CENTER", StringComparison.OrdinalIgnoreCase) ||
                                            styleName.Contains("中心"))
                                        {
                                            kind = DrawingLineKind.Centerline;
                                            thickness = 0.20;
                                        }
                                        else if (styleName.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase) ||
                                                 styleName.Contains("虚线"))
                                        {
                                            kind = DrawingLineKind.HiddenLine;
                                            thickness = 0.20;
                                        }
                                        else if (styleName.Contains("CHAIN", StringComparison.OrdinalIgnoreCase) ||
                                                 styleName.Contains("折弯"))
                                        {
                                            kind = DrawingLineKind.ThinLine;
                                            thickness = 0.25;
                                        }

                                        lines.Add(new DrawingVectorLine(x1mm, y1mm, x2mm, y2mm, kind, thickness));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            reader.Skip(1);
        }

        return lines;
    }

    /// <summary>
    /// Deserializes 2D CAD vector annotations and dimension texts from the Contents/DisplayLists stream.
    /// Extracts model view dimensions, angles, tolerances, and view callouts with physical coordinates in millimeters.
    /// Supports byte range slicing for multi-sheet drawing isolation.
    /// </summary>
    public static List<DrawingVectorText> ExtractCadTexts(
        byte[] data,
        double paperWidthMm = 420.0,
        double paperHeightMm = 297.0,
        int rangeStart = 0,
        int rangeEnd = -1)
    {
        var texts = new List<DrawingVectorText>();
        if (data == null || data.Length < 64) return texts;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int maxLen = data.Length;
        int scanStart = Math.Max(40, rangeStart);
        int scanEnd = (rangeEnd > 0 && rangeEnd <= maxLen) ? rangeEnd - 8 : maxLen - 8;

        for (int i = scanStart; i <= scanEnd; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xFE && data[i + 2] == 0xFF)
            {
                int charLen = data[i + 3];
                if (charLen <= 0 || charLen > 120) continue;

                int textStart = i + 4;
                int byteLen = charLen * 2;
                if (textStart + byteLen > maxLen) continue;

                string rawText;
                try
                {
                    rawText = System.Text.Encoding.Unicode.GetString(data, textStart, byteLen).Trim();
                }
                catch { continue; }

                if (string.IsNullOrEmpty(rawText) || rawText.EndsWith("_c", StringComparison.Ordinal))
                    continue;

                // Exclude system style and layer tokens
                if (rawText is "CONTINUOUS" or "HIDDEN" or "PHANTOM" or "CHAIN" or "CENTER" or "STITCH" or "CHAIN_THICK" or
                    "粗实线" or "细/粗_点画线" or "点画线" or "双点画线" or "中心线" or "2细线层" or "裁剪线" or "实线" or "虚线")
                    continue;

                // Physical coordinates (meters -> millimeters) are stored at i - 40 (X) and i - 32 (Y)
                double x = BitConverter.ToDouble(data, i - 40);
                double y = BitConverter.ToDouble(data, i - 32);

                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                    continue;

                if (x < 0.001 || x > 1.5 || y < 0.001 || y > 1.5)
                    continue;

                double xMm = x * 1000.0;
                double yMm = y * 1000.0;

                if (xMm < 1.0 || xMm > paperWidthMm || yMm < 1.0 || yMm > paperHeightMm)
                    continue;

                string key = $"{rawText}_{Math.Round(xMm, 1)}_{Math.Round(yMm, 1)}";
                if (seen.Add(key))
                {
                    texts.Add(new DrawingVectorText(rawText, xMm, yMm, 0.0, "SimSun", 2.5, 0.0));
                }
            }
        }

        return texts;
    }

    private static void ExtractWChars(MfcElement element, List<string> result)
    {
        var reader = element.CreateReader();
        while (reader.Remaining > 3)
        {
            if (reader.IsWCharPrefix)
            {
                string? s = reader.ReadWChar();
                if (s != null)
                    result.Add(s);
                continue;
            }
            if (reader.IsMfcTag)
                break;
            reader.Skip(1);
        }
    }
}
