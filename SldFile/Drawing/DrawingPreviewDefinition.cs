using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace SldFile.Drawing;

/// <summary>Saved native preview content, independent of the binary archive and UI frameworks.</summary>
public sealed class DrawingPreviewDefinition
{
    public IReadOnlyList<DrawingPreviewViewDefinition> Views { get; internal set; } = [];

    public int SheetIndex { get; internal set; }

    public double PaperWidth { get; set; } = 0.297;

    public double PaperHeight { get; set; } = 0.210;

    public IReadOnlyList<DrawingPreviewPrimitive> Primitives { get; internal set; } = Array.Empty<DrawingPreviewPrimitive>();

    /// <summary>False when an opaque command or unverified sheet ownership prevents full rendering.</summary>
    public bool IsResolved { get; internal set; }

    public string ResolutionMessage { get; internal set; } = string.Empty;

    /// <summary>Concrete limitations encountered in this preview, grouped by source and command type.</summary>
    public IReadOnlyList<DrawingPreviewIssue> Issues { get; internal set; } = [];

    /// <summary>Saved line-style segments in native units. No physical scale is inferred.</summary>
    public IReadOnlyList<DrawingPreviewLineStyle> NativeLineStyles { get; internal set; } = [];

    internal void AddIssue(string code, string message, string source, int? commandType = null, int? offset = null)
    {
        var issues = Issues.ToList();
        int index = issues.FindIndex(i => i.Code == code && i.Source == source && i.CommandType == commandType);
        if (index < 0) issues.Add(new DrawingPreviewIssue(code, message, source, commandType, offset, 1));
        else
        {
            var old = issues[index];
            issues[index] = new DrawingPreviewIssue(old.Code, old.Message, old.Source, old.CommandType, old.FirstOffset, old.Occurrences + 1);
        }
        Issues = issues.AsReadOnly();
        IsResolved = false;
        ResolutionMessage = string.Join("; ", Issues.Select(i => $"{i.Source}: {i.Message} ({i.Occurrences})"));
    }

    public int PreservedByteCount { get; internal set; }

    public int DecodedByteCount { get; internal set; }

    internal DrawingPreviewDefinition Clone()
    {
        var copy = (DrawingPreviewDefinition)MemberwiseClone();
        copy.Primitives = Primitives.Select(p => p.Clone()).ToArray();
        copy.Views = Views.Select(v => v.Clone()).ToArray();
        return copy;
    }
}

/// <summary>FirstOffset is a byte offset in the named native stream, when available.</summary>
public sealed class DrawingPreviewIssue(string code, string message, string source, int? commandType, int? firstOffset, int occurrences)
{
    public string Code { get; } = code;
    public string Message { get; } = message;
    public string Source { get; } = source;
    public int? CommandType { get; } = commandType;
    public int? FirstOffset { get; } = firstOffset;
    public int Occurrences { get; } = occurrences;
}

public sealed class DrawingPreviewLineStyle(string name, IReadOnlyList<double> segments, int flags)
{
    public string Name { get; } = name;
    public IReadOnlyList<double> Segments { get; } = segments;
    public int Flags { get; } = flags;
}

/// <summary>A point in sheet coordinates, in metres.</summary>
public readonly struct DrawingPreviewPoint
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public DrawingPreviewPoint(double x, double y, double z = 0) { X = x; Y = y; Z = z; }
}

public enum DrawingPreviewPrimitiveKind { Line, Arc, Triangle, Text }

/// <summary>A decoded command or projected cache primitive. Coordinates and text height are metres; angles are radians.</summary>
public sealed class DrawingPreviewPrimitive
{
    public string Id { get; internal set; } = string.Empty;
    public string ViewName { get; internal set; } = string.Empty;
    public DrawingPreviewPrimitiveKind Kind { get; internal set; }
    /// <summary>Line: ordered polyline vertices; Arc: start, end, centre; Triangle: polygon vertices; Text: origin.</summary>
    public IReadOnlyList<DrawingPreviewPoint> Points { get; set; } = Array.Empty<DrawingPreviewPoint>();
    public DrawingPreviewPoint Normal { get; set; } = new DrawingPreviewPoint(0, 0, 1);
    public uint ColorRgb { get; set; }
    public string LineStyle { get; set; } = "CONTINUOUS";
    public int? NativeLineStyleId { get; internal set; }
    public int LayerId { get; set; }
    public double LineWidth { get; set; } = 0.00025;
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public double TextHeight { get; set; } = 0.0025;
    public double Angle { get; set; }
    public double WidthFactor { get; set; } = 1;
    public bool IsBold { get; set; }
    /// <summary>True when this command belongs to a native sheet-format display bucket.</summary>
    public bool IsSheetLevelNote { get; internal set; }
    public IReadOnlyList<double> CharacterAdvances { get; internal set; } = Array.Empty<double>();

    /// <summary>Approximate named-style spacing in metres, shared by SVG and screen rendering.</summary>
    public IReadOnlyList<double> GetDashPattern() =>
        LineStyle.IndexOf("CENTER", StringComparison.OrdinalIgnoreCase) >= 0 ? new[] { .003, .001, .0005, .001 } :
        LineStyle.IndexOf("HIDDEN", StringComparison.OrdinalIgnoreCase) >= 0 ? new[] { .002, .001 } : Array.Empty<double>();

    /// <summary>Native UTF-16 advances combined into complete Unicode text elements.</summary>
    public IReadOnlyList<DrawingPreviewTextElement> GetTextElements()
    {
        if (CharacterAdvances.Count != Text.Length || CharacterAdvances.Any(a => double.IsNaN(a) || double.IsInfinity(a) || a < 0)
            || CharacterAdvances.Sum() <= 0) return [];
        int[] starts = StringInfo.ParseCombiningCharacters(Text);
        var result = new List<DrawingPreviewTextElement>();
        double position = 0;
        for (int i = 0; i < starts.Length; i++)
        {
            int end = i + 1 < starts.Length ? starts[i + 1] : Text.Length;
            double advance = 0;
            for (int j = starts[i]; j < end; j++) advance += CharacterAdvances[j];
            result.Add(new DrawingPreviewTextElement(Text.Substring(starts[i], end - starts[i]), position, advance));
            position += advance;
        }
        return result.AsReadOnly();
    }

    internal DrawingPreviewPrimitive Clone()
    {
        var copy = (DrawingPreviewPrimitive)MemberwiseClone();
        copy.Points = Points.ToArray(); copy.CharacterAdvances = CharacterAdvances.ToArray();
        return copy;
    }
}

public sealed class DrawingPreviewTextElement(string text, double offset, double advance)
{
    public string Text { get; } = text;
    public double Offset { get; } = offset;
    public double Advance { get; } = advance;
}

/// <summary>Verified model-to-sheet placement, using metres and the saved 3 by 3 matrix.</summary>
public sealed class DrawingPreviewViewDefinition
{
    public string Name { get; internal set; } = string.Empty;
    public IReadOnlyList<double> Matrix { get; internal set; } = Array.Empty<double>();
    public DrawingPreviewPoint Translation { get; internal set; }
    public double Scale { get; internal set; }
    public DrawingPreviewPoint ClipMin { get; internal set; }
    public DrawingPreviewPoint ClipMax { get; internal set; }
    internal DrawingPreviewViewDefinition Clone()
    {
        var copy = (DrawingPreviewViewDefinition)MemberwiseClone(); copy.Matrix = Matrix.ToArray(); return copy;
    }
}
