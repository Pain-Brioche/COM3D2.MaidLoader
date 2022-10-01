## Introduction

Maid Loader is the BepinEx equivalent of Neerhom's [ModLoader](https://github.com/Neerhom/COM3D2.ModLoader)  
While the latter still works and has proven its reliablility, Maid Loader adds some quality of life features for those who wish.  
Transition from one to the other is seamless.  
Part of ModLoader code was reused with Neerhom's authorisation.  

### ModLoader legacy
This is the list of features ModLoader already has and implemented in MaidLoader:
- ModPriority: Allows for Mod to override game's items.
- Asset Manager: Allows to override game's unity assets.
- Master Mod: Allows to add modded heads and bodies to classic Male Edit.
- Nei append: Fully compatible with previous ModLoader's .asset_bg loader via .nei files.
- Scripts: Load .ks (script files) to the game.
- Sounds: Load .ogg (sound files) to the game.
- .Pmat: Load modded .pmat, and check for redundancies.
- SML, SMVD, SSL support.

### New  
These features are esclusive to MaidLoader they are described in more details later:
- QuickMod: Add mods without restarting the game or leaving the edit mode.
- Auto .asset_bg: Add backgrounds and props to the game without .nei required.
- Faster Load: Reduces the game startup times.
- Mod debugging options.

### Not Implemented
What MaidLoader cannot currently do ModLoader can:
- Load .arc from the Mod folder; doing this is a terrible idea anyway and should be avoided at all time.  
The only reason to do so was removed with the ablity to load .ogg.
- ModMenuAccell support: This is in progress.


## Installation
- Download the latest MaidLoader .dll.
- Drop it in BepinEx/plugins.
- In Sybaris folder, remove if you have them:
```
COM3D2.ModLoader.Managed.dll
COM3D2.ModLoader.Patcher.dll
COM3D2.ModMenuAccel.Hook.dll
COM3D2.ModMenuAccel.Patcher.dll
```
- Optional: Edit config in the F1 menu (in game).

## New features descriptions
### QuickMod
This function monitors a folder so that any mod placed inside can be quickly added to the edit mode without any restart needed.  
__*A few things to note:*__
- This is NOT meant to replace the standard Mod folder.  
- While the plugin is capable of doing it, adding hundreds of mods at a time is not recommended.
- Mods in QuickMod have priority over standard Mod folder and game's files.
- QuickMod is made for Edit mode, while it will work elsewhere once started, it needs edit mode to start.

__*Relevant options:*__  
- *Use QuickMod: Disable if you don't want to use it.*  
- *Use standard Mod folder: If enabled the standard Mod folder will be monitored for QuickMod, performances heavily depends on your Mod folder size and disk drive.*  
- *Custom Mod folder: QuickMod dedicated folder (if option above is disabled), you may enter a simple folder name in your game Maid folder or a complete path if you wish it to be anywhere else.*  
- *Auto refresh: Enable the game to refresh new mods automatically a few seconds after the last files was added.*  
- *Auto refresh delay: Delay before the game automatically refreshes new mods.*  

### Auto .asset_bg
.asset_bg files placed in PhotoBG_NEI will automatically be added to the game's background List, without the need of .nei.
.asset_bg files placed in PhotoBG_OBJ_NEI will automatically be added to the game's props List, without the need of .nei.
Deskitem currently still required a .nei in the same way ModLoader required them to be.
