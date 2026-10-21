using System;
using Nav3D.Agents;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.API
{
	public sealed class Nav3DSphereShell : Nav3DAgentBase
	{
		[SerializeField]
		private bool m_DrawRadius;

		[SerializeField]
		private float m_Radius = 0.5f;

		private Nav3DSphereShellMover m_Mover;

		private bool m_IsInited;

		public Action<Vector3> OnPositionChanged;

		public float Radius
		{
			get
			{
				return m_Radius;
			}
			set
			{
				m_Radius = value;
				Uninitialize();
				Initialize();
			}
		}

		private void DoAwake()
		{
			m_CachedTransform = base.transform;
		}

		public override void DoFixedUpdate()
		{
			Vector3 position = m_CachedTransform.position;
			if (position != m_Mover.GetPosition())
			{
				m_Mover.SetCurrentPosition(position);
				OnPositionChanged?.Invoke(position);
			}
		}

		private void Initialize()
		{
			if (!m_IsInited)
			{
				CheckSettings();
				AgentManager.Instance.RegisterAgent(this);
				Nav3DManager.OnNav3DInitInternal += () =>
				{
					m_Mover = new Nav3DSphereShellMover(m_CachedTransform.position, m_Radius);
					m_Mover.Initialize();
					m_IsInited = true;
				};
			}
		}

		private void Uninitialize()
		{
			m_IsInited = false;
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.UnregisterAgent(this);
			}
			m_Mover.Uninitialize();
		}

		private void CheckSettings()
		{
			m_Radius = Mathf.Max(1E-05f, m_Radius);
		}

		private void Awake()
		{
			DoAwake();
		}

		private void OnEnable()
		{
			Initialize();
		}

		private void OnDisable()
		{
			Uninitialize();
		}

		private void OnDestroy()
		{
			Uninitialize();
		}
	}
}
