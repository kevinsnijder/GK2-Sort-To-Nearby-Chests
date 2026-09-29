using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// Keeps an error in this mod from breaking the game's own code (a failing inventory redraw leaves the controller
	/// without focus). Each failure is logged once per place, with the full stack trace, to both BepInEx\LogOutput.log
	/// and the game's Player.log.
	/// </summary>
	internal static class Guard
	{
		private const int MAX_REPORTS = 20;

		private static readonly HashSet<string> reported = new HashSet<string>();

		/// <summary>Runs <paramref name="action"/>. On an error, logs it and returns false.</summary>
		internal static bool Run(string place, Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				Report(place, ex);
				return false;
			}
		}

		/// <summary>Logs an error once per <paramref name="place"/>, at most <see cref="MAX_REPORTS"/> per session.</summary>
		internal static void Report(string place, Exception ex)
		{
			try
			{
				if (reported.Count > MAX_REPORTS || !reported.Add(place))
				{
					return;
				}
				if (reported.Count > MAX_REPORTS)
				{
					Plugin.Log.LogError($"{Plugin.PluginVersion}: more than {MAX_REPORTS} errors, further ones are not logged this session.");
					return;
				}
				var message = $"{Plugin.PluginVersion} (game {Application.version}): error in {place}. "
					+ $"This step was skipped and the game keeps running. Please report this message with the lines below.\n{ex}";
				Plugin.Log.LogError(message);
			}
			catch (Exception)
			{
			}
		}
	}
}
