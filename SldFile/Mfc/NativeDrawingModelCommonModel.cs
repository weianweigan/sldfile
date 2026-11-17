using System;
using System.IO;

namespace SldFile.Mfc;

// moModel::SerializeCommon 0x4C26C400 in an ordinary drawing Definition
// archive. This precedes SerializeResolved and is not a complete drawing root.
internal sealed class NativeDrawingModelCommonModel : IMfcSerializable
{
    internal sealed class VisualEntry
    {
        internal int Key;
        internal NativeDrawingObjectGraph.Node? Value;
    }
    internal sealed class NamedObjects
    {
        internal string? Key;
        internal NativeDrawingObjectGraph.Node?[] Values = Array.Empty<NativeDrawingObjectGraph.Node?>();
    }
    internal sealed class NamedObject
    {
        internal string? Key;
        internal NativeDrawingObjectGraph.Node? Value;
    }
    internal sealed class IdState { internal uint Id; internal int State; }

    internal NativeDrawingModelHeaderModel Header = new NativeDrawingModelHeaderModel();
    internal int State1DB, State1DE, State1DF, StateF04;
    internal VisualEntry[] VisualProperties = Array.Empty<VisualEntry>();
    internal NamedObject[] DetailDefinitions = Array.Empty<NamedObject>();
    internal double Scalar1EB, Scalar1EC;
    internal int DictionaryKind = 2, DictionaryState110, DictionaryState114;
    internal NamedObject[] Dictionary = Array.Empty<NamedObject>();
    internal int HasPointArray;
    internal NativeDrawingArrayModel PointArray = new NativeDrawingArrayModel();
    internal int StateEF4, HasShrinkWrap, GraphicsFlag, State1A6, State1A7;
    internal NativeDrawingObjectGraph.Node?[] Objects18E = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NamedObjects[] FlagNotes = Array.Empty<NamedObjects>();
    internal uint[] Guid76C = new uint[4], Guid78C = new uint[4];
    internal int State22C, State1164, State22D, State116C, HasComponentImport, State4E, State25B;
    internal uint State25A;
    internal IdState[] IdStates = Array.Empty<IdState>();
    internal NativeDrawingObjectGraph.Node? PublishCache;

    public void Serialize(MfcArchive ar)
    {
        Header.Serialize(ar);
        var graph = NativeDrawingModelFactory.Graph(ar);
        int version = ar.DocumentVersion;
        if (version >= 0xCF5) ar.I32(ref State1DB);
        if (version >= 0xCFB)
        {
            int count = VisualProperties.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) VisualProperties = new VisualEntry[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) VisualProperties[i] = new VisualEntry();
                ar.I32(ref VisualProperties[i].Key); graph.Object(ar, ref VisualProperties[i].Value);
            }
        }
        if (version >= 0xD06) ar.I32(ref State1DE);
        if (version >= 0xDC1) ar.I32(ref State1DF);
        if (version >= 0xDE2) ar.I32(ref StateF04);
        if (version >= 0xF5C) NamedMap(ar, ref DetailDefinitions, false);
        if (version >= 0xFCB) { ar.F64(ref Scalar1EB); ar.F64(ref Scalar1EC); }
        // 0x4C26C3A0 -> 0x4C254960: a su_CMapStringToOb followed by
        // two i32 values. Non-2 discriminators carry no dictionary payload.
        if (version >= 0x12D9)
        {
            ar.I32(ref DictionaryKind);
            if (DictionaryKind == 2)
            {
                NamedMap(ar, ref Dictionary, true);
                ar.I32(ref DictionaryState110); ar.I32(ref DictionaryState114);
            }
        }
        if (version >= 0x2778)
        {
            ar.I32(ref HasPointArray);
            if (HasPointArray != 0)
            {
                // 0x4BA97990 stores a dynamic array of 24-byte point records.
                PointArray.Serialize(ar);
                if (PointArray.ElementSize != 0 && PointArray.ElementSize % 24 != 0)
                    throw new InvalidDataException("Invalid native model point-array element size.");
            }
        }
        // SerializeXmlData (0x4C276150) does not consume archive bytes.
        if (version >= 0xE10) ar.I32(ref StateEF4);
        if (version >= 0xF2F)
        {
            ar.I32(ref HasShrinkWrap);
            if (version >= 0x2F73) ar.I32(ref GraphicsFlag);
            if (HasShrinkWrap != 0) throw new NotSupportedException("Native drawing shrink-wrap payload is not decoded.");
        }
        if (version >= 0x17E7) ar.I32(ref State1A6);
        if (version >= 0x1FBF) NativeDrawingObjectArray.Serialize(ar, ref Objects18E);
        if (version >= 0x1FCE) ar.I32(ref State1A7);
        if (version >= 0x1FD7)
        {
            int count = FlagNotes.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) FlagNotes = new NamedObjects[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) FlagNotes[i] = new NamedObjects();
                DrawingPreviewRecordModel.String(ar, ref FlagNotes[i].Key);
                int items = FlagNotes[i].Values.Length; NativeDrawingDisplayArchive.Count(ar, ref items);
                if (ar.IsLoading) FlagNotes[i].Values = new NativeDrawingObjectGraph.Node?[items];
                for (int j = 0; j < items; j++) graph.Object(ar, ref FlagNotes[i].Values[j]);
            }
        }
        if (version >= 0x2003)
        {
            for (int i = 0; i < 4; i++) ar.U32(ref Guid76C[i]); ar.I32(ref State22C);
            for (int i = 0; i < 4; i++) ar.U32(ref Guid78C[i]); ar.I32(ref State1164);
        }
        if (version >= 0x2010) { ar.I32(ref State22D); ar.I32(ref State116C); }
        if (version >= 0x23CA)
        {
            ar.I32(ref HasComponentImport);
            if (HasComponentImport != 0) throw new NotSupportedException("Native drawing component-import payload is not decoded.");
        }
        if (version >= 0x27CD) ar.I32(ref State4E);
        if (version >= 0x2B6D) ar.U32(ref State25A);
        if (version >= 0x46BC) ar.I32(ref State25B);
        if (version >= 0x46C6)
        {
            int count = IdStates.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) IdStates = new IdState[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) IdStates[i] = new IdState();
                ar.U32(ref IdStates[i].Id); ar.I32(ref IdStates[i].State);
            }
        }
        if (version >= 0x2BCF) graph.Object(ar, ref PublishCache);
    }

    private static void NamedMap(MfcArchive ar, ref NamedObject[] values, bool archiveCount)
    {
        int count = values.Length;
        if (archiveCount)
        {
            uint native = checked((uint)count); ar.Count(ref native); count = checked((int)native);
            if (ar.IsLoading && count > ar.Remaining / 6) throw new InvalidDataException("Invalid native string-object map count.");
        }
        else NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new NamedObject[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) values[i] = new NamedObject();
            DrawingPreviewRecordModel.String(ar, ref values[i].Key); graph.Object(ar, ref values[i].Value);
        }
    }
}
