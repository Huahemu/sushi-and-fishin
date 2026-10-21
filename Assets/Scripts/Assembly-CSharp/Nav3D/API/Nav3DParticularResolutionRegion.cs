using System;
using Nav3D.Common;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.API
{
	public class Nav3DParticularResolutionRegion : MonoBehaviour, IBoundable
	{
		private readonly string RESOLUTION_ZERO_LESS_EX = "[Nav3DParticularResolutionRegion]: Initialization error. The min. bucket value must be positive. The given value is {0}.";

		private readonly string RESOLUTION_INCORRECT_SIZE_EX = "[Nav3DParticularResolutionRegion]: Initialization error. The min. bucket value {0} must be greater than the minimum region size {1}.";

		private readonly Color REGION_COLOR = new Color(1f, 0.75f, 0f);

		private const string MENU_PATH = "Nav3D/Create Resolution region";

		private const string PREFAB_NAME = "Nav3DResolutionRegion";

		[SerializeField]
		private float m_MinBucketSize;

		[SerializeField]
		private float m_SizeX;

		[SerializeField]
		private float m_SizeY;

		[SerializeField]
		private float m_SizeZ;

		[SerializeField]
		private bool m_DrawAlways;

		private Bounds m_Bounds;

		private Transform m_Transform;

		public Bounds Bounds => m_Bounds;

		public float MinBucketSize => m_MinBucketSize;

		public void InitializeEditMode()
		{
			ValidateParams();
			Initialize();
			ObstacleParticularResolutionManager.Instance.Register(this);
		}

		private void ValidateParams()
		{
			if (m_MinBucketSize <= 0f)
			{
				throw new Exception(string.Format(RESOLUTION_ZERO_LESS_EX, m_MinBucketSize));
			}
			if (m_MinBucketSize >= m_SizeX || m_MinBucketSize >= m_SizeY || m_MinBucketSize >= m_SizeZ)
			{
				throw new Exception(string.Format(RESOLUTION_INCORRECT_SIZE_EX, m_MinBucketSize, Mathf.Min(m_SizeX, m_SizeY, m_SizeZ)));
			}
		}

		private void Register()
		{
			ValidateParams();
			Nav3DManager.OnNav3DPreInit += () =>
			{
				if (!ObstacleParticularResolutionManager.Doomed)
				{
					ObstacleParticularResolutionManager.Instance.Register(this);
					ObstacleManager.Instance.UpdateBoundsCrossingObstacles(Bounds);
				}
			};
		}

		private void Unregister()
		{
			if (Nav3DManager.Inited)
			{
				if (!ObstacleParticularResolutionManager.Doomed)
				{
					ObstacleParticularResolutionManager.Instance.Unregister(this);
				}
				if (!ObstacleManager.Doomed)
				{
					ObstacleManager.Instance.UpdateBoundsCrossingObstacles(Bounds);
				}
			}
		}

		private void Initialize()
		{
			m_Transform = base.transform;
			m_Bounds = new Bounds(m_Transform.position, new Vector3(m_SizeX, m_SizeY, m_SizeZ));
		}

		private void Awake()
		{
			Initialize();
		}

		private void Update()
		{
			if (m_Transform.hasChanged)
			{
				m_Bounds = new Bounds(m_Transform.position, new Vector3(m_SizeX, m_SizeY, m_SizeZ));
				m_Transform.hasChanged = false;
			}
		}

		private void OnEnable()
		{
			Register();
		}

		private void OnDisable()
		{
			Unregister();
		}
	}
}
