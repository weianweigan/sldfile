using System;
using System.Collections.Generic;

namespace SldFile.Drawing;

public enum DrawingPreviewPrimitiveKind { Line, Arc, Triangle, Text }

/// <summary>A point in sheet coordinates, in metres.</summary>
public readonly struct DrawingPreviewPoint
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public DrawingPreviewPoint(double x, double y, double z = 0) { X = x; Y = y; Z = z; }
}

/// <summary>A decoded command or projected cache primitive. Coordinates and text height are metres; angles are radians.</summary>
public sealed class DrawingPreviewPrimitive
{
    public string Id { get; internal set; } = string.Empty;
    public string ViewName { get; internal set; } = string.Empty;
    public DrawingPreviewPrimitiveKind Kind { get; internal set; }
    public IReadOnlyList<DrawingPreviewPoint> Points { get; set; } = Array.Empty<DrawingPreviewPoint>();
    public DrawingPreviewPoint Normal { get; set; } = new DrawingPreviewPoint(0, 0, 1);
    public uint ColorRgb { get; set; }
    public string LineStyle { get; set; } = "CONTINUOUS";
    public int LayerId { get; set; }
    public double LineWidth { get; set; } = 0.00025;
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public double TextHeight { get; set; } = 0.0025;
    public double Angle { get; set; }
    public double WidthFactor { get; set; } = 1;
    public bool IsBold { get; set; }
    public IReadOnlyList<double> CharacterAdvances { get; internal set; } = Array.Empty<double>();
}

/// <summary>Saved native preview content, independent of the binary archive and UI frameworks.</summary>
public sealed class DrawingPreviewDefinition
{
    public IReadOnlyList<DrawingPreviewViewDefinition> Views { get; internal set; } = Array.Empty<DrawingPreviewViewDefinition>();
    public int SheetIndex { get; internal set; }
    public double PaperWidth { get; set; } = 0.297;
    public double PaperHeight { get; set; } = 0.210;
    public IReadOnlyList<DrawingPreviewPrimitive> Primitives { get; internal set; } = Array.Empty<DrawingPreviewPrimitive>();
    /// <summary>False when an opaque command or unverified sheet ownership prevents full rendering.</summary>
    public bool IsResolved { get; internal set; }
    public string ResolutionMessage { get; internal set; } = string.Empty;
    public int PreservedByteCount { get; internal set; }
    public int DecodedByteCount { get; internal set; }
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
}
