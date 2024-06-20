// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.IO;

using PhantomBrigade.Data;

using UnityEngine;

namespace EchKode.PBMods.NewGameLoadout
{
	partial class ModSettings
	{
		internal static int ModIndex;
		internal static string ModID;

		public static void Load(string settingsPath)
		{
			if (string.IsNullOrWhiteSpace(settingsPath))
			{
				return;
			}

			var settings = UtilitiesYAML.ReadFromFile<ModSettings>(settingsPath, false);
			if (settings == null)
			{
				settings = new ModSettings();
			}

			Patch.SaveName = settings.saveName;
			Patch.ConfigEditsPath = MakePathAbsolute(settingsPath, settings.configEditsPath);
			Patch.SaveDecomposedPath = MakePathAbsolute(settingsPath, settings.saveDecomposedPath);
			Patch.LogVerbose = settings.logVerbose;

			Patch.Initialized = !string.IsNullOrWhiteSpace(Patch.SaveName)
				&& !string.IsNullOrWhiteSpace(Patch.ConfigEditsPath)
				&& !string.IsNullOrWhiteSpace(Patch.SaveDecomposedPath);

			Debug.LogFormat(
				"Mod {0} ({1}) settings | path: {2}"
					+ "\n  save name: {3}"
					+ "\n  ConfigEdits path: {4}"
					+ "\n  SaveDecomposed path: {5}",
				ModIndex,
				ModID,
				settingsPath,
				Patch.SaveName,
				Patch.ConfigEditsPath,
				Patch.SaveDecomposedPath);
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
		public bool logVerbose = false;
	}
}
