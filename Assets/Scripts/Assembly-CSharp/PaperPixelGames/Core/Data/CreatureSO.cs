using PaperPixelGames.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Creature", menuName = "Data/CreatureSO")]
	public class CreatureSO : HoldableSO
	{
		public NetworkObject DeadPrefab;

		public GameObject DeadGraphic;

		public CreatureType Type;

		public CreatureRarity Rarity;

		public CreatureSize Size;

		public OceanLayer ZoneLayer;

		public IngredientSO IngredientSO;

		public int MinYieldRange = 1;

		public int MaxYieldRange = 1;

		[Header("AgentConfig")]
		public float SensorRadius = 1f;

		public Vector3 SensorCenter;

		public float MoveSpeed = 0.1f;

		public float PanicSpeed = 0.5f;
	}
}
