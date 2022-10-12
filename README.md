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
- Loading .arc from the Mod folder (Should be avoided whenever possible though).

### New  
These features are esclusive to MaidLoader they are described in more details later:
- RefreshMod:  Add mods without restarting the game or leaving the edit mode.
- QuickMod: Same thing as RefreshMod, with a dedicated folder and faster if your Mod folder is large.
- Auto .asset_bg: Add backgrounds and props to the game without .nei required.
- Faster Load: Reduces the game startup times.
- Mod debugging options.

### Not Implemented
- Nothing


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
- Make sure you have [CM3D2.Toolkit.Guest4168Branch.dll](https://github.com/JustAGuest4168/CM3D2.Toolkit/releases), [COM3D2.API](https://github.com/DeathWeasel1337/COM3D2_Plugins/releases/tag/v3) and System.Threading.dll
- Optional: Edit config in the F1 menu (in game).
- An improved ModMenuAccel is on the work, MaidLoader is already compatible with it when it'll be released.

## New features descriptions
### ModRefresh
This function adds a button in your gear menu to refresh the entire Mod folder, and adds any new mods to your game while it's running.  
__*A few things to note:*__
- Everything is done in the background so it will not freeze the game while refreshing.
- If in edit mode, the UI will blink once when new icons are added.

### QuickMod
This function monitors a folder so that any mod placed inside can be quickly added to the game without any restart needed.  
__*A few things to note:*__
- This is NOT meant to replace the standard Mod folder.  
- Mods in QuickMod behaves as standard mods do.
- While the plugin is capable of doing it, adding hundreds of mods at a time is not recommended.
- Mods in QuickMod have priority over standard Mod folder and game's files.

__*Relevant options:*__  
- *Use QuickMod: Disable if you don't want to use it.*   
- *Custom Mod folder: QuickMod dedicated folder (if option above is disabled), you may enter a simple folder name in your game Maid folder or a complete path if you wish it to be anywhere else.*  
- *Auto refresh: Enable the game to refresh new mods automatically a few seconds after the last files was added.*  
- *Auto refresh delay: Delay before the game automatically refreshes new mods.*  

### Auto .asset_bg
.asset_bg files placed in PhotoBG_NEI will automatically be added to the game's background List, without the need of .nei.  
.asset_bg files placed in PhotoBG_OBJ_NEI will automatically be added to the game's props List, without the need of .nei.  
Deskitem currently still required a .nei in the same way ModLoader required them to be.  

### Faster Load
In order to load .ks and .ogg into the game, ModLoader as well as MaidLoader need to create a dummy .arc. Where Maid Loader does things differently is by loading said .arc earlier in the game's Init. That way one of the game's slower process only happens once. Depending on your computer you may see a significant drop in this starting phase (95s down to 39s for me). This is also one reason .arc loading isn't possible from the Mod Folder. In addition you can skip this entirely if you do not have .ks and/or .ogg in your Mod folder, gaining a few more seconds for not searching in vain for inexistant files. Or as an option creating a dedicated folder to put your .ks and .ogg into if your Mod folder is very large.

__*Relevant options:*__  
- *Load scripts (.ks): Disable to ignore .ks or if you don't have any to gain a bit of speed*  
- *Load sounds (.ogg): Disable to ignore .ogg or if you don't have any to gain a bit of speed*
- *Load Arcs (.arc): Disable to ignore .arc or if you don't have any to gain a bit of speed*
- *Advanced option: Use a specific folder for Scripts and Sounds; creates a specific Mod/Scripts&Sounds to look into.*  

### Mod Debug
Advanced options bellow are reserved for game debugging should some mod cause issue and you can't/won't disable plugins.  
__*Relevant options:*__  
- *Enable Mod override: When Enabled (default) Mod and QuickMod will override game's assets.*
- *Enable Custom Mod override: When Enabled (default) use MaidLoader's custom Mod Override similar to ModLoader's. When Disabled, use the game's own Mod Priority System, less thorough but could proove safer in some cases.*  


## Notes
- You can (and probably should) change options directly in BepinEx/config.
- Consider that any change to the options need a game restart to take effect.
- I know it doesn't really load Maidos :(
