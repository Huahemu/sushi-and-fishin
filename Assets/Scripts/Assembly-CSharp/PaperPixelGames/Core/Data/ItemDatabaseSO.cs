using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Interaction;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/Item DatabaseSO")]
	public class ItemDatabaseSO : SerializedScriptableObject
	{
		public List<ItemSO> _actorItems = new List<ItemSO>();

		public List<ItemSO> CreatureItems = new List<ItemSO>();

		public List<ItemSO> _stationItems = new List<ItemSO>();

		public List<ItemSO> _machineItems = new List<ItemSO>();

		public List<ItemSO> _staticContainerItems = new List<ItemSO>();

		public List<ItemSO> _dynamicContainerItems = new List<ItemSO>();

		public List<ItemSO> _toolItems = new List<ItemSO>();

		public List<ItemSO> _plateItems = new List<ItemSO>();

		public List<ItemSO> _trashBinItems = new List<ItemSO>();

		public List<ItemSO> _conveyerBeltItems = new List<ItemSO>();

		public List<ItemSO> _tableItems = new List<ItemSO>();

		public List<ItemSO> _lampSwitchItems = new List<ItemSO>();

		public List<ItemSO> _openSignItems = new List<ItemSO>();

		public List<ItemSO> _doorItems = new List<ItemSO>();

		public List<ItemSO> _garbageItems = new List<ItemSO>();

		public List<ItemSO> _hangiriBowlItems = new List<ItemSO>();

		public List<ItemSO> _counterStandItems = new List<ItemSO>();

		public List<ItemSO> _decorationItems = new List<ItemSO>();

		public List<ItemSO> _wholesalerItems = new List<ItemSO>();

		public List<ItemSO> _rawIngredientItems = new List<ItemSO>();

		public List<ItemSO> _processedIngredientItems = new List<ItemSO>();

		public List<ItemSO> _servedDishItems = new List<ItemSO>();

		[Tooltip("ItemSO’lara hızlı erişim için özel referans alanları")]
		public ItemSO CucumberMakiServeredDishSO;

		public ItemSO MackarelNigiriServeredDishSO;

		public ItemSO ChinookSalmonTemakiServeredDishSO;

		public ItemSO SquidNigiriServeredDishSO;

		public ItemSO CucumberLongStripSO;

		public ItemSO MackeralNigiriSO;

		public ItemSO ProcessedNoriSO;

		public ItemSO CookedRice;

		public ItemSO WholesaleNoriSO;

		public ItemSO JarWasabiSO;

		public ItemSO JarPickledGingerSO;

		public ItemSO JarEdamameSO;

		public ItemSO WholesaleNori;

		public ItemSO WholesaleRice;

		public ItemSO WholesaleCucumber;

		public ItemSO MackerelSO;

		public ItemSO YellowTailSO;

		public ItemSO ChinookSalmonSO;

		public ItemSO BlackSeaBreamSO;

		public ItemSO SquidSO;

		public List<ItemSO> _allItems = new List<ItemSO>();

		private Dictionary<string, ItemSO> _idLookup;

		private Dictionary<ItemType, List<ItemSO>> _typeLookup;

		private void RebuildLookups()
		{
			_idLookup = new Dictionary<string, ItemSO>();
			_typeLookup = new Dictionary<ItemType, List<ItemSO>>();
			foreach (ItemSO allItem in _allItems)
			{
				if (!(allItem == null) && !string.IsNullOrEmpty(allItem.Id))
				{
					_idLookup[allItem.Id] = allItem;
					if (!_typeLookup.ContainsKey(allItem.ItemType))
					{
						_typeLookup[allItem.ItemType] = new List<ItemSO>();
					}
					_typeLookup[allItem.ItemType].Add(allItem);
				}
			}
		}

		public IReadOnlyList<ItemSO> GetAllItems()
		{
			return _allItems;
		}

		public ItemSO FindById(FixedString64Bytes id)
		{
			if (_idLookup == null)
			{
				RebuildLookups();
			}
			if (!_idLookup.TryGetValue(id.ToString(), out var value))
			{
				return null;
			}
			return value;
		}

		public ItemSO FindById(string id)
		{
			if (_idLookup == null)
			{
				RebuildLookups();
			}
			if (!_idLookup.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public ItemSO FindByIdAndType(string id, ItemType type)
		{
			if (_typeLookup == null)
			{
				RebuildLookups();
			}
			if (!_typeLookup.TryGetValue(type, out var value))
			{
				return null;
			}
			return value.FirstOrDefault((ItemSO x) => x.Id == id);
		}

		public IReadOnlyList<ItemSO> GetAllOfType(ItemType type)
		{
			if (_typeLookup == null)
			{
				RebuildLookups();
			}
			if (!_typeLookup.TryGetValue(type, out var value))
			{
				return new List<ItemSO>();
			}
			return value;
		}

		public IReadOnlyList<ItemSO> GetItemsByItemType(ItemType type)
		{
			return type switch
			{
				ItemType.Actor => _actorItems, 
				ItemType.Creature => CreatureItems, 
				ItemType.Station => _stationItems, 
				ItemType.Machine => _machineItems, 
				ItemType.StaticContainer => _staticContainerItems, 
				ItemType.DynamicContainer => _dynamicContainerItems, 
				ItemType.Tool => _toolItems, 
				ItemType.Plate => _plateItems, 
				ItemType.TrashBin => _trashBinItems, 
				ItemType.ConveyerBelt => _conveyerBeltItems, 
				ItemType.Table => _tableItems, 
				ItemType.LampSwitch => _lampSwitchItems, 
				ItemType.OpenSign => _openSignItems, 
				ItemType.Door => _doorItems, 
				ItemType.Garbage => _garbageItems, 
				ItemType.HangiriBowl => _hangiriBowlItems, 
				ItemType.CounterStand => _counterStandItems, 
				ItemType.Decoration => _decorationItems, 
				ItemType.Wholesale => _wholesalerItems, 
				ItemType.RawIngredient => _rawIngredientItems, 
				ItemType.ProcessedIngredient => _processedIngredientItems, 
				ItemType.ServedDish => _servedDishItems, 
				ItemType.Bait => GetAllOfType(ItemType.Bait), 
				_ => new List<ItemSO>(), 
			};
		}
	}
}
