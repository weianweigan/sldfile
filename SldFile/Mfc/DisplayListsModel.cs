using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/DisplayLists__ZLB binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes viewport prefix, user env, line fonts, lights, visual properties, and tessellation markers.
/// </summary>
internal sealed class DisplayListsModel : IMfcSerializable
{
    public const string UserModelEnvClassName = "uiUserModelEnv_c";
    public const string LineFontMgrClassName = "uiLineFontMgr_c";
    public const string LFConfigClassName = "uiLFConfig_c";
    public const string AmbientLightClassName = "moAmbientLight_c";
    public const string DirectionLightClassName = "moDirectionLight_c";
    public const string VisualPropertiesClassName = "moVisualProperties_c";
    public const string TempBodyTessClassName = "uoTempBodyTessData_c";
    public const string TempFaceTessClassName = "uoTempFaceTessData_c";
    public const string TempAssemblySHDClassName = "uoTempAssemblySHDData_c";
    public const string TempPartTessClassName = "uoTempPartTessData_c";

    public List<double> Prefix { get; } = new();
    public string UserName { get; set; } = string.Empty;
    public string LineFontName { get; set; } = string.Empty;
    public List<LineFontConfigEntry> LineFontConfig { get; } = new();
    public int AmbientLightCount { get; set; }
    public int DirectionalLightCount { get; set; }
    public List<string> VisualPropertiesStrings { get; } = new();
    public int BodyTessDataSize { get; set; }
    public int FaceTessDataSize { get; set; }
    public bool HasAssemblyData { get; set; }
    public bool HasPartTessData { get; set; }

    public byte[]? RawData { get; set; }

    public DisplayListsModel() { }



    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            // Storing from scratch
            // 1. Prefix
            foreach (var d in Prefix)
            {
                double val = d;
                ar.F64(ref val);
            }

            // 2. uiUserModelEnv_c
            if (!string.IsNullOrEmpty(UserName))
            {
                ushort tag = 0;
                ar.WriteClassTag(UserModelEnvClassName, ref tag, schema: 1);
                string? u = UserName;
                ar.WChar(ref u);
            }

            // 3. uiLineFontMgr_c
            if (!string.IsNullOrEmpty(LineFontName))
            {
                ushort tag = 0;
                ar.WriteClassTag(LineFontMgrClassName, ref tag, schema: 1);
                string? lfn = LineFontName;
                ar.WChar(ref lfn);
            }

            // 4. uiLFConfig_c
            if (LineFontConfig.Count > 0)
            {
                ushort tag = 0;
                ar.WriteClassTag(LFConfigClassName, ref tag, schema: 1);
                foreach (var entry in LineFontConfig)
                {
                    string? s = entry.StyleName;
                    ar.WChar(ref s);
                    string? t = entry.LineType;
                    ar.WChar(ref t);
                }
            }
        }
        else
        {
            Prefix.Clear();
            LineFontConfig.Clear();
            VisualPropertiesStrings.Clear();

            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var doc = MfcDocument.Parse(data);

            if (doc.AllElements.Count > 0)
            {
                int prefixEnd = doc.AllElements[0].TagOffset;
                var reader = new MfcReader(data, 0, prefixEnd);
                while (reader.Remaining >= 8)
                {
                    Prefix.Add(reader.ReadF64());
                }
            }

            if (doc.FindFirst(UserModelEnvClassName)?.AsWChar() is { } username)
            {
                UserName = username;
            }

            if (doc.FindFirst(LineFontMgrClassName) is { } fontMgr)
            {
                var fontReader = fontMgr.CreateReader();
                if (fontReader.SeekNextWChar() && fontReader.ReadWChar() is { } fn)
                {
                    LineFontName = fn;
                }
            }

            if (doc.FindFirst(LFConfigClassName) is { } lfConfig)
            {
                var reader = lfConfig.CreateReader();
                while (reader.Remaining > 4)
                {
                    if (reader.IsWCharPrefix)
                    {
                        string? styleName = reader.ReadWChar();
                        if (styleName == null) break;

                        while (reader.Remaining > 3 && !reader.IsWCharPrefix && !reader.IsMfcTag)
                            reader.Skip(1);

                        string lineType = string.Empty;
                        if (reader.IsWCharPrefix)
                        {
                            lineType = reader.ReadWChar() ?? string.Empty;
                            while (reader.Remaining > 3 && !reader.IsWCharPrefix && !reader.IsMfcTag)
                                reader.Skip(1);
                        }

                        if (!string.IsNullOrEmpty(styleName))
                        {
                            LineFontConfig.Add(new LineFontConfigEntry
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

            AmbientLightCount = doc.FindAll(AmbientLightClassName).Count;
            DirectionalLightCount = doc.FindAll(DirectionLightClassName).Count;

            var visProps = doc.FindFirst(VisualPropertiesClassName);
            if (visProps != null)
            {
                ExtractWChars(visProps, VisualPropertiesStrings);
                int visIdx = visProps.Index;
                if (visIdx >= 0)
                {
                    for (int i = visIdx + 1; i < doc.AllElements.Count; i++)
                    {
                        var next = doc.AllElements[i];
                        if (!string.IsNullOrEmpty(next.ClassName)) break;
                        ExtractWChars(next, VisualPropertiesStrings);
                    }
                }
            }

            var bodyTess = doc.FindFirst(TempBodyTessClassName);
            if (bodyTess != null)
                BodyTessDataSize = bodyTess.DataLength;

            var faceTess = doc.FindFirst(TempFaceTessClassName);
            if (faceTess != null)
                FaceTessDataSize = faceTess.DataLength;

            HasAssemblyData = doc.FindFirst(TempAssemblySHDClassName) != null;
            HasPartTessData = doc.FindFirst(TempPartTessClassName) != null;
        }
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

internal sealed class LineFontConfigEntry
{
    public string StyleName { get; set; } = string.Empty;
    public string LineType { get; set; } = string.Empty;
}