using System;

namespace SldFile.Mfc;

// drwcplu SolidDrawingStorageMFC::iRestoreDrawingsDisplayLists 0x180005A20
// (save 0x180007AC0) appends this data after the drawing display root.
internal sealed class NativeDrawingDisplayTailModel : IMfcSerializable
{
    internal sealed class TexturedPlane : IMfcSerializable
    {
        internal NativeDrawingObjectGraph.Node? Bitmap;
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
        internal int State;
        public void Serialize(MfcArchive ar)
        {
            // uiModelDoc::iRestoreTexturedPlanes 0x4980F2B0, drawing context:
            // bitmap object, compact transform, then conditional state.
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Bitmap);
            Transform.Serialize(ar);
            if (Bitmap != null && ar.DocumentVersion >= 0x10A6) ar.I32(ref State);
        }
    }
    internal TexturedPlane[] TexturedPlanes = Array.Empty<TexturedPlane>();
    internal string? CustomPropertyTemplate;
    internal int UseSystemUnits, LengthUnit, AngleUnit;
    internal uint EnvelopeColor;
    internal int EnvelopeOption344;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion >= 0xE15)
        {
            int count = TexturedPlanes.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) TexturedPlanes = new TexturedPlane[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) TexturedPlanes[i] = new TexturedPlane();
                TexturedPlanes[i].Serialize(ar);
            }
        }
        if (ar.DocumentVersion >= 0x109B) DrawingPreviewRecordModel.String(ar, ref CustomPropertyTemplate);
        // uiModelDoc::SerializeUnitsofMeasurement 0x497FA940.
        if (ar.DocumentVersion >= 0x1FB3)
        { ar.I32(ref UseSystemUnits); ar.I32(ref LengthUnit); ar.I32(ref AngleUnit); }
        // uiModelDoc::SerializeEnvelopeSystemOptions 0x497F9300.
        if (ar.DocumentVersion >= 0x42DB)
        { ar.U32(ref EnvelopeColor); ar.I32(ref EnvelopeOption344); }
    }
}
