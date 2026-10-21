using System;
using UnityEngine;

namespace Nav3D.API
{
	[ExecuteInEditMode]
	public class Nav3DInitializer : MonoBehaviour
	{
		private readonly string INIT_ON_AWAKE_ERROR = "Nav3DInitializer.m_InitOnAwake flag is set to true, so it is not possible to call initialization manually. The Nav3D initialization will happen on the Awake event.";

		private const string MENU_PATH = "Nav3D/Create Initializer";

		private readonly string INSTANCE_HAS_EXIST_ON_SCENE_ERROR = "An instance of Nav3DInitializer is already present in the scene, duplication is not possible.";

		[SerializeField]
		private bool m_InitOnAwake = true;

		[SerializeField]
		private bool m_DisposeOnDestroy = true;

		[SerializeField]
		private float m_MinBucketSize = 0.5f;

		public float MinBucketSize
		{
			get
			{
				return m_MinBucketSize;
			}
			set
			{
				m_MinBucketSize = value;
			}
		}

		public bool IsSettingsValid => m_MinBucketSize > 0f;

		public void Init()
		{
			if (m_InitOnAwake)
			{
				throw new Exception(INIT_ON_AWAKE_ERROR);
			}
			Nav3DManager.InitNav3DRuntime(m_MinBucketSize);
		}

		public void Utilize()
		{
			Nav3DManager.Dispose3DNav();
		}

		private void Awake()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (!m_DisposeOnDestroy)
			{
				UnityEngine.Object.DontDestroyOnLoad(this);
			}
			if (m_InitOnAwake)
			{
				Nav3DManager.InitNav3DRuntime(m_MinBucketSize);
			}
		}

		private void OnDestroy()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (m_DisposeOnDestroy)
			{
				Nav3DManager.Dispose3DNav();
			}
		}
	}
}
