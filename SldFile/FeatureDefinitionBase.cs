using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Base for strongly typed FeatureData models whose fields are only populated when a controlled
/// parser has structurally verified them. Properties are nullable and have no defaults; a null
/// (or absent) value means "not resolved from a file". The resolution set records which fields a
/// parser actually decoded and must not be inferred from a default value.
/// </summary>
public abstract class FeatureDefinitionData : IFeatureData
{
    private readonly HashSet<string> _resolved = new HashSet<string>(StringComparer.Ordinal);

    public abstract string ClassName { get; }

    public abstract uint TypeId { get; }

    /// <summary>Names of fields marked as structurally resolved by a parser or authoring input.</summary>
    public IReadOnlyCollection<string> ResolvedProperties => _resolved;

    /// <summary>True only when the named field was explicitly marked resolved. Default is false.</summary>
    public bool IsResolved(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && _resolved.Contains(propertyName);

    /// <summary>Marks a field as structurally resolved. Parsers call this only after validation.</summary>
    protected void MarkResolved(string propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
            _resolved.Add(propertyName);
    }

    internal void MarkResolvedInternal(string propertyName) => MarkResolved(propertyName);

    internal void ClearResolvedInternal() => _resolved.Clear();
}

/// <summary>
/// A single selection/reference entry stored by the offline feature models. Fields are nullable;
/// no persistent topology is decoded until a controlled sample establishes the layout.
/// </summary>
public sealed class FeatureReferenceData
{
    /// <summary>Reference kind (edge, face, plane, axis, body, feature...), when decoded.</summary>
    public SelectionEntityType? EntityType { get; set; }

    /// <summary>Persistent entity id or ordinal, when decoded. May be opaque and renumbered by a resave.</summary>
    public uint? EntityId { get; set; }

    /// <summary>Opaque persistent selection id carried by the measured reference header, when decoded.</summary>
    public uint? PersistentId { get; set; }

    /// <summary>Optional decoder-provided name (for diagnostics only).</summary>
    public string? ReferenceName { get; set; }

    /// <summary>Measured class word carried by the persistent reference, when decoded.</summary>
    public uint? ClassWord { get; set; }

    /// <summary>Measured archive GUID as lowercase hex (32 chars), when decoded.</summary>
    public string? ArchiveGuid { get; set; }

    /// <summary>Number of persistent reference records in the measured edge/selection block, when decoded.</summary>
    public int? RecordCount { get; set; }
}
