using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class DefinitionSchemaMapper
{
    // Unit system class names found in Drawing definitions
    private static readonly string[] UnitClassNames =
    {
        "moLengthUserUnits_c", "moAngleUserUnits_c", "moNumberUserUnits_c",
        "moDensityUnits_c", "moFloatNumberUserUnits_c", "moSpringConstantUnits_c",
        "moUnitSysUnits_c", "moForceUnits_c", "moStressUnits_c",
        "moGravityUnits_c", "moLinearMotorUnits_c", "moRotaryMotorUnits_c",
        "moPowerUnits_c", "moEnergyUnits_c", "moTimeUnits_c", "moFrequencyUserUnits_c"
    };

    private static readonly HashSet<string> ExcludedNoteKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "宋体", "新宋体", "Century Gothic", "SimSun", "Arial", "Times New Roman", "黑体", "仿宋", "楷体",
        "moDrawing_c", "moHeader_c", "moLogs_c", "moStamp_c", "moDisplayNote_c", "moGenericText_c"
    };

    public static void Map(byte[] data, DefinitionData model)
    {
        model.RawData = data;

        // Determine file type from first u32
        var reader = new MfcReader(data);
        if (reader.TryReadU32(out uint firstVal) && Enum.IsDefined(typeof(DefinitionFileType), (int)firstVal))
        {
            model.FileType = (DefinitionFileType)firstVal;
        }

        var doc = MfcDocument.Parse(data);

        // Part/Assembly: extract styling and user info from descendant elements
        if (model.FileType == DefinitionFileType.Part || model.FileType == DefinitionFileType.Assembly)
        {
            model.LineStyle = doc.FindFirst("moLineStyle_c")?.AsWChar();
            model.LineFont = doc.FindFirst("uiLineFontMgr_c")?.AsWChar();
            model.LineFontConfig = doc.FindFirst("uiLFConfig_c")?.AsWChar();
            model.UserName = doc.FindFirst("uiUserModelEnv_c")?.AsWChar();
        }

        // Drawing: collect unit system class names, sheets, views, and notes
        if (model.FileType == DefinitionFileType.Drawing)
        {
            var sheets = new List<DrawingSheetDefinition>();
            var views = new List<DrawingViewDefinition>();

            foreach (var element in doc.AllElements)
            {
                if (element.ClassName == null) continue;

                foreach (var unitName in UnitClassNames)
                {
                    if (element.ClassName == unitName)
                    {
                        model.UnitSystemClassesBuilder.Add(unitName);
                        break;
                    }
                }

                if (element.ClassName == "moDrSheet")
                {
                    var sheet = new DrawingSheetDefinition();
                    var r = element.CreateReader();
                    if (r.SeekNextWChar() && r.ReadWChar() is { } sheetName)
                    {
                        sheet.Name = sheetName;
                    }
                    if (element.Child("moDrTemplate") is { } tplElem)
                    {
                        var tr = tplElem.CreateReader();
                        if (tr.SeekNextWChar() && tr.ReadWChar() is { } tplName)
                        {
                            sheet.TemplateName = tplName;
                        }
                    }
                    sheets.Add(sheet);
                }
                else if (element.ClassName == "moAbsoluteView" ||
                         element.ClassName == "moUnfoldedView" ||
                         element.ClassName == "moDetailView" ||
                         element.ClassName == "moView_c")
                {
                    var r = element.CreateReader();
                    string? viewName = r.SeekNextWChar() ? r.ReadWChar() : null;
                    if (!string.IsNullOrEmpty(viewName))
                    {
                        views.Add(new DrawingViewDefinition
                        {
                            Name = viewName!,
                            ClassName = element.ClassName,
                            TagOffset = element.TagOffset
                        });
                    }
                }
            }

            model.Sheets = sheets.AsReadOnly();
            model.Views = views.AsReadOnly();
            model.Notes = ExtractNotes(data).AsReadOnly();
        }

        model.Freeze();
    }

    /// <summary>
    /// Deserializes 2D drawing text notes and annotations from the Definition stream via MFC deserialization.
    /// </summary>
    public static List<DrawingNoteDefinition> ExtractNotes(byte[] data)
    {
        var notes = new List<DrawingNoteDefinition>();
        if (data == null || data.Length < 30) return notes;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int maxLen = data.Length;

        for (int i = 0; i <= maxLen - 30; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xFE && data[i + 2] == 0xFF)
            {
                int charLen = data[i + 3];
                if (charLen <= 0 || charLen > 120) continue;

                int textStart = i + 4;
                int byteLen = charLen * 2;
                int textEnd = textStart + byteLen;

                if (textEnd + 24 > maxLen) continue;

                string text;
                try
                {
                    text = System.Text.Encoding.Unicode.GetString(data, textStart, byteLen).Trim();
                }
                catch { continue; }

                if (string.IsNullOrEmpty(text) || text.EndsWith("_c", StringComparison.Ordinal) || ExcludedNoteKeywords.Contains(text))
                    continue;

                double x = BitConverter.ToDouble(data, textEnd);
                double y = BitConverter.ToDouble(data, textEnd + 8);
                double z = BitConverter.ToDouble(data, textEnd + 16);

                if (!double.IsNaN(x) && !double.IsInfinity(x) &&
                    !double.IsNaN(y) && !double.IsInfinity(y) &&
                    !double.IsNaN(z) && !double.IsInfinity(z) &&
                    x >= -0.1 && x <= 2.5 && y >= -0.1 && y <= 2.5 && Math.Abs(z) < 0.25)
                {
                    double xMm = x * 1000.0;
                    double yMm = y * 1000.0;
                    double zMm = z * 1000.0;

                    string fontName = "SimSun";
                    double heightMm = 2.5;
                    double angleRad = 0.0;

                    int postStart = textEnd + 24;
                    int postLen = Math.Min(80, maxLen - postStart);
                    for (int j = 0; j < postLen - 12; j++)
                    {
                        if (data[postStart + j] == 0xFF && data[postStart + j + 1] == 0xFE && data[postStart + j + 2] == 0xFF)
                        {
                            int flen = data[postStart + j + 3];
                            if (flen > 0 && flen <= 30 && postStart + j + 4 + flen * 2 + 16 <= maxLen)
                            {
                                try
                                {
                                    string fname = System.Text.Encoding.Unicode.GetString(data, postStart + j + 4, flen * 2);
                                    if (!string.IsNullOrWhiteSpace(fname) && !fname.EndsWith("_c", StringComparison.Ordinal))
                                    {
                                        fontName = fname.Trim();
                                        int fEnd = postStart + j + 4 + flen * 2;
                                        double h = BitConverter.ToDouble(data, fEnd);
                                        double a = BitConverter.ToDouble(data, fEnd + 8);
                                        if (h > 0.0005 && h < 0.05) heightMm = h * 1000.0;
                                        if (Math.Abs(a) < 7.0) angleRad = a;
                                    }
                                }
                                catch { }
                                break;
                            }
                        }
                    }

                    string key = $"{text}_{Math.Round(xMm, 1)}_{Math.Round(yMm, 1)}";
                    if (seen.Add(key))
                    {
                        notes.Add(new DrawingNoteDefinition
                        {
                            Text = text,
                            XMm = xMm,
                            YMm = yMm,
                            ZMm = zMm,
                            Font = fontName,
                            HeightMm = heightMm,
                            AngleRad = angleRad
                        });
                    }
                }
            }
        }

        return notes;
    }
}
