using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Optimization
{
	[DisallowMultipleComponent]
	public sealed class Nav3DBoxProxyGroup : MonoBehaviour
	{
		[SerializeField]
		private bool _drawAlways = true;

		[SerializeField]
		private Color _gizmoColor = new Color(0.1f, 0.85f, 1f, 0.8f);

		[SerializeField]
		[HideInInspector]
		[FormerlySerializedAs("_cellSize")]
		private float _baseCellSize;

		[SerializeField]
		[HideInInspector]
		private float _effectiveCellSize;

		[SerializeField]
		[HideInInspector]
		private bool _usedAdaptiveSurfaceDetail;

		[SerializeField]
		[HideInInspector]
		private int _sourceMeshCount;

		[SerializeField]
		[HideInInspector]
		private int _occupiedCellCount;

		[SerializeField]
		[HideInInspector]
		private int _boxCount;

		public float CellSize
		{
			get
			{
				if (!(_effectiveCellSize > 0f))
				{
					return _baseCellSize;
				}
				return _effectiveCellSize;
			}
		}

		public float BaseCellSize => _baseCellSize;

		public bool UsedAdaptiveSurfaceDetail => _usedAdaptiveSurfaceDetail;

		public int SourceMeshCount => _sourceMeshCount;

		public int OccupiedCellCount => _occupiedCellCount;

		public int BoxCount => _boxCount;
	}
}
