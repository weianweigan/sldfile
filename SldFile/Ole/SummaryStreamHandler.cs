using System;
using OpenMcdf;

namespace SldFile.Ole;

internal sealed class SummaryStreamReader : OleStreamReader<Summary>
{
    protected override string StreamName => "SummaryInformation";

    protected override void Parse(CfbStream stream, Summary model)
    {
        ParseBytes(ReadAllBytes(stream), model);
    }

    internal void ParseBytes(byte[] bytes, Summary model)
    {
        var ps = OlePropertySetSerializer.Parse(bytes);

        foreach (var prop in ps.Properties)
        {
            switch (prop.Id)
            {
                case 2:  model.Title       = prop.Value?.ToString(); break;
                case 3:  model.Subject     = prop.Value?.ToString(); break;
                case 4:  model.Author      = prop.Value?.ToString(); break;
                case 5:  model.Keywords    = prop.Value?.ToString(); break;
                case 6:  model.Comments    = prop.Value?.ToString(); break;
                case 8:  model.LastSavedBy = prop.Value?.ToString(); break;
                case 12: if (prop.Value is DateTime created)   model.Created   = created;   break;
                case 13: if (prop.Value is DateTime lastSaved) model.LastSaved = lastSaved; break;
            }
        }
    }
}
