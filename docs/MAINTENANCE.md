# Maintenance guide

This document covers how the inventory → Eorzea Collection feature works and how to keep it up to date.

## Overview

| Component | Role |
|-----------|------|
| `ItemContextMenu.cs` | Hooks Dalamud inventory context menu; adds **See This On EorzeaCollection** |
| `EcPieceResolver.cs` | Resolves `xivApiId` → `ecPieceId` via config cache, then piece map |
| `EcPieceMap.cs` | In-memory dictionary loaded from JSON at startup |
| `EorzeaCollectionUrls.cs` | Builds filtered glam search URLs |
| `Data/ec-piece-map.json` | Source of truth for ID mappings (~27k entries) |
| `EorzeaLink-proxy` (separate repo) | Playwright scraper + `/parse` proxy; hosts map builder script |

## Why a piece map?

Eorzea Collection uses **its own piece IDs**, not Lumina/XIVAPI item IDs.

Example — **Heirloom Jacket of Aiming**:

| ID type | Value |
|---------|-------|
| Lumina / inventory `BaseItemId` | `32597` |
| EC piece ID (used in `filter[bodyPiece]=…`) | `18143` |

EC exposes `GET /gear/{ecPieceId}` as JSON with an `XIVApiId` field, but there is **no public reverse lookup** (Lumina ID → piece ID). Name search APIs are Cloudflare-blocked for automated clients.

**Solution:** crawl all EC gear IDs once, record `{ XIVApiId: ecPieceId }`, ship the JSON with the plugin. Lookups are O(1) and offline.

## Runtime flow

```
Right-click inventory item
  → BaseItemId (Lumina item ID)
  → GetEquipSlot() — skip if Unknown
  → EcPieceResolver
       1. PluginConfig.EcPieceCache (optional local cache)
       2. EcPieceMap.TryGet(xivApiId)
  → EorzeaCollectionUrls.GlamSearchUrl(ecPieceId, slot)
  → Util.OpenLink(url)
```

On startup, `/xllog` should show:

```
EcPieceMap: loaded 26956 entries
```

If a lookup fails, chat shows the item ID and map size — `map has 0 entries` means the map file failed to load (wrong/old DLL).

## Map file format

`EorzeaLink/Data/ec-piece-map.json`:

```json
{
  "32597": 18143,
  "10056": 1234
}
```

- **Keys:** Lumina item ID (`XIVApiId` from EC gear JSON)
- **Values:** EC internal piece ID (`ID` from EC gear JSON)

The file is:

1. **Embedded** in `EorzeaLink.dll` (`EmbeddedResource` in `.csproj`)
2. **Copied** to build output as `Data/ec-piece-map.json`
3. **Included** in release zip under `Data/`

`EcPieceMap` tries embedded resource first, then files beside the DLL.

## Rebuilding the piece map

The crawler lives in the **[EorzeaLink-proxy](https://github.com/fuzzycore/EorzeaLink-proxy)** repo (sibling project).

### Prerequisites

- Node.js
- Playwright (`npm install` in `EorzeaLink-proxy`)

### Run the builder

```powershell
cd ..\EorzeaLink-proxy
node build-ec-piece-map.mjs 27508 ec-piece-map.json 8
```

| Argument | Default | Meaning |
|----------|---------|---------|
| `maxId` | `27508` | Highest EC gear ID to crawl (re-run `find-max-gear-id.mjs` if EC grows) |
| `outFile` | `ec-piece-map.json` | Output path |
| `workers` | `8` | Parallel Playwright contexts |

Expect ~15–20 minutes for a full crawl. The script uses `page.goto` to `GET /gear/{id}` (no JS) — the only reliable automated access pattern.

### Install updated map in plugin

```powershell
Copy-Item ..\EorzeaLink-proxy\ec-piece-map.json `
  EorzeaLink\Data\ec-piece-map.json -Force
dotnet build EorzeaLink\EorzeaLink.csproj -c Release
```

Verify in `/xllog` after reload that entry count increased.

### Optional: refresh proxy copy

The proxy repo also stores `ec-piece-map.json` for reference. Commit there if you want the crawler output versioned separately from plugin releases.

## Release checklist (piece map changes)

When EC adds gear and the map needs updating:

1. Rebuild map (above)
2. Copy into `EorzeaLink/Data/ec-piece-map.json`
3. Bump `<Version>` in `EorzeaLink/EorzeaLink.csproj`
4. Commit, tag (`v0.1.x`), push tag → GitHub Actions builds release zip
5. Update [ffxivrepo `pluginmaster.json`](https://github.com/fuzzycore/ffxivrepo):
   - `AssemblyVersion`
   - `DownloadLinkInstall` / `DownloadLinkUpdate` → new tag URL
   - `LastUpdate` → Unix timestamp

Release workflow (`.github/workflows/release.yml`) publishes `EorzeaLink.dll`, `EorzeaLink.json`, and `Data/ec-piece-map.json`.

## Local dev install

**Do not** copy a stale `dist/EorzeaLink.dll` — it may predate the embedded map.

Use a fresh build:

```powershell
dotnet build EorzeaLink\EorzeaLink.csproj -c Release
```

Copy to your dev plugin folder:

- `EorzeaLink\bin\Release\EorzeaLink.dll`
- `EorzeaLink\bin\Release\Data\ec-piece-map.json` → `Data/` subfolder next to the DLL

Or install via the GitHub release zip after tagging.

## Proxy repo (`EorzeaLink-proxy`)

Separate repository for:

| Endpoint / file | Purpose |
|-----------------|---------|
| `GET /parse` | Scrape EC glamour pages for `/elink` import (Playwright + HMAC auth) |
| `GET /ec-piece` | Legacy runtime lookup (slow scan); **not used by plugin v0.1.7+** |
| `build-ec-piece-map.mjs` | One-off map generator |
| `ec-piece-map.json` | Committed crawl output (optional reference) |
| `find-max-gear-id.mjs` | Binary search for highest valid EC gear ID |

The plugin no longer calls `/ec-piece` at click time; the prebuilt map replaced ~30s proxy scans.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Menu item never appears | Item slot is `Unknown` (not equippable glam piece) | Expected for non-gear items |
| `map has 0 entries` | Old DLL or missing JSON | Reinstall from release; ensure `Data/ec-piece-map.json` exists |
| `No match for item XXXXX` | Item not in map (new patch gear) | Rebuild map, ship plugin update |
| Wrong search results | Stale map entry (rare) | Rebuild map; EC IDs can shift on major site changes |
| Context menu works in dev, not release | Forgot to commit updated JSON | Commit `Data/ec-piece-map.json` before tagging |

## Key files (quick reference)

```
EorzeaLink/
  ItemContextMenu.cs       # Context menu hook
  EcPieceResolver.cs       # Lookup orchestration
  EcPieceMap.cs            # Map loader
  EorzeaCollectionUrls.cs  # URL builder
  Data/ec-piece-map.json   # Mapping data (commit this)
  PluginConfig.cs          # EcPieceCache (optional per-user cache)

EorzeaLink-proxy/          # separate repo
  build-ec-piece-map.mjs   # Map builder
  ec-scraper.mjs           # Playwright helpers
  server.mjs               # Railway proxy
```
