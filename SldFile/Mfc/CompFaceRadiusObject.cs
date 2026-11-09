using System;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Strongly-typed bi-directional model for moCompFace_c in fillet definitions.
/// Encapsulates the radius slot (offset 582) inside the component face metadata.
/// </summary>
public sealed class CompFaceRadiusObject : IMfcSerializable
{
    public const string ClassName = "moCompFace_c";
    public const int RadiusSlot = 582;

    public double Radius { get; set; }
    public byte[] TemplatePayload { get; set; }

    public CompFaceRadiusObject(byte[] templatePayload, double radius)
    {
        TemplatePayload = templatePayload ?? throw new ArgumentNullException(nameof(templatePayload));
        Radius = radius;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            var data = (byte[])TemplatePayload.Clone();
            if (RadiusSlot + 8 <= data.Length)
            {
                BitConverter.GetBytes(Radius).CopyTo(data, RadiusSlot);
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);
            ar.WriteBytes(data);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);
            TemplatePayload = ar.ReadRemainingBytes();
            if (TemplatePayload.Length >= RadiusSlot + 8)
            {
                Radius = BitConverter.ToDouble(TemplatePayload, RadiusSlot);
            }
        }
    }
}
