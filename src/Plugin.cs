using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// Adds a "Sort to nearby chests" button to the inventory screen.
	/// The mod writes nothing of its own to save files.
	/// </summary>
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "gk2.sorttonearbychests";
		public const string PluginName = "GK2 Sort To Nearby Chests";
		public const string PluginVersion = "1.0.2";

		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> OverflowToOtherChests;
		internal static ConfigEntry<bool> KeepHotbarItems;
		internal static ConfigEntry<string> KeepItemIds;
		internal static ConfigEntry<bool> MoveBags;
		internal static ConfigEntry<bool> MoveQuestItems;
		internal static ConfigEntry<bool> UseConveyorStorages;
		internal static ConfigEntry<float> NearbyRadius;

		private Harmony harmony;

		/// <summary>
		/// Loads the settings and applies the patches.
		/// If a game update broke a patch, all are removed and the game runs unmodded.
		/// </summary>
		private void Awake()
		{
			Log = Logger;
			BindConfig();

			harmony = new Harmony(PluginGuid);
			try
			{
				harmony.PatchAll(typeof(Plugin).Assembly);
			}
			catch (Exception ex)
			{
				Guard.Report("applying the patches (the mod is switched off)", ex);
				harmony.UnpatchSelf();
				return;
			}

			Log.LogInfo($"{PluginName} {PluginVersion} loaded (game {Application.version}).");
		}

		private void BindConfig()
		{
			OverflowToOtherChests = Config.Bind("Sorting", "OverflowToOtherChests", false,
				"Off: an item only goes into nearby chests that already contain it (or storages made for it, like firewood sheds); " +
				"items no nearby chest contains yet, and whatever doesn't fit, stay in the inventory. " +
				"On: those items go into any nearby chest with room.");

			KeepHotbarItems = Config.Bind("Sorting", "KeepHotbarItems", true,
				"Keep items that are pinned to the hotbar (1-4) in the inventory.");

			KeepItemIds = Config.Bind("Sorting", "KeepItemIds", "faith",
				"Comma-separated item ids that always stay in the inventory. Faith is kept by default because " +
				"Inspirations can only be paid for with faith you carry.");

			MoveBags = Config.Bind("Sorting", "MoveBags", false,
				"Also move bags (with their contents) into chests.");

			MoveQuestItems = Config.Bind("Sorting", "MoveQuestItems", false,
				"Also move quest items into chests.");

			UseConveyorStorages = Config.Bind("Sorting", "UseConveyorStorages", false,
				"Also put items into conveyor input/output chests. Off by default because production lines take items from them.");

			NearbyRadius = Config.Bind("Sorting", "NearbyRadius", 10f,
				new ConfigDescription(
					"Used only outside a storage area: storages within this many world units of the player count as nearby. " +
					"Inside an area (home, kitchen, yard...) the storages of that area are used, the same ones the inventory screen lists.",
					new AcceptableValueRange<float>(1f, 50f)));
		}
	}
}
