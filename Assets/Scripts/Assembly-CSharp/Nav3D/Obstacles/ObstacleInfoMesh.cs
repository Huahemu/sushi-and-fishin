using System.Collections.Generic;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class ObstacleInfoMesh : ObstacleInfoSingle
	{
		private Vector3[] m_Vertices;

		private int[] m_Triangles;

		private Vector3 m_Scale;

		private Quaternion m_Rotation;

		public ObstacleInfoMesh(int _ObstacleControllerID, Vector3[] _Vertices, int[] _Triangles, Vector3 _Position, Vector3 _Scale, Quaternion _Rotation)
			: base(_ObstacleControllerID)
		{
			m_Vertices = _Vertices;
			m_Triangles = _Triangles;
			m_Scale = _Scale;
			m_Rotation = _Rotation;
			ObtainTriangles(_Position);
		}

		public ObstacleInfoMesh(int _ObstacleControllerID, Bounds _Bounds, List<Triangle> _Triangles)
			: base(_ObstacleControllerID)
		{
			base.Bounds = _Bounds;
			base.Triangles = _Triangles;
		}

		protected override List<Triangle> GetTriangles(Vector3 _Position)
		{
			int num = m_Triangles.Length / 3;
			List<Triangle> list = new List<Triangle>(num);
			for (int i = 0; i < num; i++)
			{
				int num2 = i * 3;
				Vector3 vector = m_Vertices[m_Triangles[num2]];
				Vector3 vector2 = m_Vertices[m_Triangles[num2 + 1]];
				Vector3 vector3 = m_Vertices[m_Triangles[num2 + 2]];
				vector.Scale(m_Scale);
				vector = m_Rotation * vector + _Position;
				vector2.Scale(m_Scale);
				vector2 = m_Rotation * vector2 + _Position;
				vector3.Scale(m_Scale);
				vector3 = m_Rotation * vector3 + _Position;
				Triangle triangle = new Triangle(vector, vector2, vector3);
				triangle.InitSolidHunkTriangles();
				list.Add(triangle);
			}
			return list;
		}
	}
}
