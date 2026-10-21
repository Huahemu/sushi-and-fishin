using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffOutputPlacement
	{
		private readonly Dictionary<BaseHoldable, Transform> incoming = new Dictionary<BaseHoldable, Transform>();

		private readonly Dictionary<Transform, Dictionary<int, string>> claims = new Dictionary<Transform, Dictionary<int, string>>();

		private const float PointToleranceSquared = 0.01f;

		private static readonly ProfilerMarker OccupantsMarker = new ProfilerMarker("Staff.Output.Occupants");

		public void Track(BaseHoldable item, Transform destination)
		{
			incoming[item] = destination;
		}

		public Dictionary<Transform, List<BaseHoldable>> Occupants(IEnumerable<Transform> slots, BaseHoldable ignore = null)
		{
			using (OccupantsMarker.Auto())
			{
				Dictionary<Transform, List<BaseHoldable>> dictionary = slots.Where((Transform p) => p != null).Distinct().ToDictionary((Transform p) => p, (Transform _) => new List<BaseHoldable>());
				KeyValuePair<BaseHoldable, Transform>[] array = incoming.ToArray();
				for (int num = 0; num < array.Length; num++)
				{
					KeyValuePair<BaseHoldable, Transform> keyValuePair = array[num];
					if (!Physical(keyValuePair.Key) || keyValuePair.Value == null || keyValuePair.Key.CanHold.Value)
					{
						incoming.Remove(keyValuePair.Key);
					}
				}
				foreach (BaseHoldable item in BaseHoldable.StaffWorldItems.Where(Physical))
				{
					if (item == ignore)
					{
						continue;
					}
					if (incoming.TryGetValue(item, out var value))
					{
						if (dictionary.TryGetValue(value, out var value2))
						{
							value2.Add(item);
						}
						continue;
					}
					Transform transform = null;
					float num2 = float.PositiveInfinity;
					Vector3 position = item.transform.position;
					foreach (Transform key in dictionary.Keys)
					{
						float sqrMagnitude = (key.position - position).sqrMagnitude;
						if (!(sqrMagnitude >= num2))
						{
							transform = key;
							num2 = sqrMagnitude;
						}
					}
					if (transform != null && num2 <= 0.01f)
					{
						dictionary[transform].Add(item);
					}
				}
				return dictionary;
			}
		}

		private static bool Physical(BaseHoldable item)
		{
			if (item != null && item.IsServer && item.IsSpawned && !item.IsStaffCarried && !item.IsPlayerInventoryOwnedForPersistence)
			{
				return item.transform.parent == null;
			}
			return false;
		}

		private static bool Same(BaseHoldable item, string type)
		{
			if (!(item is GastroPan { IngredientId: var ingredientId }))
			{
				if (!(item is ServedDish servedDish))
				{
					return item.ItemSO?.Id == type;
				}
				return servedDish.RuntimeRecipeIdentityId == type;
			}
			return ingredientId.ToString() == type;
		}

		public bool TryReserve(IEnumerable<Transform> slots, string type, int owner, out Transform point)
		{
			point = Choose(slots, type);
			if (point == null)
			{
				return false;
			}
			if (!claims.TryGetValue(point, out var value))
			{
				value = (claims[point] = new Dictionary<int, string>());
			}
			value[owner] = type;
			return true;
		}

		public void Release(int owner)
		{
			Transform[] array = claims.Keys.ToArray();
			foreach (Transform key in array)
			{
				claims[key].Remove(owner);
				if (claims[key].Count == 0)
				{
					claims.Remove(key);
				}
			}
		}

		public Transform Choose(IEnumerable<Transform> slots, string type, BaseHoldable ignore = null)
		{
			Dictionary<Transform, List<BaseHoldable>> source = Occupants(slots, ignore);
			return source.FirstOrDefault((KeyValuePair<Transform, List<BaseHoldable>> p) => Compatible(p.Key) && p.Value.Count > 0 && p.Value.All((BaseHoldable i) => Same(i, type))).Key ?? source.FirstOrDefault((KeyValuePair<Transform, List<BaseHoldable>> p) => Compatible(p.Key) && p.Value.Count == 0 && claims.ContainsKey(p.Key)).Key ?? source.FirstOrDefault((KeyValuePair<Transform, List<BaseHoldable>> p) => Compatible(p.Key) && p.Value.Count == 0).Key;
			bool Compatible(Transform point)
			{
				if (claims.TryGetValue(point, out var value))
				{
					return value.Values.All((string t) => t == type);
				}
				return true;
			}
		}

		public bool CanPlace(IEnumerable<Transform> slots, Transform point, string type)
		{
			if (point == null || (claims.TryGetValue(point, out var value) && value.Values.Any((string t) => t != type)))
			{
				return false;
			}
			if (Occupants(slots).TryGetValue(point, out var value2))
			{
				return value2.All((BaseHoldable item) => Same(item, type));
			}
			return false;
		}

		public Transform PreparePan(IEnumerable<Transform> slots, GastroPan pan, StaffReservationService reservations, int employee)
		{
			MergePan(slots, pan, reservations, employee);
			if (!(pan != null) || !pan.IsSpawned)
			{
				return null;
			}
			return Choose(slots, pan.IngredientId.ToString(), pan);
		}

		public void MergePan(IEnumerable<Transform> slots, GastroPan pan, StaffReservationService reservations, int employee, ISet<BaseHoldable> excluded = null)
		{
			if (pan == null || !pan.IsSpawned)
			{
				return;
			}
			foreach (GastroPan item in Occupants(slots, pan).Values.SelectMany((List<BaseHoldable> v) => v).OfType<GastroPan>())
			{
				if ((excluded == null || !excluded.Contains(item)) && !reservations.IsReservedByOther("item:" + item.NetworkObjectId, employee))
				{
					pan.ServerStaffMergeInto(item);
					if (pan == null || !pan.IsSpawned)
					{
						break;
					}
				}
			}
		}
	}
}
