// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System;
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
	static class NewGameLoadoutPatch
	{
		private enum TargetType
		{
			File,
			Directory
		}

		private const string saveName = "save_internal_quickstart";
		private static readonly string modConfigOverridesDirectoryName = $"ConfigOverrides/Saves/{saveName}/";
		private static readonly string modConfigEditsDirectoryName = $"ConfigEdits/Saves/{saveName}/";

		private static bool isNewGameLoading = false;

		private static string modConfigOverridesPath;
		private static string modConfigEditsPath;
		private static List<(TargetType TargetType, string FsName, Type ContentType, string FieldName)> targets;

		[HarmonyPatch(typeof(DataManagerSave), nameof(DataManagerSave.LoadData))]
		[HarmonyPrefix]
		static void Dms_LoadDataPrefix(DataManagerSave.SaveLocation saveLocation)
		{
			isNewGameLoading = saveLocation == DataManagerSave.SaveLocation.Internal && DataManagerSave.saveName == saveName;
			if (!isNewGameLoading)
			{
				return;
			}

			if (null != modConfigOverridesPath)
			{
				return;
			}

			modConfigOverridesPath = ModLink.modPath + modConfigOverridesDirectoryName;
			modConfigEditsPath = ModLink.modPath + modConfigEditsDirectoryName;
			targets = new List<(TargetType, string, Type, string)>()
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
			};
		}

		[HarmonyPatch(typeof(DataManagerSave), nameof(DataManagerSave.LoadData))]
		[HarmonyPostfix]
		static void Dms_LoadDataPostfix()
		{
			if (!isNewGameLoading)
			{
				return;
			}

			if (!File.Exists(Path.Combine(modConfigOverridesPath, "metadata.yaml")))
			{
				return;
			}

			Debug.LogFormat(
				"Mod {0} ({1}) Using ConfigOverride for save metadata",
				ModLink.modIndex,
				ModLink.modID);

			var containerSavedMetadata = UtilitiesYAML.LoadDataFromFile<DataContainerSavedMetadata>(modConfigOverridesPath, "metadata.yaml", appendApplicationPath: false);
			if (containerSavedMetadata == null)
			{
				return;
			}
			SaveSerializationHelper.data.metadata = containerSavedMetadata;
		}

		[HarmonyPatch(typeof(DataContainerSave), nameof(DataContainerSave.OnAfterDeserialization))]
		[HarmonyPrefix]
		static void LoadCurrent(DataContainerSave __instance)
		{
			if (!isNewGameLoading)
			{
				return;
			}

			LoadOverrides(__instance);
			LoadEdits(__instance);
		}

		private static void LoadOverrides(object __instance)
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

		private static void LoadSingle(
			object inst,
			(TargetType, string FsName, Type ContentType, string FieldName) target)
		{
			if (!File.Exists(Path.Combine(modConfigOverridesPath, target.FsName)))
			{
				return;
			}

			Debug.LogFormat(
				"Mod {0} ({1}) Using ConfigOverride for {2}",
				ModLink.modIndex,
				ModLink.modID,
				target.FsName);

			var method = AccessTools.DeclaredMethod(
				typeof(SaveSerializationHelper),
				"GetContainer",
				new[] { typeof(string), typeof(string), typeof(bool) },
				new[] { target.ContentType });
			var data = method.Invoke(null, new object[] { modConfigOverridesPath, target.FsName, false });
			var field = AccessTools.DeclaredField(inst.GetType(), target.FieldName);
			field.SetValue(inst, data);
		}

		private static void LoadDecomposed(
			object inst,
			(TargetType, string FsName, Type ContentType, string FieldName) target)
		{
			if (!Directory.Exists(Path.Combine(modConfigOverridesPath, target.FsName)))
			{
				return;
			}

			Debug.LogFormat(
				"Mod {0} ({1}) LoadDecomposed on: contentType={2}; field={3}; fs={4}",
				ModLink.modIndex,
				ModLink.modID,
				target.ContentType,
				target.FieldName,
				target.FsName);

			var method = AccessTools.DeclaredMethod(
				typeof(SaveSerializationHelper),
				"GetContainers",
				new[] { typeof(string), typeof(string) },
				new[] { target.ContentType });
			if (method == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) reflection failed: method=GetContainers; targetType={2}; instType={3};",
					ModLink.modIndex,
					ModLink.modID,
					target.ContentType,
					inst.GetType().Name);
				return;
			}

			var data = method.Invoke(null, new object[] { modConfigOverridesPath, target.FsName });
			var field = AccessTools.DeclaredField(inst.GetType(), target.FieldName);
			if (field == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) reflection failed: field={2}; targetType={3}; instType={4};",
					ModLink.modIndex,
					ModLink.modID,
					target.FieldName,
					target.ContentType,
					inst.GetType().Name);
				return;
			}
			field.SetValue(inst, data);
		}

		private static void LoadEdits(object __instance)
		{
			foreach (var target in targets)
			{
				if (target.TargetType == TargetType.File)
				{
					var pathname = Path.Combine(modConfigEditsPath, target.FsName);
					Debug.LogFormat(
						"Mod {0} ({1}) Applying ConfigEdits from {2}",
						ModLink.modIndex,
						ModLink.modID,
						target.FsName);
					var (ok, edits) = LoadConfigEditSteps(pathname);
					if (!ok)
					{
						continue;
					}

					var data = AccessTools.DeclaredField(__instance.GetType(), target.FieldName).GetValue(__instance);
					ApplyConfigEditSteps(data, target.FsName, edits);
				}
				else if (target.TargetType == TargetType.Directory)
				{
					var directoryPath = Path.Combine(modConfigEditsPath, target.FsName);
					if (!Directory.Exists(directoryPath))
					{
						continue;
					}

					foreach (var pathname in Directory.EnumerateFiles(directoryPath, "*.yaml"))
					{
						Debug.LogFormat(
							"Mod {0} ({1}) Applying ConfigEdits from {2}",
							ModLink.modIndex,
							ModLink.modID,
							target.FsName);
						LoadConfigEditDecomposed(__instance, pathname, target);
					}
				}
			}
		}

		private static void LoadConfigEditDecomposed(
			object __instance,
			string pathname,
			(TargetType, string FsName, Type, string FieldName) target)
		{
			var key = Path.GetFileNameWithoutExtension(pathname).ToLowerInvariant();
			var map = AccessTools.DeclaredField(__instance.GetType(), target.FieldName).GetValue(__instance);
			if (null == map)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- not able to get instance for target field | path: {2} | field: {3}",
					ModLink.modIndex,
					ModLink.modID,
					target.FsName,
					target.FieldName);
				return;
			}
			var mt = map.GetType();
			var test = AccessTools.DeclaredMethod(mt, "ContainsKey");
			if (null == test)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- target field doesn't appear to be a map | path: {2} | field: {3}",
					ModLink.modIndex,
					ModLink.modID,
					target.FsName,
					target.FieldName);
				return;
			}
			var found = (bool)test.Invoke(map, new object[] { key });
			if (!found)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- key not found on target field | path: {2} | key: {3}",
					ModLink.modIndex,
					ModLink.modID,
					target.FsName,
					key);
				return;
			}

			var indexer = AccessTools.DeclaredPropertyGetter(mt, "Item");
			if (null == indexer)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- unable to get item for key | path: {2} | key: {3}",
					ModLink.modIndex,
					ModLink.modID,
					target.FsName,
					key);
				return;
			}
			var data = indexer.Invoke(map, new object[] { key });
			if (data == null)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Failed to apply LoadConfigEditDecomposed -- value for key is null | path: {2} | key: {3}",
					ModLink.modIndex,
					ModLink.modID,
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

		private static (bool, ModConfigEdit)
			LoadConfigEditSteps(string pathname)
		{
			if (!File.Exists(pathname))
			{
				return (false, null);
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

		private static (bool Ok, ModConfigEditStep EditStep)
			ParseEditStep(string pathname, string edit)
		{
			var parts = edit.Split(new[] { ':' }, 2);
			if (parts.Length != 2)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) | Edit from {2} has invalid number of separators: {3}",
					ModLink.modIndex,
					ModLink.modID,
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

		private static void ApplyConfigEditSteps(
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
					ModLink.modIndex,
					ModLink.modID,
					data.GetType().Name
				);
			}
		}
	}
}
