// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.IO;

using UnityEngine;

namespace EchKode.PBMods.NewGameLoadout
{
	class ModSettings
	{
		public static string SaveName;
		public static string ConfigEditsPath;
		public static string SaveDecomposedPath;

		internal static void Load()
		{
			var settingsPath = Path.Combine(ModLink.modPath, "newgameloadout.yaml");
			var settings = UtilitiesYAML.ReadFromFile<ModSettings>(settingsPath, false);
			if (settings == null)
			{
				settings = new ModSettings();
			}

			SaveName = settings.saveName;
			ConfigEditsPath = settings.configEditsPath;
			SaveDecomposedPath = settings.saveDecomposedPath;

			Debug.LogFormat(
				"Mod {0} ({1}) settings | path: {2}"
					+ "\n  save name: {3}"
					+ "\n  ConfigEdits path: {4}"
					+ "\n  SaveDecomposed path: {5}",
				ModLink.modIndex,
				ModLink.modID,
				settingsPath,
				SaveName,
				ConfigEditsPath,
				SaveDecomposedPath);
		}

		// Ignore these fields.
		// They are public so they can be picked up by the YAML deserialization routine.
		public string saveName = "save_internal_quickstart";
		public string configEditsPath = "ConfigEdits";
		public string saveDecomposedPath = "SaveDecomposed";
	}
}
