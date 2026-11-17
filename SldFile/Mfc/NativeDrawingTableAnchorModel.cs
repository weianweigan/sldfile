namespace SldFile.Mfc;

// moGeneralTableAnchor_c descriptor 0x4DAE4010, vtable 0x4D023BE8
// -> moTableAnchor::Serialize 0x4BDCDB30. No existing native anchor
// payload is present in the aggregate table models.
internal sealed class NativeDrawingTableAnchorModel : IMfcSerializable
{
    internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
    internal SldFile.Drawing.DrawingPreviewPoint Position;
    internal bool Family;
    public void Serialize(MfcArchive ar)
    {
        // moFamilyTableAnchor::Serialize 0x4BDCDCF0 emits no payload
        // through 0x42F1; newer versions use the ordinary anchor base.
        if (Family && ar.DocumentVersion <= 0x42F1) return;
        Feature.Serialize(ar); DrawingPreviewRecordModel.Point(ar, ref Position);
    }
}
