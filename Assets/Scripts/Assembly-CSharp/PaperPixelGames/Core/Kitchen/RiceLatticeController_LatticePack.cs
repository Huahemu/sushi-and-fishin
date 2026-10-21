using System;
using System.Collections.Generic;
using Lattice;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	public class RiceLatticeController_LatticePack : MonoBehaviour
	{
		private class ColumnState
		{
			public int x;

			public int z;

			public float[] baseY;
		}

		[Header("References")]
		[SerializeField]
		private global::Lattice.Lattice _lattice;

		[SerializeField]
		private List<LatticeModifierBase> _modifiersToUpdate = new List<LatticeModifierBase>();

		[Header("Mode")]
		[Tooltip("True = Wholesale style (all columns together, layered collapse). False = Gastropan style (column by column).")]
		[SerializeField]
		private bool _collapseAllColumnsTogether;

		[Header("Fill Settings")]
		[SerializeField]
		private bool _smooth = true;

		[SerializeField]
		[Min(0f)]
		private int _edgePadding = 1;

		[Header("Wholesale Layer Settings")]
		[Tooltip("Layer overlap. 0.5 = next layer starts when upper is halfway down.")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _layerLead = 0.5f;

		private Vector3[] _originalOffsets;

		private List<ColumnState> _columns;

		private int _topY;

		private int maxUses;

		private int currentUses;

		private bool _initialized;

		private void Awake()
		{
			EnsureInitialized();
		}

		private void OnEnable()
		{
			EnsureInitialized();
		}

		private void EnsureInitialized()
		{
			if (_lattice == null)
			{
				_lattice = GetComponent<global::Lattice.Lattice>();
			}
			if (_lattice == null)
			{
				return;
			}
			Vector3Int resolution = _lattice.Resolution;
			int num = resolution.x * resolution.y * resolution.z;
			bool flag = _originalOffsets != null && _originalOffsets.Length == num;
			bool flag2 = _columns != null && _columns.Count > 0;
			if (!(_initialized & flag & flag2))
			{
				CacheModifiersIfEmpty();
				if (!flag)
				{
					CacheOriginalOffsets();
				}
				BuildColumns();
				_initialized = true;
			}
		}

		private void CacheModifiersIfEmpty()
		{
			if (_modifiersToUpdate == null || _modifiersToUpdate.Count <= 0)
			{
				_modifiersToUpdate = new List<LatticeModifierBase>(GetComponentsInChildren<LatticeModifierBase>(includeInactive: true));
			}
		}

		private void CacheOriginalOffsets()
		{
			Vector3Int resolution = _lattice.Resolution;
			int num = resolution.x * resolution.y * resolution.z;
			_originalOffsets = new Vector3[num];
			for (int i = 0; i < resolution.z; i++)
			{
				for (int j = 0; j < resolution.y; j++)
				{
					for (int k = 0; k < resolution.x; k++)
					{
						int index = GetIndex(resolution, k, j, i);
						_originalOffsets[index] = _lattice.GetHandleOffset(k, j, i);
					}
				}
			}
		}

		private void BuildColumns()
		{
			_columns = new List<ColumnState>();
			Vector3Int resolution = _lattice.Resolution;
			if (resolution.y < 2)
			{
				return;
			}
			_topY = resolution.y - 1;
			for (int i = 0; i < resolution.z; i++)
			{
				for (int j = 0; j < resolution.x; j++)
				{
					if (!IsInEdgeRing(j, i, resolution))
					{
						ColumnState columnState = new ColumnState
						{
							x = j,
							z = i,
							baseY = new float[resolution.y]
						};
						for (int k = 0; k < resolution.y; k++)
						{
							columnState.baseY[k] = _lattice.GetHandlePosition(j, k, i).y;
						}
						_columns.Add(columnState);
					}
				}
			}
			maxUses = Mathf.Max(1, _columns.Count);
			currentUses = maxUses;
			if (!_collapseAllColumnsTogether)
			{
				ShuffleColumns();
			}
		}

		private bool IsInEdgeRing(int x, int z, Vector3Int res)
		{
			if (_edgePadding <= 0)
			{
				return false;
			}
			int num = (res.x - 1) / 2;
			int num2 = (res.z - 1) / 2;
			int num3 = Mathf.Min(_edgePadding, num, num2);
			if (num3 <= 0)
			{
				return false;
			}
			int num4 = res.x - 1;
			int num5 = res.z - 1;
			if (x >= num3 && x <= num4 - num3 && z >= num3)
			{
				return z > num5 - num3;
			}
			return true;
		}

		public void SetFillByUses(int current, int max)
		{
			EnsureInitialized();
			if (_initialized && max > 0)
			{
				float fillAmount = Mathf.Clamp01((float)current / (float)max);
				SetFillAmount(fillAmount);
			}
		}

		public void SetFillAmount(float overallFill)
		{
			EnsureInitialized();
			if (!_initialized || _columns == null || _columns.Count == 0)
			{
				return;
			}
			overallFill = Mathf.Clamp01(overallFill);
			if (_collapseAllColumnsTogether)
			{
				for (int i = 0; i < _columns.Count; i++)
				{
					ApplyColumnLayered(_columns[i], overallFill);
				}
				ForceModifiersUpdate();
				return;
			}
			int count = _columns.Count;
			if (!_smooth)
			{
				int num = Mathf.FloorToInt((1f - overallFill) * (float)count);
				for (int j = 0; j < count; j++)
				{
					float fill = ((j < num) ? 0f : 1f);
					ApplyColumnSimple(_columns[j], fill);
				}
			}
			else
			{
				float num2 = (1f - overallFill) * (float)count;
				for (int k = 0; k < count; k++)
				{
					float num3 = num2 - (float)k;
					float fill2 = ((!(num3 >= 1f)) ? ((!(num3 <= 0f)) ? (1f - num3) : 1f) : 0f);
					ApplyColumnSimple(_columns[k], fill2);
				}
			}
			ForceModifiersUpdate();
		}

		private void ApplyColumnSimple(ColumnState col, float fill)
		{
			float t = 1f - fill;
			float y = Mathf.Lerp(col.baseY[_topY], col.baseY[_topY - 1], t);
			Vector3 handlePosition = _lattice.GetHandlePosition(col.x, _topY, col.z);
			handlePosition.y = y;
			_lattice.SetHandlePosition(col.x, _topY, col.z, handlePosition);
		}

		private void ApplyColumnLayered(ColumnState col, float overallFill)
		{
			float num = (1f - Mathf.Clamp01(overallFill)) * (float)_topY;
			int topY = _topY;
			float[] array = new float[col.baseY.Length];
			array[0] = col.baseY[0];
			for (int i = 1; i <= topY; i++)
			{
				float num2 = (float)(topY - i) - _layerLead;
				float t = Mathf.Clamp01(num - num2);
				float num3 = Mathf.Lerp(col.baseY[i], col.baseY[i - 1], t);
				array[i] = num3;
			}
			for (int num4 = topY - 1; num4 >= 1; num4--)
			{
				float num5 = array[num4 + 1] - 0f;
				if (array[num4] > num5)
				{
					array[num4] = num5;
				}
			}
			for (int j = 1; j <= topY; j++)
			{
				Vector3 handlePosition = _lattice.GetHandlePosition(col.x, j, col.z);
				handlePosition.y = array[j];
				_lattice.SetHandlePosition(col.x, j, col.z, handlePosition);
			}
		}

		public void ResetFull()
		{
			EnsureInitialized();
			if (!_initialized)
			{
				return;
			}
			Vector3Int resolution = _lattice.Resolution;
			for (int i = 0; i < resolution.z; i++)
			{
				for (int j = 0; j < resolution.y; j++)
				{
					for (int k = 0; k < resolution.x; k++)
					{
						int index = GetIndex(resolution, k, j, i);
						_lattice.SetHandleOffset(k, j, i, _originalOffsets[index]);
					}
				}
			}
			currentUses = maxUses;
			BuildColumns();
			ForceModifiersUpdate();
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

		private void ForceModifiersUpdate()
		{
			for (int i = 0; i < _modifiersToUpdate.Count; i++)
			{
				LatticeModifierBase latticeModifierBase = _modifiersToUpdate[i];
				if (latticeModifierBase != null && latticeModifierBase.isActiveAndEnabled)
				{
					latticeModifierBase.RequestUpdate();
				}
			}
		}

		private void ShuffleColumns()
		{
			System.Random random = new System.Random();
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

		private static int GetIndex(Vector3Int res, int x, int y, int z)
		{
			return x + res.x * y + res.x * res.y * z;
		}
	}
}
