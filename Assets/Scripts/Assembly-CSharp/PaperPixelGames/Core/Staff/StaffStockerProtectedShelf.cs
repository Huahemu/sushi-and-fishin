using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[DisallowMultipleComponent]
	public sealed class StaffStockerProtectedShelf : MonoBehaviour
	{
		public enum ShelfKind
		{
			KitchenShelf = 0,
			StationStorage = 1
		}

		[SerializeField]
		private ShelfKind _kind;

		[SerializeField]
		private BoxCollider[] _placementAreas = Array.Empty<BoxCollider>();

		private static readonly HashSet<StaffStockerProtectedShelf> ActiveShelves = new HashSet<StaffStockerProtectedShelf>();

		public ShelfKind Kind => _kind;

		private void OnEnable()
		{
			ActiveShelves.Add(this);
		}

		private void OnDisable()
		{
			ActiveShelves.Remove(this);
		}

		public bool Contains(Vector3 position)
		{
			if (!base.isActiveAndEnabled)
			{
				return false;
			}
			BoxCollider[] placementAreas = _placementAreas;
			foreach (BoxCollider boxCollider in placementAreas)
			{
				if (!(boxCollider == null) && boxCollider.enabled && boxCollider.gameObject.activeInHierarchy)
				{
					Bounds bounds = new Bounds(boxCollider.center, boxCollider.size);
					bounds.Expand(0.02f);
					if (bounds.Contains(boxCollider.transform.InverseTransformPoint(position)))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool ContainsItem(BaseHoldable item, ShelfKind kind = ShelfKind.KitchenShelf)
		{
			if (item == null || item.ItemSO == null)
			{
				return false;
			}
			Vector3 position = item.transform.position;
			if (kind == ShelfKind.StationStorage)
			{
				position -= item.ItemSO.PlaceOffset.PositionOffset;
			}
			return ContainsItemPosition(position, kind);
		}

		public static bool ContainsItemPosition(Vector3 position, ShelfKind kind = ShelfKind.KitchenShelf)
		{
			foreach (StaffStockerProtectedShelf activeShelf in ActiveShelves)
			{
				if (activeShelf != null && activeShelf.Kind == kind && activeShelf.Contains(position))
				{
					return true;
				}
			}
			return false;
		}
	}
}
