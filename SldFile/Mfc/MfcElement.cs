using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

internal sealed class MfcElement
{
    public string ClassName { get; }
    public byte TagByte { get; }
    public int TagOffset { get; }
    public int DataStart { get; }
    public int DataEnd { get; internal set; }
    public int Depth { get; internal set; }
    public int Index { get; internal set; } = -1;
    public MfcElement? Parent { get; internal set; }
    public List<MfcElement> Children { get; } = new List<MfcElement>();

    public byte[] SourceData => _source;
    public int DataLength => Math.Max(0, DataEnd - DataStart);

    private readonly byte[] _source;

    public byte[] RawData => MfcPrimitives.CopyRange(_source, DataStart, DataEnd);

    internal MfcElement(string className, byte tagByte, int tagOffset, int dataStart, byte[] sourceData)
    {
        ClassName = className;
        TagByte = tagByte;
        TagOffset = tagOffset;
        DataStart = dataStart;
        _source = sourceData;
    }

    /// <summary>
    /// Creates a forward cursor reader bounded to this element's data region [DataStart, DataEnd).
    /// </summary>
    public MfcReader CreateReader() => MfcReader.FromElement(this);

    public MfcElement? Child(string className)
    {
        foreach (var child in Children)
            if (child.ClassName == className) return child;
        return null;
    }

    public IEnumerable<MfcElement> Descendants(string className)
    {
        var queue = new Queue<MfcElement>(Children);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.ClassName == className) yield return c;
            foreach (var cc in c.Children) queue.Enqueue(cc);
        }
    }

    public IEnumerable<MfcElement> Descendants()
    {
        var queue = new Queue<MfcElement>(Children);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            yield return c;
            foreach (var cc in c.Children) queue.Enqueue(cc);
        }
    }

    public string? AsWChar()
    {
        int offset = DataStart;
        int end = Math.Min(DataEnd, _source.Length);
        while (offset <= end - 4 && offset < DataStart + 16 && !MfcPrimitives.IsWCharPrefix(_source, offset))
            offset++;
        if (offset <= end - 4 && MfcPrimitives.IsWCharPrefix(_source, offset))
            return MfcPrimitives.ReadWChar(_source, ref offset);
        return null;
    }

    public ushort AsU16()
    {
        int offset = DataStart;
        return MfcPrimitives.ReadU16(_source, ref offset);
    }

    public uint AsU32()
    {
        int offset = DataStart;
        return MfcPrimitives.ReadU32(_source, ref offset);
    }

    public ulong AsU64()
    {
        int offset = DataStart;
        return MfcPrimitives.ReadU64(_source, ref offset);
    }

    public float AsF32()
    {
        int offset = DataStart;
        return MfcPrimitives.ReadF32(_source, ref offset);
    }

    public double AsF64()
    {
        int offset = DataStart;
        return MfcPrimitives.ReadF64(_source, ref offset);
    }

    public override string ToString() => $"{ClassName} @ {TagOffset} [{DataStart}..{DataEnd}) depth={Depth}";
}
