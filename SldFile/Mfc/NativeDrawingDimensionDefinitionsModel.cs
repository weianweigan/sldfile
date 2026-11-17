using System;

namespace SldFile.Mfc;

// moDimDefs_c::Serialize 0x3CB03A20. These are document dimension defaults,
// not the existing MoDisplayDimModel or sketch Dimension_c instance payloads.
// The nested dimension-style records share NativeDrawingDimensionStyleModel.
internal sealed partial class NativeDrawingDimensionDefinitionsModel
{
    internal int State68, State60, State64, State6C, State70, State74, State88, State8C;
    internal int StateA8, StateB8, StateBC, StateC0, StateC4, StateD0, StateD4, StateCC;
    internal uint StateD8;
    internal double[] Values8 = new double[11], Values78 = new double[2], Values90 = new double[3];
    internal double StateB0;
    internal NativeDrawingDimensionStyleModel Linear = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Linear };
    internal NativeDrawingDimensionStyleModel Angular = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Angle };
    internal NativeDrawingDimensionStyleModel ArcLength = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.ArcLength };
    internal NativeDrawingDimensionStyleModel Radial = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Radial };
    internal NativeDrawingDimensionStyleModel Hole = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Hole };
    internal NativeDrawingDimensionStyleModel Ordinate = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Ordinate };
    internal NativeDrawingDimensionStyleModel Chamfer = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Chamfer };
    internal NativeDrawingDimensionStyleModel Diameter = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.Diameter };
    internal NativeDrawingDimensionStyleModel AngularOrdinate = new NativeDrawingDimensionStyleModel { Kind = NativeDrawingDimensionStyleModel.Layout.AngularOrdinate };
    internal NativeDrawingDimensionStyleModel Base = new NativeDrawingDimensionStyleModel();

    internal void Serialize(MfcArchive ar)
    {
        SerializeScalarPrefix(ar);
        Linear.Serialize(ar); Angular.Serialize(ar); ArcLength.Serialize(ar); Radial.Serialize(ar);
        Hole.Serialize(ar); Ordinate.Serialize(ar); Chamfer.Serialize(ar); Diameter.Serialize(ar);
        if (ar.DocumentVersion > 0x144A) AngularOrdinate.Serialize(ar);
        Base.Serialize(ar);
    }

    internal void SerializeScalarPrefix(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy dimension definitions.");
        ar.I32(ref State68); NativeDrawingDetailDefinitionsModel.Fixed(ar, Values8, 11);
        ar.I32(ref State60); ar.I32(ref State64); ar.I32(ref State6C); ar.I32(ref State70); ar.I32(ref State74);
        NativeDrawingDetailDefinitionsModel.Fixed(ar, Values78, 2); ar.I32(ref State88); ar.I32(ref State8C);
        NativeDrawingDetailDefinitionsModel.Fixed(ar, Values90, 3); ar.U32(ref StateD8);
        ar.I32(ref StateA8); ar.F64(ref StateB0); ar.I32(ref StateB8); ar.I32(ref StateBC);
        if (v > 0x140E) ar.I32(ref StateC0);
        if (v > 0x1BDC) ar.I32(ref StateC4);
        if (v > 0x375D) { ar.I32(ref StateD0); ar.I32(ref StateD4); }
        if (v >= 0x3EEF) ar.I32(ref StateCC);
    }
}
