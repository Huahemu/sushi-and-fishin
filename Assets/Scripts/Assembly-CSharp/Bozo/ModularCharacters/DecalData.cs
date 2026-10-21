using System;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	[Serializable]
	public struct DecalData
	{
		public string decal;

		public Texture decalTexture;

		public string parent;

		public Vector3 pos;

		public float yaw;

		public float roll;

		public float pitch;

		public float scale;

		public Color color;

		public DecalData(string decal, string parent, Vector3 pos, float yaw, float roll, float pitch, float scale, Color color)
		{
			this.decal = decal;
			decalTexture = null;
			this.parent = parent;
			this.pos = pos;
			this.yaw = yaw;
			this.roll = roll;
			this.pitch = pitch;
			this.scale = scale;
			this.color = color;
		}
	}
}
