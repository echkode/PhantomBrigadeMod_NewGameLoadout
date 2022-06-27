**NewGameLoadout**
----
A library mod for [Phantom Brigade (Alpha)](https://braceyourselfgames.com/phantom-brigade/) to change the loadout of the initial player mechs when starting a new game. It allows you to make ConfigOverrides/ConfigEdits style changes to the configuration of a new game.

It is compatible with game version **0.20.0**. All library mods are fragile and susceptible to breakage whenever a new version is released.

This is not intended to be a standalone mod. It is more of a demonstration piece for authors of more comprehensive mods who want to change things such as the inital unit loadouts (what I use it for) or other starting parameters.

If you intend to use this mod directly, please read through this document up to the Internal Details section. Setting up this mod to work correctly is somewhat challenging unless you are familiar with setting up ConfigOverrides/ConfigEdits mods.

**Mod Setup**
----
The mod should follow the standard directory layout of any library mod. That is, in your `<userdir>\Documents\My Games\Phantom Brigade` directory, make a `Mods` directory if it does not exist and then create a new directory under the `Mods` directory for this mod. The name does not matter but it helps to be consistent so I name it NewGameLoadout and my directory path looks like `C:\Users\EchKode\Documents\My Games\Phantom Brigade\Mods\NewGameLoadout`.

In the last directory, you will need three things.
1. A directory named `Libraries` that contains the compiled DLL from this project.
2. A file named `metadata.yaml` that contains some information about this mod.
3. A directory named either `ConfigOverrides` or `ConfigEdits`.

A tree of the structure looks like
```
NewGameLoadout
|
+- ConfigEdits
|
+- Libraries
|  |
|  +- NewGameLoadout.dll
|
+- metadata.yaml
```

In place of `ConfigEdits` you may have `ConfigOverrides`. More on that below.

The contents of the `metadata.yaml` file should look like
```
id: com.echkode.pbmods.newgameloadout
ver: 1
includesConfigOverrides: false
includesConfigEdits: false
includesLibraries: true
includesTextures: false
includesLocalizationEdits: false
includesLocalizations: false
gameVersionMin:
gameVersionMax:
name: New Game Loadout
desc: Change the loadout of your units on a new game
```

The important bits are a unique string in the `id` field that won't clash with any other mods you may be loading and the `includesLibraries: true` line. The other `includes` lines should end with `false`.

**ConfigOverrides vs ConfigEdits**
----
The [Phantom Brigade modding system](https://wiki.braceyourselfgames.com/en/PhantomBrigade/Modding/ModSystem) has two ways of changing the game's configuration data without resorting to writing code.

With a _ConfigOverrides_ mod, you create a mirror directory structure of the game's Configs directory in the `ConfigOverrides` directory of your mod and then copy the files you want to change to the appropriate locations in the mirror directory structure. For example, say you want to change the damage value for one of the handguns. Your mod directory would look like
```
NewGameLoadout
|
+- ConfigOverrides
|  |
.  +- DataDecomposed
.     |
.     +- Equipment
         |
         +- Subsystems
            |
            +- wpn_main_handgun_01.yaml
```
You would then change the `value` field of the `wpn_damage` entry in your copy of `wpn_main_handgun_01.yaml`. When the mod runs, it'll overwrite the in-game configuration for `wpn_main_handgun_01` with the contents of your file.

A _ConfigEdits_ mod is similar in that you create the same mirror directory structure and copy over the files of the configuration you want to change but the contents of the files are very different. Say, for example, that I want to change the initial loadout of the second mech (Lancer) in a new game so its weapon is an assault rifle. I first make the mirror directory structure and copy over the target file
```
NewGameLoadout
|
+- ConfigEdits
|  |
.  +- Saves
.     |
.     +- save_internal_newgame
         |
         +- Units
            |
            +- pb_mech_02.yaml
```
Then I replace the entire contents of `pb_mech_02.yaml` with
```
removed: false
edits:
- 'parts.equipment_right.preset: wpn_rifle_assault_02_r1'
- 'parts.equipment_right.systems.internal_main_equipment.blueprint: wpn_main_assault_02'
```

**Changes This Mod Can Make**
----
This mod can do both kinds of configuration changes. The _ConfigEdits_ style is preferred since it is less fragile in the face of changes to the YAML files with new game releases. If you have the same file in the same place in both `ConfigOverrides` and `ConfigEdits`, the mod will first replace the in-game configuration with the contents of the _ConfigOverride_ file and then apply any edits in the _ConfigEdits_ file.

**Where the Standard New Game Configuration Is Stored**
----
The initial saved game data is stored in `<game install directory>\Configs\Saves\save_internal_newgame` Therefore, in your mod directory under either `ConfigOverrides` or `ConfigEdits`, you need to create the directory path `Saves\save_internal_newgame`. A saved game consists of YAML files and subdirectories that contain more YAML files. If the file you want to change is in one of the subdirectories, you will have to create that subdirectory in the appropriate location in the mod mirror directory structure (see the _ConfigEdits_ example).

**Internal Details**
----
This mod hooks a couple of central functions that are used both when starting a new game and any time a saved game is loaded so there are a some guards to prevent the mod from being triggered by a load of a normal saved game.

A new game is achieved by loading a saved game from a special location. The configuration in the special location is missing a few files that are present in normal saves. The mod knows about those extra files and will load them if you supply them in `ConfigOverrides`.  I don't know how far you can push this as I haven't dug very deeply into the structure of a saved game and I haven't followed the code far enough to know if there's some special logic surrounding new games that'll kneecap more ambitious uses of the code in this mod.