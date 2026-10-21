using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[CreateAssetMenu(fileName = "StaffCatalog", menuName = "Data/Staff/Staff Catalog")]
	public sealed class StaffCatalogSO : ScriptableObject
	{
		public StaffNpc NpcPrefab;

		[HideInInspector]
		public StaffCustomizationOption PrepCookAppearance;

		[HideInInspector]
		public List<StaffCustomizationOption> ArchivedCustomizations = new List<StaffCustomizationOption>();

		public float StockerMoveSpeed = 2.5f;

		[Min(0.1f)]
		public float WaitressMoveSpeed = 2.5f;

		[Tooltip("Stocker and Waitress speed multipliers for skill levels 1 through 5.")]
		public float[] CourierSpeedMultipliers = new float[5] { 1f, 1.2f, 1.6f, 2f, 2.4f };

		[Range(2f, 5f)]
		public int CourierRunSkillLevel = 3;

		[Min(90f)]
		public float StaffTurnSpeed = 1080f;

		[Min(1f)]
		public float StaffAcceleration = 30f;

		[Range(0.1f, 0.65f)]
		public float StaffArrivalRadius = 0.55f;

		[Range(0.03f, 0.2f)]
		public float PrepWorkArrivalRadius = 0.08f;

		[Range(30f, 120f)]
		public float StaffTurnInPlaceAngle = 75f;

		[Min(1f)]
		public float ChefEmptyConveyorCheckSeconds = 10f;

		[Min(0.1f)]
		public float WaitressCleanSeconds = 2f;

		[Min(0.1f)]
		public float WaitressOrderSeconds = 1f;

		[Min(0.1f)]
		public float WaitressScanMinSeconds = 1f;

		[Min(0.1f)]
		public float WaitressScanMaxSeconds = 2f;

		[Min(0.1f)]
		public float PrepMoveSpeed = 2.5f;

		[Min(0.1f)]
		public float ChefMoveSpeed = 2.5f;

		[Min(0.1f)]
		public float ChefProductionSeconds = 8f;

		[Min(0f)]
		public int ChefReadyPlateTarget = 4;

		public bool ChefReadyTargetPerRecipe = true;

		[Min(0f)]
		public int PrepReadyRiceCups = 10;

		[Min(0f)]
		public int PrepReadyPanStacks = 1;

		[Min(0.1f)]
		public float ChefPanSwapSeconds = 0.4f;

		[Min(0.1f)]
		public float PrepCutSeconds = 6f;

		[Min(0.1f)]
		public float PrepRiceSeconds = 7f;

		[Min(0.1f)]
		public float PrepFilletSeconds = 5f;

		public float TaskPollInterval = 0.5f;

		[Min(0.1f)]
		public float RecoveryTimeout = 10f;

		[Min(0.1f)]
		public float PathRetryInterval = 1f;

		[SerializeField]
		private List<string> _maleNames = new List<string>();

		[SerializeField]
		private List<string> _femaleNames = new List<string>();

		[SerializeField]
		private List<StaffRoleDefinition> _roleDefinitions = new List<StaffRoleDefinition>();

		public IReadOnlyList<string> MaleNames => _maleNames;

		public IReadOnlyList<string> FemaleNames => _femaleNames;

		public IReadOnlyList<StaffRoleDefinition> RoleDefinitions => _roleDefinitions;

		public bool CourierRuns(StaffEmployeeEntry employee)
		{
			if (employee.Role == StaffRole.Stocker || employee.Role == StaffRole.Waitress)
			{
				return Mathf.Clamp(employee.SkillLevel, 1, 5) >= Mathf.Clamp(CourierRunSkillLevel, 2, 5);
			}
			return false;
		}

		public float CourierMoveSpeed(StaffEmployeeEntry employee)
		{
			int num = Mathf.Clamp(employee.SkillLevel, 1, 5) - 1;
			float b = ((CourierSpeedMultipliers != null && num < CourierSpeedMultipliers.Length) ? CourierSpeedMultipliers[num] : 1f);
			return Mathf.Max(0.1f, ((employee.Role == StaffRole.Waitress) ? WaitressMoveSpeed : StockerMoveSpeed) * Mathf.Max(0.1f, b));
		}

		public StaffCustomizationOption ResolveCharacter(StaffRole role, int id)
		{
			StaffCustomizationOption staffCustomizationOption = GetDefinition(role).Customizations.Find((StaffCustomizationOption c) => c != null && c.ResolveId() == id);
			if (staffCustomizationOption != null)
			{
				return staffCustomizationOption;
			}
			if (role == StaffRole.PrepCook && PrepCookAppearance?.ResidentData != null)
			{
				return PrepCookAppearance;
			}
			foreach (StaffRoleDefinition roleDefinition in _roleDefinitions)
			{
				staffCustomizationOption = roleDefinition?.Customizations.Find((StaffCustomizationOption c) => c != null && c.ResolveId() == id);
				if (staffCustomizationOption != null)
				{
					return staffCustomizationOption;
				}
			}
			return ArchivedCustomizations.Find((StaffCustomizationOption c) => c != null && c.ResolveId() == id);
		}

		public StaffRoleDefinition GetDefinition(StaffRole role)
		{
			for (int i = 0; i < _roleDefinitions.Count; i++)
			{
				StaffRoleDefinition staffRoleDefinition = _roleDefinitions[i];
				if (staffRoleDefinition != null && staffRoleDefinition.Role == role)
				{
					return staffRoleDefinition;
				}
			}
			return CreateFallbackDefinition(role);
		}

		public static StaffRoleDefinition CreateFallbackDefinition(StaffRole role)
		{
			return role switch
			{
				StaffRole.SushiChef => Create(role, "Sushi Chef", "Work Speed", "Skill", 90, 70, 14), 
				StaffRole.PrepCook => Create(role, "Prep Cook", "Work Speed", "Skill", 72, 60, 12), 
				StaffRole.Waitress => Create(role, "Waitress", "Move Speed", "Capacity", 63, 50, 10), 
				StaffRole.Stocker => Create(role, "Stocker", "Move Speed", "Carry Capacity", 54, 50, 10), 
				_ => Create(role, role.ToString(), "Primary", "Secondary", 54, 50, 10), 
			};
		}

		private static StaffRoleDefinition Create(StaffRole role, string displayName, string primaryLabel, string secondaryLabel, int baseWage, int primaryWageRange, int secondaryWagePerPoint)
		{
			return new StaffRoleDefinition
			{
				Role = role,
				UnlockLevel = StaffRules.DefaultUnlockLevel(role),
				DisplayName = displayName,
				PrimaryStatLabel = primaryLabel,
				SecondaryStatLabel = secondaryLabel,
				BaseDailyWage = baseWage,
				PrimaryStatWageRange = primaryWageRange,
				SecondaryStatWagePerPoint = secondaryWagePerPoint
			};
		}
	}
}
