using System;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The "[LT] Sort to nearby chests" prompt in the Character window's tip bar.
	/// It is added just before drawing, after the game and other mods, so it is always last.
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
				Guard.Report("the controller prompt (switched off until the game restarts)", ex);
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

		/// <summary>Removes this prompt wherever it is; in some redraws it ends up in the middle of the text.</summary>
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

		/// <summary>Rebuilds the canvases now, so the bar isn't drawn for one frame without the prompt.</summary>
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
		/// When the game rewrites the tip bar after No More Running Back added its prompts, that mod would add them again
		/// a frame later and the bar jumps. Its tip update is run now instead.
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
				Guard.Report("updating No More Running Back's prompts (no longer tried)", ex.InnerException ?? ex);
			}
		}

		/// <summary>
		/// No More Running Back remembers the tip bar text it wrote and adds its prompts again when it changes.
		/// Its copy is updated to include this prompt, so nothing is repeated. Does nothing without that mod.
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

		/// <summary>Looks up No More Running Back's tip bar fields once. False when it isn't installed.</summary>
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
