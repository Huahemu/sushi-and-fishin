using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs
{
	[CreateAssetMenu(fileName = "HouseData", menuName = "Data/NPC/HouseDataSO")]
	public class HouseDataSO : ScriptableObject
	{
		[Serializable]
		public struct Resident
		{
			public ResidentDataSO ResidentData;

			public ResidentController ResidentPrefab;

			public int homeId;
		}

		public List<Resident> Residents;

		public Vector3 ExitPoint;

		public Quaternion ExitRotation;
	}
}
