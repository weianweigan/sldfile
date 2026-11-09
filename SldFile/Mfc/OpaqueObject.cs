using System;

namespace SldFile.Mfc;

/// <summary>
/// Represents an unparsed opaque byte block for unknown MFC objects or feature bodies.
/// Retains exact original binary data to guarantee 100% byte-accurate round-trip serialization.
/// </summary>
public sealed class OpaqueObject : IMfcSerializable
{
    public string ClassName { get; set; } = string.Empty;
    public byte[] RawData { get; set; } = Array.Empty<byte>();

    public OpaqueObject() { }

    public OpaqueObject(string className, byte[] rawData)
    {
        ClassName = className ?? string.Empty;
        RawData = rawData ?? Array.Empty<byte>();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ar.WriteBytes(RawData);
        }
        else
        {
            // Reading opaque object directly consumes remaining segment or known length
            RawData = ar.ReadRemainingBytes();
        }
    }

    public override string ToString() => $"OpaqueObject<{ClassName}> ({RawData.Length} bytes)";
}
