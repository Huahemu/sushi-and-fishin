using System;
using Nav3D.Agents;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.API
{
	public sealed class Nav3DEvader : Nav3DAgentBase
	{
		[SerializeField]
		private float m_Radius;

		[SerializeField]
		private float m_MaxSpeed;

		[SerializeField]
		private float m_ORCATau;

		[SerializeField]
		private float m_SpeedDecayFactor = 0.25f;

		private Nav3DEvaderMover m_Mover;

		private Vector3 m_PositionLast;

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

		public float MaxSpeed
		{
			get
			{
				return m_MaxSpeed;
			}
			set
			{
				m_MaxSpeed = value;
				Uninitialize();
				Initialize();
			}
		}

		public float ORCATau
		{
			get
			{
				return m_ORCATau;
			}
			set
			{
				m_ORCATau = value;
				Uninitialize();
				Initialize();
			}
		}

		public float SpeedDecayFactor
		{
			get
			{
				return m_SpeedDecayFactor;
			}
			set
			{
				m_SpeedDecayFactor = value;
				Uninitialize();
				Initialize();
			}
		}

		public override void DoFixedUpdate()
		{
			if (m_CachedTransform.position != m_Mover.GetPosition())
			{
				m_Mover.SetCurrentPosition(m_CachedTransform.position);
			}
			Vector3 vector = m_CachedTransform.position + m_Mover.GetVelocity();
			m_CachedTransform.position = vector;
			m_Mover.SetCurrentPosition(vector);
			if (m_PositionLast != vector)
			{
				m_PositionLast = vector;
				OnPositionChanged?.Invoke(m_PositionLast);
			}
		}

		private void DoAwake()
		{
			m_CachedTransform = base.transform;
		}

		private void CheckSettings()
		{
			m_Radius = Mathf.Max(1E-05f, m_Radius);
			m_MaxSpeed = Mathf.Max(1E-05f, m_MaxSpeed);
			m_ORCATau = Mathf.Max(1f, m_ORCATau);
			m_SpeedDecayFactor = Mathf.Max(1E-06f, m_SpeedDecayFactor);
		}

		private void Initialize()
		{
			if (!m_IsInited)
			{
				CheckSettings();
				AgentManager.Instance.RegisterAgent(this);
				Nav3DManager.OnNav3DInitInternal += () =>
				{
					m_Mover = new Nav3DEvaderMover(m_CachedTransform.position, m_Radius, m_MaxSpeed, m_ORCATau, m_SpeedDecayFactor);
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

		private void Awake()
		{
			DoAwake();
		}

		private void OnEnable()
		{
			Initialize();
		}

		private void OnDestroy()
		{
			Uninitialize();
		}

		private void OnDisable()
		{
			Uninitialize();
		}
	}
}
