using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	[CreateAssetMenu(fileName = "BSMC_CharacterObject", menuName = "BSMC_CharacterObject")]
	public class BSMC_CharacterObject : DataObject
	{
		[Serializable]
		private class OufitParam
		{
			public OutfitType type;

			public GameObject outfit;

			public Color[] colors = new Color[5];
		}

		[SerializeField]
		private List<OufitParam> outfits = new List<OufitParam>();

		public List<Color> SkinColor = new List<Color>();

		public List<Color> EyeColor = new List<Color>();

		public Texture skinAccessory;

		[Header("BodyShapes")]
		public float Gender;

		public float ChestSize;

		public float FaceShape;

		public float height;

		public float headSize;

		public float shoulderWidth;

		[Header("FaceShapes")]
		public float LashLength;

		public float BrowSize;

		public float EarTipLength;

		[Space]
		public Vector3 EyeSocketPosition;

		public float EyeSocketRotation;

		public Vector3 EyeSocketScale = Vector3.one;

		public float EyeUp;

		public float EyeDown;

		public float EyeSquare;

		[Space]
		public float NoseWidth;

		public float NoseUp;

		public float NoseDown;

		public float NoseBridgeAngle;

		[Space]
		public float MouthWide;

		public float MouthThin;

		[Space]
		public float pupilSize;

		public float irisSize;

		public float outerIrisColorSharpness;

		public float innerIrisColorShapness;

		public Vector2 innerIrisColorOffset;

		public override CharacterData GetCharacterData()
		{
			return UpdateVersion();
		}

		public void SaveCharacter(OutfitSystem outfitSystem)
		{
			Debug.Log("Legacy: Saving this way no longer works this way please use the new system");
		}

		public void LoadCharacter(Transform parent)
		{
			Debug.LogWarning("LoadCharacter is deperciated. Pass GetCharacterData() into Bozo_SaveSystem instead");
		}

		[ContextMenu("Update To Current Version")]
		public CharacterData UpdateVersion()
		{
			CharacterObject characterObject = ScriptableObject.CreateInstance<CharacterObject>();
			characterObject.data = new CharacterData();
			characterObject.data.outfitDatas = new List<OutfitData>();
			characterObject.data.characterName = base.name;
			foreach (OufitParam outfit in outfits)
			{
				if (!(outfit.outfit == null))
				{
					OutfitData outfitData = default;
					Outfit component = outfit.outfit.GetComponent<Outfit>();
					if (!(component.Type == null))
					{
						outfitData.outfit = component.Type.name + "/" + outfit.outfit.name;
						outfitData.colors = outfit.colors.ToList();
						outfitData.decal = "";
						outfitData.decalColors = new List<Color>(3);
						outfitData.pattern = "";
						outfitData.patternColors = new List<Color>(3);
						Debug.Log(characterObject);
						Debug.Log(outfitData);
						characterObject.data.outfitDatas.Add(outfitData);
					}
				}
			}
			OutfitData item = new OutfitData
			{
				outfit = "Head/Head_BasicHead",
				colors = SkinColor
			};
			characterObject.data.outfitDatas.Add(item);
			item.decal = "";
			item.decalColors = new List<Color>(3);
			item.pattern = "";
			item.patternColors = new List<Color>(3);
			OutfitData item2 = new OutfitData
			{
				outfit = "Body/Body_BasicBody",
				colors = SkinColor
			};
			characterObject.data.outfitDatas.Add(item2);
			item2.decal = "";
			item2.decalColors = new List<Color>(3);
			item2.pattern = "";
			item2.patternColors = new List<Color>(3);
			OutfitData item3 = new OutfitData
			{
				outfit = "Eyes/Eyes_BasicEyes",
				decal = "Decal/Decal_BasicPupil",
				pattern = "Pattern/Pattern_BasicIris",
				decalScale = new Vector4(1f, 1f, 0f, 0f),
				patternScale = new Vector4(1f, 1f, 0f, 0f),
				colors = Enumerable.Repeat(EyeColor[3], 9).ToList(),
				patternColors = new List<Color>
				{
					EyeColor[2],
					EyeColor[1],
					EyeColor[0]
				},
				decalColors = new List<Color>
				{
					EyeColor[2],
					EyeColor[1],
					EyeColor[0]
				}
			};
			characterObject.data.bodyIDs = new List<string>();
			characterObject.data.bodyIDs.Add("BodyType");
			characterObject.data.bodyIDs.Add("Chest");
			characterObject.data.bodyIDs.Add("Weight");
			characterObject.data.bodyShapes = new List<float>();
			characterObject.data.bodyShapes.Add(Gender);
			characterObject.data.bodyShapes.Add(ChestSize);
			characterObject.data.bodyShapes.Add(0f);
			characterObject.data.faceIDs = new List<string>();
			characterObject.data.faceIDs.Add("BodyType");
			characterObject.data.faceIDs.Add("Squareness");
			characterObject.data.faceIDs.Add("LashLength");
			characterObject.data.faceIDs.Add("BrowThickness");
			characterObject.data.faceIDs.Add("NoseBridgeCurve");
			characterObject.data.faceIDs.Add("NoseWidth");
			characterObject.data.faceIDs.Add("NoseTiltDown");
			characterObject.data.faceIDs.Add("NoseTiltUp");
			characterObject.data.faceIDs.Add("MouthWide");
			characterObject.data.faceIDs.Add("MouthThin");
			characterObject.data.faceIDs.Add("EyesOuterCornersLow");
			characterObject.data.faceIDs.Add("EyesOuterCornersHigh");
			characterObject.data.faceIDs.Add("EyesSquare");
			characterObject.data.faceIDs.Add("EarsElf");
			characterObject.data.faceShapes = new List<float>();
			characterObject.data.faceShapes.Add(Gender);
			characterObject.data.faceShapes.Add(FaceShape);
			characterObject.data.faceShapes.Add(LashLength);
			characterObject.data.faceShapes.Add(BrowSize);
			characterObject.data.faceShapes.Add(NoseBridgeAngle);
			characterObject.data.faceShapes.Add(NoseWidth);
			characterObject.data.faceShapes.Add(NoseDown);
			characterObject.data.faceShapes.Add(NoseUp);
			characterObject.data.faceShapes.Add(MouthWide);
			characterObject.data.faceShapes.Add(MouthThin);
			characterObject.data.faceShapes.Add(EyeDown);
			characterObject.data.faceShapes.Add(EyeUp);
			characterObject.data.faceShapes.Add(EyeSquare);
			characterObject.data.faceShapes.Add(EarTipLength);
			characterObject.data.bodyModsKeys = new List<string>();
			characterObject.data.bodyModsKeys.Add("root");
			characterObject.data.bodyModsKeys.Add("head");
			characterObject.data.bodyModsKeys.Add("clavicle_l");
			characterObject.data.bodyModsKeys.Add("eyeRoot_l");
			characterObject.data.bodyMods = new List<BodyModData>();
			BodyModData bodyModData = new BodyModData();
			bodyModData.scaleValue = height + 1f;
			BodyModData bodyModData2 = new BodyModData();
			bodyModData2.scaleValue = headSize + 1f;
			BodyModData bodyModData3 = new BodyModData();
			bodyModData3.scaleValue = shoulderWidth + 1f;
			BodyModData bodyModData4 = new BodyModData();
			bodyModData4.scale = EyeSocketScale;
			bodyModData4.position = EyeSocketPosition;
			bodyModData4.rotation = EyeSocketRotation;
			characterObject.data.bodyMods.Add(bodyModData);
			characterObject.data.bodyMods.Add(bodyModData2);
			characterObject.data.bodyMods.Add(bodyModData3);
			characterObject.data.bodyMods.Add(bodyModData4);
			characterObject.data.outfitDatas.Add(item3);
			return characterObject.data;
		}

		public List<GameObject> GetOutfitsList()
		{
			List<GameObject> list = new List<GameObject>();
			foreach (OufitParam outfit in outfits)
			{
				list.Add(outfit.outfit);
			}
			return list;
		}

		public Dictionary<OutfitType, GameObject> GetOutfitsDictionary()
		{
			Dictionary<OutfitType, GameObject> dictionary = new Dictionary<OutfitType, GameObject>();
			foreach (OufitParam outfit in outfits)
			{
				dictionary.Add(outfit.type, outfit.outfit);
			}
			return dictionary;
		}
	}
}
