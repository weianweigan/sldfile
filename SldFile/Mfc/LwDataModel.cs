using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/Config-0-LWDATA binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes type flag, body transform data, gcXhatch_c, and moLWPlaneNodeData_c entries.
/// </summary>
internal sealed class LwDataModel : IMfcSerializable
{
    public const string XHatchClassName = "gcXhatch_c";
    public const string LWPlaneNodeDataClassName = "moLWPlaneNodeData_c";
    public const uint TypeFlagPart = 0x8F;
    public const uint TypeFlagAssembly = 0x75;

    public uint TypeFlag { get; set; }
    public byte[] BodyTransformData { get; set; } = Array.Empty<byte>();
    public string? HatchPattern { get; set; }
    public List<LwPlaneNodeData> PlaneNodes { get; } = new();

    public byte[]? RawData { get; set; }

    public LwDataModel() { }



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
            uint tf = TypeFlag;
            ar.U32(ref tf);

            if (BodyTransformData != null && BodyTransformData.Length > 0)
            {
                ar.WriteBytes(BodyTransformData);
            }

            if (!string.IsNullOrEmpty(HatchPattern))
            {
                ushort hatchTag = 0;
                ar.WriteClassTag(XHatchClassName, ref hatchTag, schema: 1);
                string? hp = HatchPattern;
                ar.WChar(ref hp);
            }

            ushort planeTag = 0;
            foreach (var node in PlaneNodes)
            {
                ar.WriteClassTag(LWPlaneNodeDataClassName, ref planeTag, schema: 1);
                string? pcn = node.PlaneClassName;
                ar.WChar(ref pcn);
                string? dcn = node.DataClassName;
                ar.WChar(ref dcn);
                string? fi = node.FeatureIdentifier;
                ar.WChar(ref fi);

                if (node.TransformMatrix != null && node.TransformMatrix.Length > 0)
                {
                    ar.WriteBytes(node.TransformMatrix);
                }
            }
        }
        else
        {
            PlaneNodes.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length < 4) return;

            var reader = new MfcReader(data);
            TypeFlag = reader.ReadU32();

            var doc = MfcDocument.Parse(data);
            if (doc.AllElements.Count > 0 && doc.AllElements[0].TagOffset > reader.Offset)
            {
                int firstTagOffset = doc.AllElements[0].TagOffset;
                BodyTransformData = reader.ReadBytes(firstTagOffset - reader.Offset);
            }

            HatchPattern = doc.FindFirst(XHatchClassName)?.AsWChar();

            foreach (var element in doc.AllElements)
            {
                if (element.ClassName == LWPlaneNodeDataClassName)
                {
                    var nodeReader = element.CreateReader();
                    var node = new LwPlaneNodeData
                    {
                        PlaneClassName = nodeReader.ReadWChar() ?? string.Empty,
                        DataClassName = nodeReader.ReadWChar() ?? string.Empty,
                        FeatureIdentifier = nodeReader.ReadWChar() ?? string.Empty,
                        TransformMatrix = nodeReader.ReadRemainingBytes()
                    };

                    PlaneNodes.Add(node);
                }
            }
        }
    }
}

internal sealed class LwPlaneNodeData
{
    public string PlaneClassName { get; set; } = string.Empty;
    public string DataClassName { get; set; } = string.Empty;
    public string FeatureIdentifier { get; set; } = string.Empty;
    public byte[] TransformMatrix { get; set; } = Array.Empty<byte>();
}