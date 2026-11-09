using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Path Mate (moMatePath_c, TypeId 15 / 0x0F).
/// Serializes a follower point entity, entity help chains, and a 3D path curve.
/// </summary>
internal sealed class PathMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMatePath_c";
    public const uint FolderTypeId = 15u; // swMatePATH

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Path;

    public double PathValue { get; set; } = 0.0; // 0.0 = Free
    public bool PathIsFree => PathValue == 0.0;

    // Follower Point Entity
    public string FollowerComponentInstance { get; set; } = string.Empty;
    public string FollowerSelectionPath { get; set; } = string.Empty;

    public string FollowerEntity
    {
        get => FollowerSelectionPath;
        set { FollowerSelectionPath = value ?? string.Empty; FollowerComponentInstance = value ?? string.Empty; }
    }

    // Path Curve Reference
    public string PathCurveName { get; set; } = string.Empty;

    public string PathEntity
    {
        get => PathCurveName;
        set => PathCurveName = value ?? string.Empty;
    }

    public PathMateModel()
    {
        TypeId = FolderTypeId;
    }

    public void SetEntities(string followerPoint, string pathCurve)
    {
        FollowerEntity = followerPoint;
        PathEntity = pathCurve;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        // 1. Follower Point Entity Wrapper
        ushort ptRefTag = 0;
        ar.ClassTagOrRef("moPointRef_w", ref ptRefTag);

        ushort compPtTag = 0;
        ar.ClassTagOrRef("moCompRefPoint_c", ref compPtTag);

        string? compName = FollowerComponentInstance;
        ar.WChar(ref compName);
        if (ar.IsLoading && compName != null) FollowerComponentInstance = compName;

        string? selPath = FollowerSelectionPath;
        ar.WChar(ref selPath);
        if (ar.IsLoading && selPath != null) FollowerSelectionPath = selPath;

        ushort refPtTag = 0;
        ar.ClassTagOrRef("moRefPoint_c", ref refPtTag);

        ushort refPtDataTag = 0;
        ar.ClassTagOrRef("moRefPointData_c", ref refPtDataTag);

        // 2. Entity Help & Chain
        ushort helpTag = 0;
        ar.ClassTagOrRef("moMateEntityHelpData_c", ref helpTag);

        ushort chainCompTag = 0;
        ar.ClassTagOrRef("moChainComponent_c", ref chainCompTag);

        ushort refChainTag = 0;
        ar.ClassTagOrRef("moReferenceChain_c", ref refChainTag);

        ushort extFeatTag = 0;
        ar.ClassTagOrRef("moExtFeatureList_c", ref extFeatTag);

        // 3. Path Curve Entity Wrapper
        ushort curveRefTag = 0;
        ar.ClassTagOrRef("moGeneralCurveRef_w", ref curveRefTag);

        ushort entGrpTag = 0;
        ar.ClassTagOrRef("moCompEntGroup_c", ref entGrpTag);

        ushort compProfTag = 0;
        ar.ClassTagOrRef("moCompProfile_c", ref compProfTag);

        ushort chooserTag = 0;
        ar.ClassTagOrRef("moContourChooser_c", ref chooserTag);

        ushort sktChainTag = 0;
        ar.ClassTagOrRef("moSketchChain_c", ref sktChainTag);

        string? curve = PathCurveName;
        ar.WChar(ref curve);
        if (ar.IsLoading && curve != null) PathCurveName = curve;
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        ushort analysisTag = 0;
        ar.ClassTagOrRef("moMateAnalysisData_c", ref analysisTag);

        ushort dispPathTag = 0;
        ar.ClassTagOrRef("moDisplayPathDim_c", ref dispPathTag);

        ushort paramTag = 0;
        ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

        string? d1Name = "D1";
        ar.WChar(ref d1Name);

        double val = PathValue;
        ar.F64(ref val);
        if (ar.IsLoading) PathValue = val;
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new PathMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            PathValue = PathValue,
            ComponentPointEntity = FollowerSelectionPath,
            PathCurveEntity = PathCurveName
        };
        if (!string.IsNullOrEmpty(FollowerComponentInstance))
            def.EntitiesToMate.Add(FollowerComponentInstance);
        if (!string.IsNullOrEmpty(PathCurveName))
            def.EntitiesToMate.Add(PathCurveName);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not PathMateDefinitionData def)
            throw new ArgumentException("Definition must be PathMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        PathValue = def.PathValue;
        FollowerSelectionPath = def.ComponentPointEntity;
        PathCurveName = def.PathCurveEntity;

        if (def.ComponentPointEntity.Contains("@"))
        {
            int atIndex = def.ComponentPointEntity.IndexOf('@');
            FollowerComponentInstance = def.ComponentPointEntity.Substring(atIndex + 1);
        }
        else if (def.EntitiesToMate.Count > 0)
        {
            FollowerComponentInstance = def.EntitiesToMate[0];
        }
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.Path2026;
        data.MateTypeId = 15u;
        data.PathValue = PathValue;
        data.PathIsFree = PathIsFree;

        string feature = FollowerSelectionPath.Contains("@")
            ? FollowerSelectionPath.Substring(0, FollowerSelectionPath.IndexOf('@'))
            : string.Empty;

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.SketchEntity,
                ComponentInstanceName = FollowerComponentInstance,
                FeatureName = feature,
                SelectionName = FollowerSelectionPath
            },
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Path,
                SelectionName = PathCurveName
            }
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.PathValue.HasValue)
            PathValue = structure.PathValue.Value;

        if (structure.EntityReferences.Count > 0)
        {
            var first = structure.EntityReferences[0];
            FollowerComponentInstance = first.ComponentInstanceName;
            FollowerSelectionPath = first.SelectionName;
        }
        if (structure.EntityReferences.Count > 1)
        {
            PathCurveName = structure.EntityReferences[1].SelectionName;
        }
    }
}
