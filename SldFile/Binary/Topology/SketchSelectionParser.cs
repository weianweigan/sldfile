using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.Topology;

/// <summary>Editorial classification of the sketch-selection envelope found between a sketch and its consuming feature.</summary>
internal enum SketchSelectionEnvelopeKind
{
    /// <summary>No moContourChooser_c exists: the feature consumes the whole sketch.</summary>
    None = 0,

    /// <summary>Structurally decoded 18-byte contour chooser + suObArray + moSketchChain_c.</summary>
    Contour = 1,

    /// <summary>Structurally decoded region chooser unit layout.</summary>
    Region = 2,

    /// <summary>A chooser exists but does not match an established layout; nothing is selected.</summary>
    UnresolvedChooser = 3
}

/// <summary>Result of classifying the selection envelope between a sketch and a feature.</summary>
internal sealed class SketchSelectionEnvelope
{
    public static readonly SketchSelectionEnvelope None = new SketchSelectionEnvelope
    {
        Kind = SketchSelectionEnvelopeKind.None
    };

    public SketchSelectionEnvelopeKind Kind { get; set; }

    public IReadOnlyList<SketchContourReference> Contours { get; set; } = Array.Empty<SketchContourReference>();

    public IReadOnlyList<SketchRegionReference> Regions { get; set; } = Array.Empty<SketchRegionReference>();
}

/// <summary>
/// Classifies and decodes the persistent sketch selection between a parent sketch
/// (moProfileFeature_c) and the consuming feature tag. Controlled specimens:
/// SelectionModes/{whole, whole_resave, contour, region} and
/// SelectionContours/{contour, contour_second}.
/// </summary>
internal static class SketchSelectionParser
{
    internal static SketchSelectionEnvelope Read(MfcDocument doc, int sketchStart, int featureStart)
    {
        if (doc == null) return SketchSelectionEnvelope.None;
        var choosers = doc.AllElements
            .Where(e => e.ClassName == "moContourChooser_c" &&
                        e.TagOffset >= sketchStart && e.TagOffset < featureStart)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count == 0) return SketchSelectionEnvelope.None;

        // No specimen contains more than one chooser between a sketch and a
        // feature; refuse to guess which one applies.
        if (choosers.Count != 1) return new SketchSelectionEnvelope { Kind = SketchSelectionEnvelopeKind.UnresolvedChooser };

        var chooser = choosers[0];
        var contours = SelectedSketchContourParser.TryRead(doc, chooser);
        if (contours.Count > 0)
        {
            return new SketchSelectionEnvelope
            {
                Kind = SketchSelectionEnvelopeKind.Contour,
                Contours = contours
            };
        }

        var regions = SelectedSketchRegionParser.TryRead(doc, chooser);
        if (regions.Count > 0)
        {
            return new SketchSelectionEnvelope
            {
                Kind = SketchSelectionEnvelopeKind.Region,
                Regions = regions
            };
        }

        return new SketchSelectionEnvelope { Kind = SketchSelectionEnvelopeKind.UnresolvedChooser };
    }
}
