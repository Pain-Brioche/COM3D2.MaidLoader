## Introduction

Maid Loader is the BepinEx equivalent of Neerhom's [ModLoader](https://github.com/Neerhom/COM3D2.ModLoader)  
While the latter still works and has proven its reliablility, Maid Loader adds some quality of life features for those who wish.

## Mod Loader legacy
This is the list of features ModLoader already has and implemented in MaidLoader:
- ModPriority: Allows for Mod to override game's items.
- Asset Manager: Allows to override game's unity assets.
- Master Mod: Allows to add modded heads and bodies to classic Male Edit.
- Nei append: Fully compatible with previous ModLoader's .asset_bg loader via .nei files.
- Scripts: Load .ks (script files) to the game.
- Sounds: Load .ogg (sound files) to the game.
- .Pmat: Load modded .pmat, and check for redundancies.

## New  
These features are esclusive to MaidLoader they are described in more details later:
- QuickMod: Add mods without restarting the game or leaving the edit mode.
- Auto .asset_bg: Add backgrounds and props to the game without .nei required.
- Faster Load: Reduces the game startup times.
- Mod debugging options.

## Not Implemented
What MaidLoader cannot currently do ModLoader can:
- Load .arc from the Mod folder; doing this is a terrible idea anyway and should be avoided at all time.  
The only reason to do so was removed with the ablity to load .ogg.
