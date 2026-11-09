using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for SolidWorks .ske sketch files using MFC CArchive serialization.
/// Symmetrically reads and writes header metadata, sgSketch hierarchy, handles, and dimensions.
/// </summary>
internal sealed class SkeFileModel : IMfcSerializable
{
    private static readonly HashSet<string> KnownSkeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "sgSketch", "sgLineHandle", "sgPointHandle",
        "sgPntLineDist", "sgPntPntDist", "sgLLDist", "sgAnglDim",
        "moLengthParameter_c", "moAngleParameter_c",
        "moNodeName_c",
        "moDisplayDistanceDim_c", "moDisplayAngularDim_c",
        "moSkDimHandleValG2_c", "suObList",
    };

    public uint HeaderU32 { get; set; }
    public uint FeatureCount { get; set; }
    public List<IFeatureData> FeatureData { get; } = new();

    public byte[]? RawData { get; set; }

    public SkeFileModel() { }

    public SkeFileModel(SkeFileData data)
    {
        if (data == null) return;
        HeaderU32 = data.HeaderU32;
        FeatureCount = data.FeatureCount;
        FeatureData.AddRange(data.FeatureData);
        RawData = data.RawData;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null)
            {
                byte[] patched = PatchRawBytes();
                ar.WriteBytes(patched);
                return;
            }

            // Storing from scratch
            uint hu32 = HeaderU32;
            ar.U32(ref hu32);

            ushort sketchTag = 0;
            ar.WriteClassTag("sgSketch", ref sketchTag, schema: 1);
            uint fc = FeatureCount;
            ar.U32(ref fc);
            uint reserved = 0;
            ar.U32(ref reserved);
            uint magic = 0x00010000;
            ar.U32(ref magic);

            foreach (var fd in FeatureData)
            {
                ushort tag = 0;
                ar.WriteClassTag(fd.ClassName, ref tag, schema: 1);

                switch (fd)
                {
                    case NodeNameData nn:
                        string? nnName = nn.Name;
                        ar.WChar(ref nnName);
                        break;
                    case LengthParameterCData lp:
                        string? lpName = lp.Name;
                        ar.WChar(ref lpName);
                        break;
                    case AngleParameterCData ap:
                        string? apName = ap.Name;
                        ar.WChar(ref apName);
                        break;
                    default:
                        byte[] raw = CopyRawData(fd);
                        if (raw.Length > 0)
                            ar.WriteBytes(raw);
                        break;
                }
            }
        }
        else
        {
            FeatureData.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length < 4) return;

            HeaderU32 = BitConverter.ToUInt32(data, 0);

            var doc = MfcDocument.Parse(data);
            foreach (var elem in doc.AllElements)
            {
                if (!KnownSkeNames.Contains(elem.ClassName))
                    continue;

                if (FeatureDataRegistry.TryParse(elem, out var fd) && fd != null)
                {
                    FeatureData.Add(fd);

                    if (fd is SketchData sk)
                        FeatureCount = sk.EntityCount;
                }
            }
        }
    }

    private byte[] PatchRawBytes()
    {
        var result = new byte[RawData!.Length];
        Array.Copy(RawData, result, result.Length);

        var headerBytes = BitConverter.GetBytes(HeaderU32);
        Array.Copy(headerBytes, 0, result, 0, 4);

        var sketch = FeatureData.OfType<SketchData>().FirstOrDefault();
        if (sketch != null && sketch.RawData.Length >= 4)
        {
            int dataStart = 18;
            var fcBytes = BitConverter.GetBytes(FeatureCount);
            if (dataStart + 4 <= result.Length)
            {
                Array.Copy(fcBytes, 0, result, dataStart, 4);
            }
        }

        var doc = MfcDocument.Parse(RawData);

        var nodeNameElements = doc.AllElements.Where(e => e.ClassName == "moNodeName_c").ToList();
        var nodeNameDataList = FeatureData.OfType<NodeNameData>().ToList();
        for (int i = 0; i < Math.Min(nodeNameElements.Count, nodeNameDataList.Count); i++)
        {
            PatchElementWChar(result, nodeNameElements[i], nodeNameDataList[i].Name);
        }

        var paramElements = doc.AllElements.Where(e => e.ClassName == "moLengthParameter_c" || e.ClassName == "moAngleParameter_c").ToList();
        var paramDataList = FeatureData.Where(fd => fd is LengthParameterCData || fd is AngleParameterCData).ToList();
        for (int i = 0; i < Math.Min(paramElements.Count, paramDataList.Count); i++)
        {
            string name = (paramDataList[i] is LengthParameterCData lp) ? lp.Name : ((AngleParameterCData)paramDataList[i]).Name;
            PatchElementWChar(result, paramElements[i], name);
        }

        return result;
    }

    private static void PatchElementWChar(byte[] data, MfcElement elem, string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;
        int p = elem.DataStart;
        while (p + 3 <= elem.DataEnd && !MfcPrimitives.IsWCharPrefix(data, p))
            p++;

        if (p + 3 > elem.DataEnd) return;

        byte origCount = data[p + 3];
        byte[] utf16 = Encoding.Unicode.GetBytes(newName);
        int newCount = Math.Min((int)origCount, utf16.Length / 2);

        data[p + 3] = (byte)newCount;
        Array.Copy(utf16, 0, data, p + 4, newCount * 2);
    }

    private static byte[] CopyRawData(IFeatureData fd)
    {
        return fd switch
        {
            SketchData sk => sk.RawData,
            LineHandleData lh => lh.RawData,
            PointHandleData ph => ph.RawData,
            PntLineDistData pl => pl.RawData,
            PntPntDistData pp => pp.RawData,
            LLDistanceData ll => ll.RawData,
            AngularDimData ad => ad.RawData,
            DisplayDistanceDimData dd => dd.RawData,
            DisplayAngularDimData da => da.RawData,
            SkDimHandleValG2Data sv => sv.RawData,
            ObListData ol => ol.RawData,
            _ => Array.Empty<byte>(),
        };
    }
}
