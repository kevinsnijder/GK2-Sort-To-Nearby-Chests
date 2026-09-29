using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The "Sort to nearby chests" button: a copy of the game's "move all identical items" button on the inventory header.
	/// Mouse only; with a controller the tip bar shows an LT prompt instead. While a bag is open, its contents are sorted too.
	/// </summary>
	internal class SortButtonView : MonoBehaviour
	{
		private const string BUTTON_NAME = "SortToNearbyChestsBtn";
		private const string ICON_SPRITE = "btn_i-put_similar";
		private const float REFRESH_INTERVAL_SECONDS = 2f;
		private const float BAG_HEADER_SHIFT = -26f;
		private const float BAG_TITLE_MARGIN_LEFT = 34f;
		private const float BAG_TITLE_MARGIN_RIGHT = 76f;

		private static readonly AccessTools.FieldRef<CharMainPageWidget, MultiInventoryWidget> MultiInventoryRef =
			AccessTools.FieldRefAccess<CharMainPageWidget, MultiInventoryWidget>("multiInventoryWidget");
		private static readonly AccessTools.FieldRef<CharMainPageWidget, CharMainPageWidgetData> PageDataRef =
			AccessTools.FieldRefAccess<CharMainPageWidget, CharMainPageWidgetData>("data");
		private static readonly AccessTools.FieldRef<CharMainPageWidget, BagInventoryWidget> BagWidgetRef =
			AccessTools.FieldRefAccess<CharMainPageWidget, BagInventoryWidget>("bagInventoryWidget");

		private CharMainPageWidget page;
		private LazyButton button;
		private float refreshTimer;
		private bool canSort;
		private Inventory watchedInventory;
		private Vector2 templatePosition;
		private TMP_Text squeezedTitle;
		private Vector4 titleMargin;
		private TextOverflowModes titleOverflow;

		internal bool CanSort
		{
			get
			{
				return canSort;
			}
		}

		internal static SortButtonView Get(CharMainPageWidget page)
		{
			var view = page.GetComponent<SortButtonView>();
			if (view == null)
			{
				view = page.gameObject.AddComponent<SortButtonView>();
				view.page = page;
			}
			return view;
		}

		internal static SortButtonView Find(CharMainPageWidget page)
		{
			return page != null ? page.GetComponent<SortButtonView>() : null;
		}

		/// <summary>The bag open on the inventory page, or null.</summary>
		internal static Inventory OpenBag(CharMainPageWidget page)
		{
			var data = page != null ? PageDataRef(page) : null;
			if (data == null || !data.IsBagShown)
			{
				return null;
			}
			return data.BagInventoryWidgetData?.Inventory;
		}

		/// <summary>Takes the button off the pooled inventory header before the page releases it.</summary>
		internal void Park()
		{
			if (button == null || button.transform.parent == transform)
			{
				return;
			}
			if (UITooltip.IsTooltipShowingAtTarget(button.transform as RectTransform))
			{
				UITooltip.HideImmediately();
			}
			button.gameObject.SetActive(false);
			button.transform.SetParent(transform, false);
			RestoreTitle();
		}

		/// <summary>
		/// On the bag panel the button sits between the title and the close button, so the title is centered in the space
		/// left between the bag icon and the buttons. Long names end in "...".
		/// </summary>
		private void SqueezeTitle(InventoryHeaderWidget header)
		{
			var title = header.transform.Find("Header")?.GetComponent<TMP_Text>();
			if (title == null)
			{
				return;
			}
			squeezedTitle = title;
			titleMargin = title.margin;
			titleOverflow = title.overflowMode;
			title.margin = new Vector4(BAG_TITLE_MARGIN_LEFT, titleMargin.y, BAG_TITLE_MARGIN_RIGHT, titleMargin.w);
			title.overflowMode = TextOverflowModes.Ellipsis;
		}

		private void RestoreTitle()
		{
			if (squeezedTitle == null)
			{
				return;
			}
			squeezedTitle.margin = titleMargin;
			squeezedTitle.overflowMode = titleOverflow;
			squeezedTitle = null;
		}

		internal void RefreshNow()
		{
			refreshTimer = 0f;
			Refresh();
		}

		/// <summary>
		/// Keeps the button on the header of what it sorts (the open bag, else the player's inventory), shows it for mouse
		/// only, and greys it out when there is nothing to sort.
		/// </summary>
		private void LateUpdate()
		{
			Refresh();
		}

		/// <summary>
		/// Checking whether anything can be sorted walks every nearby storage, so it runs when the sorted inventory changes or
		/// a bag opens or closes, and otherwise only every <see cref="REFRESH_INTERVAL_SECONDS"/> for chests that change meanwhile.
		/// </summary>
		private void Refresh()
		{
			var bag = OpenBag(page);
			var header = bag != null ? FindBagHeader() : FindPlayerHeader();
			if (header == null)
			{
				Park();
				return;
			}
			if (button == null && !CreateButton(header))
			{
				return;
			}
			if (button.transform.parent != header.transform)
			{
				RestoreTitle();
				button.transform.SetParent(header.transform, false);
				button.transform.SetAsLastSibling();
				var rect = (RectTransform)button.transform;
				rect.anchoredPosition = bag != null ? templatePosition + new Vector2(BAG_HEADER_SHIFT, 0f) : templatePosition;
			}

			var sorted = bag ?? MainGame.PlayerData?.inventory;
			if (sorted != watchedInventory)
			{
				Watch(sorted);
				refreshTimer = 0f;
			}
			refreshTimer -= Time.unscaledDeltaTime;
			if (refreshTimer <= 0f)
			{
				refreshTimer = REFRESH_INTERVAL_SECONDS;
				UpdateCanSort();
			}

			var visible = !LazyInput.IsGamepadActive;
			if (button.gameObject.activeSelf != visible)
			{
				button.gameObject.SetActive(visible);
				if (!visible && UITooltip.IsTooltipShowingAtTarget(button.transform as RectTransform))
				{
					UITooltip.HideImmediately();
				}
			}
			if (visible && bag != null)
			{
				if (squeezedTitle == null)
				{
					SqueezeTitle(header);
				}
			}
			else
			{
				RestoreTitle();
			}
			if (button.interactable != canSort)
			{
				button.interactable = canSort;
			}
		}

		private void Watch(Inventory inventory)
		{
			if (watchedInventory == inventory)
			{
				return;
			}
			if (watchedInventory != null)
			{
				watchedInventory.OnItemsAdd -= OnInventoryChanged;
				watchedInventory.OnItemsRemove -= OnInventoryChanged;
			}
			watchedInventory = inventory;
			if (inventory != null)
			{
				inventory.OnItemsAdd += OnInventoryChanged;
				inventory.OnItemsRemove += OnInventoryChanged;
			}
		}

		private void OnInventoryChanged(List<Item> items)
		{
			refreshTimer = 0f;
		}

		private void OnDestroy()
		{
			Watch(null);
		}

		private void UpdateCanSort()
		{
			var playerData = MainGame.PlayerData;
			var bag = OpenBag(page);
			canSort = playerData != null && MainGame.PlayerController != null
				&& Sorter.HasMovableItems(playerData, bag) && Sorter.CanMoveAny(playerData, bag, NearbyStorage.Find(playerData));
		}

		/// <summary>The header of the player's own inventory: the first inventory drawn on the page.</summary>
		private InventoryHeaderWidget FindPlayerHeader()
		{
			var multiInventory = MultiInventoryRef(page);
			if (multiInventory == null || multiInventory.DrawnInventories.Count == 0 || MainGame.PlayerData == null)
			{
				return null;
			}
			var first = multiInventory.DrawnInventories[0];
			if (first == null || first.Data == null || first.Data.Inventory != MainGame.PlayerData.inventory)
			{
				return null;
			}
			return first.InventoryHeaderWidget;
		}

		/// <summary>The header of the open bag's panel. The button goes left of the panel's close button.</summary>
		private InventoryHeaderWidget FindBagHeader()
		{
			var bagWidget = BagWidgetRef(page);
			if (bagWidget == null || !bagWidget.isActiveAndEnabled)
			{
				return null;
			}
			return bagWidget.InventoryHeaderWidget;
		}

		private bool CreateButton(InventoryHeaderWidget header)
		{
			var template = header.MoveAllSimilarItemsToBagBtn;
			if (template == null)
			{
				return false;
			}
			var copy = Instantiate(template.gameObject, header.transform, false);
			copy.name = BUTTON_NAME;
			copy.SetActive(false);
			button = copy.GetComponent<LazyButton>();
			templatePosition = ((RectTransform)template.transform).anchoredPosition;
			RemoveControllerNavigation(copy);

			button.onClick.RemoveAllListeners();
			button.onEnter.RemoveAllListeners();
			button.onExit.RemoveAllListeners();
			button.onNotInteractableEnter.RemoveAllListeners();
			button.onNotInteractableExit.RemoveAllListeners();
			button.onEnterSound = string.Empty;
			button.onNotInteractableEnterSound = string.Empty;
			button.onClick.AddListener(OnClicked);
			button.onEnter.AddListener(ShowTooltip);
			button.onNotInteractableEnter.AddListener(ShowTooltip);
			button.onExit.AddListener(HideTooltip);
			button.onNotInteractableExit.AddListener(HideTooltip);

			SetIcon(copy);
			return true;
		}

		/// <summary>
		/// The button is mouse only. The copied template can take controller focus: when the player switches from mouse to
		/// controller, the game focuses the first target while this copy is still shown, which then hides.
		/// </summary>
		private static void RemoveControllerNavigation(GameObject copy)
		{
			foreach (var navigationItem in copy.GetComponentsInChildren<GamepadNavigationItem>(true))
			{
				DestroyImmediate(navigationItem);
			}
		}

		private static void SetIcon(GameObject copy)
		{
			var icon = copy.transform.Find("BtnImage/Icon");
			if (icon == null)
			{
				return;
			}
			var image = icon.GetComponent<Image>();
			var sprite = FindSprite(ICON_SPRITE);
			if (image != null && sprite != null)
			{
				image.sprite = sprite;
				image.SetNativeSize();
			}
		}

		private static Sprite FindSprite(string spriteName)
		{
			foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
			{
				if (sprite.name == spriteName)
				{
					return sprite;
				}
			}
			return null;
		}

		private void OnClicked()
		{
			HideTooltip();
			SortActions.SortNow(page);
		}

		private void ShowTooltip()
		{
			UITooltip.ShowSimpleInfo(button.transform, Texts.Button);
		}

		private void HideTooltip()
		{
			UITooltip.Hide();
		}
	}

	internal static class SortActions
	{
		/// <summary>Sorts the inventory into nearby storages and shows the result as a game notification.</summary>
		internal static void SortNow(CharMainPageWidget page)
		{
			var playerData = MainGame.PlayerData;
			if (playerData == null || MainGame.PlayerController == null)
			{
				return;
			}

			var storages = NearbyStorage.Find(playerData);
			if (storages.Count == 0)
			{
				Notify(Texts.NoChests);
				return;
			}
			var bag = SortButtonView.OpenBag(page);
			if (!Sorter.HasMovableItems(playerData, bag))
			{
				Notify(Texts.Nothing);
				return;
			}

			var result = Sorter.SortPlayerInventory(playerData, bag, storages);
			Plugin.Log.LogInfo($"Sorted {result.MovedCount} items into {result.StorageCount} storages, {result.LeftCount} stayed in the inventory.");
			if (result.MovedCount > 0)
			{
				LazyAudio.PlayAndForget("item_put");
				Notify(Texts.Moved);
			}
			else
			{
				Notify(Texts.NoMatch);
			}

			SortButtonView.Find(page)?.RefreshNow();
		}

		private static void Notify(string text)
		{
			try
			{
				LazySingleton<UINotificator>.Instance.ShowSimpleTextNotification(text);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning($"Could not show notification: {ex.Message}");
			}
		}
	}

	[HarmonyPatch(typeof(CharMainPageWidget), nameof(CharMainPageWidget.Redraw))]
	internal static class CharMainPageRedrawPatch
	{
		/// <summary>Adds the button and the controller prompt when the inventory page is drawn.</summary>
		private static void Postfix(CharMainPageWidget __instance)
		{
			SortButtonView.Get(__instance).RefreshNow();
			TipBarPrompt.Track(__instance);
		}
	}

	[HarmonyPatch(typeof(CharMainPageWidget), nameof(CharMainPageWidget.Hide))]
	internal static class CharMainPageHidePatch
	{
		/// <summary>Takes the button off the inventory header before the game recycles the header.</summary>
		private static void Prefix(CharMainPageWidget __instance)
		{
			SortButtonView.Find(__instance)?.Park();
		}
	}

	/// <summary>
	/// Controller: LT sorts to nearby chests on the inventory page, once per press (a held or drifting trigger would otherwise
	/// sort again at the game's key-repeat rate). The game only uses LT to switch sub-tabs on the Tech Tree and Inspirations
	/// pages, which keep that behaviour.
	/// </summary>
	[HarmonyPatch(typeof(CharacterWindow), "GetGameKeyDelegates")]
	internal static class CharacterWindowKeysPatch
	{
		internal static readonly GameKey SortKey = GameKey.PrevSubTab;

		private static readonly AccessTools.FieldRef<CharacterWindow, CharMainPageWidget> MainPageRef =
			AccessTools.FieldRefAccess<CharacterWindow, CharMainPageWidget>("mainPageWidget");

		private static void Postfix(CharacterWindow __instance, Dictionary<GameKey, Func<bool>> __result)
		{
			if (__result == null)
			{
				return;
			}
			__result.TryGetValue(SortKey, out var original);
			var window = __instance;
			__result[SortKey] = () =>
			{
				if (TrySort(window))
				{
					return true;
				}
				return original != null && original();
			};
		}

		private static bool TrySort(CharacterWindow window)
		{
			if (!LazyInput.IsGamepadActive || !window.IsShownAndTop || window.LastOpenedPage != CharacterWindowData.CharPage.Main)
			{
				return false;
			}
			var page = MainPageRef(window);
			if (page == null)
			{
				return false;
			}
			LazyInput.WaitForRelease(SortKey);
			SortActions.SortNow(page);
			return true;
		}
	}
}
