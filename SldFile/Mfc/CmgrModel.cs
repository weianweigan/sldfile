using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/CMgr binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes moNodeName_c, moVisualProperties_c, gcXhatch_c, external references, and configurations.
/// </summary>
internal sealed class CmgrModel : IMfcSerializable
{
    public const string NodeNameClassName = "moNodeName_c";
    public const string VisualPropertiesClassName = "moVisualProperties_c";
    public const string XHatchClassName = "gcXhatch_c";
    public const string CStringHandleClassName = "moCStringHandle_c";
    public const string ExtFeatureClassName = "moExtFeature_c";

    public string DisplayName { get; set; } = string.Empty;
    public CmgrVisualProperties? VisualProperties { get; set; }
    public string? HatchPattern { get; set; }
    public List<CmgrExternalRef> FilePaths { get; } = new();
    public List<string> ComponentNames { get; } = new();
    public List<CmgrConfigurationModel> Configurations { get; } = new();

    public byte[]? RawData { get; set; }

    public CmgrModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            // Storing from scratch
            // 1. moNodeName_c
            ushort nodeTag = 0;
            ar.WriteClassTag(NodeNameClassName, ref nodeTag, schema: 1);
            string? name = DisplayName;
            ar.WChar(ref name);

            // 2. moVisualProperties_c
            if (VisualProperties != null)
            {
                ushort visTag = 0;
                ar.WriteClassTag(VisualPropertiesClassName, ref visTag, schema: 1);
                uint rgb = VisualProperties.ColorRgb;
                ar.U32(ref rgb);
                float transp = VisualProperties.Transparency;
                ar.F32(ref transp);
                float amb = VisualProperties.Ambient;
                ar.F32(ref amb);
                float diff = VisualProperties.Diffuse;
                ar.F32(ref diff);
                float spec = VisualProperties.Specular;
                ar.F32(ref spec);
                float shin = VisualProperties.Shininess;
                ar.F32(ref shin);
                float emiss = VisualProperties.Emission;
                ar.F32(ref emiss);
                float blend = VisualProperties.Blend;
                ar.F32(ref blend);
            }

            // 3. gcXhatch_c
            if (!string.IsNullOrEmpty(HatchPattern))
            {
                ushort hatchTag = 0;
                ar.WriteClassTag(XHatchClassName, ref hatchTag, schema: 1);
                string? hp = HatchPattern;
                ar.WChar(ref hp);
            }

            // 4. FilePaths (moCStringHandle_c)
            foreach (var ext in FilePaths)
            {
                ushort hTag = 0;
                ar.WriteClassTag(CStringHandleClassName, ref hTag, schema: 1);
                string? p = ext.FullPath;
                ar.WChar(ref p);

                if (!string.IsNullOrEmpty(ext.ShortName))
                {
                    ushort sep = 0x8017;
                    ar.U16(ref sep);
                    string? s = ext.ShortName;
                    ar.WChar(ref s);
                }
            }

            // 5. ComponentNames (moExtFeature_c)
            if (ComponentNames.Count > 0)
            {
                ushort extFTag = 0;
                ar.WriteClassTag(ExtFeatureClassName, ref extFTag, schema: 1);
                foreach (var cName in ComponentNames)
                {
                    ushort chTag = 0;
                    ar.WriteClassTag(CStringHandleClassName, ref chTag, schema: 1);
                    string? cn = cName;
                    ar.WChar(ref cn);
                }
            }

            // 6. Configurations (moConfiguration_c)
            foreach (var cfg in Configurations)
            {
                cfg.Serialize(ar);
            }
        }
        else
        {
            FilePaths.Clear();
            ComponentNames.Clear();
            Configurations.Clear();

            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var doc = MfcDocument.Parse(data);

            DisplayName = doc.FindFirst(NodeNameClassName)?.AsWChar() ?? string.Empty;

            if (doc.FindFirst(VisualPropertiesClassName) is { } visProps)
            {
                var reader = new MfcReader(data, visProps.DataStart);
                VisualProperties = new CmgrVisualProperties
                {
                    ColorRgb = reader.ReadU32(),
                    Transparency = reader.ReadF32(),
                    Ambient = reader.ReadF32(),
                    Diffuse = reader.ReadF32(),
                    Specular = reader.ReadF32(),
                    Shininess = reader.ReadF32(),
                    Emission = reader.ReadF32(),
                    Blend = reader.ReadF32()
                };
            }

            HatchPattern = doc.FindFirst(XHatchClassName)?.AsWChar();

            foreach (var element in doc.AllElements)
            {
                if (element.ClassName == CStringHandleClassName)
                {
                    var reader = element.CreateReader();
                    var extRef = new CmgrExternalRef
                    {
                        FullPath = reader.ReadWChar() ?? string.Empty
                    };

                    for (int i = reader.Offset; i < element.DataEnd - 3; i++)
                    {
                        if (data[i] == 0x17 && data[i + 1] == 0x80)
                        {
                            var shortReader = new MfcReader(data, i + 2, element.DataEnd - (i + 2));
                            extRef.ShortName = shortReader.ReadWChar() ?? string.Empty;
                            break;
                        }
                    }

                    FilePaths.Add(extRef);
                }
                else if (element.ClassName == ExtFeatureClassName)
                {
                    foreach (var child in doc.FindChildrenOf(element, CStringHandleClassName))
                    {
                        var cName = child.AsWChar();
                        if (!string.IsNullOrEmpty(cName))
                            ComponentNames.Add(cName!);
                    }
                }
                else if (element.ClassName == CmgrConfigurationModel.MfcClassName)
                {
                    var cfg = new CmgrConfigurationModel();
                    var r = element.CreateReader();
                    if (r.Remaining >= 4) cfg.ConfigId = r.ReadU32();
                    if (r.IsWCharPrefix) cfg.Name = r.ReadWChar() ?? string.Empty;
                    if (r.IsWCharPrefix) cfg.Description = r.ReadWChar() ?? string.Empty;
                    if (r.IsWCharPrefix) cfg.Comment = r.ReadWChar() ?? string.Empty;
                    if (r.Remaining >= 4)
                    {
                        uint pid = r.ReadU32();
                        cfg.ParentConfigId = pid == 0xFFFFFFFF ? null : pid;
                    }
                    if (r.Remaining >= 1) cfg.IsActive = r.ReadU8() != 0;
                    Configurations.Add(cfg);
                }
            }
        }
    }
}

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

internal sealed class CmgrVisualProperties
{
    public uint ColorRgb { get; set; }
    public float Transparency { get; set; }
    public float Ambient { get; set; }
    public float Diffuse { get; set; }
    public float Specular { get; set; }
    public float Shininess { get; set; }
    public float Emission { get; set; }
    public float Blend { get; set; }
}

internal sealed class CmgrExternalRef
{
    public string FullPath { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
}
