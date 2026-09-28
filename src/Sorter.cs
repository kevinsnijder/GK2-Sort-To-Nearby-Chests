using System.Collections.Generic;

namespace GK2SortToNearbyChests
{
	internal struct SortResult
	{
		public int StorageCount;
		public int MovedCount;
		public int LeftCount;
	}

	/// <summary>
	/// Moves the player's inventory into nearby storages, using the same inventory calls as the chest window.
	/// </summary>
	internal static class Sorter
	{
		internal static SortResult SortPlayerInventory(PlayerData playerData, List<Inventory> storages)
		{
			var result = new SortResult { StorageCount = storages.Count };
			var playerInventory = playerData.inventory;
			var snapshot = new List<Item>(playerInventory.Data.Inventory);

			UINotificator.isSilent = true;
			try
			{
				foreach (var item in snapshot)
				{
					if (!ShouldMove(playerData, item))
					{
						continue;
					}
					result.MovedCount += MoveStack(playerInventory, item, storages);
					if (!item.IsEmpty && playerInventory.Data.Inventory.Contains(item))
					{
						result.LeftCount += item.Count;
					}
				}
			}
			finally
			{
				UINotificator.isSilent = false;
			}

			if (result.MovedCount > 0 && playerData.HasInteractingItem)
			{
				playerData.UpdateInteractingItem();
			}
			return result;
		}

		/// <summary>True when the inventory holds at least one item the sort would try to move.</summary>
		internal static bool HasMovableItems(PlayerData playerData)
		{
			foreach (var item in playerData.inventory.Data.Inventory)
			{
				if (ShouldMove(playerData, item))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>True when sorting would move at least one item into <paramref name="storages"/>. Moves nothing.</summary>
		internal static bool CanMoveAny(PlayerData playerData, List<Inventory> storages)
		{
			foreach (var item in playerData.inventory.Data.Inventory)
			{
				if (!ShouldMove(playerData, item))
				{
					continue;
				}
				foreach (var storage in storages)
				{
					if (IsTarget(storage, item) && HasRoomFor(storage, item))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>Whether sorting may put <paramref name="item"/> into <paramref name="storage"/> at all.</summary>
		private static bool IsTarget(Inventory storage, Item item)
		{
			return Plugin.OverflowToOtherChests.Value || CountTopLevel(storage, item.id) > 0 || IsMadeFor(storage, item);
		}

		private static bool IsMadeFor(Inventory storage, Item item)
		{
			return storage.Data.TryGetProperty<WhiteListFilterSerializedItemProperty>(out var filter)
				&& filter.WhiteList != null && filter.WhiteList.Contains(item.Definition);
		}

		private static bool HasRoomFor(Inventory storage, Item item)
		{
			return storage.Data.CanAddItemCountToInventory(item, considerEmptySlots: true, null, ignoreAllBags: true) > 0;
		}

		/// <summary>False for items that stay: kept ids, bags, quest items, the seed in hand and hotbar items (per settings).</summary>
		private static bool ShouldMove(PlayerData playerData, Item item)
		{
			if (item == null || item.IsEmpty || item.Definition == null)
			{
				return false;
			}
			if (IsKeptId(item.id))
			{
				return false;
			}
			if (item.IsBag && !Plugin.MoveBags.Value)
			{
				return false;
			}
			if (item.Definition.isQuestItem && !Plugin.MoveQuestItems.Value)
			{
				return false;
			}
			if (playerData.HasInteractingItem && playerData.interactingItem.id == item.id)
			{
				return false;
			}
			if (Plugin.KeepHotbarItems.Value && playerData.pinnedItems != null)
			{
				foreach (var pinned in playerData.pinnedItems)
				{
					if (!string.IsNullOrEmpty(pinned) && pinned == item.id)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static string keptIdsSource;
		private static HashSet<string> keptIds = new HashSet<string>();

		private static bool IsKeptId(string itemId)
		{
			var source = Plugin.KeepItemIds.Value ?? string.Empty;
			if (source != keptIdsSource)
			{
				keptIdsSource = source;
				keptIds = new HashSet<string>();
				foreach (var part in source.Split(','))
				{
					var id = part.Trim();
					if (id.Length > 0)
					{
						keptIds.Add(id);
					}
				}
			}
			return keptIds.Contains(itemId);
		}

		/// <summary>
		/// Moves one stack: first into storages that already hold the item (most first), then into storages made for it,
		/// then, with overflow on, into any storage with room. Returns the count moved.
		/// </summary>
		private static int MoveStack(Inventory from, Item item, List<Inventory> storages)
		{
			var moved = 0;

			var holders = new List<KeyValuePair<int, Inventory>>();
			foreach (var storage in storages)
			{
				var count = CountTopLevel(storage, item.id);
				if (count > 0)
				{
					holders.Add(new KeyValuePair<int, Inventory>(count, storage));
				}
			}
			holders.Sort((a, b) => b.Key.CompareTo(a.Key));
			foreach (var holder in holders)
			{
				moved += MoveInto(from, item, holder.Value);
				if (IsGone(from, item))
				{
					return moved;
				}
			}

			foreach (var storage in storages)
			{
				if (IsMadeFor(storage, item))
				{
					moved += MoveInto(from, item, storage);
					if (IsGone(from, item))
					{
						return moved;
					}
				}
			}

			if (Plugin.OverflowToOtherChests.Value)
			{
				foreach (var storage in storages)
				{
					moved += MoveInto(from, item, storage);
					if (IsGone(from, item))
					{
						return moved;
					}
				}
			}
			return moved;
		}

		/// <summary>
		/// Moves as much of <paramref name="item"/> as fits into <paramref name="to"/>, like the chest window does. Unstackable
		/// items (tools, bags) are copied whole so they keep their durability and contents. Returns the count moved.
		/// </summary>
		private static int MoveInto(Inventory from, Item item, Inventory to)
		{
			if (item.IsEmpty || !HasRoomFor(to, item))
			{
				return 0;
			}

			List<Item> addedItems;
			if (item.IsBag || item.Definition.stackCount <= 1)
			{
				if (!to.AddItemToInventory(Item.Copy(item), out addedItems, null, ignoreAllBags: true))
				{
					return 0;
				}
				var addedCopies = Total(addedItems);
				if (addedCopies > 0)
				{
					from.RemoveItemFromInventoryByUID(item, addedCopies);
				}
				return addedCopies;
			}

			if (!to.AddItemToInventory(new Item(item.id, item.Count), out addedItems, null, ignoreAllBags: true))
			{
				return 0;
			}
			var added = Total(addedItems);
			if (added > 0)
			{
				from.RemoveItemFromInventoryByUID(item, added);
			}
			return added;
		}

		private static bool IsGone(Inventory from, Item item)
		{
			return item.IsEmpty || !from.Data.Inventory.Contains(item);
		}

		private static int CountTopLevel(Inventory storage, string itemId)
		{
			var count = 0;
			foreach (var stored in storage.Data.Inventory)
			{
				if (stored.id == itemId)
				{
					count += stored.Count;
				}
			}
			return count;
		}

		private static int Total(List<Item> items)
		{
			var total = 0;
			if (items == null)
			{
				return 0;
			}
			foreach (var added in items)
			{
				total += added.Count;
			}
			return total;
		}
	}
}
