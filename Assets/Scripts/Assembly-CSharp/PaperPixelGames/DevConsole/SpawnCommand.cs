using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public class SpawnCommand : IDevCommand
	{
		public string Name => "spawn";

		public string Description => "  spawn <type> <index> OR\n  spawn <index> OR\n  spawn <id> OR\n  spawn df <fishId> OR\n  spawn deadfish <fishId> OR\n  spawn index <type> <index> OR\n  spawn index <type>\n  w = Wholesale\n  r = RawIng\n  p = ProcessedIng\n  s = ServedDish\n  df = DeadFish\n";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (!NetworkManager.Singleton.IsServer)
			{
				feedback("Only the server can execute 'spawn' commands.");
				return;
			}
			if (context == null)
			{
				feedback("DevConsoleContext is missing.");
				return;
			}
			if (args.Length < 2)
			{
				feedback(Description);
				return;
			}
			if (args.Length == 3 && IsDeadFishToken(args[1]))
			{
				if (!int.TryParse(args[2], out var result))
				{
					feedback("Invalid fish index. Example: spawn fish 0");
					return;
				}
				FishSO fishSO = context?.ItemDatabase.CreatureItems[result] as FishSO;
				if (!(fishSO == null))
				{
					TrySpawnDeadFish(context, fishSO, feedback);
				}
				return;
			}
			if (context?.ItemDatabase == null)
			{
				feedback("ItemDatabase is missing on DevConsole.");
				return;
			}
			ItemDatabaseSO itemDatabase = context.ItemDatabase;
			if (args.Length >= 3 && string.Equals(args[1], "index", StringComparison.OrdinalIgnoreCase))
			{
				if (!TryParseTypeLetter(args[2], out var type))
				{
					feedback("Invalid type. Use: w, r, p, s");
					return;
				}
				IReadOnlyList<ItemSO> itemsByItemType = itemDatabase.GetItemsByItemType(type);
				if (itemsByItemType == null || itemsByItemType.Count == 0)
				{
					feedback($"No items found for type: {type}");
					return;
				}
				if (args.Length == 3)
				{
					feedback($"{type} count: {itemsByItemType.Count}");
					return;
				}
				if (args.Length >= 4)
				{
					if (!int.TryParse(args[3], out var result2))
					{
						feedback("Invalid index. Example: spawn index w 0");
					}
					else if (result2 < 0 || result2 >= itemsByItemType.Count)
					{
						feedback($"Index out of range. Valid range: 0..{itemsByItemType.Count - 1}");
					}
					else
					{
						TrySpawnItem(itemsByItemType[result2], feedback);
					}
					return;
				}
			}
			int result4;
			if (args.Length >= 3 && TryParseTypeLetter(args[1], out var type2))
			{
				if (!int.TryParse(args[2], out var result3))
				{
					feedback("Invalid index. Example: spawn w 3");
					return;
				}
				IReadOnlyList<ItemSO> itemsByItemType2 = itemDatabase.GetItemsByItemType(type2);
				if (itemsByItemType2 == null || itemsByItemType2.Count == 0)
				{
					feedback($"No items found for type: {type2}");
				}
				else if (result3 < 0 || result3 >= itemsByItemType2.Count)
				{
					feedback($"Index out of range. Valid range: 0..{itemsByItemType2.Count - 1}");
				}
				else
				{
					TrySpawnItem(itemsByItemType2[result3], feedback);
				}
			}
			else if (int.TryParse(args[1], out result4))
			{
				List<ItemSO> list = BuildAutoSpawnList(itemDatabase);
				if (list.Count == 0)
				{
					feedback("Auto spawn list is empty.");
				}
				else if (result4 < 0 || result4 >= list.Count)
				{
					feedback($"Index out of range. Valid range: 0..{list.Count - 1}");
				}
				else
				{
					TrySpawnItem(list[result4], feedback);
				}
			}
			else
			{
				string text = args[1].Trim();
				ItemSO itemSO = itemDatabase.FindById(text);
				if (itemSO == null)
				{
					feedback("Item not found by id: " + text);
				}
				else
				{
					TrySpawnItem(itemSO, feedback);
				}
			}
		}

		private bool TryParseTypeLetter(string token, out ItemType type)
		{
			type = ItemType.Empty;
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}
			switch (char.ToLowerInvariant(token.Trim()[0]))
			{
			case 'w':
				type = ItemType.Wholesale;
				return true;
			case 'r':
				type = ItemType.RawIngredient;
				return true;
			case 'p':
				type = ItemType.ProcessedIngredient;
				return true;
			case 's':
				type = ItemType.ServedDish;
				return true;
			default:
				return false;
			}
		}

		private bool IsDeadFishToken(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}
			string text = token.Trim().ToLowerInvariant();
			if (!(text == "df") && !(text == "fish"))
			{
				return text == "deadfish";
			}
			return true;
		}

		private List<ItemSO> BuildAutoSpawnList(ItemDatabaseSO db)
		{
			List<ItemSO> list = new List<ItemSO>();
			list.AddRange(db.GetItemsByItemType(ItemType.Wholesale));
			list.AddRange(db.GetItemsByItemType(ItemType.RawIngredient));
			list.AddRange(db.GetItemsByItemType(ItemType.ProcessedIngredient));
			list.AddRange(db.GetItemsByItemType(ItemType.ServedDish));
			list.AddRange(db.GetItemsByItemType(ItemType.Decoration));
			list.RemoveAll((ItemSO x) => x == null);
			return list;
		}

		private void TrySpawnItem(ItemSO item, Action<string> feedback)
		{
			if (item == null)
			{
				feedback("Resolved item is null.");
				return;
			}
			if (!(item is HoldableSO holdableSO))
			{
				feedback($"Item is not HoldableSO: {item.name} (Type: {item.ItemType})");
				return;
			}
			Singleton<QuickSlotHandler>.Instance.SpawnItem(holdableSO, select: true);
			feedback($"Spawned: {holdableSO.name} (Type: {item.ItemType}, Id: {item.Id})");
		}

		private void TrySpawnDeadFish(DevConsoleContext context, FishSO fish, Action<string> feedback)
		{
			if (fish == null)
			{
				feedback("FishSO is null.");
				return;
			}
			if (fish.DeadPrefab == null)
			{
				feedback("DeadPrefab is missing on fish: " + fish.name);
				return;
			}
			Vector3 position = Vector3.zero;
			_ = Quaternion.identity;
			if (context.SpawnOrigin != null)
			{
				position = context.SpawnOrigin.position + context.SpawnOrigin.forward * 1.5f;
				_ = context.SpawnOrigin.rotation;
			}
			PPG_Player localPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
			Singleton<SeaCreatureManager>.Instance.SpawnDeadFish(fish, position, localPlayer.OwnerClientId, localPlayer.NetworkObject.ToReference(), select: true, canHold: true);
			feedback("Spawned dead fish: " + fish.name + " | Id: " + fish.Id);
		}
	}
}
