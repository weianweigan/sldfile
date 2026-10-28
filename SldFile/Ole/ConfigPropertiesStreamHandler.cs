using System;
using System.Linq;
using OpenMcdf;

namespace SldFile.Ole;

internal sealed class ConfigPropertiesStreamReader
{
    public void Read(CfbStream stream, ConfigProperties model)
    {
        var bytes = ReadAllBytes(stream);
        ReadFromBytes(bytes, model);
    }

    public void Read(byte[] data, ConfigProperties model)
    {
        ReadFromBytes(data, model);
    }

    private static void ReadFromBytes(byte[] bytes, ConfigProperties model)
    {
        var ps = OlePropertySetSerializer.Parse(bytes);

        foreach (var prop in ps.UserDefinedProperties)
        {
            if (!string.IsNullOrEmpty(prop.Name))
            {
                model.CustomProperties[prop.Name] = new CustomProperty(
                    prop.Name,
                    prop.Value,
                    prop.VtType
                );
            }
        }
    }

    private static byte[] ReadAllBytes(CfbStream stream)
    {
        int len = checked((int)stream.Length);
        var bytes = new byte[len];
        int totalRead = 0;
        while (totalRead < len)
        {
            int read = stream.Read(bytes, totalRead, len - totalRead);
            if (read <= 0) break;
            totalRead += read;
        }
        return bytes;
    }
}
