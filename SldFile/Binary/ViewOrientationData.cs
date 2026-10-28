namespace SldFile.Binary;

/// <summary>
/// View Orientation Data stream: view orientation settings.
/// Located at Contents/View Orientation Data__ZLB (ZLB-compressed MFC stream).
/// Note: This stream is empty (prefix only, no compressed data) in all test files.
/// </summary>
internal sealed class ViewOrientationData
{
    /// <summary>Whether this stream has decompressed data.</summary>
    public bool HasData { get; internal set; }

    internal byte[]? RawData;
}
