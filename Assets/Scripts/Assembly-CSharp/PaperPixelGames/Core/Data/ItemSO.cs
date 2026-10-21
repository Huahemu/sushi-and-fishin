using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewItem", menuName = "Data/ItemSO")]
	public class ItemSO : ScriptableObject
	{
		public bool Active;

		public string Id;

		public ItemType ItemType;

		public string Name;

		public Sprite Sprite;

		public NetworkObject Prefab;

		public string LocalizedName => LocalizationDatabase.GetString("ItemsTable", "item." + Id + ".name", Name);
	}
}
