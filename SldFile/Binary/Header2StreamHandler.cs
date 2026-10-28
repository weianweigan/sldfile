using System;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Reads the Header2 stream and populates a <see cref="Header2Data"/> model.
/// Uses the MfcDocument tree model: scan → tree build → schema mapping.
/// </summary>
internal sealed class Header2StreamReader : BinaryStreamReader<Header2Data>
{
    public override string StreamName => "Header2";

    public override void Parse(byte[] data, Header2Data model)
    {
        var doc = MfcDocument.Parse(data);
        Header2SchemaMapper.Map(doc, model);
    }
}
