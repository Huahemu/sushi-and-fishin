using System.Collections.Generic;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class ObstacleInfoTerrain : ObstacleInfoSingle
	{
		private float[,] m_HeightMap;

		private int m_HeightMapResolution;

		private Vector3 m_RealSize;

		public ObstacleInfoTerrain(int _ObstacleControllerID, float[,] _HeightMap, int _HeightMapResolution, Vector3 _RealSize, Vector3 _Position)
			: base(_ObstacleControllerID)
		{
			m_HeightMap = _HeightMap;
			m_HeightMapResolution = _HeightMapResolution;
			m_RealSize = _RealSize;
			ObtainTriangles(_Position);
		}

		public ObstacleInfoTerrain(int _ObstacleControllerID, Bounds _Bounds, List<Triangle> _Triangles)
			: base(_ObstacleControllerID)
		{
			base.Bounds = _Bounds;
			base.Triangles = _Triangles;
		}

		protected override List<Triangle> GetTriangles(Vector3 _Position)
		{
			List<Triangle> list = new List<Triangle>();
			float x = m_RealSize.x;
			float y = m_RealSize.y;
			float z = m_RealSize.z;
			float num = 1f / (float)m_HeightMapResolution;
			for (int i = 1; i < m_HeightMapResolution; i++)
			{
				int num2 = i - 1;
				int num3 = 0;
				Vector3 v = _Position + new Vector3(x * (float)num2 * num, m_HeightMap[num3, num2] * y, z * (float)num3 * num);
				Vector3 vector = _Position + new Vector3(x * (float)i * num, m_HeightMap[num3, i] * y, z * (float)num3 * num);
				for (int j = 0; j < m_HeightMapResolution; j++)
				{
					Vector3 vector2 = _Position + new Vector3(x * (float)num2 * num, m_HeightMap[j, num2] * y, z * (float)j * num);
					Vector3 vector3 = _Position + new Vector3(x * (float)i * num, m_HeightMap[j, i] * y, z * (float)j * num);
					list.Add(new Triangle(v, vector2, vector));
					list.Add(new Triangle(vector, vector2, vector3));
					num3++;
					v = vector2;
					vector = vector3;
				}
			}
			return list;
		}
	}
}
