using System;
using System.Linq;
using OpenMcdf;

namespace SldFile.Ole;

internal sealed class DocumentSummaryStreamReader : OleStreamReader<DocumentSummary>
{
    protected override string StreamName => "DocumentSummaryInformation";

    protected override void Parse(CfbStream stream, DocumentSummary model)
    {
        ParseBytes(ReadAllBytes(stream), model);
    }

    internal void ParseBytes(byte[] bytes, DocumentSummary model)
    {
        var ps = OlePropertySetSerializer.Parse(bytes);

        foreach (var prop in ps.Properties)
        {
            switch (prop.Id)
            {
                case 2:  model.Category = prop.Value?.ToString(); break;
                case 14: model.Manager  = prop.Value?.ToString(); break;
                case 15: model.Company  = prop.Value?.ToString(); break;
            }
        }

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
}
