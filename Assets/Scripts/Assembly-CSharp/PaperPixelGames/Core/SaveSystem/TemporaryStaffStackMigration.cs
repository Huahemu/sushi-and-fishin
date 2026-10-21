using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Staff;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	public static class TemporaryStaffStackMigration
	{
		public sealed class Area
		{
			public readonly List<TransformData> Sources = new List<TransformData>();

			public readonly List<TransformData> Slots = new List<TransformData>();
		}

		public sealed class FixedPoint
		{
			public string Kind;

			public string ItemId;

			public TransformData Pose;
		}

		public sealed class Layout
		{
			public readonly List<Area> Prep = new List<Area>();

			public readonly List<Area> Chef = new List<Area>();

			public readonly List<TransformData> LegacyChefPanPoints = new List<TransformData>();

			public readonly List<FixedPoint> Fixed = new List<FixedPoint>();

			public readonly Dictionary<string, int> PanCapacity = new Dictionary<string, int>();

			public readonly HashSet<string> SushiIds = new HashSet<string>();
		}

		public sealed class Result
		{
			public RestaurantData Data;

			public int Moved;

			public int RemovedEmptyPans;

			public int RetiredMergedPans;

			public int UnplacedGroups;

			public override string ToString()
			{
				return $"moved={Moved}, emptyPans={RemovedEmptyPans}, mergedPans={RetiredMergedPans}, unplacedGroups={UnplacedGroups}";
			}
		}

		private sealed class Item
		{
			public string Kind;

			public string Id;

			public TransformData Pose;

			public GastropanData Pan;

			public bool Attached;

			public bool Removed;

			public string Key => Kind + ":" + Id;
		}

		public const bool EnabledForThisBuild = true;

		public const int Revision = 1;

		public const string BackupTag = "before-staff-stack-test-v1";

		private const float ToleranceSquared = 0.0101f;

		public static bool ShouldRun(SaveSlotData slot, bool enabled)
		{
			if (enabled && slot != null)
			{
				return slot.TemporaryStaffStackRevision < 1;
			}
			return false;
		}

		public static Layout CaptureLayout(StaffWorkplaceRegistry world, RestaurantData data)
		{
			if (world == null || world.Database == null)
			{
				throw new InvalidOperationException("Staff migration requires the loaded workplace layout and item database.");
			}
			Layout layout = new Layout();
			foreach (StaffPrepWorkplace prepStation in world.PrepStations)
			{
				Area area = new Area();
				Add(area.Sources, prepStation.PreparedPanSlots);
				Add(area.Sources, prepStation.PreparedPanPoints.Select((StaffStoragePoint p) => p.Point));
				Add(area.Sources, new Transform[1] { prepStation.PanStackPoint });
				if (prepStation.Station != null && prepStation.Station.PhysicalStateController != null && prepStation.Station.PhysicalStateController.IsPhysicallyUsable)
				{
					Add(area.Slots, prepStation.PreparedPanSlots);
				}
				layout.Prep.Add(area);
			}
			foreach (StaffChefWorkplace chefDesk in world.ChefDesks)
			{
				Area area2 = new Area();
				Add(area2.Sources, chefDesk.ReadySushiPoints.Concat(chefDesk.PanOutputs).Concat(new Transform[2] { chefDesk.OutputPoint, chefDesk.PanStackPoint }));
				if (chefDesk.Desk != null && chefDesk.Desk.PhysicalStateController != null && chefDesk.Desk.PhysicalStateController.IsPhysicallyUsable)
				{
					Add(area2.Slots, chefDesk.ReadySushiPoints);
				}
				layout.Chef.Add(area2);
				Add(layout.LegacyChefPanPoints, chefDesk.PanOutputs.Concat(chefDesk.ReadySushiPoints).Concat(new Transform[1] { chefDesk.PanStackPoint }));
			}
			foreach (StaffStoragePoint item in world.Storage.Concat(world.ReadyDishPoints))
			{
				if (!(item.Item == null) && !(item.Point == null))
				{
					layout.Fixed.Add(new FixedPoint
					{
						Kind = ((item.Item is WholesaleSO) ? "wholesale" : "ingredient"),
						ItemId = item.Item.Id,
						Pose = Pose(item.Point)
					});
					if (item.Item is ServedDishSO)
					{
						layout.Fixed.Add(new FixedPoint
						{
							Kind = "dish",
							ItemId = item.Item.Id,
							Pose = Pose(item.Point)
						});
					}
				}
			}
			ProcessedIngredientSO processedIngredientSO = world.PrepRecipes.FirstOrDefault((StaffPrepRecipe r) => r.Kind == StaffPrepKind.Rice)?.Output;
			if (processedIngredientSO != null)
			{
				foreach (Transform item2 in world.PrepStations.Select((StaffPrepWorkplace p) => p.OutputPoint).Concat(from m in world.PrepMachines
					where m.RiceCooker != null
					select m.OutputPoint))
				{
					if (item2 != null)
					{
						layout.Fixed.Add(new FixedPoint
						{
							Kind = "ingredient",
							ItemId = processedIngredientSO.Id,
							Pose = Pose(item2)
						});
					}
				}
			}
			foreach (GastropanData item3 in data.GastropanData ?? new List<GastropanData>())
			{
				if (item3 != null && !string.IsNullOrEmpty(item3.ItemId))
				{
					ItemSO itemSO = world.Database.FindById(item3.ItemId);
					ProcessedIngredientSO processedIngredientSO2 = (itemSO as ProcessedIngredientSO) ?? (itemSO as IngredientSO)?.ProcessedIngredients?.FirstOrDefault();
					if (processedIngredientSO2 != null)
					{
						layout.PanCapacity[item3.ItemId] = processedIngredientSO2.MaxPanCapacity;
					}
				}
			}
			foreach (ServedDishData item4 in data.ServedDishData ?? new List<ServedDishData>())
			{
				if (item4 != null && !string.IsNullOrEmpty(item4.Id) && world.Database.FindById(item4.Id) is SushiSO)
				{
					layout.SushiIds.Add(item4.Id);
				}
			}
			return layout;
		}

		private static TransformData Pose(Transform point)
		{
			return new TransformData
			{
				Position = point.position,
				Rotation = point.eulerAngles
			};
		}

		private static void Add(List<TransformData> target, IEnumerable<Transform> points)
		{
			foreach (Transform point in points)
			{
				if (point != null && !target.Any((TransformData p) => Near(p, Pose(point))))
				{
					target.Add(Pose(point));
				}
			}
		}

		private static bool Near(TransformData a, TransformData b)
		{
			if (a != null && b != null)
			{
				return (a.Position - b.Position).sqrMagnitude <= 0.0101f;
			}
			return false;
		}

		private static bool AtAny(TransformData pose, IEnumerable<TransformData> points)
		{
			return points.Any((TransformData p) => Near(pose, p));
		}

		public static Result Plan(RestaurantData source, Layout layout)
		{
			if (source == null || layout == null)
			{
				throw new ArgumentNullException();
			}
			Result result = new Result
			{
				Data = JsonUtility.FromJson<RestaurantData>(JsonUtility.ToJson(source))
			};
			RestaurantData data = result.Data;
			RestaurantData restaurantData = data;
			if (restaurantData.GastropanData == null)
			{
				restaurantData.GastropanData = new List<GastropanData>();
			}
			restaurantData = data;
			if (restaurantData.IngredientData == null)
			{
				restaurantData.IngredientData = new List<IngredientData>();
			}
			restaurantData = data;
			if (restaurantData.WholesaleData == null)
			{
				restaurantData.WholesaleData = new List<WholesaleData>();
			}
			restaurantData = data;
			if (restaurantData.ServedDishData == null)
			{
				restaurantData.ServedDishData = new List<ServedDishData>();
			}
			restaurantData = data;
			if (restaurantData.DeadFishData == null)
			{
				restaurantData.DeadFishData = new List<DeadFishData>();
			}
			List<Item> items = new List<Item>();
			foreach (GastropanData gastropanDatum in data.GastropanData)
			{
				if (gastropanDatum != null)
				{
					items.Add(new Item
					{
						Kind = "pan",
						Id = gastropanDatum.ItemId,
						Pose = gastropanDatum.TransformData,
						Pan = gastropanDatum,
						Attached = gastropanDatum.IsOnDesk
					});
				}
			}
			foreach (IngredientData ingredientDatum in data.IngredientData)
			{
				if (ingredientDatum != null)
				{
					items.Add(new Item
					{
						Kind = "ingredient",
						Id = ingredientDatum.IngredientId,
						Pose = ingredientDatum.TransformData
					});
				}
			}
			foreach (WholesaleData wholesaleDatum in data.WholesaleData)
			{
				if (wholesaleDatum != null)
				{
					items.Add(new Item
					{
						Kind = "wholesale",
						Id = wholesaleDatum.Id,
						Pose = wholesaleDatum.TransformData
					});
				}
			}
			foreach (ServedDishData servedDishDatum in data.ServedDishData)
			{
				if (servedDishDatum != null)
				{
					items.Add(new Item
					{
						Kind = "dish",
						Id = servedDishDatum.Id,
						Pose = servedDishDatum.TransformData,
						Attached = (servedDishDatum.HasTrayAttachment || servedDishDatum.HasConveyorAttachment)
					});
				}
			}
			foreach (DeadFishData deadFishDatum in data.DeadFishData)
			{
				if (deadFishDatum != null)
				{
					items.Add(new Item
					{
						Kind = "obstacle",
						Pose = deadFishDatum.TransformData
					});
				}
			}
			Dictionary<Item, Vector3> source2 = items.Where((Item i) => i.Pose != null).ToDictionary((Item i) => i, (Item i) => i.Pose.Position);
			TransformData[] prepSources = layout.Prep.SelectMany((Area a) => a.Sources).Concat(layout.LegacyChefPanPoints).ToArray();
			foreach (Item item2 in from i in items.Where(PanCandidate)
				where i.Pan.Amount == 0
				select i)
			{
				item2.Removed = true;
				result.RemovedEmptyPans++;
			}
			foreach (Area area in layout.Prep)
			{
				foreach (IGrouping<string, Item> item3 in from i in items.Where((Item i) => !i.Removed && PanCandidate(i) && AtAny(i.Pose, area.Sources)).ToArray()
					group i by i.Id)
				{
					PlacePans(item3.ToList(), area.Slots, items, layout, result);
				}
			}
			foreach (IGrouping<string, Item> item4 in from i in items.Where((Item i) => !i.Removed && PanCandidate(i) && AtAny(i.Pose, layout.LegacyChefPanPoints) && !AtAny(i.Pose, layout.Prep.SelectMany((Area a) => a.Slots))).ToArray()
				group i by i.Id)
			{
				Vector3 origin = item4.First().Pose.Position;
				List<TransformData> slots = (from a in layout.Prep
					where a.Slots.Count > 0
					orderby a.Slots.Min((TransformData p) => (p.Position - origin).sqrMagnitude)
					select a).SelectMany((Area a) => a.Slots).ToList();
				PlacePans(item4.ToList(), slots, items, layout, result);
			}
			foreach (Area area2 in layout.Chef)
			{
				foreach (IGrouping<string, Item> item5 in from i in items.Where((Item i) => !i.Removed && i.Kind == "dish" && !i.Attached && layout.SushiIds.Contains(i.Id ?? "") && AtAny(i.Pose, area2.Sources)).ToArray()
					group i by i.Id)
				{
					List<Item> list = item5.ToList();
					TransformData transformData = Choose(area2.Slots, list[0].Key, items);
					if (transformData == null)
					{
						result.UnplacedGroups++;
						continue;
					}
					foreach (Item item6 in list)
					{
						Move(item6, transformData);
					}
				}
			}
			foreach (Item item in items.Where((Item i) => !i.Removed && !i.Attached && i.Kind != "pan"))
			{
				FixedPoint fixedPoint = layout.Fixed.FirstOrDefault((FixedPoint p) => p.Kind == item.Kind && p.ItemId == item.Id && Near(item.Pose, p.Pose));
				if (fixedPoint != null)
				{
					Move(item, fixedPoint.Pose);
				}
			}
			data.GastropanData.RemoveAll((GastropanData p) => items.Any((Item i) => i.Pan == p && i.Removed));
			result.Moved = source2.Count((KeyValuePair<Item, Vector3> p) => !p.Key.Removed && (p.Key.Pose.Position - p.Value).sqrMagnitude > 1E-06f);
			Dictionary<string, long> dictionary = Inventory(source);
			Dictionary<string, long> after = Inventory(data);
			if (dictionary.Count != after.Count || dictionary.Any((KeyValuePair<string, long> p) => !after.TryGetValue(p.Key, out var value) || value != p.Value))
			{
				throw new InvalidOperationException("Staff test migration failed inventory conservation; source save was not changed.");
			}
			return result;
			bool PanCandidate(Item item2)
			{
				if (item2.Kind == "pan" && !item2.Attached)
				{
					return AtAny(item2.Pose, prepSources);
				}
				return false;
			}
		}

		private static void Move(Item item, TransformData target)
		{
			item.Pose.Position = target.Position;
			item.Pose.Rotation = target.Rotation;
		}

		private static TransformData Choose(List<TransformData> slots, string key, List<Item> items)
		{
			(TransformData, Item[])[] source = slots.Select((TransformData p) => (pose: p, occupants: items.Where((Item i) => !i.Removed && Near(i.Pose, p)).ToArray())).ToArray();
			return source.FirstOrDefault(((TransformData pose, Item[] occupants) p) => p.occupants.Length != 0 && p.occupants.All((Item i) => !i.Attached && i.Key == key)).Item1 ?? source.FirstOrDefault(((TransformData pose, Item[] occupants) p) => p.occupants.Length == 0).Item1;
		}

		private static void PlacePans(List<Item> group, List<TransformData> slots, List<Item> items, Layout layout, Result result)
		{
			if (group.Count == 0)
			{
				return;
			}
			string id = group[0].Id;
			if (string.IsNullOrEmpty(id) || !layout.PanCapacity.TryGetValue(id, out var value) || value <= 0 || value > 255)
			{
				result.UnplacedGroups++;
				return;
			}
			TransformData target = Choose(slots, group[0].Key, items);
			if (target == null)
			{
				result.UnplacedGroups++;
				return;
			}
			List<Item> list = items.Where((Item i) => !i.Removed && !i.Attached && i.Pan != null && i.Id == id && Near(i.Pose, target)).Concat(group).Distinct()
				.ToList();
			for (int num = 0; num < list.Count; num++)
			{
				for (int num2 = num + 1; num2 < list.Count; num2++)
				{
					if (list[num].Pan.Amount >= value)
					{
						break;
					}
					int num3 = Math.Min(value - list[num].Pan.Amount, list[num2].Pan.Amount);
					list[num].Pan.Amount += (byte)num3;
					list[num2].Pan.Amount -= (byte)num3;
				}
			}
			foreach (Item item in list)
			{
				if (item.Pan.Amount == 0)
				{
					item.Removed = true;
					result.RetiredMergedPans++;
				}
				else
				{
					Move(item, target);
				}
			}
		}

		private static Dictionary<string, long> Inventory(RestaurantData data)
		{
			Dictionary<string, long> counts = new Dictionary<string, long>();
			foreach (GastropanData item in data.GastropanData ?? new List<GastropanData>())
			{
				if (item != null)
				{
					Add("pan:" + item.ItemId, item.Amount);
				}
			}
			foreach (IngredientData item2 in data.IngredientData ?? new List<IngredientData>())
			{
				if (item2 != null)
				{
					Add("ingredient:" + item2.IngredientId, item2.Quantity);
					Add("ingredient-container:" + item2.IngredientId, 1L);
				}
			}
			foreach (WholesaleData item3 in data.WholesaleData ?? new List<WholesaleData>())
			{
				if (item3 != null)
				{
					Add("wholesale:" + item3.Id, item3.Quantity);
					Add("box:" + item3.Id, 1L);
				}
			}
			foreach (ServedDishData item4 in data.ServedDishData ?? new List<ServedDishData>())
			{
				if (item4 != null)
				{
					Add("dish:" + item4.Id, 1L);
				}
			}
			return counts;
			void Add(string key, long count)
			{
				if (count != 0L)
				{
					counts.TryGetValue(key, out var value);
					counts[key] = value + count;
				}
			}
		}
	}
}
