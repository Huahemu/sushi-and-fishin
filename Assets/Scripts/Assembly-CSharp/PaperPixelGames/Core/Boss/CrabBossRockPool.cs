using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	public sealed class CrabBossRockPool : MonoBehaviour
	{
		public const int RequiredSlotCount = 10;

		[Header("Local Rock Slots")]
		[SerializeField]
		private CrabBossLocalRock[] _projectileSlots = new CrabBossLocalRock[10];

		[SerializeField]
		private CrabBossLocalRock[] _fallingRockSlots = new CrabBossLocalRock[10];

		private readonly List<Transform> _detachedSlotContainers = new List<Transform>();

		public int ProjectileSlotCount
		{
			get
			{
				CrabBossLocalRock[] projectileSlots = _projectileSlots;
				if (projectileSlots == null)
				{
					return 0;
				}
				return projectileSlots.Length;
			}
		}

		public int FallingRockSlotCount
		{
			get
			{
				CrabBossLocalRock[] fallingRockSlots = _fallingRockSlots;
				if (fallingRockSlots == null)
				{
					return 0;
				}
				return fallingRockSlots.Length;
			}
		}

		private void Awake()
		{
			DetachSlotContainersFromRotatingBoss();
			ResetAll();
		}

		private void OnDestroy()
		{
			for (int i = 0; i < _detachedSlotContainers.Count; i++)
			{
				Transform transform = _detachedSlotContainers[i];
				if (transform != null && Application.isPlaying)
				{
					Object.Destroy(transform.gameObject);
				}
			}
			_detachedSlotContainers.Clear();
		}

		public CrabBossLocalRock GetProjectileSlot(int index)
		{
			if (index < 0 || index >= ProjectileSlotCount)
			{
				return null;
			}
			return _projectileSlots[index];
		}

		public CrabBossLocalRock GetFallingRockSlot(int index)
		{
			if (index < 0 || index >= FallingRockSlotCount)
			{
				return null;
			}
			return _fallingRockSlots[index];
		}

		public void ResetProjectiles()
		{
			ResetSlots(_projectileSlots);
		}

		public void ResetFallingRocks()
		{
			ResetSlots(_fallingRockSlots);
		}

		public void ResetAll()
		{
			ResetProjectiles();
			ResetFallingRocks();
		}

		private void DetachSlotContainersFromRotatingBoss()
		{
			Transform parent = base.transform.parent;
			if (!(parent == null))
			{
				Transform parent2 = parent.parent;
				CollectAndDetachSlotContainers(_projectileSlots, parent, parent2);
				CollectAndDetachSlotContainers(_fallingRockSlots, parent, parent2);
			}
		}

		private void CollectAndDetachSlotContainers(CrabBossLocalRock[] slots, Transform bossRoot, Transform stableParent)
		{
			if (slots == null)
			{
				return;
			}
			foreach (CrabBossLocalRock crabBossLocalRock in slots)
			{
				if (!(crabBossLocalRock == null))
				{
					Transform parent = crabBossLocalRock.transform;
					while (parent.parent != null && parent.parent != bossRoot)
					{
						parent = parent.parent;
					}
					if (!(parent.parent != bossRoot) && !_detachedSlotContainers.Contains(parent))
					{
						parent.SetParent(stableParent, worldPositionStays: true);
						_detachedSlotContainers.Add(parent);
					}
				}
			}
		}

		public bool ValidateConfiguration(out string error)
		{
			if (!ValidateProjectileConfiguration(out error))
			{
				return false;
			}
			return ValidateFallingRockConfiguration(out error);
		}

		public bool ValidateProjectileConfiguration(out string error)
		{
			return ValidateSlots(_projectileSlots, "Projectile", out error);
		}

		public bool ValidateFallingRockConfiguration(out string error)
		{
			return ValidateSlots(_fallingRockSlots, "Falling rock", out error);
		}

		private static void ResetSlots<TSlot>(TSlot[] slots) where TSlot : CrabBossLocalRockSlot
		{
			if (slots != null)
			{
				for (int i = 0; i < slots.Length; i++)
				{
					slots[i]?.ResetSlot();
				}
			}
		}

		private static bool ValidateSlots<TSlot>(TSlot[] slots, string displayName, out string error) where TSlot : CrabBossLocalRockSlot
		{
			if (slots == null || slots.Length != 10)
			{
				error = $"{displayName} pool must contain exactly {10} slots.";
				return false;
			}
			for (int i = 0; i < slots.Length; i++)
			{
				TSlot val = slots[i];
				if (val == null)
				{
					error = $"{displayName} pool slot {i} is missing.";
					return false;
				}
				for (int j = i + 1; j < slots.Length; j++)
				{
					if (val == slots[j])
					{
						error = $"{displayName} pool uses the same object in slots {i} and {j}.";
						return false;
					}
				}
				if (!val.ValidatePresentation(out error))
				{
					return false;
				}
				if (val.GetComponentInChildren<NetworkObject>(includeInactive: true) != null)
				{
					error = $"{displayName} pool slot {i} must not contain a NetworkObject.";
					return false;
				}
			}
			error = null;
			return true;
		}
	}
}
