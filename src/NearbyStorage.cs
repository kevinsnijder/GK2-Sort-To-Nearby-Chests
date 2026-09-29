using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>Finds the storages the player's items may be sorted into.</summary>
	internal static class NearbyStorage
	{
		private struct Found
		{
			public float Distance;
			public Inventory Inventory;
			public string Name;
		}

		private static readonly Dictionary<Inventory, string> names = new Dictionary<Inventory, string>();

		/// <summary>In a storage area: all storages of that area. Outside one: storages within NearbyRadius. Nearest first.</summary>
		internal static List<Inventory> Find(PlayerData playerData)
		{
			var playerPos = MainGame.PlayerController.MovablePosition;
			var found = new List<Found>();
			var zone = playerData.CurrentWorldZoneData;
			if (zone != null)
			{
				var worldData = MainGame.Instance.GameSave.worldData;
				foreach (var guid in zone.wgoDataList)
				{
					TryAdd(worldData.GetWgoData(guid), playerPos, float.MaxValue, found);
				}
			}
			else
			{
				var scene = MainGame.Instance.GameSave.WorldData.GetGameSceneDataById(playerData.currentGameSceneId);
				if (scene != null)
				{
					var radius = Plugin.NearbyRadius.Value;
					foreach (var wgoData in scene.wgoDataList)
					{
						TryAdd(wgoData, playerPos, radius, found);
					}
				}
			}

			found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
			names.Clear();
			var result = new List<Inventory>(found.Count);
			foreach (var storage in found)
			{
				if (storage.Inventory != playerData.inventory && !result.Contains(storage.Inventory))
				{
					result.Add(storage.Inventory);
					names[storage.Inventory] = storage.Name;
				}
			}
			return result;
		}

		/// <summary>The storage's object id and unique id, for log messages.</summary>
		internal static string Describe(Inventory inventory)
		{
			return inventory != null && names.TryGetValue(inventory, out var name) ? name : "(unknown storage)";
		}

		/// <summary>Adds the storage when it is usable and in range. A storage that fails the check is skipped and logged.</summary>
		private static void TryAdd(WgoData wgoData, Vector3 playerPos, float radius, List<Found> found)
		{
			if (wgoData == null)
			{
				return;
			}
			try
			{
				if (!IsUsableStorage(wgoData))
				{
					return;
				}
				var distance = Vector3.Distance(wgoData.Position, playerPos);
				var inventory = wgoData.Inventory;
				if (distance > radius || inventory == null || inventory.Data == null || inventory.Data.Inventory == null)
				{
					return;
				}
				found.Add(new Found { Distance = distance, Inventory = inventory, Name = NameOf(wgoData) });
			}
			catch (Exception ex)
			{
				Guard.Report($"checking storage {NameOf(wgoData)}", ex);
			}
		}

		/// <summary>
		/// Uses the game's own rule for storages shown in the inventory screen (chests, sheds, pallets).
		/// Conveyor storages only when enabled.
		/// </summary>
		private static bool IsUsableStorage(WgoData wgoData)
		{
			var definition = wgoData.Definition;
			if (definition == null || definition.inventorySize == 0 || !definition.OpenInMultiInventory)
			{
				return false;
			}
			if (!Plugin.UseConveyorStorages.Value && definition.conveyorType != ConveyorElementType.None)
			{
				return false;
			}
			return true;
		}

		private static string NameOf(WgoData wgoData)
		{
			try
			{
				return $"'{wgoData.id}' ({wgoData.UniqueId})";
			}
			catch (Exception)
			{
				return "(unreadable storage)";
			}
		}
	}
}
