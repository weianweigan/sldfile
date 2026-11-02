# Sheet metal forming samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-hem/`.
Copied without modification.

| Sample | SHA-256 | Native `IHemFeatureData` |
| --- | --- | --- |
| `sheetmetal_hem_open.SLDPRT` | `673ab12d23b58194ec0e969d4864c5658b45e8af3e618adec225e710a3f43005` | Type=0 (open), length=.005, gap=.0005, radius=.001, angle=π, edges=1, V=2.97123889803847e-7, area=0.000663960168588078, centroid (0.01,0.0057868702,-2.42e-6) |
| `sheetmetal_hem_closed.SLDPRT` | `d5bfb9aa5facd9bc6d909d8c8d0bdf6891a6711e9a38c1e367f39cee04dfea59` | Type=1 (closed), length=.005, gap=.0001, radius=.001, angle=π, edges=1, V=2.92557519189488e-7, area=0.000654370790297924, centroid (0.01,0.0057314306,-0.0001379972) |
| `sheetmetal_hem_open_resave.SLDPRT` | `d3ca95aeaafa79e637dc685934e7d2aeca3c3116d37345599e8a472e26a8d366b` | same definition, rebuild verified |
| `sheetmetal_hem_closed_resave.SLDPRT` | `0890fc22a0222480d81b46855ec9d05462910acda96659d242019d058b421d79` | same definition, rebuild verified |
| `hem_closed_len10.SLDPRT` | `6268572F5B98FE3E523AF3E961A2503832A38A08FDA00D41C7828EBCAFFEAC6A` | Type=1, Length=.010, Gap=.0001, Radius=.001, Angle=π, Miter=.0005, V=3.92557519189488e-7 |
| `hem_closed_len10_resave.SLDPRT` | `7933D5B013BB3420C3CE9889473487CC2C362923BE1B6B31874B5DFF2CBF6C3A` | same definition, rebuild verified |
| `hem_open_mitergap1.SLDPRT` | `7E7CFDA609560804FCDBE43BD6EAF10C9191F001718723700D31B6332F66F83F` | Type=0, Length=.005, Gap=.0005, MiterGap=.001, V=2.97123889803847e-7 |
| `hem_open_mitergap1_resave.SLDPRT` | `D9086D2603C9E7503178AFA78A2620AEE6F84C9595B312F4985F100997AF95CA` | same definition, rebuild verified |

All four: OpenDoc6 0/0, `ForceRebuild3(true)`, feature error/warning 0, one body, hash unchanged. The hem edge selection was made with `SelectObject`/`SelectEdge` mark 0; edge persist reference and full `IHemFeatureData` snapshot are in `verify-hem.json`.

## Verified serialized mapping (library evidence, 2026-10-30)

All four streams are ResolvedFeatures **version 0x73**. The actual class is
`moHem_c` with FolderHeader **TypeId 0x67** (not the catalog candidate 0x65);
the selection block follows the feature folder. Type is the u16 at **D8 value
+ 0x6E**: 0 = native Type 0 (Open), 1 = native Type 1 (Closed), confirmed by all
five open variants and both closed samples/resaves. The first D1 value is gap/2
and serves as a layout gate.

| API value | Serialized evidence |
| --- | --- |
| `Type` | u16 at D8 value + 0x6E (0 Open, 1 Closed) |
| `Length` | named `D7` (0.005 baseline; 0.010 len10 sample) |
| `GapDistance` | named `D8` after D7 (0.0005 baseline; 0.001 gap1 sample; 0.0001 closed) |
| `Angle` | named `D4` (3.1415926535898 in both) |
| `Radius` | named `D1` between D4 and D7 (0.001 in both) |
| `MiterGap` | named `D9` after D7 (0.0005 baseline; 0.001 mitergap1 sample) |
| `EdgeCount` | first u32 of the single `moEdgeRef_c` (1) |

`BendPosition` (0), `ReverseDirection` (false) and `UseDefaultBendAllowance`
(true) are constant in both samples and are not exposed by the parser.

## Parameterization sample matrix

Implemented from the variants: Open Length 0.010 (hem_open_len10), Open
Gap 0.001 (hem_open_gap1), Closed Length 0.010 (hem_closed_len10) and Open
MiterGap 0.001 (hem_open_mitergap1), each with matching stripped-base
signatures. Radius 0.002 (hem_open_rad2) changes the API callout but not the
geometry (same volume as baseline) and is read-only/callout-only for authoring;
Angle 3π/4 does not persist (native readback stays π) and is rejected.
Closed Gap 0.0005 was requested but native readback still returns 0.0001
(blocked, not persisted), so it stays rejected. Arbitrary values and remaining
combinations require the following single-variable samples:

| # | Type | Changed field | Baseline values for the others |
| --- | --- | --- | --- |
| 1 | Open | Length = 0.0075 | Gap 0.0005, Radius 0.001, Angle π, MiterGap 0.0005 |
| 2 | Open | Gap = 0.0002 | Length 0.005, Radius 0.001, Angle π, MiterGap 0.0005 |
| 3 | Open | combined Length 0.010 + Gap 0.001 | Radius 0.001, Angle π, MiterGap 0.0005 |
| 4 | Closed | Length = 0.0075 | Gap 0.0001, Radius 0.001, Angle π, MiterGap 0.0005 |
| 5 | Closed | Gap (only after a sample shows real persistence) | Length 0.005, Radius 0.001, Angle π, MiterGap 0.0005 |
| 6 | Closed | MiterGap = 0.001 | Length 0.005, Gap 0.0001, Radius 0.001, Angle π |

Each sample should also be resaved; the readback must include Type, Length,
GapDistance, Angle, Radius, MiterGap, BendPosition, ReverseDirection,
UseDefaultBendAllowance, edge count, feature error/warning and body volume. These
samples allow locating every derived display/geometry cache before arbitrary
authoring values are exposed.

## Blocked / not started

- `sheetmetal_hem_teardrop`: `InsertSheetMetalHem2` returned NULL with the same edge; blocked, no file.
- Jog (`IModelDoc2.InsertSheetMetalJog`), standalone SM normal cut (`AddSMNormalCut`), fold and unfold remain blocked; see `reverse_workspace/validation/agent2-sheetmetal-blocked-retry-20261030/`.
- Lofted bend was sampled in that retry round: `LoftedBendOptions/` holds the non-formed 0x72 specimen; fixed-face/corner-treatment flat-pattern variants live in `FlatPatternOptions/`.
- The existing NormalCut extrusion sample is `features2026/ExtrudeOptions/extrude_normal_cut.SLDPRT`; it is a sheet-metal normal cut created through `FeatureCut4`, not through the standalone `AddSMNormalCut` API.
