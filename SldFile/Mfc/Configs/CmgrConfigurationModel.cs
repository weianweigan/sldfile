using System;
using System.Collections.Generic;

namespace SldFile.Mfc.Configs;

/// <summary>
/// Direct strongly-typed MFC model for configuration entity (<c>moConfiguration_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// Tracks derived configuration relationship, description, comments, and feature suppression state matrix.
/// </summary>
public sealed class CmgrConfigurationModel : IMfcSerializable
{
    public const string MfcClassName = "moConfiguration_c";

    public uint ConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public uint? ParentConfigId { get; set; }
    public bool IsDerived => ParentConfigId.HasValue && ParentConfigId.Value != 0xFFFFFFFF;
    public bool IsActive { get; set; }

    /// <summary>
    /// Feature suppression dictionary: maps FeatureName to true (Suppressed) or false (Unsuppressed).
    /// </summary>
    public Dictionary<string, bool> FeatureSuppressionStates { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Configuration dimension override dictionary: maps DimensionName to value.
    /// </summary>
    public Dictionary<string, double> DimensionOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public byte[]? RawData { get; set; }

    public CmgrConfigurationModel() { }

    public CmgrConfigurationModel(string name, uint id = 0, uint? parentId = null)
    {
        Name = name;
        ConfigId = id;
        ParentConfigId = parentId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            uint cid = ConfigId; ar.U32(ref cid);
            string? name = Name; ar.WChar(ref name);
            string? desc = Description; ar.WChar(ref desc);
            string? comm = Comment; ar.WChar(ref comm);

            uint pid = ParentConfigId ?? 0xFFFFFFFF; ar.U32(ref pid);
            bool act = IsActive; ar.Boolean(ref act);

            ushort suppCount = (ushort)FeatureSuppressionStates.Count;
            ar.U16(ref suppCount);
            foreach (var kvp in FeatureSuppressionStates)
            {
                string? featName = kvp.Key; ar.WChar(ref featName);
                bool supp = kvp.Value; ar.Boolean(ref supp);
            }

            ushort dimCount = (ushort)DimensionOverrides.Count;
            ar.U16(ref dimCount);
            foreach (var kvp in DimensionOverrides)
            {
                string? dimName = kvp.Key; ar.WChar(ref dimName);
                double val = kvp.Value; ar.F64(ref val);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            uint cid = 0; ar.U32(ref cid); ConfigId = cid;
            string? name = null; ar.WChar(ref name); Name = name ?? string.Empty;
            string? desc = null; ar.WChar(ref desc); Description = desc ?? string.Empty;
            string? comm = null; ar.WChar(ref comm); Comment = comm ?? string.Empty;

            uint pid = 0; ar.U32(ref pid); ParentConfigId = pid == 0xFFFFFFFF ? null : pid;
            bool act = false; ar.Boolean(ref act); IsActive = act;

            ushort suppCount = 0; ar.U16(ref suppCount);
            FeatureSuppressionStates.Clear();
            for (int i = 0; i < suppCount; i++)
            {
                string? fName = null; ar.WChar(ref fName);
                bool supp = false; ar.Boolean(ref supp);
                if (fName != null) FeatureSuppressionStates[fName] = supp;
            }

            ushort dimCount = 0; ar.U16(ref dimCount);
            DimensionOverrides.Clear();
            for (int i = 0; i < dimCount; i++)
            {
                string? dName = null; ar.WChar(ref dName);
                double dVal = 0; ar.F64(ref dVal);
                if (dName != null) DimensionOverrides[dName] = dVal;
            }
        }
    }
}
