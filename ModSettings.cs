// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.IO;

using PhantomBrigade.Data;
using PhantomBrigade.Mods;

using UnityEngine;

namespace EchKode.PBMods.NewGameLoadout
{
	partial class ModSettings
	{
		public static bool Initialized;
		public static string SaveName;
		public static string ConfigEditsPath;
		public static string SaveDecomposedPath;

		public static bool LogVerbose;
		public static int ModIndex = -1;
		public static string ModID;

		public static void Load(string modID, string settingsPath, bool logVerbose)
		{
			var modIndex = ModManager.metadataPreloadList.FindIndex(mod => StringComparer.Ordinal.Compare(mod.metadata.id, modID) == 0);
			if (modIndex == -1)
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(settingsPath))
			{
				return;
			}

			var settings = UtilitiesYAML.ReadFromFile<ModSettings>(settingsPath, false);
			if (settings == null)
			{
				settings = new ModSettings();
			}

			SaveName = settings.saveName;
			ConfigEditsPath = MakePathAbsolute(settingsPath, settings.configEditsPath);
			SaveDecomposedPath = MakePathAbsolute(settingsPath, settings.saveDecomposedPath);
			Initialized = !string.IsNullOrWhiteSpace(SaveName)
				&& !string.IsNullOrWhiteSpace(ConfigEditsPath)
				&& !string.IsNullOrWhiteSpace(SaveDecomposedPath);

			ModIndex = modIndex;
			ModID = modID;
			LogVerbose = logVerbose;

			Debug.LogFormat(
				"Mod {0} ({1}) settings | path: {2}"
					+ "\n  save name: {3}"
					+ "\n  ConfigEdits path: {4}"
					+ "\n  SaveDecomposed path: {5}",
				ModIndex,
				ModID,
				settingsPath,
				SaveName,
				ConfigEditsPath,
				SaveDecomposedPath);
		}

		static string MakePathAbsolute(string settingsPath, string targetPath)
		{
			if (string.IsNullOrWhiteSpace(targetPath))
			{
				return targetPath;
			}
			targetPath = DataPathHelper.GetCleanPath(targetPath);

			var targetRoot = Path.GetPathRoot(targetPath);
			if (targetRoot != "")
			{
				return targetPath;
			}
			if (string.IsNullOrWhiteSpace(settingsPath))
			{
				if (targetRoot == "")
				{
					targetPath = DataPathHelper.GetCombinedCleanPath(Environment.CurrentDirectory, targetPath);
				}
				return targetPath;
			}

			var settingsRoot = Path.GetPathRoot(settingsPath);
			var currentDirectory = settingsRoot != ""
				? Path.GetDirectoryName(settingsPath)
				: Environment.CurrentDirectory;
			return DataPathHelper.GetCombinedCleanPath(currentDirectory, targetPath);
		}

		// Ignore these fields.
		// They are public so they can be picked up by the YAML deserialization routine.
		public string saveName = "save_internal_quickstart";
		public string configEditsPath = "ConfigEdits";
		public string saveDecomposedPath = "SaveDecomposed";
	}
}
