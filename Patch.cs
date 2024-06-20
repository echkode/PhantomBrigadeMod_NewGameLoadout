// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using HarmonyLib;

using PhantomBrigade.Data;
using PhantomBrigade.Mods;

using UnityEngine;

namespace EchKode.PBMods.NewGameLoadout
{
	[HarmonyPatch]
	static class Patch
	{
		internal static int ModIndex;
		internal static string ModID;

		public static bool Initialized;

		public static string SaveName;
		public static string ConfigEditsPath;
		public static string SaveDecomposedPath;
		public static bool LogVerbose;

		[HarmonyPatch(typeof(DataManagerSave), nameof(DataManagerSave.LoadData))]
		[HarmonyPrefix]
		static void Dms_LoadDataPrefix(DataManagerSave.SaveLocation saveLocation)
		{
			if (!Initialized)
			{
				return;
			}
			isNewGameLoading = saveLocation == DataManagerSave.SaveLocation.Internal
				&& DataManagerSave.saveName == SaveName;
			if (!isNewGameLoading)
			{
				return;
			}

			if (LogVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) configured paths\n  SaveDecomposed: {2}\n  ConfigEdits: {3}",
					ModIndex,
					ModID,
					SaveDecomposedPath,
					ConfigEditsPath);
			}
		}

		[HarmonyPatch(typeof(DataManagerSave), nameof(DataManagerSave.LoadData))]
		[HarmonyPostfix]
		static void Dms_LoadDataPostfix()
		{
			if (!Initialized)
			{
				return;
			}
			if (!isNewGameLoading)
			{
				return;
			}

			var filePath = DataPathHelper.GetCombinedCleanPath(SaveDecomposedPath, "metadata.yaml");
			if (!File.Exists(filePath))
			{
				return;
			}

			var containerSavedMetadata = UtilitiesYAML.LoadDataFromFile<DataContainerSavedMetadata>(filePath, appendApplicationPath: false);
			if (containerSavedMetadata == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to deserialize save metadata | path: {2}",
					ModIndex,
					ModID,
					filePath);
				return;
			}

			if (LogVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Replacing save metadata",
					ModIndex,
					ModID);
			}

			SaveSerializationHelper.data.metadata = containerSavedMetadata;
		}

		[HarmonyPatch(typeof(DataContainerSave), nameof(DataContainerSave.OnAfterDeserialization))]
		[HarmonyPrefix]
		static void LoadCurrent(DataContainerSave __instance)
		{
			if (!Initialized)
			{
				return;
			}
			if (!isNewGameLoading)
			{
				return;
			}

			LoadOverrides(__instance);
			LoadEdits(__instance);
		}

		static void LoadOverrides(object __instance)
		{
			foreach (var target in targets)
			{
				if (target.TargetType == TargetType.File)
				{
					LoadSingle(__instance, target);
				}
				else if (target.TargetType == TargetType.Directory)
				{
					LoadDecomposed(__instance, target);
				}
			}
		}

		static void LoadSingle(
			object inst,
			(TargetType, string FsName, Type ContentType, string FieldName) target)
		{
			if (!File.Exists(DataPathHelper.GetCombinedCleanPath(SaveDecomposedPath, target.FsName)))
			{
				return;
			}

			if (LogVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Replacing SaveDecomposed file {2}",
					ModIndex,
					ModID,
					target.FsName);
			}

			var method = AccessTools.DeclaredMethod(
				typeof(SaveSerializationHelper),
				"GetContainer",
				new[] { typeof(string), typeof(string), typeof(bool) },
				new[] { target.ContentType });
			var data = method.Invoke(null, new object[] { SaveDecomposedPath, target.FsName, false });
			var field = AccessTools.DeclaredField(inst.GetType(), target.FieldName);
			field.SetValue(inst, data);
		}

		static void LoadDecomposed(
			object inst,
			(TargetType, string FsName, Type ContentType, string FieldName) target)
		{
			if (!Directory.Exists(DataPathHelper.GetCombinedCleanPath(SaveDecomposedPath, target.FsName)))
			{
				return;
			}

			if (LogVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Replacing SaveDecomposed directory {4}\n  LoadDecomposed on: contentType={2}; field={3}",
					ModIndex,
					ModID,
					target.ContentType,
					target.FieldName,
					target.FsName);
			}

			var method = AccessTools.DeclaredMethod(
				typeof(SaveSerializationHelper),
				"GetContainers",
				new[] { typeof(string), typeof(string) },
				new[] { target.ContentType });
			if (method == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) reflection failed: method=GetContainers; targetType={2}; instType={3};",
					ModIndex,
					ModID,
					target.ContentType,
					inst.GetType().Name);
				return;
			}

			var data = method.Invoke(null, new object[] { SaveDecomposedPath + "/", target.FsName });
			if (data is IDictionary dict)
			{
				var keys = new List<object>();
				foreach (var key in dict.Keys)
				{
					keys.Add(key);
				}
				if (LogVerbose)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) keys ({2}): {3}",
						ModIndex,
						ModID,
						keys.Count,
						string.Join(", ", keys));
				}
			}
			var field = AccessTools.DeclaredField(inst.GetType(), target.FieldName);
			if (field == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) reflection failed: field={2}; targetType={3}; instType={4};",
					ModIndex,
					ModID,
					target.FieldName,
					target.ContentType,
					inst.GetType().Name);
				return;
			}
			field.SetValue(inst, data);
		}

		static void LoadEdits(object __instance)
		{
			foreach (var target in targets)
			{
				if (target.TargetType == TargetType.File)
				{
					var filePath = DataPathHelper.GetCombinedCleanPath(ConfigEditsPath, target.FsName);
					var (ok, edits) = LoadConfigEditSteps(filePath);
					if (!ok)
					{
						continue;
					}

					var data = AccessTools.DeclaredField(__instance.GetType(), target.FieldName).GetValue(__instance);
					ApplyConfigEditSteps(data, target.FsName, edits);
				}
				else if (target.TargetType == TargetType.Directory)
				{
					var directoryPath = DataPathHelper.GetCombinedCleanPath(ConfigEditsPath, target.FsName);
					if (!Directory.Exists(directoryPath))
					{
						continue;
					}

					foreach (var pathname in Directory.EnumerateFiles(directoryPath, "*.yaml"))
					{
						if (LogVerbose)
						{
							Debug.LogFormat(
								"Mod {0} ({1}) Applying ConfigEdits from directory {2}",
								ModIndex,
								ModID,
								target.FsName);
						}
						LoadConfigEditDecomposed(__instance, DataPathHelper.GetCleanPath(pathname), target);
					}
				}
			}
		}

		static void LoadConfigEditDecomposed(
			object __instance,
			string pathname,
			(TargetType, string FsName, Type, string FieldName) target)
		{
			var key = Path.GetFileNameWithoutExtension(pathname).ToLowerInvariant();
			var map = AccessTools.DeclaredField(__instance.GetType(), target.FieldName).GetValue(__instance);
			if (map == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- not able to get instance for target field | path: {2} | field: {3}",
					ModIndex,
					ModID,
					target.FsName,
					target.FieldName);
				return;
			}
			var mt = map.GetType();
			var test = AccessTools.DeclaredMethod(mt, "ContainsKey");
			if (test == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- target field doesn't appear to be a map | path: {2} | field: {3}",
					ModIndex,
					ModID,
					target.FsName,
					target.FieldName);
				return;
			}
			var found = (bool)test.Invoke(map, new object[] { key });
			if (!found)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- key not found on target field | path: {2} | key: {3}",
					ModIndex,
					ModID,
					target.FsName,
					key);
				return;
			}

			var indexer = AccessTools.DeclaredPropertyGetter(mt, "Item");
			if (indexer == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- unable to get item for key | path: {2} | key: {3}",
					ModIndex,
					ModID,
					target.FsName,
					key);
				return;
			}
			var data = indexer.Invoke(map, new object[] { key });
			if (data == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- value for key is null | path: {2} | key: {3}",
					ModIndex,
					ModID,
					target.FsName,
					key);
				return;
			}

			var (ok, edits) = LoadConfigEditSteps(pathname);
			if (!ok)
			{
				return;
			}

			ApplyConfigEditSteps(data, target.FsName + "+" + key, edits);
		}

		static (bool, ModConfigEdit) LoadConfigEditSteps(string pathname)
		{
			if (!File.Exists(pathname))
			{
				return (false, null);
			}

			if (LogVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Applying ConfigEdits from {2}",
					ModIndex,
					ModID,
					pathname);
			}

			var configEditSerialized = UtilitiesYAML.ReadFromFile<ModConfigEditSerialized>(pathname, false);
			if (configEditSerialized == null)
			{
				return (false, null);
			}

			if (configEditSerialized.edits == null)
			{
				return (false, null);
			}

			var configEditSteps = new ModConfigEdit()
			{
				removed = configEditSerialized.removed,
				edits = configEditSerialized.edits
					.Select(edit => ParseEditStep(pathname, edit))
					.Where(x => x.Ok)
					.Select(x => x.EditStep)
					.ToList(),
			};

			return (true, configEditSteps);
		}

		static (bool Ok, ModConfigEditStep EditStep) ParseEditStep(string pathname, string edit)
		{
			var parts = edit.Split(stepSeparator, 2);
			if (parts.Length != 2)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) | Edit from {2} has invalid number of separators: {3}",
					ModIndex,
					ModID,
					pathname,
					edit);
				return (false, null);
			}

			var editPath = parts[0];
			var editValue = parts[1].TrimStart(' ');
			if (string.IsNullOrEmpty(editPath))
			{
				return (false, null);
			}

			return (true, new ModConfigEditStep()
			{
				path = editPath,
				value = editValue,
			});
		}

		static void ApplyConfigEditSteps(
			object data,
			string filename,
			ModConfigEdit configEditSteps)
		{
			foreach (var editStep in configEditSteps.edits)
			{
				ModUtilities.ProcessFieldEdit(
					data,
					filename,
					editStep.path,
					editStep.value,
					ModIndex,
					ModID,
					data.GetType().Name
				);
			}
		}

		enum TargetType
		{
			File,
			Directory
		}

		static bool isNewGameLoading = false;

		static readonly char[] stepSeparator = { ':' };
		static readonly List<(TargetType TargetType, string FsName, Type ContentType, string FieldName)> targets =
			new List<(TargetType, string, Type, string)>()
			{
				(TargetType.File, "core.yaml", typeof(DataContainerSavedCore), "core"),
				(TargetType.File, "stats.yaml", typeof(DataContainerSavedStats), "stats"),
				(TargetType.File, "world.yaml", typeof(DataContainerSavedWorld), "world"),
				(TargetType.File, "crawler.yaml", typeof(DataContainerSavedCrawler), "crawler"),
				(TargetType.Directory, "OverworldProvinces", typeof(DataContainerSavedOverworldProvince), "provinces"),
				(TargetType.Directory, "OverworldEntities", typeof(DataContainerSavedOverworldEntity), "overworldEntities"),
				(TargetType.File, "combat_setup.yaml", typeof(DataContainerSavedCombatSetup), "combatSetup"),
				(TargetType.File, "combat.yaml", typeof(DataContainerSavedCombat), "combat"),
				(TargetType.Directory, "Units", typeof(DataContainerSavedUnit), "units"),
				(TargetType.Directory, "Pilots", typeof(DataContainerSavedPilot), "pilots"),
				(TargetType.Directory, "OverworldActions", typeof(DataContainerSavedOverworldAction), "overworldActions"),
				(TargetType.Directory, "CombatActions", typeof(DataContainerSavedAction), "combatActions"),
				(TargetType.File, "difficulty.yaml", typeof(DataContainerSavedDifficulty), "difficulty"),
			};
	}
}
