using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Declarative schema builder for parsing SolidWorks feature definitions from a
/// <see cref="FeatureWindowContext"/>. Automatically validates required sub-nodes,
/// extracts named parameters, checks bounds, and marks resolved properties on
/// <see cref="MfcFeatureModelBase"/>.
/// </summary>
internal sealed class FeatureSchema<TDef> where TDef : MfcFeatureModelBase, new()
{
    private readonly string _className;
    private readonly uint _typeId;
    private readonly ushort? _streamVersion;
    private readonly List<Func<FeatureWindowContext, TDef, bool>> _steps = new();

    public FeatureSchema(string className, uint typeId, ushort? streamVersion = null)
    {
        _className = className;
        _typeId = typeId;
        _streamVersion = streamVersion;
    }

    public FeatureSchema<TDef> RequireSingleNode(string subClassName, int? expectedLength = null)
    {
        _steps.Add((ctx, _) => expectedLength.HasValue
            ? ctx.HasSingleNodeWithLength(subClassName, expectedLength.Value)
            : ctx.GetSingleNode(subClassName) != null);
        return this;
    }

    public FeatureSchema<TDef> RejectIfAnyNode(params string[] forbiddenClassNames)
    {
        _steps.Add((ctx, _) =>
        {
            foreach (var node in ctx.Nodes)
            {
                foreach (string forbidden in forbiddenClassNames)
                {
                    if (node.ClassName == forbidden)
                        return false;
                }
            }
            return true;
        });
        return this;
    }

    public FeatureSchema<TDef> BindUniqueDouble(
        string paramName,
        string propertyName,
        Action<TDef, double> setter,
        double minExclusive = 0.0,
        double maxInclusive = double.MaxValue)
    {
        _steps.Add((ctx, def) =>
        {
            if (!ctx.TryGetUniqueDoubleParameter(paramName, out double val))
                return false;
            if (double.IsNaN(val) || double.IsInfinity(val) || val <= minExclusive || val > maxInclusive)
                return false;
            setter(def, val);
            def.MarkResolvedInternal(propertyName);
            return true;
        });
        return this;
    }

    public FeatureSchema<TDef> BindSingleEdgeReference(
        string propertyName,
        Action<TDef, FeatureReferenceData> setter)
    {
        _steps.Add((ctx, def) =>
        {
            var edgeRef = ctx.TryReadSingleEdgeReference();
            if (edgeRef == null) return false;
            setter(def, edgeRef);
            def.MarkResolvedInternal(propertyName);
            return true;
        });
        return this;
    }

    public FeatureSchema<TDef> CustomRule(Func<FeatureWindowContext, TDef, bool> rule)
    {
        if (rule != null)
            _steps.Add(rule);
        return this;
    }

    public TDef? TryParse(MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreate(
            doc, className, typeId, start, end,
            expectedClassName: _className,
            expectedTypeId: _typeId,
            expectedStreamVersion: _streamVersion);
        if (ctx == null)
            return null;

        var def = new TDef();
        foreach (var step in _steps)
        {
            if (!step(ctx, def))
                return null;
        }
        return def;
    }
}
