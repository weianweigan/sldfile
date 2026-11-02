# LoftedBendOptions — Task 2.3 lofted bend samples

Controlled non-formed lofted-bend specimen produced by the timed native retry
round (`reverse_workspace/validation/agent2-sheetmetal-blocked-retry-20261030/`)
with the SolidWorks 2026 API (`InsertSheetMetalLoftedBend2`), plus its native
save/resave. The feature joins two straight line profiles on a sheet-metal base.

| File | SHA-256 | ResolvedFeatures stream | Notes |
| --- | --- | --- | --- |
| `lofted_bend_2lines.SLDPRT` | `938D01851ACB428C6E4B9E7FC50A9519C9D19CE84E1CA5038DC65B3823E10620` | 59,600 bytes | Primary native sample |
| `lofted_bend_2lines_resave.SLDPRT` | `68AB9C0E658BE35310A53E42E1F2E816A140BB5B5B7E7DCDA39092D158371F95` | 59,912 bytes | Resave of the primary |

## Serialization facts

- Stream version: `0x74` (`Contents/Config-0-ResolvedFeatures`).
- MFC class: `moLoftedBend_c`, feature tag byte `0x01`.
- FolderHeader TypeId: **`0x72`** (the catalog/TODO candidate `0x69` was not observed).
- Top-level folder count: 26.
- Primary stream feature tag (`moLoftedBend_c`): `0x6043`; the measured 8-byte
  `0x67` marker block is at `0x6033` with value 19.
- The formed variant produced during the retry (`formedMethod=true`) uses
  TypeId `0x6E` and a different layout; it is not part of this sample set and is
  rejected by the strict parser.

## Resolved fields (`LoftedBendFeatureDefinitionData`)

Thickness and NumberOfBendLines are resolved; everything else stays null with
its resolution flag false.

| Field | Evidence |
| --- | --- |
| `Thickness` | Double at **D14 name − 0xBB4** = 0.001. Layout gates: two `1.0` doubles at **−0xBC5** and **−0xBBD**, and a second positive 0.001 companion at **−0xBAC**. |
| `NumberOfBendLines` | Value of the unique named scalar record **D14** (a 3-character WChar name) = 2. |

Primary slot offsets: D14 name `0x77B4`, Thickness `0x6C00`, gates
`0x6BEF`/`0x6BF7`, companion `0x6C08`. Resave (records shifted): D14 `0x78EC`,
Thickness `0x6D38`, gates `0x6D27`/`0x6D2F`, companion `0x6D40`.

### Unresolved fields

`Direction`, `BendLineControlOption`, `MaximumDeviation`, `ReferToEndPoint`,
`FacetingOption`, `FacetValue`, `FormedMethod`, `SymmetricThickness`,
`Profiles` (the two line-profile references) and `ProfileCount`. A single
variant sample with `referToEndPoint=true` changed only the Thickness slot, so
there is no reliable serialized slot or varying sample for these values; the
parser returns them as null rather than guessing.

## Authoring template

Fixed measured authoring (`SldPart.AddLoftedBend`):

- Stripped base prefix length **0x6043** (keeps the sheet-metal body and the two
  line profiles).
- Normalized prefix SHA-256 (folder count zeroed at offset 4):
  `A94F196BD56D6F0014146C5F527C501600AE3EE6D66CD9B8DFC58ABFD5B597DD`.
- Full folder count 26; measured `0x67`/19 marker at `0x6033`.
- Suffix (feature tag to stream end) SHA-256:
  `FB76CA2461DF51899797C161FD607703583E359AB293E2CA02B6437A2A1499D3`
  (34,957 bytes, embedded gzip/base64).
- Fixed values: display name `LoftedBend`, Thickness 0.001 m,
  NumberOfBendLines 2, two line profiles. The other API values may be null or
  equal the measured create snapshot (`Direction=false`,
  `BendLineControlOption=0`, `MaximumDeviation=0.0005`, `ReferToEndPoint=false`,
  `FacetingOption=0`, `FacetValue=0.001`, `FormedMethod=false`,
  `SymmetricThickness=false`, `ProfileCount=2`); other values, persistent
  profile references, custom names, another stream version/base and a
  non-stripped document are rejected before the stream is modified.

Native verification of these authored files passed (Agent 5, 2026-10-31):
OpenDoc6 0/0, feature error 0, readback Thickness 0.001 / ProfileCount 2 /
NumberOfBendLines 2, volume 4.0e-7 m^3, area 0.00092 m^2, and the design stream
stayed byte-identical to the native source. Evidence:
`reverse_workspace/validation/agent5-loftedbend-authoring-verify-20261030/verify.json`.
