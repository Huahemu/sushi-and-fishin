using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	[Serializable]
	public struct OutfitData
	{
		public string outfit;

		public List<Color> colors;

		public string decal;

		public List<Color> decalColors;

		public Vector4 decalScale;

		public string pattern;

		public List<Color> patternColors;

		public Vector4 patternScale;

		public bool[] partVisibility;

		public Color color;

		public int swatch;

		public List<DecalData> decalDatas;

		public OutfitData(OutfitData d)
		{
			outfit = d.outfit;
			colors = d.colors;
			decal = d.decal;
			decalColors = d.decalColors;
			decalScale = d.decalScale;
			pattern = d.pattern;
			patternColors = d.patternColors;
			patternScale = d.patternScale;
			partVisibility = d.partVisibility;
			color = d.color;
			swatch = d.swatch;
			decalDatas = d.decalDatas;
		}
	}
}
