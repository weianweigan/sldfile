# TutorialSheetMetal — SolidWorks 2026 tutorial samples with Jog/Fold/UnFold

Copied unchanged from the installed SolidWorks 2026 tutorial content by the
2026-10-31 corpus scan (`reverse_workspace/validation/agent2-tutorial-scan-20261031`).
These are the first real samples for the previously blocked features and use
much newer ResolvedFeatures generations than the controlled Task 2.3 samples.

| File | Source | SHA-256 | Classes |
| --- | --- | --- | --- |
| `Paper Airplane.SLDPRT` | `...\samples\learn\Paper Airplane.SLDPRT` | `076F784777A85932F6AF07D825640AC1893AA27C892A0FAB35BDDF6299791423` | `moUnFold_c` 0x144, `moFold_c` 0x153 (version 0x155, folders 38) |
| `Complex Sheet Metal Part.SLDPRT` | `...\samples\learn\Sheet Metal\Complex Sheet Metal Part.SLDPRT` | `A08A81C84A0B38828547F275B622DDD9FF7E1403AD79E5A012D09823ECD56BB2` | `moJog_c` 0x159 (version 0x1AA, folders 50) |
| `drive sled.sldprt` | `...\samples\tutorial\Sustainability\drive sled.sldprt` | `F423BD5C426D932C73B94E8751794C8B26EE16B7E27F65AD7A82412A330CA88A` | `moUnFold_c` 0x209, `moFold_c` 0x213 (version 0xC7, folders 76, Config-1 stream) |

## Reading evidence (native inspect, 2026-10-31)

- Jog1: `IJogFeatureData` BendRadius 0.00254 m, JogAngle π/2, OffsetDistance
  0.0127 m, useDefaultBendRadius=true, useDefaultBendAllowance=true,
  offsetType=1, jogPositionType=2, dimensionPositionType=2,
  fixProjectedLength=true, reverseDirection=true, reverseOffset=false.
  The parser resolves all numeric/flag values from three anchored tutorial
  blocks (see `agent2-tutorial-field-probe-20261031/`). FixedFace stays null:
  native is an IFace2, but only an edge-ref pair and a later face-ref feature
  exist near the Jog.
- Unfold2/Fold2 (Paper): `IFoldsFeatureData` IGetBendsCount=3, fixed face and
  three OneBend selections (EdgeBend1, EdgeBend2, SketchBend1).
- Unfold1/Fold1 (drive sled): IGetBendsCount=4.
  The parser resolves TypeId and BendsCount by counting unique EdgeBend*/
  SketchBend* WChar names before the feature. FixedFace and individual bend
  references remain unresolved.
- All three documents open with 0 errors/warnings and rebuild true; the files
  are unchanged (SHA before/after equal).

Miter and SM NormalCut have zero hits in the corpus and remain BLOCKED.

Fixed authoring templates and their Primary/Resave artifacts live under
`reverse_workspace/validation/agent2-tutorial-authoring-20261031/`.
