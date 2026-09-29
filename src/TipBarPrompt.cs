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

		private static readonly GameKeyIconType[] ICON_TYPES = { GameKeyIconType.Default, GameKeyIconType.Inactive };

		private static CharacterWindow window;
		private static CharMainPageWidget page;
		private static bool subscribed;
		private static string lastWritten;

		private static bool notepadResolved;
		private static FieldInfo notepadInstance;
		private static FieldInfo notepadLabel;
		private static FieldInfo notepadWritten;
		private static MethodInfo notepadUpdate;

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

			if (label.text != lastWritten)
			{
				UpdateNotepadNow();
			}
			var original = label.text ?? string.Empty;
			var text = RemovePrompts(original);
			var wanted = ShouldShow() ? Prompt(CurrentIconType()) : null;
			var result = wanted == null ? text : text + (text.Length > 0 ? SEPARATOR : string.Empty) + wanted;
			lastWritten = result;
			if (result == original)
			{
				return;
			}
			label.text = result;
			SyncNotepad(label, original, result);
			RedrawNow();
		}

		/// <summary>
		/// Removes this prompt wherever it is. When the game rewrites the tip bar outside its input update (an inventory
		/// redraw, for example), the prompt is added before No More Running Back adds its own prompts after the text, so it
		/// can end up in the middle.
		/// </summary>
		private static string RemovePrompts(string text)
		{
			foreach (var iconType in ICON_TYPES)
			{
				var prompt = Prompt(iconType);
				if (prompt == null || text.IndexOf(prompt, StringComparison.Ordinal) < 0)
				{
					continue;
				}
				text = text.Replace(SEPARATOR + prompt, string.Empty).Replace(prompt + SEPARATOR, string.Empty).Replace(prompt, string.Empty);
			}
			return text;
		}

		/// <summary>
		/// The canvases may already be rebuilt this frame with the old text; without this the bar is drawn one frame without
		/// the prompt. The rebuild raises the render event again, which finds the text unchanged and returns.
		/// </summary>
		private static void RedrawNow()
		{
			Canvas.ForceUpdateCanvases();
		}

		private static bool ShouldShow()
		{
			return LazyInput.IsGamepadActive && window.IsShownAndTop && window.LastOpenedPage == CharacterWindowData.CharPage.Main
				&& page.isActiveAndEnabled;
		}

		private static GameKeyIconType CurrentIconType()
		{
			var view = SortButtonView.Find(page);
			return view != null && view.CanSort ? GameKeyIconType.Default : GameKeyIconType.Inactive;
		}

		private static string Prompt(GameKeyIconType iconType)
		{
			var icon = ControllerIconLibrary.GetIconId(CharacterWindowKeysPatch.SortKey, iconType, trailingSpace: false);
			if (string.IsNullOrEmpty(icon))
			{
				return null;
			}
			return icon + Texts.Button;
		}

		/// <summary>
		/// The game sometimes rewrites the tip bar after No More Running Back added its prompts for the frame (opening a bag,
		/// for example). That mod would add them one frame later, in front of this prompt, so the bar jumps. Its own tip update
		/// is run now instead; it does nothing when its prompts are already there.
		/// </summary>
		private static void UpdateNotepadNow()
		{
			if (!ResolveNotepad() || notepadUpdate == null)
			{
				return;
			}
			var injector = notepadInstance.GetValue(null) as Behaviour;
			if (injector == null || !injector.isActiveAndEnabled)
			{
				return;
			}
			try
			{
				notepadUpdate.Invoke(injector, null);
			}
			catch (Exception ex)
			{
				notepadUpdate = null;
				Plugin.Log.LogWarning($"Could not update No More Running Back's prompts: {ex.InnerException?.Message ?? ex.Message}");
			}
		}

		/// <summary>
		/// The "No More Running Back" Workshop mod (GK2Notepad) adds its own prompts and remembers the text it wrote; if the
		/// text changed, it adds them again. Its copy is updated to include this prompt, so they are not repeated. Does nothing
		/// without that mod. It switches itself off when its code is patched, so it is only read, written and called.
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
				notepadUpdate = type?.GetMethod("UpdateTips", INSTANCE, null, Type.EmptyTypes, null);
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
