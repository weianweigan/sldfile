using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Scans MFC binary data for class tags and produces an ordered flat list.
/// Uses a stack-based scope and DataEnd extension algorithm to establish
/// element bounds and parent/child hierarchy.
/// </summary>
internal static class MfcTreeBuilder
{
    public sealed class ScanResult
    {
        public List<MfcElement> Elements { get; }
        public List<int> EndMarkerPositions { get; }

        public ScanResult(List<MfcElement> elements, List<int> endMarkerPositions)
        {
            Elements = elements;
            EndMarkerPositions = endMarkerPositions;
        }
    }

    public static ScanResult Scan(byte[] data)
    {
        var elements = new List<MfcElement>();
        var endMarkerPositions = new List<int>();
        int pos = 0;

        while (pos < data.Length - 6)
        {
            if (MfcPrimitives.IsEndMarker(data, pos))
            {
                endMarkerPositions.Add(pos);
                pos += 6;
                continue;
            }

            if (MfcPrimitives.IsMfcTag(data, pos))
            {
                int saved = pos;
                var mfcTag = MfcPrimitives.ReadMfcTag(data, ref pos);
                if (mfcTag != null)
                {
                    elements.Add(new MfcElement(
                        mfcTag.ClassName,
                        mfcTag.TagByte,
                        mfcTag.TagOffset,
                        mfcTag.DataStart,
                        data));
                }
                else
                {
                    pos = saved + 1;
                }
            }
            else
            {
                pos++;
            }
        }

        if (elements.Count == 0)
            return new ScanResult(elements, endMarkerPositions);

        // Assign linear indices
        for (int i = 0; i < elements.Count; i++)
        {
            elements[i].Index = i;
        }

        // ── Stage 1: Provisional DataEnd ──
        int emIdx = 0;
        for (int i = 0; i < elements.Count; i++)
        {
            int nextPos = (i + 1 < elements.Count)
                ? elements[i + 1].TagOffset
                : data.Length;

            while (emIdx < endMarkerPositions.Count
                   && endMarkerPositions[emIdx] <= elements[i].TagOffset)
                emIdx++;

            if (emIdx < endMarkerPositions.Count
                && endMarkerPositions[emIdx] < nextPos)
            {
                nextPos = endMarkerPositions[emIdx];
            }

            elements[i].DataEnd = nextPos;
        }

        // ── Stage 1b: Fix-up for empty data regions ──
        for (int i = elements.Count - 2; i >= 0; i--)
        {
            if (elements[i].DataEnd == elements[i].DataStart
                && i + 2 < elements.Count)
            {
                elements[i].DataEnd = elements[i + 2].TagOffset;
            }
        }

        // ── Stage 2: Build tree hierarchy and extend DataEnd ──
        var children = new List<int>[elements.Count];
        for (int i = 0; i < elements.Count; i++)
            children[i] = new List<int>();

        var stack = new Stack<int>();
        emIdx = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            // Close scopes based on end markers
            while (emIdx < endMarkerPositions.Count
                   && endMarkerPositions[emIdx] < elements[i].TagOffset)
            {
                if (stack.Count > 0)
                    stack.Pop();
                emIdx++;
            }

            // Pop elements whose DataEnd doesn't cover this element
            while (stack.Count > 0 && elements[i].TagOffset > elements[stack.Peek()].DataEnd)
                stack.Pop();

            // Assign parent and establish bidirectional link
            if (stack.Count > 0)
            {
                int parentIdx = stack.Peek();
                children[parentIdx].Add(i);

                var parentElem = elements[parentIdx];
                elements[i].Parent = parentElem;
                parentElem.Children.Add(elements[i]);
                elements[i].Depth = stack.Count;

                if (elements[i].DataEnd > parentElem.DataEnd)
                    parentElem.DataEnd = elements[i].DataEnd;
            }

            stack.Push(i);
        }

        // Close remaining scopes
        while (emIdx < endMarkerPositions.Count)
        {
            if (stack.Count > 0)
                stack.Pop();
            emIdx++;
        }

        // ── Stage 3: Bottom-up DataEnd recalculation ──
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            if (children[i].Count > 0)
            {
                int lastChild = children[i][children[i].Count - 1];
                if (elements[lastChild].DataEnd > elements[i].DataEnd)
                    elements[i].DataEnd = elements[lastChild].DataEnd;
            }
        }

        return new ScanResult(elements, endMarkerPositions);
    }
}
