using System;

namespace SldFile.Binary;

/// <summary>
/// Represents the ModelStamps stream data.
/// Typical size is 12 bytes containing 3 Int32 fields.
/// </summary>
internal class ModelStamps
{
    public int Timestamp1 { get; set; }
    public int Timestamp2 { get; set; }
    public int Timestamp3 { get; set; }
    
    // Store original data if size != 12, to ensure roundtripping works
    internal byte[]? RawData { get; set; }
}
