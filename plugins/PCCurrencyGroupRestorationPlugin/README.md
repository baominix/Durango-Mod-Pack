# PC Currency Group Restoration Plugin

Restores the original mobile T-Stone and Warp Gem strip in the PC Bag only.

Version 1.9.0 keeps the first working restoration method: it prevents
`CurrencyWidgetTweakerForPC.Awake` from disabling the retained mobile currency
widgets. The bypass is now strictly scoped to a tweaker whose parent is
`InventoryGroup`, so the widgets do not appear on Skill, Domain, Shop, or other
PC pages.

No replacement or cloned UI is created. `InventoryGroup_PC`'s original
`CurrencyWidget` objects and the packaged mobile `PresetCurrencyWidget` prefab
are allowed to initialize through the normal client lifecycle. Offline visual
creation and wallet updates are supplied by `CashShopRestorationPlugin`.

The original holders retain their working horizontal layout, while a Bag-only
binder installed from `InventoryGroup.Open` or `CurrencyWidget.MakeComponent`
aligns their vertical centre to the instantiated `UITitleWidget_PC`.
This keeps both balances attached to the black Bag title bar across window and
anchor changes instead of overlapping the tab row below it.

The currency strip is positioned automatically from the actual left edge of
the Bag Close button. This avoids scaling errors from a fixed left offset.
`Layout.CloseButtonGap` controls only the small gap between the strip and the
Close button; its default is 10 UI units.
