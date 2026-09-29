## ⚠️ AI DISCLAIMER: This project is a vibecoded project.
I have not doublechecked every line of code in this repository.

# GK2 Sort To Nearby Chests

A BepInEx 5 mod for **Graveyard Keeper 2** that adds a **Sort to nearby chests** button to the inventory screen. One click puts your items into the chests around you that already hold the same items.

[Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3809824680)

## Features

- **One-click sorting** from the inventory screen (Character tab), with mouse or controller
- **Items go where they belong:** into chests that already contain that item, or into storages made for it (like firewood sheds)
- **Keeps what you need:** tools, hotbar items, the seed in your hand, faith, bags and quest items stay with you
- **Sort a bag:** open a bag and sort, and only that bag's contents go into the chests
- **Works like the game:** the button looks and behaves like the game's own "move all identical items" button, and items are moved the same way the chest window moves them
- **Translated** into all the game's languages

## How to use

| Input | Action |
|---|---|
| Mouse | Click the button on the right of the **Inventory** header (with a bag open: on the bag's header) |
| Controller | Press **LT** on the Character page. The prompt appears at the end of the tip bar |

The button turns grey (the LT icon dims) when there is nothing to sort or no chest nearby. The result shows up as a normal game notification.

**Bags:** with no bag open, only your inventory is sorted and bags keep their contents. With a bag open, only that bag is sorted; the button then sits on the bag's header, next to its close button. LT works the same way.

LT is otherwise unused on the Character page. On the Tech Tree and Inspirations pages it still switches sub-tabs.

## What gets sorted

**Which chests count as nearby.** Inside a storage area (home, kitchen, yard…): that area's storages, the same ones the inventory screen lists. Outside an area: storages within `NearbyRadius` world units.

**Where each item goes:**
1. Chests that already contain it, the one holding the most first.
2. Storages made for it (for example firewood sheds).

Items that no nearby chest contains yet stay in your inventory, and so does whatever doesn't fit. Turn on `OverflowToOtherChests` to put those into any nearby chest with room, nearest first.

**What always stays with you:**
- Equipped tools, weapons and armour (the tool belt is never touched)
- Items pinned to the hotbar (1–4)
- The seed or fertilizer you're holding to plant
- Faith, because Inspirations can only be paid with faith you carry
- Bags and quest items, and the contents of bags that aren't open

Conveyor chests and pallets are skipped by default, because production lines take items from them. Unstackable items keep their durability and bag contents.

## Install

Requires [BepInEx 5](https://github.com/BepInEx/BepInEx/releases/latest). No other mods needed.

**Steam Workshop:** subscribe. With the GK2 Workshop auto-loader installed, it loads on the next game start.

**Manual:**

1. Download `GK2SortToNearbyChests-<version>.zip` from the [Releases](../../releases) page and copy its `BepInEx` folder into the game folder (Steam → right-click Graveyard Keeper 2 → Manage → Browse local files) and merge the folders:
   ```
   Graveyard Keeper 2
   └─ BepInEx
      └─ plugins
         └─ GK2SortToNearbyChests
            └─ GK2SortToNearbyChests.dll
   ```
2. Start the game. `BepInEx/LogOutput.log` should contain `GK2 Sort To Nearby Chests 1.0.1 loaded.`

**Uninstall:** delete `BepInEx/plugins/GK2SortToNearbyChests`, and optionally `BepInEx/config/gk2.sorttonearbychests.cfg`.

## Settings

`BepInEx/config/gk2.sorttonearbychests.cfg` is created on first start.

| Setting | Default | What it does |
|---|---|---|
| `OverflowToOtherChests` | `false` | `true`: items without a matching chest, and whatever doesn't fit, go into any nearby chest with room. |
| `KeepHotbarItems` | `true` | Keep items pinned to the hotbar. |
| `KeepItemIds` | `faith` | Comma-separated item ids that always stay with you. |
| `MoveBags` | `false` | Also move bags (with their contents). |
| `MoveQuestItems` | `false` | Also move quest items. |
| `UseConveyorStorages` | `false` | Also use conveyor chests and pallets. |
| `NearbyRadius` | `10` | Outside a storage area: how far (world units) a chest may be. |

## Safety

- The mod adds no data of its own to save files. Items are moved with the game's own inventory calls, so they are saved like any other item.
- Removing the DLL restores the unmodded game.
- If a game update breaks one of the mod's patches, the mod removes all of them and the game runs unmodded. The reason is logged in `BepInEx/LogOutput.log`.

## Compatibility

Tested with [No More Running Back](https://steamcommunity.com/sharedfiles/filedetails/?id=3806668942) (installs `GK2Notepad.dll`), [GK2 Performance](https://steamcommunity.com/sharedfiles/filedetails/?id=3809616755), Better Auto Crafting and GK2 Move Stations.

No More Running Back is optional. When it's installed, the LT prompt comes after its own prompts, and its record of the tip bar text is kept up to date so its prompts aren't repeated. When the game rewrites the tip bar late in a frame (opening a bag, for example), this mod runs that mod's tip update right away, so the bar doesn't jump a frame later. That mod switches itself off when another mod patches its code, so this mod only reads and sets one of its values and calls its tip update.

## Changelog

**1.0.1**
- Sort an open bag: with a bag open, only that bag's contents are sorted. Closed bags keep their contents.
- Fixed the controller tip bar jumping when moving between items or opening a bag, and the LT prompt showing twice next to No More Running Back's prompts.
- Fixed the controller focus possibly landing on the hidden mouse button after switching from mouse to controller.
- Holding LT sorts only once.
- Less work while the inventory is open: whether there is something to sort is only checked when the inventory changes, and every 2 seconds.

**1.0.0**
- First release.

## Building

Needs the .NET SDK. The project references the game's own files from the install folder; nothing is copied into it.

```
dotnet build -c Release
# game installed somewhere else:
dotnet build -c Release -p:GameDir="E:\Steam\steamapps\common\Graveyard Keeper 2"
```

The DLL ends up in `bin/Release/GK2SortToNearbyChests.dll`. `nuget.config` pins nuget.org as the only package source.

**Making a release:** copy the DLL to `dist/BepInEx/plugins/GK2SortToNearbyChests/` (and `workshop/content/BepInEx/plugins/GK2SortToNearbyChests/` for the Workshop), zip the `dist/BepInEx` folder as `GK2SortToNearbyChests-<version>.zip` and attach it to a GitHub Release. Both folders are git-ignored.
