using System;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Reads the ResolvedFeatures stream and populates a <see cref="ResolvedFeaturesData"/> model.
/// Uses the MfcDocument tree model: scan → tree build → schema mapping.
/// </summary>
internal sealed class ResolvedFeaturesStreamReader : BinaryStreamReader<ResolvedFeaturesData>
{
    public override string StreamName => "ResolvedFeatures";

    public override void Parse(byte[] data, ResolvedFeaturesData model)
    {
        var doc = MfcDocument.Parse(data);
        ResolvedFeaturesSchemaMapper.Map(doc, model);
    }

    /// <summary>
    /// Internal entry point for parsing stream data directly from bytes
    /// (used when ResolvedFeatures is discovered inside a config storage).
    /// </summary>
    internal void ParseBytes(byte[] data, ResolvedFeaturesData model)
    {
        Parse(data, model);
    }
}
