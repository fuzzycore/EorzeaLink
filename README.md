# EorzeaLink

Import and apply glamour sets from [EorzeaCollection.com](https://ffxiv.eorzeacollection.com) via [Glamourer](https://github.com/Ottermandias/Glamourer).

<img width="593" height="312" alt="image" src="https://github.com/user-attachments/assets/587c44a6-604f-4981-a71e-0119901e6ec6" />

## Features

- **`/elink`** — paste an Eorzea Collection glamour URL to preview and apply the set
- **Glam history** — sidebar list of recently applied glams with quick restore
- **Context menu** *(v0.1.7+)* — right-click an equippable item in inventory, armory chest, or glamour dresser → **See This On EorzeaCollection** opens a filtered glam search for that piece in your browser

## Install

Custom repo (add in Dalamud → Settings → Experimental):

```
https://raw.githubusercontent.com/fuzzycore/ffxivrepo/refs/heads/main/pluginmaster.json
```

Or download the latest zip from [Releases](https://github.com/fuzzycore/EorzeaLink/releases).

Requires **Glamourer** installed and enabled.

## Development

```powershell
dotnet build EorzeaLink\EorzeaLink.csproj -c Release
```

Copy `EorzeaLink\bin\Release\EorzeaLink.dll` and `EorzeaLink\bin\Release\Data\ec-piece-map.json` into your dev plugins folder (`Data/` subfolder next to the DLL).

Releases are built automatically when a `v*` tag is pushed (see `.github/workflows/release.yml`).

## Documentation

| Doc | Contents |
|-----|----------|
| [docs/CHANGELOG.md](docs/CHANGELOG.md) | Version history |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | Piece map architecture, rebuild instructions, release checklist |

The inventory context menu depends on a prebuilt **Lumina item ID → EC piece ID** map. See the maintenance guide for how to regenerate it when new gear is added to Eorzea Collection.

## Related repos

- **[EorzeaLink-proxy](https://github.com/fuzzycore/EorzeaLink-proxy)** — Playwright proxy for glamour page parsing; hosts the piece-map crawler
- **[ffxivrepo](https://github.com/fuzzycore/ffxivrepo)** — custom plugin repo manifest
