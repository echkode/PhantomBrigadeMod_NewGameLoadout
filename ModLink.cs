// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

namespace EchKode.PBMods.NewGameLoadout
{
	public partial class ModLink : PhantomBrigade.Mods.ModLink
	{
		internal static int ModIndex;
		internal static string ModID;
		internal static string ModPath;

		public override void OnLoadStart()
		{
			ModIndex = ModSettings.ModIndex = Patch.ModIndex = modIndexPreload;
			ModID = ModSettings.ModID = Patch.ModID = modID;
			ModPath = modPath;

			ModSettings.Load();

			// Uncomment to get a file on the desktop showing the IL of the patched methods.
			// Output from FileLog.Log() will trigger the generation of that file regardless if this is set so
			// FileLog.Log() should be put in a guard.
			//EnableHarmonyFileLog();
		}
	}

	partial class ModSettings
	{
		internal static void Load()
		{
			var settingsPath = System.IO.Path.Combine(ModLink.ModPath, "newgameloadout.yaml");
			Load(settingsPath);
		}
	}
}
