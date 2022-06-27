using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using HarmonyLib;
using PhantomBrigade.Data;
using UnityEngine;

namespace EchKode.PBMods.NewGameLoadout
{
	public class ModLink : PhantomBrigade.Mods.ModLink
	{
		internal static string modId;
		internal static string modPath;

		public override void OnLoad(Harmony harmonyInstance)
		{
			// Uncomment to get a file on the desktop showing the IL of the patched methods and any output from FileLog.Log()
			//Harmony.DEBUG = true;

			modId = metadata.id;
			modPath = metadata.path;
			var patchAssembly = typeof(ModLink).Assembly;
			Debug.Log($"Mod {metadata.id} is executing OnLoad | Using HarmonyInstance.PatchAll on assembly ({patchAssembly.FullName}) | Directory: {metadata.directory} | Full path: {metadata.path}");
			harmonyInstance.PatchAll(patchAssembly);
		}
	}

	[HarmonyPatch]
	static class NewGameLoadoutPatch
	{
		private enum TargetType
		{
			File,
			Directory
		}

		private const string saveName = "save_internal_newgame";
		private static readonly string modConfigOverridesDirectoryName = $"ConfigOverrides/Saves/{saveName}/";
		private static readonly string modConfigEditsDirectoryName = $"ConfigEdits/Saves/{saveName}/";

		private static bool isNewGameLoading = false;

		private static string modConfigOverridesPath;
		private static string modConfigEditsPath;
		private static MethodInfo modConfigFieldEditMethod;
		private static List<(TargetType TargetType, string FsName, Type ContentType, string FieldName)> targets;

		[HarmonyPatch(typeof(DataManagerSave))]
		[HarmonyPatch("LoadData", MethodType.Normal)]
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

			modConfigOverridesPath = $"{ModLink.modPath}{modConfigOverridesDirectoryName}";
			modConfigEditsPath = $"{ModLink.modPath}{modConfigEditsDirectoryName}";
			modConfigFieldEditMethod = AccessTools.DeclaredMethod(typeof(PhantomBrigade.Mods.ModManager), "ProcessFieldEdit");
			targets = new List<(TargetType, string, Type, string)>()
			{
				(TargetType.File, "core.yaml", typeof(DataContainerSavedCore), "core"),
				(TargetType.File, "stats.yaml", typeof(DataContainerSavedStats), "stats"),
				(TargetType.File, "world.yaml", typeof(DataContainerSavedWorld), "world"),
				(TargetType.File, "crawler.yaml", typeof(DataContainerSavedCrawler), "crawler"),
				(TargetType.Directory, "OverworldProvinces", typeof(DataContainerSavedOverworldProvince), "provinces"),
				(TargetType.Directory, "OverworldEntities", typeof(DataContainerSavedOverworldEntity), "overWorldEntities"),
				(TargetType.File, "combat_setup.yaml", typeof(DataContainerSavedCombatSetup), "combatSetup"),
				(TargetType.File, "combat.yaml", typeof(DataContainerSavedCombat), "combat"),
				(TargetType.Directory, "Units", typeof(DataContainerSavedUnit), "units"),
				(TargetType.Directory, "Pilots", typeof(DataContainerSavedPilot), "pilots"),
				(TargetType.Directory, "OverworldActions", typeof(DataContainerSavedOverworldAction), "overworldActions"),
				(TargetType.Directory, "CombatActions", typeof(DataContainerSavedAction), "combatActions"),
			};
		}

		[HarmonyPatch(typeof(DataManagerSave))]
		[HarmonyPatch("LoadData", MethodType.Normal)]
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

			var containerSavedMetadata = UtilitiesYAML.LoadDataFromFile<DataContainerSavedMetadata>(modConfigOverridesPath, "metadata.yaml", appendApplicationPath: false);
			if (containerSavedMetadata == null)
			{
				return;
			}
			SaveSerializationHelper.data.metadata = containerSavedMetadata;
		}

		[HarmonyPatch(typeof(DataContainerSave))]
		[HarmonyPatch("OnAfterDeserialization", MethodType.Normal)]
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

			var method = AccessTools.DeclaredMethod(
				typeof(SaveSerializationHelper),
				"GetContainers",
				new[] { typeof(string), typeof(string) },
				new[] { target.ContentType });
			var data = method.Invoke(null, new object[] { modConfigOverridesPath, target.FsName });
			var field = AccessTools.DeclaredField(inst.GetType(), target.FieldName);
			field.SetValue(inst, data);
		}

		private static void LoadEdits(object __instance)
		{
			foreach (var target in targets)
			{
				if (target.TargetType == TargetType.File)
				{
					var pathname = Path.Combine(modConfigEditsPath, target.FsName);
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
			var mt = map.GetType();
			var test = AccessTools.DeclaredMethod(mt, "ContainsKey");
			var found = (bool)test.Invoke(map, new object[] { key });
			if (!found)
			{
				return;
			}

			var indexer = AccessTools.DeclaredPropertyGetter(mt, "Item");
			var data = indexer.Invoke(map, new object[] { key });
			if (data == null)
			{
				return;
			}

			var (ok, edits) = LoadConfigEditSteps(pathname);
			if (!ok)
			{
				return;
			}

			ApplyConfigEditSteps(data, $"{target.FsName}+{key}", edits);
		}

		private static (bool, PhantomBrigade.Mods.ModConfigEdit)
			LoadConfigEditSteps(string pathname)
		{
			if (!File.Exists(pathname))
			{
				return (false, null);
			}

			var configEditSerialized = UtilitiesYAML.ReadFromFile<PhantomBrigade.Mods.ModConfigEditSerialized>(pathname, false);
			if (configEditSerialized == null)
			{
				return (false, null);
			}

			if (configEditSerialized.edits == null)
			{
				return (false, null);
			}

			var configEditSteps = new PhantomBrigade.Mods.ModConfigEdit()
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

		private static (bool Ok, PhantomBrigade.Mods.ModConfigEditStep EditStep)
			ParseEditStep(string pathname, string edit)
		{
			var parts = edit.Split(new[] { ':' }, 2);
			if (parts.Length != 2)
			{
				Debug.LogWarning($"Mod {ModLink.modId} | Edit from {pathname} has invalid number of separators: {edit}");
				return (false, null);
			}

			var editPath = parts[0];
			var editValue = parts[1].TrimStart(' ');
			if (string.IsNullOrEmpty(editPath))
			{
				return (false, null);
			}

			return (true, new PhantomBrigade.Mods.ModConfigEditStep()
			{
				path = editPath,
				value = editValue,
			});
		}

		private static void ApplyConfigEditSteps(
			object data,
			string filename,
			PhantomBrigade.Mods.ModConfigEdit configEditSteps)
		{
			foreach (var editStep in configEditSteps.edits)
			{
				modConfigFieldEditMethod.Invoke(null, new object[]
				{
					data,
					filename,
					editStep.path,
					editStep.value,
					-1,
					ModLink.modId,
					data.GetType().Name,
				});
			}
		}
	}
}
