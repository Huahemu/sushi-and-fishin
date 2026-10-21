using Bozo.ModularCharacters;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs
{
	[CreateAssetMenu(fileName = "ResidentData", menuName = "Data/NPC/ResidentSO")]
	public class ResidentDataSO : ScriptableObject
	{
		public const int BakedCustomizationVersion = 2;

		public int NpcID;

		public string Name;

		[Tooltip("Selects gender-specific resident dialogue and restaurant voice banks.")]
		public ResidentVoiceGender VoiceGender;

		public float MoveSpeed;

		public ResidentScheduleSO Schedule;

		public DataObject CharacterData;

		[Header("Baked Customization")]
		[Tooltip("Editor-generated visual prefab. CharacterData remains the source of truth.")]
		public GameObject BakedVisualPrefab;

		[HideInInspector]
		public string BakedCustomizationHash;

		public bool HasUsableBakedVisual
		{
			get
			{
				if (BakedVisualPrefab == null)
				{
					return false;
				}
				return true;
			}
		}
	}
}
