# Flange options: controlled native samples (2026-10-30)

Sources: `reverse_workspace/validation/agent5-feature-samples-20261030/samples4/`
(90-degree baseline) and `samples12/` (parameter variants). Files were copied
without modification. These are the controlled evidence for Task 2.2 edge-flange
reading/authoring. Do not overwrite them.

| Sample | Source | SHA-256 |
| --- | --- | --- |
| `edge_flange_90_len10.SLDPRT` | `samples4/edge_flange_90_len10.SLDPRT` | `dfbbece38ad0c84cd9ec53d18c8bb0cd1beff1272171c38cd70c981518218409` |
| `edge_flange_90_len10_resave.SLDPRT` | `samples4/edge_flange_90_len10_resave.SLDPRT` | `5f5e35ce28cceb89933b365d771a14def06895e22c7958be92fe8f4739da533b` |
| `edge_flange_120_len10.SLDPRT` | `samples12/edge_flange_120_len10.SLDPRT` | `6f25d6b16fda6de0aac61becacd25fd8cae3d67acdadcb3baffc67c895a7633b` |
| `edge_flange_120_len10_resave.SLDPRT` | `samples12/edge_flange_120_len10_resave.SLDPRT` | `d868f87f01e9a30073d56013f6c443251632e89fff8cd48a98a6302fe5f7625e` |
| `edge_flange_len20.SLDPRT` | `samples12/edge_flange_len20.SLDPRT` | `329bec14cff3bcbd6db14e995b194bbbc59bf9339311a7709bf59e26ac7c2987` |
| `edge_flange_len20_resave.SLDPRT` | `samples12/edge_flange_len20_resave.SLDPRT` | `0e4f2fda251d1431e02d0a4edef36f4b0dee991857ea4684516830c1847d9990` |
| `edge_flange_pos_outer.SLDPRT` | `samples12/edge_flange_pos_outer.SLDPRT` | `f2716b99bd8c81129c3371e8b27e8ee844a9a22e3a38a137a4609b7bfecbfcc8` |
| `edge_flange_pos_outer_resave.SLDPRT` | `samples12/edge_flange_pos_outer_resave.SLDPRT` | `16610f544da3e484e4eb8c4ad21cd347b7ec08d1ea980a4d0df2cbbde83995f1` |
| `edge_flange_pos_bend_outside.SLDPRT` | `samples12/edge_flange_pos_bend_outside.SLDPRT` | `23335f6ca1850413c00d0fbe26a8e93993e2be880ffa72c270eb188f5a29ae8e` |
| `edge_flange_pos_bend_outside_resave.SLDPRT` | `samples12/edge_flange_pos_bend_outside_resave.SLDPRT` | `34fd5155956d4583827b49721f2e8e75c64767d581a7b21c229b054341cf118d` |
| `edge_flange_relief_custom.SLDPRT` | `samples12/edge_flange_relief_custom.SLDPRT` | `b5ac6e7dc9de0d5f1b463ad82bd1b74c12dc60a58c3fa821610f5e6969104dfa` |
| `edge_flange_relief_custom_resave.SLDPRT` | `samples12/edge_flange_relief_custom_resave.SLDPRT` | `22aac14692924015852a40df756da30142117c08a6de4d5f4fa9b253332fc1f4` |
| `edge_flange_position_offset.SLDPRT` | `samples-edgeflange-offset/edge_flange_position_offset.SLDPRT` | `FED27EF62B4934AF33E9DE89A04603AC127A1BD646FDF34EE4CBB2D91F218AD2` |
| `edge_flange_position_offset_resave.SLDPRT` | `samples-edgeflange-offset/edge_flange_position_offset_resave.SLDPRT` | `725EFC7D054F8ECE110DD118D9414C0C06CB01010057ACB9A3B614B009A6FA03` |
| `edge_flange_position_offset10.SLDPRT` | `samples-edgeflange-offset/edge_flange_position_offset10.SLDPRT` | `81F1DF680100203DA5B6702E35508EAA96D58382D7E583C147E5248732F0D6E6` |
| `edge_flange_position_offset10_resave.SLDPRT` | `samples-edgeflange-offset/edge_flange_position_offset10_resave.SLDPRT` | `7DE3C52F5E4CC932353BE66FF3D8F285230BB2F094543A88CCB78EF858D3AC2E` |

All twelve are native SolidWorks 2026 parts with ResolvedFeatures stream version
**0x74** (verified by reading the first two bytes of every file; the earlier
"0x70" note was incorrect). Independent verification (fresh process, OpenDoc6
errors/warnings 0, `ForceRebuild3(false)=true`, hash unchanged) passed for every
file. Native `IEdgeFlangeFeatureData` snapshots and volumes:

| Sample group | Angle (rad) | Length (m) | Position | Volume (m^3) |
| --- | --- | --- | --- | --- |
| `edge_flange_90_len10` | 1.5707963267949 | 0.010 | 1 MaterialInside | 3.78539816339745e-7 |
| `edge_flange_120_len10` | 2.0943951023932 | 0.010 | 1 MaterialInside | 3.43061679746565e-7 |
| `edge_flange_len20` | 1.5707963267949 | 0.020 | 1 MaterialInside | 5.78539816339744e-7 |
| `edge_flange_pos_outer` | 1.5707963267949 | 0.010 | 2 MaterialOutside | 3.98539816339745e-7 |
| `edge_flange_pos_bend_outside` | 1.5707963267949 | 0.010 | 3 BendOutside | 4.38539816339745e-7 |
| `edge_flange_relief_custom` | 1.5707963267949 | 0.010 | 1 MaterialInside | 3.78539816339745e-7 |

`edge_flange_relief_custom` records rectangular relief width/depth
0.0015/0.0025 m; `edge_flange_90_len10` records 0.001/0.001 m. All samples use
`BendRadius=0.002`, `GapDistance=0.001`, `ReliefRatio=0.5`,
`UseDefaultBendRelief=false`, `UseReliefRatio=true`, feature error/warning 0/0.

## Verified serialized slots (library evidence, 2026-10-30)

These offsets are for the non-resave baseline (`edge_flange_90_len10`); resaves
shift every absolute offset by about +0x114 while keeping the payload bytes
identical.

| API value | Serialized evidence |
| --- | --- |
| `BendAngle` | named `D4` value at 0x69DF: 90 deg = 1.5707963267948966, 120 deg = 2.0943951023931953 |
| `OffsetDistance` | named `D7` value at 0x9DD2: length-20 changes it 0.010 -> 0.020 while `D6` stays 0.010 |
| `PositionOffsetDistance` | named `D6` value at 0x974B: position_offset 0.005, position_offset10 0.010, all other variants 0.010 |
| `UsePositionOffset` | u32 at D6 value + 0x68: 1 in both position-offset samples and resaves, 0 in all six original variants/resaves |
| `PositionType` | u32 at 0x9AE7: 1 baseline, 2 pos_outer, 3 pos_bend_outside |
| `ReliefWidth` | double at 0x9ACF: 0.001 baseline, 0.0015 relief_custom |
| `ReliefDepth` | double at 0x9AD7: 0.001 baseline, 0.0025 relief_custom |

The two relief values and the position u32 sit in one unique anonymous block
`[ReliefRatio=0.5][width][depth][u64=1][positionType]` at 0x9AC7. `D8`/`D9`
(0x76C1/0x7C9E) both display the relief **depth** (0.0025 in relief_custom), not
the width. `D6` is resolved as PositionOffsetDistance by the position-offset
samples; PositionOffsetType stays unresolved (constant 1 in every native
snapshot with no disambiguating sample).

## Unresolved-field audit (2026-10-30)

All six original variants, the four position-offset samples and their resaves
were compared with the create12.json / verify-edgeflange-offset.json native
snapshots. OffsetDistance (D7), PositionType, ReliefWidth/Depth,
PositionOffsetDistance (D6) and UsePositionOffset vary and are resolved. The
following values are constant in every sample and therefore still have no
single-variable evidence: `OffsetType` = 1, `PositionOffsetType` = 1,
`UseDefaultBendRadius` = false, `UseDefaultBendAllowance` = true,
`UseDefaultBendRelief` = false, `AutoReliefType` = 1, `ReliefTearType` = 1,
`ReliefRatio` = 0.5, `UseReliefRatio` = true, `GapDistance` = 0.001,
`LockAngle` = true, `NormalToFlangePlane` = true,
`PerpendicularToFace` = false, `ReverseOffset` = false and
`UsePositionTrimSideBends` = false. The anonymous block gate
`ReliefRatio=0.5`/`u64=1` is likewise constant. These fields stay null with
resolution=false; dedicated one-variable samples are required.

Creating the samples requires the vendor-documented sequence:
`IModelDoc2::InsertSketchForEdgeFlange` -> edit the returned profile sketch ->
`SketchUseEdge` -> add the flange profile lines -> `CreateDefinition(swFmEdgeFlange)`,
`AddEdges` (typed `IEdge[]`/`ISketch[]` arrays are required), then `CreateFeature`.

Miter flange is not yet available: SolidWorks requires a profile sketch normal to the
first edge; the current 0x70 base-flange geometry plus legacy
`InsertSheetMetalMiterFlange` returned NULL for every tested sketch/selection
combination. This is recorded as an API/SOP gap, not a library failure.
