# Changelog

## v0.1.8 — Glamour dresser context menu & preview polish

### Added

- **Glamour dresser context menu** — right-click an item in the glamour dresser → **See This On EorzeaCollection** (same EC glam search as inventory).

### Changed

- **Preview table** — dye columns show stain names instead of numeric IDs; item ID column removed from the UI (IDs still used internally for Glamourer apply).

### Fixed

- **Dresser context menu crash** — avoid dereferencing invalid `AgentPtr` on glamour dresser menus.

---

## v0.1.7 — Inventory context menu

### Added

- **Inventory context menu** — right-click an equippable inventory item → **See This On EorzeaCollection** opens a filtered glam search on [ffxiv.eorzeacollection.com](https://ffxiv.eorzeacollection.com/glamours) for that piece.
- **Prebuilt piece map** — `EorzeaLink/Data/ec-piece-map.json` maps Lumina item IDs (`xivApiId`) to Eorzea Collection internal piece IDs (`ecPieceId`). Lookups are instant (no network).
- **`EcPieceMap`** — loads the map at plugin startup from the embedded resource or `Data/ec-piece-map.json` beside the DLL.
- **`EorzeaCollectionUrls`** — builds the full EC glam search URL including slot filter and `filter[save]=`.

### Changed

- Release zip now includes `Data/ec-piece-map.json` as a fallback alongside the embedded map in `EorzeaLink.dll`.

### Notes

- Only items that exist in the piece map and have a known equip slot show the menu entry.
- See [MAINTENANCE.md](./MAINTENANCE.md) for rebuilding the map when EC adds new gear.

---

## v0.1.6 — Glam history

- Sidebar history of applied glams (up to 20 entries) with quick restore.

## v0.1.5 — Dalamud API 15

- Updated for Dalamud API level 15.
