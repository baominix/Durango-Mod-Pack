# Tamed Island Restoration Plugin

Restores the Original client's offline Domain/Tamed Island requests while
keeping Tamed and Unstable islands as separate Harbor destinations.

The restored selector exposes all five packaged personal terrains,
`pe10gr_1` through `pe10gr_5`, with their matching original region templates.
They are exposed as `Role.Personal`, use `tamed.<terrain>.10` snapshots, and do
not share the Unstable route IDs or saves. The plugin remembers the selected
personal terrain and also recognizes it when a profile starts on one of those
five layouts.

Implemented offline responses:

- `GetEstateLicenses`
- `GetPersonalRegionInfo`
- `RecommendPersonalRegion`
- `GetEstateLicenseById`
- `VisitEstate`
- `ReturnToEstate`
- `EstateGrids` synchronization around the local player
- `DeclareEstate` for player-selected first-cell placement
- `ExpandEstate`
- `ShrinkEstate`
- `RemoveEstate` (resets the permanent personal estate to one cell)

The Tamed Island starts without an estate. The player uses the Original
declaration grid to choose the first 4x4 cell; only then is the license created.
The estate cells are stored per player and per Tamed terrain. The expansion
limit now follows the Original 15-rank Tamed Island Pioneer progression:
100/150/200/250/300/350 plots at ranks 1/2/6/7/11/15. The Original client
normally hides Reduce buttons for `PersonalPlayer`; this plugin restores them
for Tamed regions. Releasing the last cell returns the island to the undeclared
state so the player can select a new location.

The plugin also replaces the stock offline `Welcome` region only while a
`pe10gr_*` Tamed terrain is loaded. The stock server reports every map as
region `1` / `Role.Rural`; Tamed maps are now reported with their matching
`tamed|<terrain>|10` ID, `Role.Personal`, and `PersonalRegionId`. Terrain ID
remains `1` because the Original offline Gateway serves terrain data at that
fixed endpoint.

Pioneer Rank is persisted per player. The Original `GetPioneerGradeInfo` and
`UseItemsForPioneerPoint` messages are restored. Players build a Personal
Communication Station (`operating_office_01`) and transmit valid
`pioneering_material` inventory items; their `PioneerCost` is converted using
the Original daily rate table. Rank 3-5, 8-10, and 12-14 unlock Life, Light,
and Heavy Tech Lab tiers I-III respectively.

Existing and newly created offline items have the retail server-owned
`PioneerCost` and pioneering-material tag level restored so valid materials
appear in the Personal Communication Station list. Tradable restoration is
owned by the separate `TradeAvailablePlugin`.

`CashShopRestorationPlugin` can activate the Original seven-day Signal
Amplifier. Its persisted expiration unlocks the paid 400% daily Pioneer rate,
appears in the Original popup timer, and is honored by item transmission.

For isolated Pioneer transmission testing, Chat Command Plugin provides
`/givepioneer [prototype] [level] [count] [tagLevel]` (alias `/gpioneer`).
With no arguments it creates 10 `clam_product` items at level 1 with
`pioneering_material` level 1.
