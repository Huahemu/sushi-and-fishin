using System;
using System.Collections.Generic;
using Deform;
using Unity.Mathematics;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	public class RiceLatticeController : MonoBehaviour
	{
		private struct ColumnState
		{
			public int x;

			public int z;

			public int topIndex;

			public int bottomIndex;

			public float topBaseY;

			public float bottomBaseY;

			public float fill;

			public float distance;
		}

		[SerializeField]
		private LatticeDeformer _lattice;

		[Tooltip("Lattice merkez noktası (-1,-1 = otomatik merkez)")]
		[SerializeField]
		private Vector2Int _startXZ = new Vector2Int(-1, -1);

		[Tooltip("Smooth progressive fill/drain (true) veya kolon kolon step (false)")]
		[SerializeField]
		private bool _smooth = true;

		[Tooltip("Kaç grid kalınlığında kenarı sabit bırakayım? 1 = dıştaki ringi koru")]
		[SerializeField]
		[Min(0f)]
		private int _edgePadding = 1;

		private int _randomSeed;

		private bool _useRandomSeed;

		private float3[] _originalPoints;

		private List<ColumnState> _columns;

		private int _topY;

		private int maxUses = 10;

		private int currentUses = 10;

		private void Awake()
		{
			if (_lattice == null)
			{
				_lattice = GetComponent<LatticeDeformer>();
			}
			float3[] controlPoints = _lattice.ControlPoints;
			_originalPoints = new float3[controlPoints.Length];
			Array.Copy(controlPoints, _originalPoints, controlPoints.Length);
			BuildColumns();
		}

		private void BuildColumns()
		{
			_columns = new List<ColumnState>();
			Vector3Int resolution = _lattice.Resolution;
			_topY = resolution.y - 1;
			int num = ((_startXZ.x >= 0) ? _startXZ.x : ((resolution.x - 1) / 2));
			int num2 = ((_startXZ.y >= 0) ? _startXZ.y : ((resolution.z - 1) / 2));
			for (int i = 0; i < resolution.z; i++)
			{
				for (int j = 0; j < resolution.x; j++)
				{
					if (!IsInEdgeRing(j, i, resolution))
					{
						int index = _lattice.GetIndex(j, _topY, i);
						int index2 = _lattice.GetIndex(j, _topY - 1, i);
						float3 float5 = _originalPoints[index];
						float3 float6 = _originalPoints[index2];
						ColumnState item = new ColumnState
						{
							x = j,
							z = i,
							topIndex = index,
							bottomIndex = index2,
							topBaseY = float5.y,
							bottomBaseY = float6.y,
							fill = 1f,
							distance = Vector2.Distance(new Vector2(j, i), new Vector2(num, num2))
						};
						_columns.Add(item);
					}
				}
			}
			ShuffleColumns();
		}

		private bool IsInEdgeRing(int x, int z, Vector3Int res)
		{
			if (_edgePadding <= 0)
			{
				return false;
			}
			int num = res.x - 1;
			int num2 = res.z - 1;
			if (x >= _edgePadding && x <= num - _edgePadding && z >= _edgePadding)
			{
				return z > num2 - _edgePadding;
			}
			return true;
		}

		public void SetFillByUses(int currentUses, int maxUses)
		{
			if (maxUses > 0)
			{
				float fillAmount = Mathf.Clamp01((float)currentUses / (float)maxUses);
				SetFillAmount(fillAmount);
			}
		}

		public void SetFillAmount(float overallFill)
		{
			if (_columns == null)
			{
				return;
			}
			overallFill = Mathf.Clamp01(overallFill);
			int count = _columns.Count;
			if (!_smooth)
			{
				int num = Mathf.FloorToInt((1f - overallFill) * (float)count);
				for (int i = 0; i < count; i++)
				{
					ColumnState columnState = _columns[i];
					columnState.fill = ((i < num) ? 0f : 1f);
					_columns[i] = columnState;
					ApplyColumn(columnState);
				}
				return;
			}
			float num2 = (1f - overallFill) * (float)count;
			for (int j = 0; j < count; j++)
			{
				ColumnState columnState2 = _columns[j];
				float num3 = num2 - (float)j;
				if (num3 >= 1f)
				{
					columnState2.fill = 0f;
				}
				else if (num3 <= 0f)
				{
					columnState2.fill = 1f;
				}
				else
				{
					columnState2.fill = 1f - num3;
				}
				_columns[j] = columnState2;
				ApplyColumn(columnState2);
			}
		}

		private void ApplyColumn(ColumnState col)
		{
			float t = 1f - col.fill;
			float y = Mathf.Lerp(col.topBaseY, col.bottomBaseY, t);
			float3 controlPoint = _lattice.GetControlPoint(col.x, _topY, col.z);
			controlPoint.y = y;
			_lattice.SetControlPoint(col.x, _topY, col.z, controlPoint);
		}

		private void ShuffleColumns()
		{
			System.Random random = (_useRandomSeed ? new System.Random(_randomSeed) : new System.Random());
			int num = _columns.Count;
			while (num > 1)
			{
				num--;
				int index = random.Next(num + 1);
				ColumnState value = _columns[index];
				_columns[index] = _columns[num];
				_columns[num] = value;
			}
		}

		public void ResetFull()
		{
			float3[] controlPoints = _lattice.ControlPoints;
			for (int i = 0; i < controlPoints.Length; i++)
			{
				controlPoints[i] = _originalPoints[i];
			}
			currentUses = maxUses;
			BuildColumns();
		}

		private void TakeRice()
		{
			if (currentUses > 0)
			{
				currentUses--;
				SetFillByUses(currentUses, maxUses);
			}
		}

		private void FillBack()
		{
			if (currentUses < maxUses)
			{
				currentUses++;
				SetFillByUses(currentUses, maxUses);
			}
		}
	}
}
