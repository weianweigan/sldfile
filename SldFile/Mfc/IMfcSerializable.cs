using System;

namespace SldFile.Mfc;

/// <summary>
/// Defines a contract for MFC CArchive bi-directional serialization.
/// Implementing classes can read from or write to an <see cref="MfcArchive"/> using a unified schema.
/// </summary>
public interface IMfcSerializable
{
    /// <summary>
    /// Serializes or deserializes object state based on <see cref="MfcArchive.IsStoring"/>.
    /// </summary>
    void Serialize(MfcArchive ar);
}

/// <summary>
/// Base contract for MFC feature models representing SolidWorks features.
/// </summary>
public interface IMfcFeatureModel : IFeatureData, IMfcSerializable
{
}

public static class MfcSerializableExtensions
{
    /// <summary>
    /// Deserializes object state from an <see cref="MfcArchive"/> in loading mode.
    /// </summary>
    public static void Deserialize(this IMfcSerializable obj, MfcArchive ar) => obj.Serialize(ar);
}
