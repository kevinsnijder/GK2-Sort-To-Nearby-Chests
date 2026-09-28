using System;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The controller prompt "[LT] Sort to nearby chests" at the end of the Character window's tip bar. It is added just
	/// before the screen is drawn, after the game and other mods wrote their prompts, so it is always the last one.
	/// </summary>
	internal static class TipBarPrompt
	{
		private const string SEPARATOR = "  ";
		private const string NOTEPAD_ASSEMBLY = "GK2Notepad.Core";
		private const string NOTEPAD_TIPS_TYPE = "GK2Notepad.ItemMenuInjector";

		private static readonly AccessTools.FieldRef<LazyWindow<CharacterWindowData>, LazyButtonTipsStr> TipsRef =
			AccessTools.FieldRefAccess<LazyWindow<CharacterWindowData>, LazyButtonTipsStr>("lazyButtonTips");
		private static readonly AccessTools.FieldRef<LazyButtonTipsStr, TextMeshProUGUI> LabelRef =
			AccessTools.FieldRefAccess<LazyButtonTipsStr, TextMeshProUGUI>("label");

		private static CharacterWindow window;
		private static CharMainPageWidget page;
		private static string appended;
		private static bool subscribed;

		private static bool notepadResolved;
		private static FieldInfo notepadInstance;
		private static FieldInfo notepadLabel;
		private static FieldInfo notepadWritten;

		internal static void Track(CharMainPageWidget mainPage)
		{
			page = mainPage;
			window = mainPage.GetComponentInParent<CharacterWindow>(true);
			if (!subscribed)
			{
				Canvas.willRenderCanvases += OnWillRenderCanvases;
				subscribed = true;
			}
		}

		private static void OnWillRenderCanvases()
		{
			try
			{
				Apply();
			}
			catch (Exception ex)
			{
				Canvas.willRenderCanvases -= OnWillRenderCanvases;
				subscribed = false;
				Plugin.Log.LogWarning($"Controller prompt disabled: {ex.Message}");
			}
		}

		private static void Apply()
		{
			if (window == null || page == null)
			{
				return;
			}
			var tips = TipsRef(window);
			var label = tips != null ? LabelRef(tips) : null;
			if (label == null)
			{
				return;
			}

			var text = label.text ?? string.Empty;
			var original = text;
			if (appended != null && text.EndsWith(appended, StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - appended.Length);
			}

			var wanted = ShouldShow() ? Prompt(text.Length > 0) : null;
			var result = wanted == null ? text : text + wanted;
			appended = wanted;
			if (result == original)
			{
				return;
			}
			label.text = result;
			SyncNotepad(label, original, result);
		}

		private static bool ShouldShow()
		{
			return LazyInput.IsGamepadActive && window.IsShownAndTop && window.LastOpenedPage == CharacterWindowData.CharPage.Main
				&& page.isActiveAndEnabled && !page.IsBagModeEnabled();
		}

		private static string Prompt(bool withSeparator)
		{
			var view = SortButtonView.Find(page);
			var iconType = view != null && view.CanSort ? GameKeyIconType.Default : GameKeyIconType.Inactive;
			var icon = ControllerIconLibrary.GetIconId(CharacterWindowKeysPatch.SortKey, iconType, trailingSpace: false);
			if (string.IsNullOrEmpty(icon))
			{
				return null;
			}
			return (withSeparator ? SEPARATOR : string.Empty) + icon + Texts.Button;
		}

		/// <summary>
		/// The "No More Running Back" Workshop mod (GK2Notepad) adds its own prompts and remembers the text it wrote; if the
		/// text changed, it adds them again. Its copy is updated to include this prompt, so they are not repeated. Does nothing
		/// without that mod. It switches itself off when its code is patched, so its fields are only read and written.
		/// </summary>
		private static void SyncNotepad(TextMeshProUGUI label, string before, string after)
		{
			if (!ResolveNotepad())
			{
				return;
			}
			var injector = notepadInstance.GetValue(null);
			if (injector == null || !ReferenceEquals(notepadLabel.GetValue(injector), label))
			{
				return;
			}
			if ((string)notepadWritten.GetValue(injector) == before)
			{
				notepadWritten.SetValue(injector, after);
			}
		}

		/// <summary>Looks up that mod's tip bar fields once. False when the mod is not installed.</summary>
		private static bool ResolveNotepad()
		{
			if (notepadResolved)
			{
				return notepadWritten != null;
			}
			notepadResolved = true;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.GetName().Name != NOTEPAD_ASSEMBLY)
				{
					continue;
				}
				var type = assembly.GetType(NOTEPAD_TIPS_TYPE);
				const BindingFlags INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
				notepadInstance = type?.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
				notepadLabel = type?.GetField("tipsLabel", INSTANCE);
				notepadWritten = type?.GetField("tipsWritten", INSTANCE);
				if (notepadInstance == null || notepadLabel == null || notepadWritten?.FieldType != typeof(string))
				{
					notepadWritten = null;
					Plugin.Log.LogWarning("No More Running Back's tip bar changed; its controller prompts may be repeated.");
				}
				break;
			}
			return notepadWritten != null;
		}
	}
}
