# Chamfer options: controlled native samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples2/`
(copied without modification). These four samples are the controlled evidence for
Task 1.3 chamfer reading/authoring. Do not overwrite them.

| Sample | Source file | SHA-256 |
| --- | --- | --- |
| `chamfer_dist_angle_2mm45.SLDPRT` | `chamfer_dist_angle_2mm45.SLDPRT` | `644d515d753f81cd0e45e8f76fd60e205d49acd3090845cb9474fd99468a116b` |
| `chamfer_dist_angle_2mm45_resave.SLDPRT` | `chamfer_dist_angle_2mm45_resave.SLDPRT` | `3c04302860bf1c41d88b796a48747898b4e3065c9fde89c872275119d534e627` |
| `chamfer_dist_dist_2_3mm.SLDPRT` | `chamfer_dist_dist_2_3mm.SLDPRT` | `50001b8c794983c44fa8c00b56dfee493f94d56231aa13817b23ac17ed1c0676` |
| `chamfer_dist_dist_2_3mm_resave.SLDPRT` | `chamfer_dist_dist_2_3mm_resave.SLDPRT` | `b4bdf8b2ccd137547e8a6023215ae491a7bb063b6989ce54e2cc4eb929f5ad36` |

All four are native SolidWorks 2026 parts with ResolvedFeatures stream version 0x70.
The base body is the controlled extruded cube used by `fillet_2mm`; only the chamfer
mode/amplitudes differ. Parsing/authoring evidence and slot offsets are documented in
the Agent 1 report and in `SldFile/Binary/FeatureData/ChamferSegments.cs`.

## Persistent edge selection (reading, 2026-10-30)

`Chamfer_c` requires exactly one `moEdgeRef_c` and one `moCompEdge_c` plus the measured
`moSurfaceIdRep_c`/`moFilletSurfIdRep_c`. The edge payload uses the same four 20-byte
records and the same archive GUID as fillet; measured record order is layout A
(`0x80F1/1, 0x80F1/4, 0x80E8/1, 0x80E8/0`). All four chamfer samples expose
`Edges = [Edge, EntityId=4, ClassWord=0, PersistentId=0, RecordCount=4]`
(main and resave identical). Tampering the class word, GUID, record marker or edge id is
rejected. Full fillet/variable layout notes are in `../FilletOptions/README.md`.
