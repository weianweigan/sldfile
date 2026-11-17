using System;
using System.IO;

namespace SldFile.Mfc;

internal sealed class NativeDrawingLineFontConfigModel : IMfcSerializable
{
    internal string? Name;
    internal NativeDrawingLineWidthModel Weight = new NativeDrawingLineWidthModel { RestoreType = 2 };
    internal ushort EndCap;

    // sldmfcu.dll uiLFConfig_c::Serialize 0x3CA50B60.
    public void Serialize(MfcArchive ar)
    {
        DrawingPreviewRecordModel.String(ar, ref Name);
        Weight.Serialize(ar);
        if (ar.DocumentVersion >= 0xC9F) ar.U16(ref EndCap);
    }
}

internal sealed class NativeDrawingLineFontManagerModel : IMfcSerializable
{
    internal sealed class Entry
    {
        internal string? Key;
        internal NativeDrawingObjectGraph.Node? Configuration;
    }
    internal Entry[] Entries = Array.Empty<Entry>();

    // slduiu.dll uiLineFontMgr_c::Serialize 0x49DB76F0.
    public void Serialize(MfcArchive ar)
    {
        if (Entries.Length > ushort.MaxValue) throw new InvalidDataException("Too many native line-font configurations.");
        ushort count = (ushort)Entries.Length; ar.U16(ref count);
        if (ar.IsLoading) Entries = new Entry[count];
        for (int i = 0; i < Entries.Length; i++)
        {
            if (ar.IsLoading) Entries[i] = new Entry();
            DrawingPreviewRecordModel.String(ar, ref Entries[i].Key);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Entries[i].Configuration);
        }
    }
}

internal sealed class NativeDrawingLayerManagerModel : IMfcSerializable
{
    internal sealed class NameEntry { internal int Id; internal string? Name; }
    internal sealed class StateEntry { internal int Id, State; }
    internal NameEntry[] Names = Array.Empty<NameEntry>();
    internal StateEntry[] States = Array.Empty<StateEntry>();

    // slduiu.dll uiLayerMgr_c::Serialize 0x49E1BC50. Before 0x1BE5,
    // its two integer maps use CArchive::WriteCount/ReadCount; later maps use i32.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion <= 0) throw new InvalidDataException("A native version is required for layer maps.");
        int names = Names.Length; Count(ar, ref names);
        if (ar.IsLoading) Names = new NameEntry[names];
        for (int i = 0; i < Names.Length; i++)
        {
            if (ar.IsLoading) Names[i] = new NameEntry();
            ar.I32(ref Names[i].Id); DrawingPreviewRecordModel.String(ar, ref Names[i].Name);
        }
        int states = States.Length; Count(ar, ref states);
        if (ar.IsLoading) States = new StateEntry[states];
        for (int i = 0; i < States.Length; i++)
        {
            if (ar.IsLoading) States[i] = new StateEntry();
            ar.I32(ref States[i].Id); ar.I32(ref States[i].State);
        }
    }

    private static void Count(MfcArchive ar, ref int count)
    {
        if (ar.DocumentVersion < 0x1BE5)
        {
            uint native = checked((uint)count); ar.Count(ref native); count = checked((int)native);
        }
        else ar.I32(ref count);
        if (count < 0 || ar.IsLoading && count > ar.Remaining / 4)
            throw new InvalidDataException("Invalid native layer-map count.");
    }
}

internal static class NativeDrawingModelFactory
{
    internal static NativeDrawingObjectGraph CreateGraph(Func<string, ushort, IMfcSerializable?>? additionalFactory = null, uint initialIndex = 1)
    {
        NativeDrawingObjectGraph graph = null!;
        graph = new NativeDrawingObjectGraph((name, schema) =>
        {
            var additional = additionalFactory?.Invoke(name, schema);
            if (additional != null) return additional;
            if (schema != 1 && !(schema == 0 && (name == "su_CStringArray" || name == "su_CMapStringToString"))) throw new NotSupportedException($"Unverified schema {schema} for {name}.");
            return name switch
            {
                "moHeader_c" => new NativeDrawingHeaderModel(),
                "moDimText_c" => new NativeDrawingDimTextModel(),
                // moGenericText vtable 0x4D0A7AB0 -> 0x4BF23430 uses
                // the same dim-text base and appends its actual native tail.
                "moGenericText_c" => new NativeDrawingDimTextModel { Generic = true },
                "moGenTextDataList_c" => new NativeDrawingGenericTextListModel(),
                "moTextDataLineHead_c" => new NativeDrawingTextLineHeadModel(),
                "moTextDataChar_c" => new NativeDrawingTextCharacterModel(),
                "moTextDataCustomProperty_c" => new NativeDrawingTextPropertyModel(),
                "moTextDataFont_c" => new NativeDrawingTextFontModel(),
                "utCharFormat_c" => new NativeDrawingCharacterFormatModel(),
                "sgEntHandle" => new SldFile.Binary.EntHandleData(),
                // sgTextHandle vtable 0x4D1E1D60+0x28 uses the same
                // sgEntHandle serializer. Reuse the existing payload type.
                "sgTextHandle" => new SldFile.Binary.EntHandleData("sgTextHandle"),
                // sgEllipseHandle vtable 0x4D1D6648+0x28 also uses 0x4C5B0AB0.
                "sgEllipseHandle" => new SldFile.Binary.EntHandleData("sgEllipseHandle"),
                // sgSplineHandle vtable 0x4D1E18D0+0x28 uses 0x4C5B0AB0.
                "sgSplineHandle" => new SldFile.Binary.EntHandleData("sgSplineHandle"),
                "sgPointHandle" => new SldFile.Binary.PointHandleData(),
                "sgLineHandle" => new SldFile.Binary.LineHandleData(),
                "sgArcHandle" => new SldFile.Binary.ArcHandleData(),
                "moDetailCabinet_c" => new NativeDrawingDetailCabinetModel(),
                "moRelMgr_c" => new NativeDrawingRelationManagerModel(),
                "moDetailFolder_c" => new NativeDrawingDetailFolderModel(),
                DrSheetMfcModel.MfcClassName => new DrSheetMfcModel { NativeSheet = new DrSheetMfcModel.NativeState() },
                DrTemplateMfcModel.NativeClassName => new DrTemplateMfcModel { NativeTemplate = new DrTemplateMfcModel.NativeState() },
                DrViewMfcModel.AbsoluteViewClassName => new DrViewMfcModel { NativeView = new DrViewMfcModel.NativeState() },
                DrViewMfcModel.UnfoldedViewClassName => new DrViewMfcModel { ClassName = DrViewMfcModel.UnfoldedViewClassName, NativeView = new DrViewMfcModel.NativeState { Unfolded = true } },
                "moDetailView" => new DrViewMfcModel { ClassName = "moDetailView", NativeView = new DrViewMfcModel.NativeState { Detail = true } },
                "moDetailViewLabel_c" => new NativeDrawingViewLabelModel(),
                // Ortho factory 0x4C963F00 -> ctor 0x4B72D6E0,
                // final vtable 0x4CE8B718 uses the same 0x4C96F870.
                "moOrthoViewLabel_c" => new NativeDrawingViewLabelModel(),
                "moTextDataLabel_c" => new NativeDrawingTextLabelModel(),
                "moReferenceInternal_c" => new NativeDrawingReferenceModel(),
                "moExtFeature_c" => new NativeDrawingExternalFeatureModel(),
                "moDVAlign_c" => new NativeDrawingViewAlignmentModel(),
                "moFR_c" => new NativeDrawingFeatureReferenceModel(),
                MoDisplayDistanceDimModel.MfcClassName => new MoDisplayDistanceDimModel { NativeDimension = new MoDisplayDimModel.NativeState() },
                MoDisplayAngularDimModel.MfcClassName => new MoDisplayAngularDimModel { NativeDimension = new MoDisplayDimModel.NativeState { Angular = true } },
                MoDisplayRadialDimModel.MfcClassName => new MoDisplayRadialDimModel { NativeDimension = new MoDisplayDimModel.NativeState { RadialDimension = new MoDisplayDimModel.NativeRadialState() } },
                "moRefDimHandle_c" => new NativeDrawingDimensionHandleModel(),
                "moRefDim2E_c" => new NativeDrawingReferenceDimensionModel(),
                "moRefDim1E_c" => new NativeDrawingReferenceDimensionModel { SingleEntity = true },
                // Factory 0x4BBE76E0 -> ctor 0x4B869700, vtable
                // 0x4CEAE828 + 0x28 -> shared moCompFeature 0x4BBF56F0.
                "moCompDetailCircle_c" => new NativeDrawingComponentFeatureReferenceModel(),
                "moDimTolerance_c" => new SldFile.Binary.SketchData.NativeDimensionTolerance(),
                MfcCompEdgeModel.MfcClassName => new MfcCompEdgeModel { NativeEdge = new MfcCompEdgeModel.NativeState() },
                MfcEdgeRefModel.MfcClassName => new MfcEdgeRefModel { NativeReference = new MfcEdgeRefModel.NativeState() },
                "moCompVertex_c" => new NativeDrawingComponentEntityReferenceModel(),
                "moVertexRef_c" => new NativeDrawingVertexReferenceModel(),
                "moFromSktEnt3IntSurfIdRep_c" => new FromSktEnt3IntSurfIdRepData(),
                "moDisplayProfileFeature_c" => new NativeDrawingDisplayProfileModel(),
                "moCompProfile_c" => new NativeDrawingComponentFeatureReferenceModel(),
                "sgExtEnt_c" => new SldFile.Binary.ExtEntData(),
                "moSketchExtRef_w" => new SldFile.Binary.SketchExtRefData(),
                "moPointBackedUpData_c" => new NativeDrawingPointBackupModel(),
                "moDetailCircle_c" => new NativeDrawingDetailCircleModel(),
                "moEndFaceSurfIdRep_c" => new SldFile.Binary.EndFaceSurfIdRepData(),
                // BendEnd vtable 0x4CF86688 shares 0x4BBF3640 and its
                // +0x258 method 0x4BBFB080 writes the same two i32s.
                "moBendEndSurfIdRep_c" => new SldFile.Binary.EndFaceSurfIdRepData { NativeClassName = "moBendEndSurfIdRep_c" },
                // UnBend 0x4CF86420 -> 0x4BBF9B90 uses the same fields;
                // its only extra work is a pre-0x245 runtime migration.
                "moUnBendSurfIdRep_c" => new SldFile.Binary.EndFaceSurfIdRepData { NativeClassName = "moUnBendSurfIdRep_c" },
                // ThinClosing vtable 0x4CF85350 also uses the shared
                // 0x4BBF3640 base and two-i32 0x4BBFB080 tail.
                "moThinClosingSurfIdRep_c" => new SldFile.Binary.EndFaceSurfIdRepData { NativeClassName = "moThinClosingSurfIdRep_c" },
                "moFromSktEntSurfIdRep_c" => new SldFile.Binary.FromSktEntSurfIdRepData(),
                // ThinOffset uses the same 0x4BBF9B00 native serializer:
                // shared feature link plus one i32, with its own class tag.
                "moThinOffsetSurfIdRep_c" => new SldFile.Binary.FromSktEntSurfIdRepData { NativeClassName = "moThinOffsetSurfIdRep_c" },
                "moCompCmarkSketchEntHandle_c" => new SldFile.Binary.CompSketchEntHandleData { NativeCenterMark = true },
                // Ordinary handle ctor 0x4B83CEA0 sets 0x4CEA87F8;
                // Serialize slot 0x4BBF7650 is the center-mark's shared base.
                "moCompSketchEntHandle_c" => new SldFile.Binary.CompSketchEntHandleData(),
                "moDisplayCenterMarkSym_c" => new NativeDrawingCenterMarkModel(),
                "moCenterMarkSym_c" => new NativeDrawingCenterMarkDataModel(),
                "moDisplayCmarkNode_c" => new NativeDrawingCenterMarkNodeModel(),
                "moParametricPoint_c" => new NativeDrawingAttachPointModel { Parametric = true },
                "moLabelAttachPoint_c" => new NativeDrawingAttachPointModel { Label = true },
                "moCircleRef_w" => new NativeDrawingCircleReferenceModel(),
                "moGeneralCurveRef_w" => new NativeDrawingGeneralCurveReferenceModel(),
                DrDimensionMfcModel.DimBackupDefClassName => new DrDimensionMfcModel { NativeBackup = new DrDimensionMfcModel.NativeBackupState() },
                // moCompDetailItem descriptor 0x4DB069A0, vtable
                // 0x4CF0A300 -> 0x4C96CD50, already shared by CompNote.
                "moCompDetailItem_c" => new DrNoteMfcModel.NativeComponentState(),
                "moGeneralTableAnchor_c" or "moHoleTableAnchor_c" or "moWeldmentTableAnchor_c"
                    or "moRevisionTableAnchor_c" or "moWeldTableAnchor_c" or "moBendTableAnchor_c"
                    or "moPunchTableAnchor_c" => new NativeDrawingTableAnchorModel(),
                "moFamilyTableAnchor_c" => new NativeDrawingTableAnchorModel { Family = true },
                DrBomTableMfcModel.MfcClassName => new DrBomTableMfcModel { NativeAnchor = new NativeDrawingTableAnchorModel() },
                "moRefPlane_c" => new NativeDrawingReferencePlaneModel(),
                "moAbsoluteRefPlnData_c" => new NativeDrawingPlaneDataModel(),
                "moDrawingRefPlnData_c" => new NativeDrawingPlaneDataModel { Drawing = true },
                "moCommentsFolder_c" or "moBlockFolder_c" or "moDocsFolder_c" or "moInkMarkupFolder_c" or "moMarkupCommentFolder_c" => new NativeDrawingFolderModel(),
                "su_CStringArray" => new NativeDrawingStringArrayModel(),
                "su_CMapStringToString" => new NativeDrawingStringMapModel(),
                // bentRefArr::Serialize 0x4C5AB640 delegates directly to
                // suObArray in this ordinary (non-pointer-export) context.
                "suObList" or "suObArray" or "bentRefArr" => new NativeDrawingObjectListModel(),
                "moLengthParameter_c" => new MoLengthParameterModel { NativeWire = true },
                // moAngleParameter 0x4C1B79E0 calls the same double
                // parameter codec; its extra read is pre-0x2DD only.
                "moAngleParameter_c" => new MoAngleParameterModel { NativeWire = true },
                "sgPntPntDist" => new SldFile.Binary.PntPntDistData(),
                "sgSkOffsetDim" => new NativeSketchOffsetDimensionModel(),
                "sgSlot_c" => new SldFile.Binary.SketchData.NativeSlot(),
                "sgSketch" => new SldFile.Binary.SketchData(),
                SketchFeatureModel.ClassName2D => new SketchFeatureModel { NativeProfile = new SketchFeatureModel.NativeProfileState() },
                "moCompRefPlane_c" => new SldFile.Binary.CompRefPlaneData(),
                DrNoteMfcModel.CompNoteClassName => new DrNoteMfcModel { NativeComponent = new DrNoteMfcModel.NativeComponentState() },
                DrNoteMfcModel.DisplayNoteClassName => new DrNoteMfcModel { NativeDisplay = new DrNoteMfcModel.NativeDisplayState() },
                "moNoteData_c" => new DrNoteMfcModel { NativeData = new DrNoteMfcModel.NativeDataState() },
                "moAbsolutePoint_c" => new NativeDrawingAttachPointModel(),
                "moAttachPointData_c" => new NativeDrawingAttachPointDataModel(),
                "moFavoriteHandle_c" => new SldFile.Binary.FavoriteHandleData(),
                "moSketchRelMgr_c" => new SldFile.Binary.SketchData.NativeRelationManager(),
                "moSketchRegion_c" => new MfcSketchRegionModel { NativeWire = true },
                "sgPGMProfileData_c" => new NativeSketchOffsetProfileModel(),
                "sgPGMLoopOffsetData_c" => new NativeSketchOffsetLoopModel(),
                "sgPGMLoopOffsetEdgeData_c" => new NativeSketchOffsetEdgeModel(),
                // All three native vtables dispatch through 0x4CBB6B3C to
                // suObList::Serialize, without an additional scalar tail.
                "sgSplineList_c" or "modifSplineList_c" or "cptsSplineList_c" => new NativeDrawingObjectListModel(),
                "moExtObjectList_c" => new NativeDrawingObjectListModel(),
                "moLogs_c" => new NativeDrawingLogsModel(),
                "moStamp_c" => new NativeDrawingStampModel(),
                "moExtObject_c" => new NativeDrawingExternalObjectModel(),
                "moSharedFileDef_c" => new NativeDrawingSharedFileModel(),
                "moCStringHandle_c" => new NativeDrawingStringHandleModel(),
                "moNodeName_c" => new NativeDrawingStringHandleModel(),
                "moUnitsTable_c" => new NativeDrawingObjectListModel(),
                "moUnitComponent_c" => new NativeDrawingComponentReferenceModel(),
                "moChainComponent_c" => new NativeDrawingComponentReferenceModel { Chain = true },
                "moReferenceChain_c" => new NativeDrawingReferenceChainModel(),
                "moExtFeatureList_c" => new NativeDrawingObjectListModel { Int32Count = true },
                "moTransRefPlaneData_c" => new NativeDrawingReferencePlaneStyleModel(),
                "moScaleSysScale_c" => new NativeDrawingScaleSystemModel(),
                "moLengthUserUnits_c" => new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Length },
                "moAngleUserUnits_c" or "moForceUnits_c" or "moUnitSysUnits_c" => new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Selector },
                "moEnergyUnits_c" or "moPowerUnits_c" or "moTimeUnits_c" => new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.GatedSelector },
                "moDensityUnits_c" => new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Density },
                "moTemperatureUnits_c" => new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Temperature },
                "moFloatNumberUserUnits_c" or "moNumberUserUnits_c" or "moFrequencyUserUnits_c" or "moGravityUnits_c" or "moLinearMotorUnits_c" or "moRotaryMotorUnits_c" or "moSpringConstantUnits_c" or "moStressUnits_c" => new NativeDrawingUnitsModel(),
                "moLineStyle_c" => new NativeDrawingLineStyleModel(),
                "moLayerData_c" => new NativeDrawingLayerDataModel(),
                "uiLFConfig_c" => new NativeDrawingLineFontConfigModel(),
                "uiLineFontMgr_c" => new NativeDrawingLineFontManagerModel(),
                "uiLayerMgr_c" => new NativeDrawingLayerManagerModel(),
                "uiViewBucket_c" => new NativeDrawingViewBucketModel(),
                "moAmbientLight_c" => new NativeDrawingLightModel(),
                "moDirectionLight_c" => new NativeDrawingLightModel { Directional = true },
                "moVisualProperties_c" => new NativeDrawingVisualPropertiesModel(),
                "uoTempBodyTessData_c" => new NativeDrawingBodyTessModel(),
                "uoTempFaceTessData_c" => new NativeDrawingFaceTessModel(),
                "uoBodyPropInfo_c" => new NativeDrawingBodyPropertiesModel(),
                "uoTempPartTessData_c" => new NativeDrawingPartTessModel(),
                "uoTempAssemblySHDData_c" => new NativeDrawingAssemblyTessModel(),
                "uoTempSubAssemblySHDData_c" => new NativeDrawingAssemblyTessModel(),
                _ => throw new NotSupportedException($"Native drawing serializer is not registered for {name}.")
            };
        }, initialIndex);
        return graph;
    }

    internal static NativeDrawingObjectGraph Graph(MfcArchive ar) => ar.Context.NativeDrawingObjects
        ?? throw new InvalidOperationException("A native drawing object graph must own this payload.");
}
