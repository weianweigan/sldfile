using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    // Shared by Dimension_c in sketches and moRefDim in drawings. There is
    // no existing tolerance aggregate with this native wire layout.
    // sldmfcu factory 0x3CAFAB90 -> moDimTolerance constructor;
    // Serialize 0x3CB040E0. su_CObject contributes no ordinary archive bytes.
    internal sealed class NativeDimensionTolerance : IMfcSerializable
    {
        internal int State8, StateC, State58, StateB8, StateBC;
        internal double Value10, Value18, Value80, Value88, Value90, ValueA0, ValueA8, ValueB0;
        internal byte State78, State48, State98, State5C, State60;
        internal string? String20, String28, String30, String38, String40;

        public void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000)
                throw new NotSupportedException("Unverified legacy dimension tolerance layout.");
            ar.I32(ref State8); ar.I32(ref StateC);
            ar.F64(ref Value10); ar.F64(ref Value18);
            ar.Byte(ref State78);
            ar.F64(ref Value80); ar.F64(ref Value88); ar.F64(ref Value90);
            DrawingPreviewRecordModel.String(ar, ref String20);
            DrawingPreviewRecordModel.String(ar, ref String28);
            ar.Byte(ref State48); ar.I32(ref State58); ar.Byte(ref State98);
            ar.F64(ref ValueA0); ar.F64(ref ValueA8); ar.F64(ref ValueB0);
            ar.Byte(ref State5C); ar.Byte(ref State60);
            if (ar.DocumentVersion >= 0x23D3) ar.I32(ref StateB8);
            if (ar.DocumentVersion > 0x2B6D)
            {
                DrawingPreviewRecordModel.String(ar, ref String30);
                DrawingPreviewRecordModel.String(ar, ref String38);
                DrawingPreviewRecordModel.String(ar, ref String40);
            }
            if (ar.DocumentVersion > 0x2B76) ar.I32(ref StateBC);
        }
    }
}
