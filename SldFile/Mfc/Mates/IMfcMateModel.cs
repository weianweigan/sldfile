using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC serialization model for an assembly mate in Contents/Config-0-MatesList.
/// </summary>
internal interface IMfcMateModel : IMfcSerializable
{
    /// <summary>Exact runtime MFC class name serialized in the CArchive tag.</summary>
    string ClassName { get; }

    /// <summary>SolidWorks FeatureManager display name (e.g. "Width1", "Coincident1").</summary>
    string DisplayName { get; set; }

    /// <summary>Global SolidWorks feature type identifier (swFeatureNameID_e).</summary>
    uint TypeId { get; set; }

    /// <summary>Semantic mate type enum.</summary>
    AssemblyMateType MateType { get; }

    /// <summary>Mate alignment condition (Aligned, AntiAligned, Closest).</summary>
    AssemblyMateAlignment Alignment { get; set; }

    /// <summary>Internal mate condition flags.</summary>
    ushort MateFlags { get; set; }

    /// <summary>Topological entities constrained by this mate.</summary>
    IReadOnlyList<AssemblyMateEntityReference> EntityReferences { get; }

    /// <summary>Converts internal serialization model to public definition object.</summary>
    AssemblyMateDefinitionData ToDefinition();

    /// <summary>Populates internal serialization model from public definition object.</summary>
    void FromDefinition(AssemblyMateDefinitionData definition);

    /// <summary>Decoded structural information for backwards compatibility.</summary>
    MateStructureData? Structure { get; }

    /// <summary>Decoded reference wrapper information for backwards compatibility.</summary>
    IReadOnlyList<MateReferenceEntry> References { get; }

    /// <summary>Converts internal serialization model to legacy MateEntry structure.</summary>
    MateEntry ToMateEntry();

    /// <summary>Serializes the mate payload after class tag has been emitted.</summary>
    void SerializePayload(MfcArchive ar);

    /// <summary>Deserializes the mate payload after class tag has been consumed.</summary>
    void DeserializePayload(MfcArchive ar);

    /// <summary>Initializes model from a parsed MFC element.</summary>
    void InitializeFromElement(MfcElement element, byte[] sourceData);

    /// <summary>Initializes model from a legacy MateEntry.</summary>
    void InitializeFromMateEntry(MateEntry entry, byte[] sourceData);
}
