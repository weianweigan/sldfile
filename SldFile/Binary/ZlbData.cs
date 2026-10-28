using System;
using System.Text;

namespace SldFile.Binary;

/// <summary>
/// Represents data inside a __ZLB stream.
/// The standard 24-byte prefix consists of a 16-byte GUID followed by
/// decompressed size (UInt32 LE) and compressed size (UInt32 LE).
/// <para>
/// XML vs binary classification is determined by inspecting the decompressed
/// content, not by the stream path. If the content starts with <c>&lt;?xml</c>
/// (after an optional UTF-8 BOM), it is treated as XML; otherwise as raw binary.
/// </para>
/// </summary>
internal class ZlbData
{
    private string _xmlContent = string.Empty;
    private byte[]? _rawData;

    /// <summary>
    /// XML text content. Only populated when <see cref="IsXml"/> is true.
    /// </summary>
    public string XmlContent
    {
        get => _xmlContent;
        set
        {
            _xmlContent = value ?? string.Empty;
            _rawData = null;
        }
    }

    internal byte[]? Prefix { get; set; }

    /// <summary>
    /// Raw decompressed bytes for binary (non-XML) ZLB streams.
    /// Only populated when <see cref="IsXml"/> is false.
    /// </summary>
    public byte[]? RawData
    {
        get => _rawData;
        internal set
        {
            _rawData = value;
            if (value != null)
                _xmlContent = string.Empty;
        }
    }

    /// <summary>
    /// Whether the decompressed content is XML. Computed from content, not path.
    /// </summary>
    public bool IsXml => RawData == null && !string.IsNullOrEmpty(_xmlContent);

    /// <summary>Decompressed size stored in the prefix (bytes 16–19).</summary>
    public int DecompressedSize =>
        Prefix != null && Prefix.Length >= 20
            ? BitConverter.ToInt32(Prefix, 16)
            : 0;

    /// <summary>Compressed size stored in the prefix (bytes 20–23).</summary>
    public int CompressedSize =>
        Prefix != null && Prefix.Length >= 24
            ? BitConverter.ToInt32(Prefix, 20)
            : 0;

    /// <summary>The known GUID at the start of the prefix (bytes 0–15) across all ZLB streams.</summary>
    public static readonly byte[] ExpectedGuid =
    {
        0x23, 0x1D, 0xD5, 0x71, 0xDA, 0x81, 0x48, 0xA2,
        0xA8, 0x58, 0x98, 0xB2, 0x1B, 0x89, 0xEF, 0x99
    };

    /// <summary>
    /// Inspects decompressed bytes and classifies as XML or binary based on content.
    /// If the data starts with a UTF-8 BOM followed by <c>&lt;?xml</c>, or directly
    /// with <c>&lt;?xml</c>, it is stored as <see cref="XmlContent"/>.
    /// Otherwise it is stored as <see cref="RawData"/>.
    /// </summary>
    internal void SetDecompressedContent(byte[] decodedData)
    {
        if (IsXmlContent(decodedData))
        {
            _xmlContent = Encoding.UTF8.GetString(decodedData);
            _rawData = null;
        }
        else
        {
            _rawData = decodedData;
            _xmlContent = string.Empty;
        }
    }

    private static bool IsXmlContent(byte[] data)
    {
        if (data == null || data.Length < 5)
            return false;

        int start = 0;

        // Skip UTF-8 BOM if present
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            start = 3;

        // Check for "<?xml"
        return data.Length >= start + 5 &&
               data[start] == '<' &&
               data[start + 1] == '?' &&
               data[start + 2] == 'x' &&
               data[start + 3] == 'm' &&
               data[start + 4] == 'l';
    }
}
