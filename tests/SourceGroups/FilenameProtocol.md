# Two-character source-group filenames

Numeric source-group IDs remain numeric in source definitions, GRAL.geb, temporal headers, receptor headers and decay settings. The supported public range is 1..1295. Only the filename token is encoded.

| IDs | Filename tokens |
| --- | --- |
| 1..99 | 01..99 |
| 100..359 | A0..A9, B0..B9, through Z0..Z9 |
| 360..619 | 0A..9A, 0B..9B, through 0Z..9Z |
| 620..1295 | AA..AZ, BA..BZ, through ZA..ZZ |

00 is reserved. Output uses uppercase ASCII letters and digits. Decoding accepts their lowercase ASCII equivalents, without assigning separate IDs to them. This gives 1295 unique names on filesystems that ignore case. The proposed A0/a0 distinction would collide on ordinary Windows directories, so lowercase letters do not extend this alphabet.

Concentration and odour names retain their existing five-digit situation, hyphen, one-digit slice and two-character group token. Deposition has no slice. Examples for situation 1, slice 1:

| Group | Concentration | Deposition | GUI modulation |
| --- | --- | --- | --- |
| 1 | 00001-101.con | 00001-01.dep | emissions001.dat |
| 99 | 00001-199.con | 00001-99.dep | emissions099.dat |
| 100 | 00001-1A0.con | 00001-A0.dep | emissions0A0.dat |
| 1295 | 00001-1ZZ.con | 00001-ZZ.dep | emissions0ZZ.dat |

Odour uses the same name as concentration with .odr. Compressed and standalone outputs use the same tokens. The GUI's existing three-character modulation suffix keeps its leading zero: emissions0 plus the two-character token.

IDs 1..99 therefore retain byte-for-byte identical filenames. Extended IDs require both patched core and GUI. This is a proposed shared format for upstream review, not a claim that older GUI versions understand extended IDs.

The earlier experimental branch used decimal filenames at ID 100 and above. Do not mix those outputs or modulation files with this format. Keep that project with the archived Int32 build, or use a separate computation directory and regenerate modulation and results. IDs above 1295 must be renumbered only through an explicit source/temporal mapping; they are rejected rather than silently clamped.
