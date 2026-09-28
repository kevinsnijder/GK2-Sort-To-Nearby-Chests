using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The "Sort to nearby chests" button: a copy of the game's "move all identical items" button on the inventory header.
	/// Mouse only; with a controller the tip bar shows an LT prompt instead. Hidden while a bag is open.
	/// </summary>
	internal class SortButtonView : MonoBehaviour
	{
		private const string BUTTON_NAME = "SortToNearbyChestsBtn";
		private const string ICON_SPRITE = "btn_i-put_similar";
		private const float REFRESH_INTERVAL_SECONDS = 0.25f;

		private static readonly AccessTools.FieldRef<CharMainPageWidget, MultiInventoryWidget> MultiInventoryRef =
			AccessTools.FieldRefAccess<CharMainPageWidget, MultiInventoryWidget>("multiInventoryWidget");

		private CharMainPageWidget page;
		private LazyButton button;
		private float refreshTimer;
		private bool canSort;

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
		}

		internal void RefreshNow()
		{
			refreshTimer = 0f;
			Refresh();
		}

		/// <summary>Keeps the button on the player's inventory header, shows it for mouse only, and greys it out when there is nothing to sort.</summary>
		private void LateUpdate()
		{
			Refresh();
		}

		private void Refresh()
		{
			var header = FindPlayerHeader();
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
				button.transform.SetParent(header.transform, false);
				button.transform.SetAsLastSibling();
			}

			refreshTimer -= Time.unscaledDeltaTime;
			if (refreshTimer <= 0f)
			{
				refreshTimer = REFRESH_INTERVAL_SECONDS;
				UpdateCanSort();
			}

			var visible = !LazyInput.IsGamepadActive && !page.IsBagModeEnabled();
			if (button.gameObject.activeSelf != visible)
			{
				button.gameObject.SetActive(visible);
				if (!visible && UITooltip.IsTooltipShowingAtTarget(button.transform as RectTransform))
				{
					UITooltip.HideImmediately();
				}
			}
			if (button.interactable != canSort)
			{
				button.interactable = canSort;
			}
		}

		private void UpdateCanSort()
		{
			var playerData = MainGame.PlayerData;
			canSort = playerData != null && MainGame.PlayerController != null
				&& Sorter.HasMovableItems(playerData) && Sorter.CanMoveAny(playerData, NearbyStorage.Find(playerData));
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
			if (!Sorter.HasMovableItems(playerData))
			{
				Notify(Texts.Nothing);
				return;
			}

			var result = Sorter.SortPlayerInventory(playerData, storages);
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
	/// Controller: LT sorts to nearby chests on the inventory page. The game only uses LT to switch sub-tabs on the
	/// Tech Tree and Inspirations pages, which keep that behaviour.
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
			if (page == null || page.IsBagModeEnabled())
			{
				return false;
			}
			SortActions.SortNow(page);
			return true;
		}
	}
}
