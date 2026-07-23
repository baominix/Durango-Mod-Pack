# Terrain entry-point and Harbor audit

Source: packaged `Resources/offline/terrains/*.bytes` in the Original client.

| Terrain | Entry points |
|---|---|
| pe10gr_1 | (63,71) |
| pe10gr_2 | (179,54) |
| pe10gr_3 | (146,35) |
| pe10gr_4 | (165,57) |
| pe10gr_5 | (76,32) |
| ra60sw | (73,92), (144,123) |
| ri35de | (28,52) |
| ri35te | (40,177) |
| ri40tr | (107,224) |
| ri45sa | (28,130) |
| ri50sn | (137,43) |
| ri55tu | (42,72) |
| ua60vol | (250,116) |

There are 14 entry-point records across 13 packaged terrains. Twelve terrains
have one record. `ra60sw` is the only terrain with two records.

The Original offline `World.EntryPoint` implementation reads only
`entry_points[0]`. Harbor spawning follows `World.EntryPoint`, creates at most
one automatic Harbor (`harbor:auto:<terrain>`), and reuses an existing entity
type 7001 near that entry. It does not create one Harbor for every entry-point
record.

Audit of slot 9 on 2026-07-16 found one automatic Harbor in every current
Harbor snapshot. No snapshot contained two Harbor entities. The slot 9 home
snapshot had two total artifacts, but only one was a Harbor; the second was a
camp fire.
