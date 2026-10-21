using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.API
{
	[ExecuteInEditMode]
	public class Nav3DObstacle : MonoBehaviour
	{
		private class IntersectStaticObstacleException : Exception
		{
			public IntersectStaticObstacleException(string _Name)
				: base(string.Format(CROSS_STATIC_OBSTACLE_ERROR, _Name))
			{
			}
		}

		public enum ObstacleDataSource
		{
			RUNTIME_PROCESSING = 0,
			LOAD_FROM_BINARY = 1
		}

		private const float AUTO_UPDATE_MIN_PERIOD = 1f;

		private const string ADD_REMOVAL_UNAVAILABLE_TIP = "Addition/Removal operations is unavailable at runtime for pre-baked obstacles.";

		private static readonly string CROSS_STATIC_OBSTACLE_ERROR = "An intersection with a static obstacle has been detected (GameObject name: {0})! (Static obstacles - loaded on scene from binary). Runtime addition of obstacles that cross static obstacles on the scene is not allowed.";

		private static readonly string HAS_NO_OBSTACLE_INFOS_ERROR = "There is no obstacle data components found for GameObject: \"{0}\". Make sure that the game object or its children in the transform hierarchy has attached MeshFilter component with the selected Mesh, or Terrain component and that \"Auto add child transforms\" parameter is enabled. ";

		[SerializeField]
		private bool m_ProcessChildren = true;

		[SerializeField]
		private ObstacleDataSource m_ObstacleDataSource;

		[SerializeField]
		private bool m_AutoUpdateIfChanged;

		[SerializeField]
		private float m_AutoUpdateMinPeriod = 1f;

		private int m_InstanceID;

		private Transform m_Transform;

		private DateTime m_LastAutoUpdateTime;

		private List<ObstacleInfoBase> m_ObstacleInfos;

		private ObstacleAdditionProgress m_RuntimeAdditionProgress;

		private Nav3DObstacleLoader m_Loader;

		public int InstanceID => m_InstanceID;

		public int OctreeLayersCount => ObstacleManager.Instance.GetObstacleOctreeLayersMaxCount(InstanceID);

		public ObstacleDataSource DataSource => m_ObstacleDataSource;

		public IObstacleAdditionProgress AdditionProgress
		{
			get
			{
				if (DataSource != ObstacleDataSource.RUNTIME_PROCESSING)
				{
					return (m_Loader != null) ? m_Loader.DeserializingProgress : null;
				}
				return m_RuntimeAdditionProgress;
			}
		}

		public bool TransformHasChanged => GetTransformHasChanged();

		private Transform CachedTransform => m_Transform = ((m_Transform != null) ? m_Transform : base.transform);

		public int NodesCount => ObstacleManager.Instance.GetObstacleNodesCount(m_InstanceID);

		public event Action OnObstacleAdded;

		public void MarkTransformHasChangedTrue()
		{
			CachedTransform.hasChanged = true;
		}

		public void MarkAsSerialized()
		{
			MarkTransformHasChanged();
		}

		public void InvokeOnObstacleAdded()
		{
			OnObstacleAdded?.Invoke();
		}

		public List<ObstacleInfoBase> PrepareObstacleInfos(out string[] _ProcessablesImprint)
		{
			return m_ObstacleInfos = ObstacleInfoBase.GroupInfos(((IEnumerable<ObstacleInfoSingle>)GetObstacleProcessingInfos(out _ProcessablesImprint)).Select((Func<ObstacleInfoSingle, ObstacleInfoBase>)((ObstacleInfoSingle _Data) => _Data)).ToList());
		}

		public void Init()
		{
			m_InstanceID = GetInstanceID();
		}

		public string[] GetImprint()
		{
			return GetAllProcessableChildren().Keys.ToArray();
		}

		private bool GetTransformHasChanged()
		{
			if (!CachedTransform.hasChanged)
			{
				return CachedTransform.GetAllChildren().Any((Transform _Transform) => _Transform.hasChanged);
			}
			return true;
		}

		private void MarkTransformHasChanged(bool _Value = false)
		{
			CachedTransform.hasChanged = _Value;
			CachedTransform.GetAllChildren().ForEach((Transform _Transform) =>
			{
				_Transform.hasChanged = _Value;
			});
		}

		private void ProcessObstacleRuntime()
		{
			if (!m_ObstacleInfos.Any())
			{
				throw new Exception(string.Format(HAS_NO_OBSTACLE_INFOS_ERROR, base.gameObject.name));
			}
			Bounds bounds = ExtensionBounds.Union(m_ObstacleInfos.Select((ObstacleInfoBase _Info) => _Info.Bounds).ToArray());
			if (ObstacleManager.Instance.BoundsCrossStaticObstacles(bounds))
			{
				throw new IntersectStaticObstacleException(base.gameObject.name);
			}
			Nav3DManager.OnNav3DInit += () =>
			{
				if (!ObstacleManager.Doomed)
				{
					m_RuntimeAdditionProgress = ObstacleManager.Instance.AddObstacles(m_ObstacleInfos, InvokeOnObstacleAdded);
					m_LastAutoUpdateTime = DateTime.UtcNow;
				}
			};
		}

		private void RemoveObstacle()
		{
			if (!ObstacleManager.Doomed)
			{
				ObstacleManager.Instance.RemoveObstacles(m_InstanceID);
			}
		}

		private void UpdateObstacle()
		{
			if (!ObstacleManager.Doomed)
			{
				RemoveObstacle();
				PrepareObstacleInfos(out var _);
				ProcessObstacleRuntime();
				m_LastAutoUpdateTime = DateTime.UtcNow;
			}
		}

		private List<ObstacleInfoSingle> GetObstacleProcessingInfos(out string[] _ProcessablesImprint)
		{
			List<ObstacleInfoSingle> obstaclesProcessingData = new List<ObstacleInfoSingle>();
			Dictionary<string, Transform> allProcessableChildren = GetAllProcessableChildren();
			allProcessableChildren.ForEach((KeyValuePair<string, Transform> _Kvp) =>
			{
				if (_Kvp.Value.TryGetObstacleInfo(this, out var _AdditionData))
				{
					obstaclesProcessingData.Add(_AdditionData);
				}
			});
			_ProcessablesImprint = allProcessableChildren.Keys.ToArray();
			return obstaclesProcessingData;
		}

		private Dictionary<string, Transform> GetAllProcessableChildren()
		{
			Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>();
			string _TransformImprint;
			if (m_ProcessChildren)
			{
				GetProcessableChildrenRecursively(dictionary, CachedTransform, 0, 0);
			}
			else if (IsTransformProcessable(CachedTransform, out _TransformImprint))
			{
				dictionary.Add("0" + _TransformImprint, CachedTransform);
			}
			return dictionary;
		}

		private int GetProcessableChildrenRecursively(Dictionary<string, Transform> _Result, Transform _Transform, int _Index, int _Depth)
		{
			if (IsTransformProcessable(_Transform, out var _TransformImprint))
			{
				_Result.Add(string.Join(null, _Index, _TransformImprint, _Depth), _Transform);
				_Index++;
			}
			foreach (Transform item in _Transform)
			{
				_Index = GetProcessableChildrenRecursively(_Result, item, _Index, _Depth + 1);
			}
			return _Index;
		}

		private bool IsTransformProcessable(Transform _Transform, out string _TransformImprint)
		{
			if (!_Transform.gameObject.activeInHierarchy || _Transform.gameObject.GetComponent<Nav3DIgnoreObstacleProcessingTag>() != null)
			{
				_TransformImprint = null;
				return false;
			}
			if (_Transform.TryGetComponent<MeshFilter>(out var component) && component.sharedMesh.triangles.Length != 0)
			{
				_TransformImprint = "mesh" + GetTransformImprintStr(_Transform);
				return true;
			}
			if (_Transform.TryGetComponent<Terrain>(out var component2) && component2.terrainData.size != Vector3.zero)
			{
				_TransformImprint = "terrain" + GetTransformImprintStr(_Transform);
				return true;
			}
			_TransformImprint = null;
			return false;
		}

		private string GetTransformImprintStr(Transform _Transform)
		{
			return _Transform.position.ToStringExt() + "|" + _Transform.rotation.ToStringExt() + "|" + _Transform.lossyScale.ToStringExt() + "|" + _Transform.gameObject.name;
		}

		private void Awake()
		{
			MarkTransformHasChanged();
			if (Application.isPlaying && base.enabled)
			{
				m_Loader = UnityEngine.Object.FindObjectOfType<Nav3DObstacleLoader>();
			}
		}

		private void Update()
		{
			if (m_ObstacleDataSource == ObstacleDataSource.RUNTIME_PROCESSING && m_AutoUpdateIfChanged && CachedTransform.hasChanged && (DateTime.UtcNow - m_LastAutoUpdateTime).TotalSeconds > (double)m_AutoUpdateMinPeriod)
			{
				UpdateObstacle();
				CachedTransform.hasChanged = false;
			}
		}

		private void OnEnable()
		{
			Init();
			if (m_ObstacleDataSource == ObstacleDataSource.LOAD_FROM_BINARY)
			{
				Debug.LogWarning("Addition/Removal operations is unavailable at runtime for pre-baked obstacles.");
			}
			else
			{
				if (m_ObstacleDataSource != ObstacleDataSource.RUNTIME_PROCESSING)
				{
					return;
				}
				Nav3DManager.OnNav3DInit += () =>
				{
					if (!ObstacleManager.Doomed)
					{
						Init();
						PrepareObstacleInfos(out var _);
						ProcessObstacleRuntime();
					}
				};
			}
		}

		private void OnDisable()
		{
			if (m_ObstacleDataSource == ObstacleDataSource.LOAD_FROM_BINARY)
			{
				Debug.LogWarning("Addition/Removal operations is unavailable at runtime for pre-baked obstacles.");
			}
			else if (m_ObstacleDataSource == ObstacleDataSource.RUNTIME_PROCESSING)
			{
				RemoveObstacle();
			}
		}
	}
}
