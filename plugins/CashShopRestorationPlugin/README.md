# Cash Shop Restoration Plugin

Restores the Original Cash Shop in offline mode by enabling the Shop menu and
answering the missing shop protocol requests. The client continues to use its
packaged commodity, category, localization, icon and confirmation UI data.

Implemented requests:

- `GetCommodities`
- `GetPurchases`
- `GetAcceptableSubPurchases`
- `GetUserFirstPurchaseHistory`
- `GetSpecialDeals`
- `PurchaseCommodity`
- `PurchaseCommodityWithVoucher` fallback
- `AcceptPurchase`
- `PlaceCapsulatedArtifact` for purchased buildings

Each offline profile receives persistent configurable Coin, Warp Gem and
T-Stone balances. Purchases are persisted in the retail Storage page. Currency
packages are opened there, and items can be received exactly once before their
Storage entry is permanently removed. First-purchase bonuses and history are
also persisted. Regular items, fixed building capsules, and modular building
capsules can be purchased locally; received building capsules contain their
blueprint/display data and can be placed by the restored offline backend.
Legacy fixed-building items made by plugin 0.1.x are repaired on login.
Unsupported server-only effects are rejected rather than granting incomplete
rewards.

Version 0.2.4 restores the retail currency widgets which the client normally
refuses to instantiate outside Online cluster mode. The PC title tweaker is
bypassed only inside ShopGroup. Other PC screens retain their original layout,
preventing duplicate Skill Point widgets and misplaced currencies in narrow
panels such as Bag. The Shop header displays live Coin, Warp Gem and T-Stone
balances. The Menu wallet button is also enabled and opens the complete
currency summary backed by the persistent offline wallet.

Version 0.2.4 also supplies the retired `/prototypes/{id}/{level}` response
locally from the packaged prototype and performance YAML. This restores the
original 3D preview pipeline used by the Animal `ModelView` page and by the
selected contents of the Express Cargo `RandomBox` page. Pet previews receive
their required `pet_entity_type`; equipment, instruments and building capsules
receive their corresponding local preview metadata.

`signal_amplifier_package` and its special-deal variant activate the persisted
seven-day Pioneer 400% zone through Tamed Island Restoration Plugin 0.7.0. The
gift button in the Original Pioneer Points popup already links directly to
this commodity, so no replacement UI patch is needed.
