using System;
using System.Text;
using UnityEngine;

namespace Nav3D.API
{
	[CreateAssetMenu(fileName = "AgentConfig", menuName = "Nav3D/Agent config", order = 150)]
	public class Nav3DAgentConfig : ScriptableObject
	{
		private readonly string ORCA_TAU_INVALID = "There is invalid ORCATau parameter value: {0}. Correct it, or perform project update.";

		private readonly string VELOCITY_WEIGHTS_INVALID = "Some of the weight values are set incorrectly.  Check the current values in the config. You probably set an incorrect value, or you didn't update the project.";

		private const float DEFAULT_RADIUS = 0.25f;

		private const float DEFAULT_SPEED = 0.01f;

		private const float DEFAULT_SPEED_TO_MAX_SPEED_MULTIPLIER = 1.15f;

		private const int DEFAULT_MAX_SPEED_OBTAIN_MODE = 0;

		private const bool DEFAULT_USE_CONSIDERED_AGENTS_NUMBER_LIMIT = false;

		private const int DEFAULT_CONSIDERED_AGENTS_NUMBER_LIMIT = 3;

		private const int DEFAULT_PATHFINDING_TIMEOUT = 2000;

		private const bool DEFAULT_SMOOTH_PATH = false;

		private const int DEFAULT_SMOOTH_PATH_RATIO = 3;

		private const MotionNavigationType DEFAULT_MOTION_NAV_TYPE = MotionNavigationType.COMBINED;

		private const bool DEFAULT_AUTOUPDATE_PATH = true;

		private const int DEFAULT_AUTOUPDATE_PATH_COOLDOWN = 2000;

		private const float DEFAULT_MAX_DEGREES_ROTATION_PER_TICK = 5f;

		private const float DEFAULT_ROTATION_VECTOR_LERP_FACTOR = 0.1f;

		private const bool DEFAULT_TRY_REPOSITION_TARGET_IF_OCCUPIED = false;

		private const float DEFAULT_ORCA_TAU = 2.5f;

		private const bool DEFAULT_AVOID_STATIC_OBSTACLES = true;

		private const float DEFAULT_PATH_VELOCITY_WEIGHT = 1f;

		private const float DEFAULT_AGENTS_AVOIDANCE_VELOCITY_WEIGHT = 10f;

		private const float DEFAULT_OBSTACLES_AVOIDANCE_VELOCITY_WEIGHT = 5f;

		private const float DEFAULT_PATH_VELOCITY_WEIGHT1 = 1f;

		private const float DEFAULT_OBSTACLES_AVOIDANCE_VELOCITY_WEIGHT1 = 3f;

		private const float DEFAULT_PATH_VELOCITY_WEIGHT2 = 1f;

		private const float DEFAULT_AGENTS_AVOIDANCE_VELOCITY_WEIGHT1 = 10f;

		private const float DEFAULT_OBSTACLES_AVOIDANCE_VELOCITY_WEIGHT2 = 1f;

		private const float DEFAULT_AGENTS_AVOIDANCE_VELOCITY_WEIGHT2 = 1f;

		private const bool DEFAULT_USE_LOG = false;

		private const int DEFAULT_LOG_SIZE = 50;

		[SerializeField]
		private float m_Radius = 0.25f;

		[SerializeField]
		private float m_Speed = 0.01f;

		[SerializeField]
		private float m_MaxSpeed;

		[SerializeField]
		private float m_SpeedToMaxSpeedMultiplier = 1.15f;

		[SerializeField]
		private int m_MaxSpeedObtainMode;

		[SerializeField]
		private bool m_UseConsideredAgentsNumberLimit;

		[SerializeField]
		private int m_ConsideredAgentsNumberLimit = 3;

		[SerializeField]
		private float m_ORCATau = 2.5f;

		[SerializeField]
		private bool m_AvoidStaticObstacles = true;

		[SerializeField]
		private int m_PathfindingTimeout = 2000;

		[SerializeField]
		private bool m_SmoothPath;

		[SerializeField]
		private int m_SmoothRatio = 3;

		[SerializeField]
		private MotionNavigationType m_MotionNavigationType;

		[SerializeField]
		private float m_TargetReachDistance = 0.25f;

		[SerializeField]
		private bool m_AutoUpdatePath = true;

		[SerializeField]
		private int m_PathAutoUpdateCooldown = 2000;

		[SerializeField]
		private float m_MaxAgentDegreesRotationPerTick = 5f;

		[SerializeField]
		private float m_RotationVectorLerpFactor = 0.1f;

		[SerializeField]
		private bool m_TryRepositionTargetIfOccupied;

		[SerializeField]
		private float m_PathVelocityWeight = 1f;

		[SerializeField]
		private float m_AgentsAvoidanceVelocityWeight = 10f;

		[SerializeField]
		private float m_ObstacleAvoidanceVelocityWeight = 5f;

		[SerializeField]
		private float m_PathVelocityWeight1 = 1f;

		[SerializeField]
		private float m_ObstacleAvoidanceVelocityWeight1 = 3f;

		[SerializeField]
		private float m_PathVelocityWeight2 = 1f;

		[SerializeField]
		private float m_AgentsAvoidanceVelocityWeight1 = 3f;

		[SerializeField]
		private float m_AgentsAvoidanceVelocityWeight2 = 1f;

		[SerializeField]
		private float m_ObstacleAvoidanceVelocityWeight2 = 1f;

		[SerializeField]
		private bool m_UseLog;

		[SerializeField]
		private int m_LogSize = 50;

		private float? m_TargetReachDistanceSqr;

		private float m_VelocityDangerRadiusTau;

		private float m_VelocityDangerRadius;

		private float m_VelocityDangerRadiusSqr;

		private bool m_VelocityDangerRadiusTauIsDirty = true;

		private bool m_VelocityDangerRadiusIsDirty = true;

		public float Radius
		{
			get
			{
				return m_Radius;
			}
			set
			{
				if (!(value <= 0f) && !Mathf.Approximately(m_Radius, value))
				{
					m_Radius = value;
					m_VelocityDangerRadiusTauIsDirty = true;
					m_VelocityDangerRadiusIsDirty = true;
				}
			}
		}

		public float Speed
		{
			get
			{
				return m_Speed;
			}
			set
			{
				if (value <= 0f || Mathf.Approximately(m_Speed, value))
				{
					return;
				}
				if (m_MaxSpeedObtainMode == 0)
				{
					m_Speed = value;
					MaxSpeed = m_Speed * m_SpeedToMaxSpeedMultiplier;
				}
				else
				{
					if (m_MaxSpeedObtainMode != 1)
					{
						throw new Exception($"Unknown m_MaxSpeedObtainMode type {value}");
					}
					m_Speed = Mathf.Clamp(value, 0f, m_MaxSpeed);
				}
				m_VelocityDangerRadiusTauIsDirty = true;
				m_VelocityDangerRadiusIsDirty = true;
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
				float num = Mathf.Max(value, m_Speed);
				if (!Mathf.Approximately(m_MaxSpeed, num))
				{
					m_MaxSpeed = num;
					m_VelocityDangerRadiusTauIsDirty = true;
					m_VelocityDangerRadiusIsDirty = true;
				}
			}
		}

		public float SqrMaxSpeed => MaxSpeed * MaxSpeed;

		public float FactualMaxSpeed
		{
			get
			{
				if (MotionNavigationType == MotionNavigationType.GLOBAL)
				{
					return Speed;
				}
				return MaxSpeed;
			}
		}

		public float SpeedToMaxSpeedMultiplier
		{
			get
			{
				return m_SpeedToMaxSpeedMultiplier;
			}
			set
			{
				m_SpeedToMaxSpeedMultiplier = Mathf.Max(1f, value);
			}
		}

		public int MaxSpeedObtainMode
		{
			get
			{
				return m_MaxSpeedObtainMode;
			}
			set
			{
				m_MaxSpeedObtainMode = value;
			}
		}

		public bool UseConsideredAgentsNumberLimit
		{
			get
			{
				return m_UseConsideredAgentsNumberLimit;
			}
			set
			{
				m_UseConsideredAgentsNumberLimit = value;
			}
		}

		public int ConsideredAgentsNumberLimit
		{
			get
			{
				return m_ConsideredAgentsNumberLimit;
			}
			set
			{
				m_ConsideredAgentsNumberLimit = Mathf.Max(1, value);
			}
		}

		public int PathfindingTimeout
		{
			get
			{
				return m_PathfindingTimeout;
			}
			set
			{
				if (value > 0 && m_PathfindingTimeout != value)
				{
					m_PathfindingTimeout = value;
				}
			}
		}

		public bool SmoothPath
		{
			get
			{
				return m_SmoothPath;
			}
			set
			{
				m_SmoothPath = value;
			}
		}

		public int SmoothRatio
		{
			get
			{
				return m_SmoothRatio;
			}
			set
			{
				if (value > 0 && m_SmoothRatio != value)
				{
					m_SmoothRatio = value;
				}
			}
		}

		public MotionNavigationType MotionNavigationType
		{
			get
			{
				return m_MotionNavigationType;
			}
			set
			{
				m_MotionNavigationType = value;
				m_VelocityDangerRadiusTauIsDirty = true;
				m_VelocityDangerRadiusIsDirty = true;
			}
		}

		public float TargetReachDistance
		{
			get
			{
				return m_TargetReachDistance;
			}
			set
			{
				if (!(value < 0f))
				{
					m_TargetReachDistance = value;
					m_TargetReachDistanceSqr = m_TargetReachDistance * m_TargetReachDistance;
				}
			}
		}

		public float TargetReachDistanceSqr
		{
			get
			{
				float valueOrDefault = m_TargetReachDistanceSqr.GetValueOrDefault();
				if (!m_TargetReachDistanceSqr.HasValue)
				{
					valueOrDefault = m_TargetReachDistance * m_TargetReachDistance;
					m_TargetReachDistanceSqr = valueOrDefault;
					return valueOrDefault;
				}
				return valueOrDefault;
			}
		}

		public bool AutoUpdatePath
		{
			get
			{
				return m_AutoUpdatePath;
			}
			set
			{
				m_AutoUpdatePath = value;
			}
		}

		public int PathAutoUpdateCooldown
		{
			get
			{
				return m_PathAutoUpdateCooldown;
			}
			set
			{
				m_PathAutoUpdateCooldown = Mathf.Max(1, value);
			}
		}

		public float MaxAgentDegreesRotationPerTick
		{
			get
			{
				return m_MaxAgentDegreesRotationPerTick;
			}
			set
			{
				m_MaxAgentDegreesRotationPerTick = Mathf.Max(0f, value);
			}
		}

		public float RotationVectorLerpFactor
		{
			get
			{
				return m_RotationVectorLerpFactor;
			}
			set
			{
				m_RotationVectorLerpFactor = Mathf.Max(0f, value);
			}
		}

		public bool TryRepositionTargetIfOccupied
		{
			get
			{
				return m_TryRepositionTargetIfOccupied;
			}
			set
			{
				m_TryRepositionTargetIfOccupied = value;
			}
		}

		public float PathVelocityWeight
		{
			get
			{
				return m_PathVelocityWeight;
			}
			set
			{
				m_PathVelocityWeight = Mathf.Max(value, float.Epsilon);
			}
		}

		public float PathVelocityWeight1
		{
			get
			{
				return m_PathVelocityWeight1;
			}
			set
			{
				m_PathVelocityWeight1 = Mathf.Max(value, float.Epsilon);
			}
		}

		public float PathVelocityWeight2
		{
			get
			{
				return m_PathVelocityWeight2;
			}
			set
			{
				m_PathVelocityWeight2 = Mathf.Max(value, float.Epsilon);
			}
		}

		public float AgentsAvoidanceVelocityWeight
		{
			get
			{
				return m_AgentsAvoidanceVelocityWeight;
			}
			set
			{
				m_AgentsAvoidanceVelocityWeight = Mathf.Max(value, float.Epsilon);
			}
		}

		public float AgentsAvoidanceVelocityWeight1
		{
			get
			{
				return m_AgentsAvoidanceVelocityWeight1;
			}
			set
			{
				m_AgentsAvoidanceVelocityWeight1 = Mathf.Max(value, float.Epsilon);
			}
		}

		public float AgentsAvoidanceVelocityWeight2
		{
			get
			{
				return m_AgentsAvoidanceVelocityWeight2;
			}
			set
			{
				m_AgentsAvoidanceVelocityWeight2 = Mathf.Max(value, float.Epsilon);
			}
		}

		public float ObstaclesAvoidanceVelocityWeight
		{
			get
			{
				return m_ObstacleAvoidanceVelocityWeight;
			}
			set
			{
				m_ObstacleAvoidanceVelocityWeight = Mathf.Max(value, float.Epsilon);
			}
		}

		public float ObstaclesAvoidanceVelocityWeight1
		{
			get
			{
				return m_ObstacleAvoidanceVelocityWeight1;
			}
			set
			{
				m_ObstacleAvoidanceVelocityWeight1 = Mathf.Max(value, float.Epsilon);
			}
		}

		public float ObstaclesAvoidanceVelocityWeight2
		{
			get
			{
				return m_ObstacleAvoidanceVelocityWeight2;
			}
			set
			{
				m_ObstacleAvoidanceVelocityWeight2 = Mathf.Max(value, float.Epsilon);
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
				if (!Mathf.Approximately(m_ORCATau, value) && !(m_ORCATau <= 0f))
				{
					m_ORCATau = value;
					m_VelocityDangerRadiusTauIsDirty = true;
				}
			}
		}

		public bool AvoidStaticObstacles
		{
			get
			{
				return m_AvoidStaticObstacles;
			}
			set
			{
				m_AvoidStaticObstacles = value;
			}
		}

		public float VelocityRadius
		{
			get
			{
				if (m_VelocityDangerRadiusIsDirty)
				{
					m_VelocityDangerRadius = Radius + FactualMaxSpeed;
					m_VelocityDangerRadiusSqr = m_VelocityDangerRadius * m_VelocityDangerRadius;
					m_VelocityDangerRadiusIsDirty = false;
				}
				return m_VelocityDangerRadius;
			}
		}

		public float VelocityRadiusSqr => m_VelocityDangerRadiusSqr;

		public bool UseLog
		{
			get
			{
				return m_UseLog;
			}
			set
			{
				m_UseLog = value;
			}
		}

		public int LogSize
		{
			get
			{
				return m_LogSize;
			}
			set
			{
				int logSize = Mathf.Max(value, 5);
				m_LogSize = logSize;
			}
		}

		public static Nav3DAgentConfig DefaultConfig => ScriptableObject.CreateInstance(typeof(Nav3DAgentConfig)) as Nav3DAgentConfig;

		public void SetDefaultAttributes()
		{
			m_Radius = 0.25f;
			m_Speed = 0.01f;
			m_SpeedToMaxSpeedMultiplier = 1.15f;
			m_MaxSpeedObtainMode = 0;
			m_UseConsideredAgentsNumberLimit = false;
			m_ConsideredAgentsNumberLimit = 3;
			m_TargetReachDistance = m_Radius;
			m_PathfindingTimeout = 2000;
			m_SmoothPath = false;
			m_SmoothRatio = 3;
			m_MotionNavigationType = MotionNavigationType.COMBINED;
			m_AutoUpdatePath = true;
			m_PathAutoUpdateCooldown = 2000;
			m_MaxAgentDegreesRotationPerTick = 5f;
			m_RotationVectorLerpFactor = 0.1f;
			m_TryRepositionTargetIfOccupied = false;
			m_PathVelocityWeight = 1f;
			m_PathVelocityWeight1 = 1f;
			m_PathVelocityWeight2 = 1f;
			m_AgentsAvoidanceVelocityWeight = 10f;
			m_AgentsAvoidanceVelocityWeight1 = 10f;
			m_AgentsAvoidanceVelocityWeight2 = 1f;
			m_ObstacleAvoidanceVelocityWeight = 5f;
			m_ObstacleAvoidanceVelocityWeight1 = 3f;
			m_ObstacleAvoidanceVelocityWeight2 = 1f;
			m_ORCATau = 2.5f;
			m_AvoidStaticObstacles = true;
			m_UseLog = false;
			m_LogSize = 50;
		}

		public void SetDefaultORCATau()
		{
			m_ORCATau = 2.5f;
		}

		public void SetDefaultTargetReachDistance()
		{
			m_TargetReachDistance = m_Radius;
		}

		public void SetDefaultPathVelocityWeight1()
		{
			m_PathVelocityWeight1 = 1f;
		}

		public void SetDefaultObstacleAvoidanceVelocityWeihgt1()
		{
			m_ObstacleAvoidanceVelocityWeight1 = 3f;
		}

		public void SetDefaultPathVelocityWeight2()
		{
			m_PathVelocityWeight2 = 1f;
		}

		public void SetDefaultAgentsAvoidanceVelocityWeight1()
		{
			m_AgentsAvoidanceVelocityWeight1 = 10f;
		}

		public void SetDefaultAgentsAvoidanceVelocityWeight2()
		{
			m_AgentsAvoidanceVelocityWeight2 = 1f;
		}

		public void SetDefaultObstacleAvoidanceVelocityWeight2()
		{
			m_ObstacleAvoidanceVelocityWeight2 = 1f;
		}

		public void SetDefaultVelocitiesBlendingWeights()
		{
			m_PathVelocityWeight = 1f;
			m_PathVelocityWeight1 = 1f;
			m_PathVelocityWeight2 = 1f;
			m_ObstacleAvoidanceVelocityWeight = 5f;
			m_ObstacleAvoidanceVelocityWeight1 = 3f;
			m_ObstacleAvoidanceVelocityWeight2 = 1f;
			m_AgentsAvoidanceVelocityWeight = 10f;
			m_AgentsAvoidanceVelocityWeight1 = 10f;
			m_AgentsAvoidanceVelocityWeight2 = 1f;
		}

		public void FixInvalidParams(bool _Verbose = true)
		{
			if (m_ORCATau <= 0f)
			{
				if (_Verbose)
				{
					Debug.LogWarning(string.Format(ORCA_TAU_INVALID, ORCATau));
				}
				SetDefaultORCATau();
			}
			if (m_PathVelocityWeight1 == 0f || m_PathVelocityWeight2 == 0f || m_AgentsAvoidanceVelocityWeight1 == 0f || m_AgentsAvoidanceVelocityWeight2 == 0f || m_ObstacleAvoidanceVelocityWeight1 == 0f || m_ObstacleAvoidanceVelocityWeight2 == 0f)
			{
				if (_Verbose)
				{
					Debug.LogWarning(VELOCITY_WEIGHTS_INVALID);
				}
				SetDefaultVelocitiesBlendingWeights();
			}
		}

		public Nav3DAgentConfig Copy()
		{
			Nav3DAgentConfig obj = ScriptableObject.CreateInstance(typeof(Nav3DAgentConfig)) as Nav3DAgentConfig;
			obj.m_Radius = m_Radius;
			obj.m_MaxSpeedObtainMode = m_MaxSpeedObtainMode;
			obj.m_SpeedToMaxSpeedMultiplier = m_SpeedToMaxSpeedMultiplier;
			obj.m_Speed = m_Speed;
			obj.m_MaxSpeed = m_MaxSpeed;
			obj.m_UseConsideredAgentsNumberLimit = m_UseConsideredAgentsNumberLimit;
			obj.m_ConsideredAgentsNumberLimit = m_ConsideredAgentsNumberLimit;
			obj.m_TargetReachDistance = m_TargetReachDistance;
			obj.m_PathfindingTimeout = m_PathfindingTimeout;
			obj.m_SmoothPath = m_SmoothPath;
			obj.m_SmoothRatio = m_SmoothRatio;
			obj.m_MotionNavigationType = m_MotionNavigationType;
			obj.m_AutoUpdatePath = m_AutoUpdatePath;
			obj.m_PathAutoUpdateCooldown = m_PathAutoUpdateCooldown;
			obj.m_MaxAgentDegreesRotationPerTick = m_MaxAgentDegreesRotationPerTick;
			obj.m_RotationVectorLerpFactor = m_RotationVectorLerpFactor;
			obj.m_TryRepositionTargetIfOccupied = m_TryRepositionTargetIfOccupied;
			obj.m_PathVelocityWeight = m_PathVelocityWeight;
			obj.m_PathVelocityWeight1 = m_PathVelocityWeight1;
			obj.m_PathVelocityWeight2 = m_PathVelocityWeight2;
			obj.m_AgentsAvoidanceVelocityWeight = m_AgentsAvoidanceVelocityWeight;
			obj.m_AgentsAvoidanceVelocityWeight1 = m_AgentsAvoidanceVelocityWeight1;
			obj.m_AgentsAvoidanceVelocityWeight2 = m_AgentsAvoidanceVelocityWeight2;
			obj.m_ObstacleAvoidanceVelocityWeight = m_ObstacleAvoidanceVelocityWeight;
			obj.m_ObstacleAvoidanceVelocityWeight1 = m_ObstacleAvoidanceVelocityWeight1;
			obj.m_ObstacleAvoidanceVelocityWeight2 = m_ObstacleAvoidanceVelocityWeight2;
			obj.m_ORCATau = m_ORCATau;
			obj.m_AvoidStaticObstacles = m_AvoidStaticObstacles;
			obj.m_UseLog = m_UseLog;
			obj.m_LogSize = m_LogSize;
			return obj;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_Radius", m_Radius));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_Speed", m_Radius));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_MaxSpeed", m_MaxSpeed));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_SpeedToMaxSpeedMultiplier", m_SpeedToMaxSpeedMultiplier));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_MaxSpeedObtainMode", m_MaxSpeedObtainMode));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_UseConsideredAgentsNumberLimit", m_UseConsideredAgentsNumberLimit));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_ConsideredAgentsNumberLimit", m_ConsideredAgentsNumberLimit));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_PathfindingTimeout", m_PathfindingTimeout));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_SmoothPath", m_SmoothPath));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_SmoothRatio", m_SmoothRatio));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_MotionNavigationType", m_MotionNavigationType));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_TargetReachDistance", m_TargetReachDistance));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_AutoUpdatePath", m_AutoUpdatePath));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_PathAutoUpdateCooldown", m_PathAutoUpdateCooldown));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_MaxAgentDegreesRotationPerTick", m_MaxAgentDegreesRotationPerTick));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_RotationVectorLerpFactor", m_RotationVectorLerpFactor));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_TryRepositionTargetIfOccupied", m_TryRepositionTargetIfOccupied));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_PathVelocityWeight", m_PathVelocityWeight));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_PathVelocityWeight1", m_PathVelocityWeight1));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_PathVelocityWeight2", m_PathVelocityWeight2));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_AgentsAvoidanceVelocityWeight", m_AgentsAvoidanceVelocityWeight));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_AgentsAvoidanceVelocityWeight1", m_AgentsAvoidanceVelocityWeight1));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_AgentsAvoidanceVelocityWeight2", m_AgentsAvoidanceVelocityWeight2));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_ObstacleAvoidanceVelocityWeight", m_ObstacleAvoidanceVelocityWeight));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_ObstacleAvoidanceVelocityWeight1", m_ObstacleAvoidanceVelocityWeight1));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_ObstacleAvoidanceVelocityWeight2", m_ObstacleAvoidanceVelocityWeight2));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_ORCATau", m_ORCATau));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_AvoidStaticObstacles", m_AvoidStaticObstacles));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_UseLog", m_UseLog));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_LogSize", m_LogSize));
			return stringBuilder.ToString();
		}
	}
}
