using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Mfc;

/// <summary>
/// Serializable contour item for sketch chain entity loops.
/// </summary>
public sealed class MfcContourItem : IMfcSerializable, ISelectionReference
{
    public string ReferenceType => "Contour";
    public uint EntityId { get; set; }
    public uint ChainId { get; set; }
    public bool IsClosed { get; set; } = true;
    public List<uint> EntityIndices { get; set; } = new();
    public bool IsValid => EntityIndices.Count > 0 || EntityId != 0;

    public MfcContourItem() { }

    public MfcContourItem(SketchContourReference def)
    {
        if (def == null) return;
        EntityId = def.EntityId;
        ChainId = def.ChainId;
        IsClosed = def.IsClosed;
        EntityIndices = def.EntityIndices != null ? def.EntityIndices.ToList() : new List<uint>();
    }

    public SketchContourReference ToDefinition() => new SketchContourReference
    {
        EntityId = EntityId,
        ChainId = ChainId,
        IsClosed = IsClosed,
        EntityIndices = EntityIndices.ToArray()
    };

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            uint eid = EntityId;
            ar.U32(ref eid);

            uint cid = ChainId;
            ar.U32(ref cid);

            bool ic = IsClosed;
            ar.Boolean(ref ic);

            uint count = (uint)EntityIndices.Count;
            ar.Count(ref count);
            for (int i = 0; i < EntityIndices.Count; i++)
            {
                uint idx = EntityIndices[i];
                ar.U32(ref idx);
            }
        }
        else
        {
            if (ar.Remaining >= 4)
            {
                uint eid = 0;
                ar.U32(ref eid);
                EntityId = eid;
            }

            if (ar.Remaining >= 4)
            {
                uint cid = 0;
                ar.U32(ref cid);
                ChainId = cid;
            }

            if (ar.Remaining >= 1)
            {
                bool ic = true;
                ar.Boolean(ref ic);
                IsClosed = ic;
            }

            if (ar.Remaining >= 4)
            {
                uint count = 0;
                ar.Count(ref count);
                EntityIndices.Clear();
                for (int i = 0; i < count && ar.Remaining >= 4; i++)
                {
                    uint idx = 0;
                    ar.U32(ref idx);
                    EntityIndices.Add(idx);
                }
            }
        }
    }
}

/// <summary>
/// Serializable region item for planar sketch boundary selections.
/// </summary>
public sealed class MfcRegionItem : IMfcSerializable, ISelectionReference
{
    public string ReferenceType => "Region";
    public uint EntityId { get; set; }
    public uint RegionIndex { get; set; }
    public uint LoopCount { get; set; }
    public List<uint> EdgeIds { get; set; } = new();
    public bool IsValid => RegionIndex > 0 || EntityId > 0 || EdgeIds.Count > 0;

    public MfcRegionItem() { }

    public MfcRegionItem(SketchRegionReference def)
    {
        if (def == null) return;
        EntityId = def.EntityId;
        RegionIndex = def.RegionIndex;
        LoopCount = def.LoopCount;
        EdgeIds = def.EdgeIds != null ? def.EdgeIds.ToList() : new List<uint>();
    }

    public SketchRegionReference ToDefinition() => new SketchRegionReference
    {
        EntityId = EntityId,
        RegionIndex = RegionIndex,
        LoopCount = LoopCount,
        EdgeIds = EdgeIds.ToArray()
    };

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            uint eid = EntityId;
            ar.U32(ref eid);

            uint rid = RegionIndex;
            ar.U32(ref rid);

            uint lc = LoopCount;
            ar.U32(ref lc);

            uint count = (uint)EdgeIds.Count;
            ar.Count(ref count);
            for (int i = 0; i < EdgeIds.Count; i++)
            {
                uint edge = EdgeIds[i];
                ar.U32(ref edge);
            }
        }
        else
        {
            if (ar.Remaining >= 4)
            {
                uint eid = 0;
                ar.U32(ref eid);
                EntityId = eid;
            }

            if (ar.Remaining >= 4)
            {
                uint rid = 0;
                ar.U32(ref rid);
                RegionIndex = rid;
            }

            if (ar.Remaining >= 4)
            {
                uint lc = 0;
                ar.U32(ref lc);
                LoopCount = lc;
            }

            if (ar.Remaining >= 4)
            {
                uint count = 0;
                ar.Count(ref count);
                EdgeIds.Clear();
                for (int i = 0; i < count && ar.Remaining >= 4; i++)
                {
                    uint edge = 0;
                    ar.U32(ref edge);
                    EdgeIds.Add(edge);
                }
            }
        }
    }
}

/// <summary>
/// Unified strongly-typed model for moSketchChain_c contour and region selections.
/// Implements <see cref="IMfcSerializable"/> to serialize sketch selection modes and loops.
/// </summary>
public sealed class MfcContourSelection : IMfcSerializable
{
    public const string ClassName = "moSketchChain_c";

    public SketchSelectionMode SelectionMode { get; set; } = SketchSelectionMode.WholeSketch;
    public List<MfcContourItem> Contours { get; set; } = new();
    public List<MfcRegionItem> Regions { get; set; } = new();

    public MfcContourSelection() { }

    public MfcContourSelection(ExtrudeFeatureModel def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        SelectionMode = def.SketchSelectionMode;
        if (def.SelectedContours != null)
        {
            Contours = def.SelectedContours.Select(c => new MfcContourItem(c)).ToList();
        }
        if (def.SelectedRegions != null)
        {
            Regions = def.SelectedRegions.Select(r => new MfcRegionItem(r)).ToList();
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 1;
            ar.U16(ref schema);

            int mode = (int)SelectionMode;
            ar.I32(ref mode);

            uint cCount = (uint)Contours.Count;
            ar.Count(ref cCount);
            foreach (var c in Contours)
                c.Serialize(ar);

            uint rCount = (uint)Regions.Count;
            ar.Count(ref rCount);
            foreach (var r in Regions)
                r.Serialize(ar);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            if (ar.Remaining >= 4)
            {
                int mode = 0;
                ar.I32(ref mode);
                SelectionMode = (SketchSelectionMode)mode;
            }

            if (ar.Remaining >= 4)
            {
                uint cCount = 0;
                ar.Count(ref cCount);
                Contours.Clear();
                for (int i = 0; i < cCount && ar.Remaining > 0; i++)
                {
                    var item = new MfcContourItem();
                    item.Serialize(ar);
                    Contours.Add(item);
                }
            }

            if (ar.Remaining >= 4)
            {
                uint rCount = 0;
                ar.Count(ref rCount);
                Regions.Clear();
                for (int i = 0; i < rCount && ar.Remaining > 0; i++)
                {
                    var item = new MfcRegionItem();
                    item.Serialize(ar);
                    Regions.Add(item);
                }
            }
        }
    }
}
