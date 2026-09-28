using System.Collections.Generic;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>Finds the storages the player's items may be sorted into.</summary>
	internal static class NearbyStorage
	{
		/// <summary>
		/// Inside a storage area: every storage of that area (the same list the inventory screen shows under the
		/// player's inventory). Outside one: storages of the current scene within <see cref="Plugin.NearbyRadius"/>.
		/// Nearest first.
		/// </summary>
		internal static List<Inventory> Find(PlayerData playerData)
		{
			var playerPos = MainGame.PlayerController.MovablePosition;
			var found = new List<KeyValuePair<float, Inventory>>();
			var zone = playerData.CurrentWorldZoneData;
			if (zone != null)
			{
				var worldData = MainGame.Instance.GameSave.worldData;
				foreach (var guid in zone.wgoDataList)
				{
					var wgoData = worldData.GetWgoData(guid);
					if (IsUsableStorage(wgoData))
					{
						found.Add(new KeyValuePair<float, Inventory>(Vector3.Distance(wgoData.Position, playerPos), wgoData.Inventory));
					}
				}
			}
			else
			{
				var scene = MainGame.Instance.GameSave.WorldData.GetGameSceneDataById(playerData.currentGameSceneId);
				var radius = Plugin.NearbyRadius.Value;
				if (scene != null)
				{
					foreach (var wgoData in scene.wgoDataList)
					{
						if (!IsUsableStorage(wgoData))
						{
							continue;
						}
						var distance = Vector3.Distance(wgoData.Position, playerPos);
						if (distance <= radius)
						{
							found.Add(new KeyValuePair<float, Inventory>(distance, wgoData.Inventory));
						}
					}
				}
			}

			found.Sort((a, b) => a.Key.CompareTo(b.Key));
			var result = new List<Inventory>(found.Count);
			foreach (var pair in found)
			{
				if (pair.Value != null && pair.Value != playerData.inventory && !result.Contains(pair.Value))
				{
					result.Add(pair.Value);
				}
			}
			return result;
		}

		/// <summary>
		/// Uses the game's own rule for storages listed in the inventory screen (chests, sheds, pallets), which leaves out
		/// crafting stations, NPCs and graves. Conveyor storages only when enabled.
		/// </summary>
		private static bool IsUsableStorage(WgoData wgoData)
		{
			if (wgoData == null || wgoData.Definition == null)
			{
				return false;
			}
			var definition = wgoData.Definition;
			if (definition.inventorySize == 0 || !definition.OpenInMultiInventory)
			{
				return false;
			}
			if (!Plugin.UseConveyorStorages.Value && definition.conveyorType != ConveyorElementType.None)
			{
				return false;
			}
			return true;
		}
	}
}
