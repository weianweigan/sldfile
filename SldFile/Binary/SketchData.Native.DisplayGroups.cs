using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeEntityFields[] NativeExternalBlocks = Array.Empty<NativeEntityFields>();
    internal NativeDisplayGroups DisplayGroups = new NativeDisplayGroups();
    internal NativeDrawingObjectGraph.Node? NativeGroupedObjects;

    // After the relation manager: +108 external blocks, optional inline +5D0
    // display maps and the versioned tagged +5D8 list. Silhouettes follow.
    internal void SerializeNativeDisplayGroups(MfcArchive ar, bool ownerIs3D)
    {
        // sgExternalBlock's helper 0x4C5AD850 tail-jumps to 0x4C5AC710,
        // the existing shared entity codec. There is no second block body.
        Rows(ar, ref NativeExternalBlocks, row => row.Serialize(ar, ownerIs3D));
        if (ar.DocumentVersion >= 0x1C4F || (ownerIs3D && ar.DocumentVersion > 0x1C4A)) DisplayGroups.Serialize(ar);
        if (ar.DocumentVersion > 0x2398) NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeGroupedObjects);
    }

    internal sealed class NativeDisplayGroups
    {
        internal NativeDrawingFacetModel.Pair[][] Groups =
        {
            Array.Empty<NativeDrawingFacetModel.Pair>(), Array.Empty<NativeDrawingFacetModel.Pair>(),
            Array.Empty<NativeDrawingFacetModel.Pair>(), Array.Empty<NativeDrawingFacetModel.Pair>()
        };
        // +5D0 object 0x4B9AFEB0; helper 0x4B9B1860 writes one i32 count
        // and i32 pairs, while native loading also builds the reverse maps.
        internal void Serialize(MfcArchive ar)
        {
            if (Groups.Length != 4) throw new System.IO.InvalidDataException("Four native display map slots are required.");
            int groups = ar.DocumentVersion >= 0x1C54 ? 4 : 3;
            for (int i = 0; i < groups; i++)
            {
                int count = Groups[i].Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading) Groups[i] = new NativeDrawingFacetModel.Pair[count];
                for (int j = 0; j < count; j++)
                {
                    if (ar.IsLoading) Groups[i][j] = new NativeDrawingFacetModel.Pair();
                    ar.I32(ref Groups[i][j].Key); ar.I32(ref Groups[i][j].Value);
                }
            }
        }
    }
}
