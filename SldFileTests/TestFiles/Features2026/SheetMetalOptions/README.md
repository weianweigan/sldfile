# SheetMetalOptions — base-flange native samples

Controlled base-flange samples produced by the Agent 5 sheet-metal harness and
the 2026-10-31 remaining-field retry (`agent2-baseflange-field-retry-20261030`).
All are L-profile base flanges with T=0.001 m, R=0.002 m, D1=0.010 m unless
noted.

## Baseline and option matrix

The original matrix (`sm_l_d10`, `sm_l_dir2_*`, `sm_l_k03_fixed`,
`sm_l_override_radius`, `sm_l_relief_*`, `sm_l_gauge_baseline`, ...) is
documented by its `*.json` readback snapshots. KFactor is read from the global
`moSheetMetal_c` named D2; relief ratio from the template D6.

## 2026-10-31 retry additions

| File | Native readback | Status |
| --- | --- | --- |
| `sm_l_allowance_kfactor_persisted.SLDPRT` (+ resave) | type 2, KFactor 0.3 | PASS |
| `sm_l_allowance_direct_persisted.SLDPRT` (+ resave) | type 3, BendAllowance 0.001 | PASS |
| `sm_l_allowance_deduction_persisted.SLDPRT` (+ resave) | type 4, BendDeduction 0.001 | PASS |
| `sm_l_gauge_table.SLDPRT` (+ resave) | UseGaugeTable true, name 规格 5, table T/R/K 0.001/0.001/-1 | PASS |
| `sm_l_relief_global_override.SLDPRT` (+ resave) | UseDefaultBendRelief false, ReliefType 2; width/depth/ratio 0/0/true | PARTIAL |

Serialized slots used by the parser:

- Allowance type byte at `moSheetMetal_c` D2 name − 0x2A2: 1→KFactor(2),
  2→Direct(3), 3→Deduction(4). D3 carries the allowance/deduction value.
- `UseDefaultBendRelief` = bit 0x40 of byte `moSheetMetal_c` data + 0x1B.
- `moSMEnvTableFeat_c` presence = UseGaugeTable true; its WChar name is the
  stored table name. The full gauge-table path is stored as a WChar record.
- OverrideRadius=true, custom relief width/depth/ratio and bend-table persistence
  are BLOCKED natively and remain unresolved/rejected.

The original `AddSheetMetalBaseFlange` (`sm_l_d10` suffix) still rejects these
options; the fixed retry templates are authorable through the separate method
below.

## Fixed option-template authoring (2026-10-31)

`SldPart.AddSheetMetalBaseFlangeFromOptionTemplate` accepts the stripped base
of the matching retry sample (ResolvedFeatures truncated at the
`moSMBaseFlange_c` tag, 0x3EF1 bytes) and appends the measured suffix verbatim.
Supported fixed combinations: KFactor 0.3, Direct 0.001, Deduction 0.001 and
gauge table. Authored streams are byte-identical to
`sm_l_allowance_kfactor_persisted`, `sm_l_allowance_direct_persisted`,
`sm_l_allowance_deduction_persisted` and `sm_l_gauge_table`. This is a separate
fixed-geometry group from the `sm_l_d10` authoring template (same controlled
open-L sketch, different stream generation); do not mix the bases.
