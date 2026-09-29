using System;
using System.Collections.Generic;

namespace GK2SortToNearbyChests
{
	internal struct SortResult
	{
		public int StorageCount;
		public int MovedCount;
		public int LeftCount;
	}

	/// <summary>An item the sort may move, and the inventory it comes from (the player's inventory or the open bag).</summary>
	internal struct SortCandidate
	{
		public Item Item;
		public Inventory From;
	}

	/// <summary>
	/// Moves the player's inventory, or the open bag, into nearby storages.
	/// Uses the same calls as the chest window.
	/// </summary>
	internal static class Sorter
	{
		private static Inventory currentStorage;

		internal static SortResult SortPlayerInventory(PlayerData playerData, Inventory openBag, List<Inventory> storages)
		{
			var result = new SortResult { StorageCount = storages.Count };

			UINotificator.isSilent = true;
			try
			{
				foreach (var candidate in Candidates(playerData, openBag))
				{
					currentStorage = null;
					try
					{
						result.MovedCount += MoveStack(candidate, storages);
						if (!IsGone(candidate))
						{
							result.LeftCount += candidate.Item.Count;
						}
					}
					catch (Exception ex)
					{
						ReportItem("sorting", candidate.Item, ex);
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
		internal static bool HasMovableItems(PlayerData playerData, Inventory openBag)
		{
			return Candidates(playerData, openBag).Count > 0;
		}

		/// <summary>True when sorting would move at least one item. Moves nothing.</summary>
		internal static bool CanMoveAny(PlayerData playerData, Inventory openBag, List<Inventory> storages)
		{
			foreach (var candidate in Candidates(playerData, openBag))
			{
				currentStorage = null;
				try
				{
					foreach (var storage in storages)
					{
						currentStorage = storage;
						if (IsTarget(storage, candidate.Item) && HasRoomFor(storage, candidate.Item))
						{
							return true;
						}
					}
				}
				catch (Exception ex)
				{
					ReportItem("checking", candidate.Item, ex);
				}
			}
			return false;
		}

		/// <summary>The items to try: with a bag open only its contents, otherwise the player's inventory.</summary>
		private static List<SortCandidate> Candidates(PlayerData playerData, Inventory openBag)
		{
			var from = openBag ?? playerData.inventory;
			var result = new List<SortCandidate>();
			foreach (var item in from.Data.Inventory)
			{
				if (ShouldMoveSafe(playerData, item))
				{
					result.Add(new SortCandidate { Item = item, From = from });
				}
			}
			return result;
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

		/// <summary>An item that fails the check stays in the inventory and is logged.</summary>
		private static bool ShouldMoveSafe(PlayerData playerData, Item item)
		{
			try
			{
				return ShouldMove(playerData, item);
			}
			catch (Exception ex)
			{
				currentStorage = null;
				ReportItem("checking", item, ex);
				return false;
			}
		}

		/// <summary>Logs a failure with the item and, when known, the storage it was being checked against.</summary>
		private static void ReportItem(string action, Item item, Exception ex)
		{
			string itemId;
			try
			{
				itemId = item?.id ?? "(null)";
			}
			catch (Exception)
			{
				itemId = "(unreadable)";
			}
			var storage = currentStorage != null ? $" with storage {NearbyStorage.Describe(currentStorage)}" : string.Empty;
			Guard.Report($"{action} item '{itemId}'{storage}", ex);
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
		/// Moves one stack: first into storages that already hold the item, then into storages made for it,
		/// then, with overflow on, into any storage with room. Returns the count moved.
		/// </summary>
		private static int MoveStack(SortCandidate candidate, List<Inventory> storages)
		{
			var from = candidate.From;
			var item = candidate.Item;
			var moved = 0;

			var holders = new List<KeyValuePair<int, Inventory>>();
			foreach (var storage in storages)
			{
				currentStorage = storage;
				var count = CountTopLevel(storage, item.id);
				if (count > 0)
				{
					holders.Add(new KeyValuePair<int, Inventory>(count, storage));
				}
			}
			holders.Sort((a, b) => b.Key.CompareTo(a.Key));
			foreach (var holder in holders)
			{
				currentStorage = holder.Value;
				moved += MoveInto(from, item, holder.Value);
				if (IsGone(candidate))
				{
					return moved;
				}
			}

			foreach (var storage in storages)
			{
				currentStorage = storage;
				if (IsMadeFor(storage, item))
				{
					moved += MoveInto(from, item, storage);
					if (IsGone(candidate))
					{
						return moved;
					}
				}
			}

			if (Plugin.OverflowToOtherChests.Value)
			{
				foreach (var storage in storages)
				{
					currentStorage = storage;
					moved += MoveInto(from, item, storage);
					if (IsGone(candidate))
					{
						return moved;
					}
				}
			}
			return moved;
		}

		/// <summary>
		/// Moves as much of the item as fits, like the chest window does. Tools and bags are moved whole,
		/// so they keep their durability and contents. Returns the count moved.
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

		private static bool IsGone(SortCandidate candidate)
		{
			return candidate.Item.IsEmpty || !candidate.From.Data.Inventory.Contains(candidate.Item);
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
