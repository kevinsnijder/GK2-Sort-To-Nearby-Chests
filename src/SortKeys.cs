using System;
using LazyBearTechnology;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The controller button that sorts, from the settings.
	/// Each controller button is read through the game key the game binds to it; these game keys do nothing on the Character page.
	/// </summary>
	internal static class SortKeys
	{
		internal const string DEFAULT_BUTTON = "LT";
		private const string NO_BUTTON = "None";
		private const string RIGHT_STICK_ICON = "stick-R";
		private const string LEFT_STICK_PUSH_ICON = "stick-L_push";
		private const string RIGHT_STICK_PUSH_ICON = "stick-R_push";

		internal static readonly string[] ButtonNames = { "LT", "RT", "LStick", "RStick", NO_BUTTON };

		/// <summary>The game keys of the buttons in <see cref="ButtonNames"/>, in the same order (None has none).</summary>
		internal static readonly GameKey[] ButtonKeys = { GameKey.PrevSubTab, GameKey.NextSubTab, GameKey.LeftStick, GameKey.RightStick };

		/// <summary>The game key of the chosen controller button, or null for None.</summary>
		internal static GameKey ControllerKey
		{
			get
			{
				var name = Plugin.ControllerButton.Value;
				for (var i = 0; i < ButtonKeys.Length; i++)
				{
					if (ButtonNames[i] == name)
					{
						return ButtonKeys[i];
					}
				}
				return null;
			}
		}

		/// <summary>
		/// The icon of a button for the current controller, or an empty string when there is none.
		/// The game has no icon for pressing the left stick and shows the right stick moving for pressing it, so the stick buttons
		/// use the game's "stick pressed" sprites, found from the right stick icon of the current controller (xbox_stick-R becomes
		/// xbox_stick-L_push, and so on).
		/// </summary>
		internal static string IconText(GameKey key, GameKeyIconType iconType)
		{
			if (key != GameKey.LeftStick && key != GameKey.RightStick)
			{
				return ControllerIconLibrary.GetIconId(key, iconType, trailingSpace: false);
			}
			var stick = ControllerIconLibrary.GetIconId(GameKey.RightStick, iconType, trailingSpace: false);
			if (stick.IndexOf(RIGHT_STICK_ICON, StringComparison.Ordinal) < 0)
			{
				return key == GameKey.RightStick ? stick : string.Empty;
			}
			return stick.Replace(RIGHT_STICK_ICON, key == GameKey.LeftStick ? LEFT_STICK_PUSH_ICON : RIGHT_STICK_PUSH_ICON);
		}
	}
}
