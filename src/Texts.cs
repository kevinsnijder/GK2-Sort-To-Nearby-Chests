using System.Collections.Generic;
using LazyBearTechnology;

namespace GK2SortToNearbyChests
{
	/// <summary>
	/// The mod's UI strings in the game's languages. The game's text lookup returns unknown ids unchanged,
	/// so these strings can be passed to its tooltip, tip bar and notification code as they are.
	/// </summary>
	internal static class Texts
	{
		private sealed class Entry
		{
			public string Button;
			public string Moved;
			public string NoChests;
			public string NoMatch;
			public string Nothing;
		}

		private static readonly Dictionary<string, Entry> Languages = new Dictionary<string, Entry>
		{
			{ "en", new Entry { Button = "Sort to nearby chests", Moved = "Items sorted into nearby chests", NoChests = "No chests nearby", NoMatch = "No matching chests with room nearby", Nothing = "Nothing to sort" } },
			{ "de", new Entry { Button = "In Truhen in der Nähe einsortieren", Moved = "Gegenstände in Truhen in der Nähe einsortiert", NoChests = "Keine Truhen in der Nähe", NoMatch = "Keine passenden Truhen mit Platz in der Nähe", Nothing = "Nichts einzusortieren" } },
			{ "fr", new Entry { Button = "Ranger dans les coffres proches", Moved = "Objets rangés dans les coffres proches", NoChests = "Aucun coffre à proximité", NoMatch = "Aucun coffre correspondant avec de la place à proximité", Nothing = "Rien à ranger" } },
			{ "es", new Entry { Button = "Guardar en cofres cercanos", Moved = "Objetos guardados en cofres cercanos", NoChests = "No hay cofres cerca", NoMatch = "No hay cofres adecuados con espacio cerca", Nothing = "No hay nada que guardar" } },
			{ "pt", new Entry { Button = "Guardar nos baús próximos", Moved = "Itens guardados nos baús próximos", NoChests = "Nenhum baú por perto", NoMatch = "Nenhum baú correspondente com espaço por perto", Nothing = "Nada para guardar" } },
			{ "it", new Entry { Button = "Riponi nei forzieri vicini", Moved = "Oggetti riposti nei forzieri vicini", NoChests = "Nessun forziere nelle vicinanze", NoMatch = "Nessun forziere adatto con spazio nelle vicinanze", Nothing = "Niente da riporre" } },
			{ "ru", new Entry { Button = "Разложить по ближайшим сундукам", Moved = "Предметы разложены по ближайшим сундукам", NoChests = "Рядом нет сундуков", NoMatch = "Рядом нет подходящих сундуков со свободным местом", Nothing = "Нечего раскладывать" } },
			{ "uk", new Entry { Button = "Розкласти по найближчих скринях", Moved = "Предмети розкладено по найближчих скринях", NoChests = "Поруч немає скринь", NoMatch = "Поруч немає відповідних скринь із вільним місцем", Nothing = "Нічого розкладати" } },
			{ "pl", new Entry { Button = "Rozłóż do pobliskich skrzyń", Moved = "Przedmioty rozłożone do pobliskich skrzyń", NoChests = "Brak skrzyń w pobliżu", NoMatch = "Brak pasujących skrzyń z miejscem w pobliżu", Nothing = "Nie ma czego rozłożyć" } },
			{ "nl", new Entry { Button = "Opbergen in kisten in de buurt", Moved = "Voorwerpen opgeborgen in kisten in de buurt", NoChests = "Geen kisten in de buurt", NoMatch = "Geen passende kisten met ruimte in de buurt", Nothing = "Niets om op te bergen" } },
			{ "tr", new Entry { Button = "Yakındaki sandıklara yerleştir", Moved = "Eşyalar yakındaki sandıklara yerleştirildi", NoChests = "Yakında sandık yok", NoMatch = "Yakında uygun ve boş yeri olan sandık yok", Nothing = "Yerleştirilecek bir şey yok" } },
			{ "ja", new Entry { Button = "近くのチェストに整理", Moved = "アイテムを近くのチェストに整理しました", NoChests = "近くにチェストがありません", NoMatch = "空きのある該当チェストが近くにありません", Nothing = "整理するものがありません" } },
			{ "ko", new Entry { Button = "근처 상자로 정리", Moved = "아이템을 근처 상자로 정리했습니다", NoChests = "근처에 상자가 없습니다", NoMatch = "근처에 공간이 있는 해당 상자가 없습니다", Nothing = "정리할 아이템이 없습니다" } },
			{ "zh-hans", new Entry { Button = "整理到附近的箱子", Moved = "物品已整理到附近的箱子", NoChests = "附近没有箱子", NoMatch = "附近没有可放入的对应箱子", Nothing = "没有可整理的物品" } },
			{ "zh-hant", new Entry { Button = "整理到附近的箱子", Moved = "物品已整理到附近的箱子", NoChests = "附近沒有箱子", NoMatch = "附近沒有可放入的對應箱子", Nothing = "沒有可整理的物品" } },
		};

		private static Entry Current
		{
			get
			{
				var lang = (LLBase.CurrentLang ?? "en").ToLowerInvariant();
				if (Languages.TryGetValue(lang, out var entry))
				{
					return entry;
				}
				if (lang.StartsWith("zh"))
				{
					return lang.Contains("tw") || lang.Contains("hk") || lang.Contains("hant") ? Languages["zh-hant"] : Languages["zh-hans"];
				}
				var dash = lang.IndexOf('-');
				if (dash > 0 && Languages.TryGetValue(lang.Substring(0, dash), out entry))
				{
					return entry;
				}
				return Languages["en"];
			}
		}

		internal static string Button
		{
			get
			{
				return Current.Button;
			}
		}

		internal static string Moved
		{
			get
			{
				return Current.Moved;
			}
		}

		internal static string NoChests
		{
			get
			{
				return Current.NoChests;
			}
		}

		internal static string NoMatch
		{
			get
			{
				return Current.NoMatch;
			}
		}

		internal static string Nothing
		{
			get
			{
				return Current.Nothing;
			}
		}
	}
}
