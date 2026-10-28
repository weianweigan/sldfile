using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Container for a parsed MFC binary stream.
/// Holds the source byte array and a flat, ordered list of all named elements.
/// Supports both fast position-based range queries and direct element navigation.
/// </summary>
internal sealed class MfcDocument
{
    public byte[] SourceData { get; }
    public IReadOnlyList<MfcElement> AllElements { get; }

    internal MfcDocument(byte[] sourceData, List<MfcElement> allElements,
                         List<int> endMarkerPositions)
    {
        SourceData = sourceData;
        AllElements = allElements.AsReadOnly();
    }

    public static MfcDocument Parse(byte[] data)
    {
        var result = MfcTreeBuilder.Scan(data);
        return new MfcDocument(data, result.Elements, result.EndMarkerPositions);
    }

    public MfcElement? FindFirst(string className) => FindFirstDescendant(className);

    public List<MfcElement> FindAll(string className) => FindAllDescendants(className);

    public MfcElement? FindFirstDescendant(string className)
    {
        foreach (var elem in AllElements)
        {
            if (elem.ClassName == className)
                return elem;
        }
        return null;
    }

    public List<MfcElement> FindAllDescendants(string className)
    {
        var results = new List<MfcElement>();
        foreach (var elem in AllElements)
        {
            if (elem.ClassName == className)
                results.Add(elem);
        }
        return results;
    }

    /// <summary>
    /// Returns the first element with the given class name whose TagOffset
    /// falls within <paramref name="parent"/>'s DataEnd (which already
    /// includes nested child scope via the scanner's stack-based extension).
    /// </summary>
    public MfcElement? FindChildOf(MfcElement parent, string className)
    {
        int startIdx = IndexOf(parent);
        if (startIdx < 0) return null;

        for (int i = startIdx + 1; i < AllElements.Count; i++)
        {
            var elem = AllElements[i];
            if (elem.TagOffset >= parent.DataEnd)
                break;
            if (elem.ClassName == className)
                return elem;
        }
        return null;
    }

    /// <summary>
    /// Returns all elements within <paramref name="parent"/>'s scope,
    /// optionally filtered by class name.
    /// </summary>
    public List<MfcElement> FindChildrenOf(MfcElement parent, string? className = null)
    {
        var results = new List<MfcElement>();
        int startIdx = IndexOf(parent);
        if (startIdx < 0) return results;

        for (int i = startIdx + 1; i < AllElements.Count; i++)
        {
            var elem = AllElements[i];
            if (elem.TagOffset >= parent.DataEnd)
                break;
            if (className == null || elem.ClassName == className)
                results.Add(elem);
        }
        return results;
    }

    private int IndexOf(MfcElement element)
    {
        if (element.Index >= 0 && element.Index < AllElements.Count && AllElements[element.Index] == element)
            return element.Index;

        for (int i = 0; i < AllElements.Count; i++)
        {
            if (AllElements[i] == element)
                return i;
        }
        return -1;
    }
}
