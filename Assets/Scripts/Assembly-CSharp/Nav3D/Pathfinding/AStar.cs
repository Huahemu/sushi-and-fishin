using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nav3D.API;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Pathfinding
{
	public class AStar
	{
		private struct PricedLeaf : IComparable<PricedLeaf>
		{
			public Leaf Leaf { get; private set; }

			public float GCost { get; private set; }

			public float HCost { get; private set; }

			public PricedLeaf(Leaf _Leaf, float _GCost, float _HCost)
			{
				Leaf = _Leaf;
				GCost = _GCost;
				HCost = _HCost;
			}

			public int CompareTo(PricedLeaf _Other)
			{
				float num = GCost - _Other.GCost;
				if (Mathf.Abs(num) < 1E-06f)
				{
					return 0;
				}
				if (!(num > 0f))
				{
					return -1;
				}
				return 1;
			}
		}

		private Vector3 m_Start;

		private Vector3 m_Goal;

		private Vector3 m_Direction;

		private MinHeap<PricedLeaf> m_OpenList;

		public Leaf StartLeaf { get; set; }

		public Leaf GoalLeaf { get; set; }

		public CancellationToken CancellationTokenTimeout { get; set; }

		public CancellationToken CancellationTokenControl { get; set; }

		public PathResolverResult GetPath()
		{
			m_Direction = m_Goal - m_Start;
			m_Start = StartLeaf.NavigationBounds.center;
			m_Goal = GoalLeaf.NavigationBounds.center;
			m_OpenList = new MinHeap<PricedLeaf>();
			Dictionary<Leaf, float> dictionary = new Dictionary<Leaf, float>();
			float hCost = 0f;
			AddToOpenList(StartLeaf, hCost);
			while (m_OpenList.Count != 0)
			{
				if (CancellationTokenTimeout.IsCancellationRequested)
				{
					return new PathResolverResult(null, PathfindingResultCode.TIMEOUT);
				}
				if (CancellationTokenControl.IsCancellationRequested)
				{
					return new PathResolverResult(null, PathfindingResultCode.CANCELLED);
				}
				PricedLeaf pricedLeaf = m_OpenList.PopMin();
				if (dictionary.ContainsKey(pricedLeaf.Leaf))
				{
					continue;
				}
				if (pricedLeaf.Leaf.Equals(GoalLeaf))
				{
					List<Leaf> path = ConstructPath(dictionary);
					if (CancellationTokenTimeout.IsCancellationRequested)
					{
						return new PathResolverResult(null, PathfindingResultCode.TIMEOUT);
					}
					if (CancellationTokenControl.IsCancellationRequested)
					{
						return new PathResolverResult(null, PathfindingResultCode.CANCELLED);
					}
					return new PathResolverResult(path, PathfindingResultCode.SUCCEEDED);
				}
				dictionary.Add(pricedLeaf.Leaf, pricedLeaf.HCost);
				foreach (Leaf freeAdjacent in pricedLeaf.Leaf.FreeAdjacents)
				{
					if (!dictionary.ContainsKey(freeAdjacent))
					{
						float hCost2 = HCost(pricedLeaf.Leaf, freeAdjacent, pricedLeaf.HCost);
						AddToOpenList(freeAdjacent, hCost2);
					}
				}
			}
			return new PathResolverResult(null, PathfindingResultCode.PATH_DOES_NOT_EXIST);
		}

		public virtual void AddToOpenList(Leaf _Leaf, float _HCost)
		{
			m_OpenList.Add(new PricedLeaf(_Leaf, GCost(_Leaf, _HCost), _HCost));
		}

		public virtual List<Leaf> GetHistory()
		{
			return new List<Leaf>();
		}

		private List<Leaf> ConstructPath(Dictionary<Leaf, float> _ClosedList)
		{
			Leaf item = GoalLeaf;
			float num = float.MaxValue;
			foreach (Leaf freeAdjacent in GoalLeaf.FreeAdjacents)
			{
				if (freeAdjacent == StartLeaf)
				{
					return new List<Leaf> { StartLeaf, GoalLeaf };
				}
				if (_ClosedList.TryGetValue(freeAdjacent, out var value) && value <= num)
				{
					num = value;
					item = freeAdjacent;
				}
			}
			if (Mathf.Abs(num - float.MaxValue) < 1E-06f)
			{
				num = 0f;
			}
			List<Leaf> list = new List<Leaf>((int)Mathf.Round(num) + 1) { GoalLeaf, item };
			while (num > 0f)
			{
				if (CancellationTokenTimeout.IsCancellationRequested || CancellationTokenControl.IsCancellationRequested)
				{
					return new List<Leaf>();
				}
				foreach (Leaf freeAdjacent2 in list.Last().FreeAdjacents)
				{
					if (_ClosedList.TryGetValue(freeAdjacent2, out var value2) && value2 < num)
					{
						num = value2;
						item = freeAdjacent2;
					}
				}
				list.Add(item);
			}
			list.Reverse();
			return list;
		}

		private float GCost(Leaf _Leaf, float _AccruedCost)
		{
			Vector3 center = _Leaf.NavigationBounds.center;
			return 1f * ManhattanDistance(m_Goal, center) + 1f * _AccruedCost + 1f * ManhattanDistance(Vector3.Project(center, m_Direction), center);
		}

		private float HCost(Leaf _LeafA, Leaf _LeafB, float _AccruedCost)
		{
			return _AccruedCost + ManhattanDistance(_LeafA.NavigationBounds.center, _LeafB.NavigationBounds.center);
		}

		private float ManhattanDistance(Vector3 _A, Vector3 _B)
		{
			return Mathf.Abs(_A.x - _B.x) + Mathf.Abs(_A.y - _B.y) + Mathf.Abs(_A.z - _B.z);
		}
	}
}
