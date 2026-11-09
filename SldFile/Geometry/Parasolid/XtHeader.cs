using System;
using System.Text;

namespace SldFile.Geometry.Parasolid;

public enum XtStreamKind
{
    Transmit,
    Partition,
    Deltas,
    Unknown
}

/// <summary>
/// Header information parsed from the PS\0\0 binary Parasolid transmit stream.
/// </summary>
public sealed class XtHeader
{
    public XtStreamKind Kind { get; set; } = XtStreamKind.Unknown;
    public string Description { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public int SchemaVersionMajor { get; set; }
    public int SchemaVersionMinor { get; set; }

    public static bool TryParse(byte[] data, out XtHeader header)
    {
        header = null!;
        if (data == null || data.Length < 16) return false;

        // Check PS\0\0 magic
        if (data[0] != (byte)'P' || data[1] != (byte)'S' || data[2] != 0 || data[3] != 0)
            return false;

        int descLen = (data[4] << 8) | data[5];
        if (descLen <= 0 || descLen > data.Length - 6) return false;

        string desc = Encoding.ASCII.GetString(data, 6, descLen);
        var kind = XtStreamKind.Unknown;
        if (desc.StartsWith(": TRANSMIT FILE (partition)", StringComparison.OrdinalIgnoreCase))
            kind = XtStreamKind.Partition;
        else if (desc.StartsWith(": TRANSMIT FILE (deltas)", StringComparison.OrdinalIgnoreCase))
            kind = XtStreamKind.Deltas;
        else if (desc.StartsWith(": TRANSMIT FILE", StringComparison.OrdinalIgnoreCase))
            kind = XtStreamKind.Transmit;

        header = new XtHeader
        {
            Kind = kind,
            Description = desc
        };

        // Scan for SCH_ tag
        int windowStart = 6 + descLen;
        int windowEnd = Math.Min(data.Length - 4, windowStart + 128);
        for (int i = windowStart; i < windowEnd; i++)
        {
            if (data[i] == (byte)'S' && data[i + 1] == (byte)'C' && data[i + 2] == (byte)'H' && data[i + 3] == (byte)'_')
            {
                int end = i + 4;
                while (end < data.Length && data[end] >= 0x20 && data[end] <= 0x7E && data[end] != ' ')
                {
                    end++;
                }
                header.Schema = Encoding.ASCII.GetString(data, i, end - i);
                break;
            }
        }

        return true;
    }
}
