using System;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Strongly-typed bi-directional model for ThreeDRadiusDim_c radius dimension object.
/// Encapsulates the radius value and synchronized positive/negative display copies.
/// Eliminates raw slot-based hardcoded offsets in fillet serializers.
/// </summary>
public sealed class ThreeDRadiusDimObject : IMfcSerializable
{
    public const string ClassName = "ThreeDRadiusDim_c";

    public double Radius { get; set; }
    public byte[] TemplatePayload { get; set; }

    public ThreeDRadiusDimObject(byte[] templatePayload, double radius)
    {
        TemplatePayload = templatePayload ?? throw new ArgumentNullException(nameof(templatePayload));
        Radius = radius;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            var data = (byte[])TemplatePayload.Clone();
            // Positive slots: 38, 318, 552, 576, 1024
            int[] posSlots = { 38, 318, 552, 576, 1024 };
            foreach (int slot in posSlots)
            {
                if (slot + 8 <= data.Length)
                    BitConverter.GetBytes(Radius).CopyTo(data, slot);
            }
            // Negative slot: 1056
            if (1056 + 8 <= data.Length)
                BitConverter.GetBytes(-Radius).CopyTo(data, 1056);

            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);
            ar.WriteBytes(data);
        }
        else
        {
            // Reading
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);
            TemplatePayload = ar.ReadRemainingBytes();
            if (TemplatePayload.Length >= 46)
            {
                Radius = BitConverter.ToDouble(TemplatePayload, 38);
            }
        }
    }
}
