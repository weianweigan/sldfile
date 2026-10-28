using System;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Reads a .ske file (SolidWorks sketch-based feature definition) and populates
/// a <see cref="SkeFileData"/> model.
///
/// Uses the MfcDocument tree model: scan → tree build → schema mapping.
/// The <see cref="SkeSchemaMapper"/> walks the tree, dispatches each recognized
/// named element through <see cref="FeatureDataRegistry"/>, and populates
/// typed <see cref="IFeatureData"/> instances with RawData preserved for roundtrip.
/// </summary>
internal sealed class SkeFileReader
{
    /// <summary>
    /// Parses raw .ske file bytes into a <see cref="SkeFileData"/> model.
    /// </summary>
    public SkeFileData Parse(byte[] data)
    {
        var model = new SkeFileData();
        var doc = MfcDocument.Parse(data);
        SkeSchemaMapper.Map(doc, model);
        return model;
    }
}
