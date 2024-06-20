**NewGameLoadout**
----
A library mod for [Phantom Brigade (Alpha)](https://braceyourselfgames.com/phantom-brigade/) to change the loadout of the initial player mechs when starting a new game. It allows you to make ConfigOverrides/ConfigEdits style changes to the configuration of a new game.

It is compatible with game version **1.3.3**. All library mods are fragile and susceptible to breakage whenever a new version is released.

This is not intended to be a standalone mod. It is more of a demonstration piece for authors of more comprehensive mods who want to change things such as the initial unit loadouts (what I use it for) or other starting parameters.

**Mod Setup**

The mod reads some configuration information from the `newgameloadout.yaml` file. This should be located in the top level of the mod directory. This file allows you to configure the paths where the config files are located. An example set of config files can be found in the `QuickStartLoadout` folder.

You can use ConfigEdits style configs if you are changing what already exists. The `SaveDecomposed` folder is for new configs or ones that entirely replacing existing configs. While the `SaveDecomposed` folder is very similar to the `DataDecomposed` folder in a ConfigOverride, it has a very important difference. It must contain all the files you want in the save. This is different than how ConfigOverride DataDecomposed works where you only need to have the changed or new files.

The mod will first load the files in `SaveDecomposed` and then it will apply the ConfigEdits. You can edit a new file that you supply.

**Using the Code in Your Own Mod**

The `Patch` class is standalone so you can lift it out and put it in your mod. There are a few static variables at the top that you will need to assign values to in your mod's start up routine.

**Where the Standard New Game Configuration Is Stored**

The initial saved game data is stored in `<game install directory>\Configs\Saves\save_internal_newgame` or `<game install directory>\Configs\Saves\save_internal_quickstart`. A saved game consists of YAML files and subdirectories that contain more YAML files. If the file you want to change is in one of the subdirectories, you will have to create that subdirectory in the appropriate location in the mod mirror directory structure.
