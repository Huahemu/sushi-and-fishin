using System;
using System.Collections.Generic;

namespace Bozo.ModularCharacters
{
	[Serializable]
	public class CharacterData
	{
		public int versionID;

		public string characterName;

		public List<string> bodyIDs;

		public List<float> bodyShapes;

		public List<string> faceIDs;

		public List<float> faceShapes;

		public List<string> bodyModsKeys;

		public List<BodyModData> bodyMods;

		public List<OutfitData> outfitDatas;

		public OutfitData bodyData;

		public float stance;

		public string material;

		public CharacterData()
		{
			versionID = 0;
		}

		public CharacterData(CharacterData copyData)
		{
			versionID = copyData.versionID;
			characterName = copyData.characterName;
			bodyIDs = new List<string>(copyData.bodyIDs);
			bodyShapes = new List<float>(copyData.bodyShapes);
			faceIDs = new List<string>(copyData.faceIDs);
			faceShapes = new List<float>(copyData.faceShapes);
			bodyModsKeys = new List<string>(copyData.bodyModsKeys);
			bodyMods = new List<BodyModData>(copyData.bodyMods);
			outfitDatas = new List<OutfitData>(copyData.outfitDatas);
			bodyData = copyData.bodyData;
			stance = copyData.stance;
			material = copyData.material;
		}
	}
}
