using System.Collections.Generic;

namespace SldFile.Mfc.Configs;

/// <summary>
/// Unified model for Contents/CMgr binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes moNodeName_c, moVisualProperties_c, gcXhatch_c, external references, and configurations.
/// </summary>
public sealed class CmgrModel : IMfcSerializable
{
    public const string NodeNameClassName = "moNodeName_c";
    public const string VisualPropertiesClassName = "moVisualProperties_c";
    public const string XHatchClassName = "gcXhatch_c";
    public const string CStringHandleClassName = "moCStringHandle_c";
    public const string ExtFeatureClassName = "moExtFeature_c";

    public string DisplayName { get; set; } = string.Empty;

    public CmgrVisualProperties? VisualProperties { get; set; }

    public string? HatchPattern { get; set; }

    public List<CmgrExternalRef> FilePaths { get; } = [];

    public List<string> ComponentNames { get; } = [];

    public List<CmgrConfigurationModel> Configurations { get; } = [];

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
