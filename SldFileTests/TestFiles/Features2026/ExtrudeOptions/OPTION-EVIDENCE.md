# Extrusion options: evidence slice, 2026-10-30

This slice establishes ReverseDirection reading for native moExtrusion_c streams version 0x70 only. Reverse authoring and the option authoring slices (two directions, draft, start offset) were implemented later; see README.md's Reverse authoring validation and Authoring extension sections.

## Controlled evidence

Source inputs: FeatureLab/Program.cs extrude_reverse sets Dir=true; the seven controls set Dir=false. Draft cases set Dchk1=true and Dang1=pi/60 or pi/30. Offset cases set T0=3 and StartOffset=.002 or .004. JSON snapshots alongside this report record a native reopen, rebuild=true, physical properties and SHA-256. The reopen happened in the same SOLIDWORKS process; process isolation was not enforced for this reading slice. Fresh-process isolation of authoring is covered separately by the Agent 5 audit (`reverse_workspace/validation/agent5-audit-20261030/manifest.json`).

The relevant field is a uint32 at offset 10 relative to the data immediately after the moEndSpec_c class-name bytes (data includes the two initial zero bytes). The schema starts u16=0, u32 at 2 is 0 or 1, u32 at 6=0, u16 at 14=0. Reverse is exactly 0 or 1; unknown schema/version/class/flag values are not promoted to a value.

| Sample | moEndSpec tag offset | data offset | reverse at data+10 |
| --- | ---: | ---: | ---: |
| baseline | 10105 | 10122 | 0 |
| baseline resave | 10335 | 10352 | 0 |
| reverse | 10105 | 10122 | 1 |
| draft 3deg | 10105 | 10122 | 0 |
| draft 6deg | 10105 | 10122 | 0 |
| offset 2mm | 10105 | 10122 | 0 |
| offset 4mm | 10105 | 10122 | 0 |
| two directions | 10105 | 10122 | 0 |

Baseline resave inserts objects before the end specification, moving its absolute location by 230 bytes. The field and surrounding header remain identical. Reverse also changes the repeated boolean within moFromEndSpec_c, independently supporting the interpretation. The implementation reads the parsed EndSpec child owned by the extrusion, never a file-wide float or byte search.

## Candidates deliberately left unimplemented

Draft3/6 insert moDisplayAngularDim_c -> AngleDim_c -> moAngleParameter_c before moFromEndSpec_c. The angle parameter named D3 has the common named-parameter signature and values pi/60 and pi/30 respectively. The role/enable/direction relationship still needs structural isolation; no generic angle is labelled draft based on its value alone.

Offset2/4 extend moFromEndSpec_c by 1747 bytes. A D19 length parameter through indexed class references has values .002/.004 at relative offset 713; the other copies are geometry/display data. Explicit parameter/reference ownership and start-type mapping remain unfinished.

## Test assets

Eight native samples were copied to SldFileTests/TestFiles/Features2026/ExtrudeOptions/. ExtrudeOptionReadingTests reads all controls and the reverse sample, also asserting the main depth remains .01m. It additionally rejects unknown stream versions, Cut class, truncated data and non-boolean flag values. Source samples and extracted *.resolved.bin streams remain in samples/ for reproducibility.

Verification: focused ExtrudeOptionReadingTests passed 9/9, zero skipped (2026-10-30). Build succeeded; test execution required the established local testhost socket escalation.

## Second slice: draft and start offset (implemented)

The earlier candidates above are now promoted only under these structural restrictions:

- Single-direction moEndSpec_c: u16+0=0, u32+2=1, u32+6=0, u32+10 in {0,1}, u16+14=0. Other direction layouts are excluded.
- Draft: exactly one moAngleParameter_c, preceded in the same feature by moFavoriteHandle_c -> moDisplayAngularDim_c -> AngleDim_c. The u32 at 24 bytes before the display object's tag must be 1 and must lie within the Favorite object's range; its following 20 bytes must be zero. This flag is zero in baseline/resave/reverse/offset controls and one in both draft amplitudes. The angle parameter must have schema 0x8004, a D<number> WChar name, and the full parameter metadata header: double 2.0, u32 FFFFFFFF, u32 zero, empty WChar, u16 zero, then finite double value. Read results are pi/60 and pi/30 radians. Both DraftWhileExtruding and DraftAngle are bound. DraftOutward and direction-2 draft are still unknown.
- Offset: the feature-owned moFromEndSpec_c starts with internal discriminator 5 (API FromType=3), an indexed reference, and sixteen zero header bytes. Exactly one complete named parameter with the header above is required. At relative offset 713 its scalar is .002/.004; these values are structurally decoded from the named parameter, not selected by floating-point value. FromType and FromOffsetDistance are bound. Flipped offset remains unimplemented.

The generic named-object scanner truncates angle RawData at a FFFF sentinel in metadata (DataStart=12788, DataEnd=12808). Therefore this reader bounds the angle parameter by the next named object within the same feature, and the FromEndSpec block by feature end. It does not alter the generic scanner and never searches outside the owning feature.

All eight native samples are exercised for draft/offset values and unchanged main .01m depth. Added malformed tests cover missing draft enable, truncated offset metadata/value, unknown offset discriminator, duplicate complete parameter blocks and malformed WChar count.

Verification: build succeeded, zero warnings/errors; ExtrudeOptionReadingTests passed 19/19, zero skipped. Two-direction depth was subsequently established with the 3/5/7 mm controls and the resave; see two-direction-evidence.md and README.md's Authoring extension section. Authoring is bounded to 0x70/0x71 streams with a parsed moSketchChain_c and the 0x67 marker (options: 0x70 only); see README.md.
