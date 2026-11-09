using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Common base helper for MFC feature models that track structurally resolved properties.
/// </summary>
public class MfcFeatureModelBase
{
    private readonly HashSet<string> _resolved = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Names of fields marked as structurally resolved by a parser or authoring input.</summary>
    public IReadOnlyCollection<string> ResolvedProperties => _resolved;

    /// <summary>True only when the named field was explicitly marked resolved. Default is false.</summary>
    public bool IsResolved(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && _resolved.Contains(propertyName);

    /// <summary>Marks a field as structurally resolved. Parsers call this only after validation.</summary>
    public void MarkResolved(string propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
            _resolved.Add(propertyName);
    }

    internal void MarkResolvedInternal(string propertyName) => MarkResolved(propertyName);

    internal void ClearResolvedInternal() => _resolved.Clear();
}
