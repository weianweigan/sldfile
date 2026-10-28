using System;
using System.Collections.Generic;
using OpenMcdf;
using SldFile.Diagnostics;

namespace SldFile.Ole;

/// <summary>
/// Reads the proprietary "ISolidWorksInformation" stream using custom binary parsing.
/// SolidWorks writes duplicate dictionary IDs and UTF-8 in VT_LPSTR fields,
/// both of which are non-standard for OLE property sets. This reader uses
/// <see cref="OlePropertySetReader"/> for safe, bounds-checked access.
/// </summary>
internal sealed class SolidWorksInformationStreamReader : OleStreamReader<SolidWorksInformation>
{
    protected override string StreamName => "ISolidWorksInformation";

    protected override CfbStream? OpenStream(RootStorage root)
    {
        if (root.TryOpenStream(StreamName, out var stream))
            return stream;

        return null;
    }

    protected override void Parse(CfbStream stream, SolidWorksInformation model)
    {
        ParseBytes(ReadAllBytes(stream), model);
    }

    internal void ParseBytes(byte[] bytes, SolidWorksInformation model)
    {
        OlePropertySetReader reader;
        try
        {
            reader = new OlePropertySetReader(bytes, StreamName);
        }
        catch (StreamCorruptedException)
        {
            return;
        }

        foreach (var (pId, absOff) in reader.EnumerateProperties())
        {
            if (pId == 0 || pId == 1) continue;

            int vtType = reader.ReadVtType(absOff);

            try
            {
                switch (vtType)
                {
                    case 30: // VT_LPSTR
                        MapStringProperty(model, pId, reader.ReadLpstr(absOff, pId));
                        break;

                    case 3: // VT_I4
                        MapInt32Property(model, pId, reader.ReadInt32(absOff, pId));
                        break;
                }
            }
            catch (PropertyReadException)
            {
                continue;
            }
        }
    }

    private static void MapStringProperty(SolidWorksInformation model, uint pId, string value)
    {
        switch (pId)
        {
            case 3:  model.FileName          = value; break;
            case 4:  model.FolderName        = value; break;
            case 5:  model.ShortDate         = value; break;
            case 6:  model.LongDate          = value; break;
            case 7:  model.ConfigurationName = value; break;
            case 8:  model.Author            = value; break;
            case 9:  model.Keywords          = value; break;
            case 10: model.Comments          = value; break;
            case 11: model.Title             = value; break;
            case 12: model.Subject           = value; break;
            case 13: model.CreatedDate       = value; break;
            case 14: model.LastSavedDate     = value; break;
            case 15: model.LastSavedBy       = value; break;
            case 16: model.MassPropConfig0   = value; break;
            default: model.CustomProperties[pId] = value; break;
        }
    }

    private static void MapInt32Property(SolidWorksInformation model, uint pId, int value)
    {
        switch (pId)
        {
            case 17: model.HasDesignTable = value; break;
            case 31: model.ComponentType  = value; break;
            default: model.CustomProperties[pId] = value; break;
        }
    }
}
