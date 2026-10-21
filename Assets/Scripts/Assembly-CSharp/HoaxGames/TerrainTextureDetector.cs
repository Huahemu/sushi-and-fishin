using System;
using UnityEngine;

namespace HoaxGames
{
	[RequireComponent(typeof(Terrain))]
	public class TerrainTextureDetector : MonoBehaviour
	{
		private Terrain ThisTerrain;

		private float[,,] CachedTerrainAlphamapData;

		private TerrainData ThisTerrainData => ThisTerrain.terrainData;

		private void Start()
		{
			ThisTerrain = GetComponent<Terrain>();
			CachedTerrainAlphamapData = ThisTerrainData.GetAlphamaps(0, 0, ThisTerrainData.alphamapWidth, ThisTerrainData.alphamapHeight);
		}

		public int GetDominantTextureIndexAt(Vector3 worldPosition)
		{
			Vector3Int vector3Int = ConvertToAlphamapCoordinates(worldPosition);
			if (!ContainsIndex(CachedTerrainAlphamapData, vector3Int.x, 1))
			{
				return -1;
			}
			if (!ContainsIndex(CachedTerrainAlphamapData, vector3Int.z, 0))
			{
				return -1;
			}
			int result = 0;
			float num = float.MinValue;
			int length = CachedTerrainAlphamapData.GetLength(2);
			for (int i = 0; i < length; i++)
			{
				float num2 = CachedTerrainAlphamapData[vector3Int.z, vector3Int.x, i];
				if (num2 > num)
				{
					num = num2;
					result = i;
				}
			}
			return result;
			Vector3Int ConvertToAlphamapCoordinates(Vector3 _worldPosition)
			{
				Vector3 vector = _worldPosition - base.transform.position;
				return new Vector3Int(Mathf.RoundToInt(vector.x / ThisTerrainData.size.x * (float)ThisTerrainData.alphamapWidth), 0, Mathf.RoundToInt(vector.z / ThisTerrainData.size.z * (float)ThisTerrainData.alphamapHeight));
			}
		}

		private bool ContainsIndex(Array array, int index, int dimension)
		{
			if (index < 0)
			{
				return false;
			}
			return index < array.GetLength(dimension);
		}
	}
}
