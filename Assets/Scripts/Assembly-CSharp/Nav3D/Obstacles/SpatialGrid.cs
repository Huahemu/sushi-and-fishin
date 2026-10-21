using System.Collections.Concurrent;
using System.Collections.Generic;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public class SpatialGrid
	{
		private int m_LayersCount;

		private float m_MinBucketSize;

		private List<ConcurrentDictionary<Vector3Int, Leaf>> m_Layers;

		private GraphConstructionProgress m_ConstructionProgress;

		public int MinLayer => 0;

		public int MaxLayer => m_LayersCount - 1;

		public SpatialGrid(int _LayersCount, float _MinBucketSize, GraphConstructionProgress _ConstructionProgress)
		{
			m_LayersCount = _LayersCount;
			m_MinBucketSize = _MinBucketSize;
			m_Layers = new List<ConcurrentDictionary<Vector3Int, Leaf>>(m_LayersCount);
			m_ConstructionProgress = _ConstructionProgress;
			for (int i = 0; i < m_LayersCount; i++)
			{
				m_Layers.Add(new ConcurrentDictionary<Vector3Int, Leaf>());
			}
		}

		public void SubtractLeafFromProgress(int _GridLayer, bool _Processed)
		{
			int prunedPower = GetPrunedPower(_GridLayer, MaxLayer);
			m_ConstructionProgress.AddTotalLeavesCount(-prunedPower + (_Processed ? 1 : 0));
			if (_Processed)
			{
				m_ConstructionProgress.AddProcessedLeavesCount(1);
			}
		}

		public void AddLeaf(Leaf _Leaf)
		{
			m_Layers[_Leaf.GridLayer].TryAdd(_Leaf.Index, _Leaf);
		}

		public void FormLeavesConnections(GraphConstructionProgress _Progress)
		{
			if (m_Layers.IsNullOrEmpty())
			{
				return;
			}
			for (int i = 0; i < m_Layers.Count; i++)
			{
				ConcurrentDictionary<Vector3Int, Leaf> concurrentDictionary = m_Layers[i];
				foreach (KeyValuePair<Vector3Int, Leaf> item in concurrentDictionary)
				{
					Vector3Int key = item.Key;
					Leaf value = item.Value;
					Vector3 center = value.Bounds.center;
					foreach (Vector3Int facesDirection in value.FacesDirections)
					{
						Vector3Int key2 = key + facesDirection;
						Leaf value2 = null;
						if (concurrentDictionary.TryGetValue(key2, out value2))
						{
							if (!value2.Occupied)
							{
								value.AddFreeAdjacent(value2);
								if (!value.Occupied)
								{
									value2.AddFreeAdjacent(value);
									value2.RemoveFaceConsideration(-facesDirection);
								}
							}
							continue;
						}
						float x = value.Bounds.size.x;
						Vector3 point = value.Bounds.center + new Vector3((float)facesDirection.x * x, (float)facesDirection.y * x, (float)facesDirection.z * x);
						float num = x * 2f;
						int num2 = i - 1;
						Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(center, num);
						key2 = UtilsSpatialGrid.GetBucketIndex(point, num);
						while (num2 >= 0 && bucketIndex != key2 && !m_Layers[num2].TryGetValue(key2, out value2))
						{
							num2--;
							num *= 2f;
							bucketIndex = UtilsSpatialGrid.GetBucketIndex(center, num);
							key2 = UtilsSpatialGrid.GetBucketIndex(point, num);
						}
						if (value2 != null && !value2.Occupied)
						{
							value.AddFreeAdjacent(value2);
							if (!value.Occupied)
							{
								value2.AddFreeAdjacent(value);
							}
						}
					}
				}
				_Progress.SetLayersProcessignProgress(i, m_LayersCount);
			}
		}

		private int GetPrunedPower(int _Layer, int _MaxLayer)
		{
			return (int)Mathf.Pow(8f, _MaxLayer - _Layer);
		}
	}
}
