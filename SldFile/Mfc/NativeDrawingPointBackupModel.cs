using SldFile.Drawing;

namespace SldFile.Mfc;

// moPointBackedUpData_c factory 0x4C29FB00 sets final vtable
// 0x4D168EE0 -> Serialize 0x4C2B1FD0. su_CObject has no ordinary
// payload, followed by mgPoint's three-double operator encoding.
internal sealed class NativeDrawingPointBackupModel : IMfcSerializable
{
    internal DrawingPreviewPoint Point;
    public void Serialize(MfcArchive ar) => DrawingPreviewRecordModel.Point(ar, ref Point);
}
