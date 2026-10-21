using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class VertexInfo
	{
		public Vector3 Vertex;

		public List<int> Occasions_Vertex;

		public List<int> Occasions_Submesh;

		public List<Vector3> Neighbors;

		public VertexInfo(int occasion_Vertex, int occasion_Submesh)
		{
			Occasions_Vertex = new List<int> { occasion_Vertex };
			Occasions_Submesh = new List<int> { occasion_Submesh };
			Neighbors = new List<Vector3>();
		}
	}
}
