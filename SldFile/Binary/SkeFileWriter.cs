using System;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Writes a <see cref="SkeFileData"/> model back to .ske file bytes.
/// Uses a clone-and-patch strategy: copies the original raw bytes and overlays
/// modified sections using exact MFC element locations, preserving unrecognized regions for lossless roundtrips.
/// </summary>
internal sealed class SkeFileWriter
{
    /// <summary>
    /// Serializes the model to a byte array. If RawData is available, uses
    /// clone-and-patch; otherwise builds from scratch.
    /// </summary>
    public byte[] Serialize(SkeFileData model)
    {
        if (model.RawData != null)
            return SerializeWithPatch(model);

        return SerializeFromScratch(model);
    }

    private static byte[] SerializeWithPatch(SkeFileData model)
    {
        var result = new byte[model.RawData!.Length];
        Array.Copy(model.RawData, result, result.Length);

        // Patch header u32
        var headerBytes = BitConverter.GetBytes(model.HeaderU32);
        Array.Copy(headerBytes, 0, result, 0, 4);

        // Patch SketchData if FeatureCount changed
        var sketch = model.Sketch;
        if (sketch != null && sketch.RawData.Length >= 4)
        {
            int dataStart = 18; // offset 4 (tag) + 4 (tag header) + 2 (name_len) + 8 ("sgSketch")
            var fcBytes = BitConverter.GetBytes(model.FeatureCount);
            if (dataStart + 4 <= result.Length)
            {
                Array.Copy(fcBytes, 0, result, dataStart, 4);
            }
        }

        // Use MfcDocument on the original bytes to get exact element boundaries
        var doc = MfcDocument.Parse(model.RawData);

        // 1. Patch moNodeName_c dimension display names
        var nodeNameElements = doc.AllElements.Where(e => e.ClassName == "moNodeName_c").ToList();
        var nodeNameDataList = model.FeatureData.OfType<NodeNameData>().ToList();
        for (int i = 0; i < Math.Min(nodeNameElements.Count, nodeNameDataList.Count); i++)
        {
            PatchElementWChar(result, nodeNameElements[i], nodeNameDataList[i].Name);
        }

        // 2. Patch moLengthParameter_c and moAngleParameter_c names
        var paramElements = doc.AllElements.Where(e => e.ClassName == "moLengthParameter_c" || e.ClassName == "moAngleParameter_c").ToList();
        var paramDataList = model.FeatureData.Where(fd => fd is LengthParameterCData || fd is AngleParameterCData).ToList();
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

    private static byte[] SerializeFromScratch(SkeFileData model)
    {
        var writer = new MfcStreamWriter();

        // Header u32
        writer.WriteU32(model.HeaderU32);

        // sgSketch tag + header
        writer.WriteMfcTag("sgSketch");
        writer.WriteU32(model.FeatureCount);
        writer.WriteU32(0);            // reserved
        writer.WriteU32(0x00010000);   // magic

        foreach (var fd in model.FeatureData)
        {
            writer.WriteMfcTag(fd.ClassName);

            switch (fd)
            {
                case NodeNameData nn:
                    writer.WriteWChar(nn.Name);
                    break;
                case LengthParameterCData lp:
                    writer.WriteWChar(lp.Name);
                    break;
                case AngleParameterCData ap:
                    writer.WriteWChar(ap.Name);
                    break;
                default:
                    // For other objects, RawData contains everything after the tag
                    writer.WriteBytes(CopyRawData(fd));
                    break;
            }
        }

        return writer.ToArray();
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
