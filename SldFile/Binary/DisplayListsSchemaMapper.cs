using System;
using System.Collections.Generic;
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
