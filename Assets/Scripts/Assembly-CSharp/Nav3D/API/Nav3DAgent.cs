using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nav3D.Agents;
using Nav3D.Common;
using Nav3D.Common.AgentCore;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.API
{
	public class Nav3DAgent : Nav3DAgentBase
	{
		private readonly string LOG_CONFIG_SET = "Nav3DAgent.SetConfig";

		private readonly string LOG_MOVE_TO = "Nav3DAgent.MoveTo: {0}";

		private readonly string LOG_MOVE_TO_POINTS = "Nav3DAgent.MoveToPoints: {0}";

		private readonly string LOG_MOVE_TO_POINTS_TRIMM = "Nav3DAgent.MoveToPoints: Source points sequence was trimmed: {0} -> {1}";

		private readonly string MOVE_TO_POINTS_COUNT_ERROR = "Nav3DAgent.MoveToPoints: Array must contain at least one point.";

		private readonly string LOG_FOLLOW_TARGET = "Nav3DAgent.FollowTarget: Transfrom: {0}:{1}, ToleranceUpdate: {2}, Reach dist: {3}";

		private readonly string LOG_STOP = "Nav3DAgent.StopInternal";

		private const string LOG_DISABLED_ERROR = "Logging is disabled for agent.";

		private readonly string ON_INIT_UNSUBSCRIBE_ERROR = "There is no need to unsubscribe from the OnAgentInit event. All subscriptions will be unsubscribed after Nav3DAgent is initialized.";

		[SerializeField]
		private Nav3DAgentConfig m_Config;

		private readonly List<Action> m_OnInitSubscribers = new List<Action>();

		private readonly List<Action> m_OnInitInternalSubscribers = new List<Action>();

		protected Nav3DAgentConfig m_ConfigInternal;

		protected Nav3DAgentMover m_Mover;

		private MovementLogic m_MovementLogic;

		private AgentOrder m_ProcessingOrder;

		private Log m_Log;

		private Vector3 m_RotationVector;

		public bool Inited { get; private set; }

		public bool Operates => m_MovementLogic?.IsValid ?? false;

		public Transform Transform => m_CachedTransform;

		public float Radius => m_ConfigInternal?.Radius ?? 0f;

		public Vector3[] CurrentPath => m_MovementLogic?.CurrentPath ?? new Vector3[0];

		public Nav3DAgentConfig Config => m_Config ?? m_ConfigInternal;

		private event Action OnAgentInitInternal
		{
			add
			{
				if (value != null)
				{
					if (Inited)
					{
						value();
						return;
					}
					m_OnInitInternalSubscribers.Add(value);
					m_OnAgentInitInternal += value;
				}
			}
			remove
			{
				m_OnInitInternalSubscribers.Remove(value);
				m_OnAgentInitInternal -= value;
			}
		}

		public event Action OnAgentInit
		{
			add
			{
				if (value == null)
				{
					return;
				}
				if (Inited)
				{
					ThreadDispatcher.BeginInvoke(value);
					return;
				}
				Action action = () =>
				{
					ThreadDispatcher.BeginInvoke(value);
				};
				m_OnInitSubscribers.Add(action);
				m_OnAgentInit += action;
			}
			remove
			{
				Debug.LogError(ON_INIT_UNSUBSCRIBE_ERROR);
			}
		}

		public event Action<PathfindingResult> OnPathUpdated;

		private event Action m_OnAgentInit;

		private event Action m_OnAgentInitInternal;

		public void SetConfig(Nav3DAgentConfig _Config)
		{
			Nav3DManager.CheckInitedSoft();
			OnAgentInitInternal += () =>
			{
				m_ConfigInternal = (m_Config = _Config);
				m_ConfigInternal.FixInvalidParams();
				InitLog();
				InitMover();
				m_Log?.Write(LOG_CONFIG_SET);
			};
		}

		public void MoveTo(Vector3 _Point, Action _OnReach = null, Action _OnPathReady = null, Action<PathfindingResult> _OnPathfindingFail = null, float? _ReachDistance = null, bool _InterruptCurrentOrderImmediate = false)
		{
			Nav3DManager.CheckInitedSoft();
			OnAgentInitInternal += () =>
			{
				m_Log?.WriteFormat(LOG_MOVE_TO, _Point.ToStringExt());
				m_ProcessingOrder?.Cancel();
				PathfindingRequest request = new PathfindingRequest(new Vector3[2] { m_CachedTransform.position, _Point }, _Loop: false, _SkipUnpassableTargets: false, _TryRepositionStartIfOccupied: true, m_ConfigInternal.TryRepositionTargetIfOccupied, m_ConfigInternal.PathfindingTimeout, m_ConfigInternal.SmoothPath, m_ConfigInternal.SmoothRatio, $"{base.name}({GetInstanceID()})");
				m_ProcessingOrder = new AgentOrder(request, new PointsFollowingLogic(m_CachedTransform, m_Mover, request, _ReachDistance ?? m_ConfigInternal.TargetReachDistanceSqr, null, _OnReach, InvokeOnPathUpdated, _OnPathfindingFail));
				m_ProcessingOrder.Init((Nav3DPath _Path) =>
				{
					OnAgentOrderBecomeReady(_Path);
					_OnPathReady?.Invoke();
				}, _OnPathfindingFail, m_Log);
				if (_InterruptCurrentOrderImmediate)
				{
					DisposeCurrentMovementLogic();
				}
			};
		}

		public void MoveToPoints(Vector3[] _Points, bool _Loop = false, bool _StartFromClosest = false, bool _SkipUnpassableTargets = false, float? _ReachDistance = null, Action<Vector3> _OnTargetPassed = null, Action _OnFinished = null, Action _OnPathReady = null, Action<PathfindingResult> _OnPathfindingFail = null, bool _InterruptCurrentOrderImmediate = false)
		{
			Nav3DManager.CheckInitedSoft();
			OnAgentInitInternal += () =>
			{
				m_Log?.WriteFormat(LOG_MOVE_TO_POINTS + "\n" + Environment.StackTrace, UtilsCommon.GetPointsString(_Points));
				m_ProcessingOrder?.Cancel();
				if (!_Points.Any())
				{
					throw new Exception(MOVE_TO_POINTS_COUNT_ERROR);
				}
				Vector3[] array = UtilsCommon.TrimEqualPoints(_Points);
				if (array.Length < _Points.Length)
				{
					m_Log?.WriteFormat(LOG_MOVE_TO_POINTS_TRIMM, UtilsCommon.GetPointsString(_Points), UtilsCommon.GetPointsString(array));
				}
				if (_StartFromClosest)
				{
					array = UtilsCommon.ReorderStartingFromClosest(array, m_CachedTransform.position, _Loop);
				}
				PathfindingRequest request = new AgentPathfindingRequest(array, m_CachedTransform.position, _Loop, _SkipUnpassableTargets, _TryRepositionStartIfOccupied: true, m_ConfigInternal.TryRepositionTargetIfOccupied, m_ConfigInternal.PathfindingTimeout, m_ConfigInternal.SmoothPath, m_ConfigInternal.SmoothRatio, $"{base.name}({GetInstanceID()})");
				m_ProcessingOrder = new AgentOrder(request, new PointsFollowingLogic(m_CachedTransform, m_Mover, request, _ReachDistance ?? m_ConfigInternal.TargetReachDistanceSqr, _OnTargetPassed, _OnFinished, InvokeOnPathUpdated, _OnPathfindingFail));
				m_ProcessingOrder.Init((Nav3DPath _Path) =>
				{
					OnAgentOrderBecomeReady(_Path);
					_OnPathReady?.Invoke();
				}, _OnPathfindingFail, m_Log);
				if (_InterruptCurrentOrderImmediate)
				{
					DisposeCurrentMovementLogic();
				}
			};
		}

		public void FollowTarget(Transform _Target, bool _FollowContinuously = false, float _TargetOffsetUpdate = 0.1f, float _TargetReachDistance = 0f, Action _OnReach = null, Action _OnFail = null, Action _OnPathReady = null, Action<PathfindingResult> _OnPathfindingFail = null, bool _InterruptCurrentOrderImmediate = false)
		{
			Nav3DManager.CheckInitedSoft();
			OnAgentInitInternal += () =>
			{
				if (m_ConfigInternal.UseLog)
				{
					m_Log?.WriteFormat(LOG_FOLLOW_TARGET, _Target.name, _Target.GetInstanceID(), _TargetOffsetUpdate, _TargetReachDistance);
				}
				m_ProcessingOrder?.Cancel();
				PathfindingRequest request = new PathfindingRequest(new Vector3[2] { m_CachedTransform.position, _Target.position }, _Loop: false, _SkipUnpassableTargets: false, _TryRepositionStartIfOccupied: true, m_ConfigInternal.TryRepositionTargetIfOccupied, m_ConfigInternal.PathfindingTimeout, m_ConfigInternal.SmoothPath, m_ConfigInternal.SmoothRatio, $"{base.name}({GetInstanceID()})");
				m_ProcessingOrder = new AgentOrder(request, new TargetFollowingLogic(m_CachedTransform, m_Mover, request, _Target, _TargetOffsetUpdate, _TargetReachDistance, _FollowContinuously, _OnReach, InvokeOnPathUpdated, _OnFail, _OnPathfindingFail));
				m_ProcessingOrder.Init((Nav3DPath _Path) =>
				{
					OnAgentOrderBecomeReady(_Path);
					_OnPathReady?.Invoke();
				}, _OnPathfindingFail, m_Log);
				if (_InterruptCurrentOrderImmediate)
				{
					DisposeCurrentMovementLogic();
				}
			};
		}

		public void Stop()
		{
			Nav3DManager.CheckInitedSoft();
			OnAgentInitInternal += StopInternal;
		}

		public Nav3DAgent[] GetAgentsInRadius(float _Radius, Predicate<Nav3DAgent> _Predicate = null)
		{
			Nav3DManager.CheckInitedHard();
			Nav3DAgent[] agentsInSphere = Nav3DAgentManager.GetAgentsInSphere(m_CachedTransform.position, _Radius);
			if (_Predicate == null)
			{
				return agentsInSphere;
			}
			return agentsInSphere.Where((Nav3DAgent _Agent) => _Predicate(_Agent)).ToArray();
		}

		public string GetLogText()
		{
			if (!m_ConfigInternal.UseLog)
			{
				return "Logging is disabled for agent.";
			}
			int _LinesCount;
			return m_Log.GetText(out _LinesCount);
		}

		public string GetInnerStatusString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Nav3DAgent status:");
			stringBuilder.AppendLine(string.Format("{0}: {1}", "m_MovementLogic", m_MovementLogic?.GetType()));
			stringBuilder.AppendLine(string.Format("{0}: {1}: {2}", "m_MovementLogic", "IsValid", m_MovementLogic?.IsValid));
			stringBuilder.Append(m_Mover.GetInnerStatusString() ?? "");
			return stringBuilder.ToString();
		}

		public string GetLastPathfindingStatsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Last pathfinding stats:");
			stringBuilder.Append(m_Mover.GetLastPathfindingStatsString() ?? "");
			return stringBuilder.ToString();
		}

		public override void DoFixedUpdate()
		{
			if (Inited)
			{
				DoFrameLogic();
			}
		}

		private void OnAgentOrderBecomeReady(Nav3DPath _Path)
		{
			if (m_ProcessingOrder != null && _Path.IsValid && _Path.PathfindingResult != null)
			{
				DisposeCurrentMovementLogic();
				m_MovementLogic = m_ProcessingOrder.MovementLogic;
				m_MovementLogic.Init(_Path);
				m_ProcessingOrder = null;
			}
		}

		private void DoAwake()
		{
			m_CachedTransform = base.transform;
		}

		private void Initialize()
		{
			if (!Inited)
			{
				AgentManager.Instance.RegisterAgent(this);
				Nav3DManager.OnNav3DInitInternal += () =>
				{
					SetConfig(m_Config?.Copy() ?? Nav3DAgentConfig.DefaultConfig);
					MarkAsInited();
				};
			}
		}

		private void InitLog()
		{
			m_Log = (m_ConfigInternal.UseLog ? new Log($"{base.gameObject.name}: {base.gameObject.GetInstanceID()}", m_ConfigInternal.LogSize) : null);
		}

		protected virtual void InitMover()
		{
			m_Mover?.Uninitialize();
			m_Mover = m_ConfigInternal.MotionNavigationType switch
			{
				MotionNavigationType.GLOBAL => new Nav3DAgentMoverGlobal(m_CachedTransform.position, m_ConfigInternal, this, m_Log), 
				MotionNavigationType.COMBINED => new Nav3DAgentMoverCombined(m_CachedTransform.position, m_ConfigInternal, this, m_Log), 
				_ => throw new ArgumentOutOfRangeException($"Unknown navigation type: {m_ConfigInternal.MotionNavigationType}"), 
			};
		}

		private void Uninitialize()
		{
			Inited = false;
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.UnregisterAgent(this);
			}
			m_ProcessingOrder?.Cancel();
			m_Mover?.Uninitialize();
			m_Log?.Clear();
		}

		private void MarkAsInited()
		{
			Inited = true;
			m_OnAgentInitInternal?.Invoke();
			m_OnAgentInit?.Invoke();
			UnsubscribeOnInitInternalSubscribers();
			UnsubscribeOnInitSubscribers();
		}

		private void UnsubscribeOnInitInternalSubscribers()
		{
			foreach (Action onInitInternalSubscriber in m_OnInitInternalSubscribers)
			{
				m_OnAgentInitInternal -= onInitInternalSubscriber;
			}
			m_OnInitInternalSubscribers.Clear();
		}

		private void UnsubscribeOnInitSubscribers()
		{
			foreach (Action onInitSubscriber in m_OnInitSubscribers)
			{
				m_OnAgentInit -= onInitSubscriber;
			}
			m_OnInitSubscribers.Clear();
		}

		private void StopInternal()
		{
			m_ProcessingOrder?.Cancel();
			m_MovementLogic?.Dispose();
			if (m_ConfigInternal.UseLog)
			{
				m_Log?.Write(LOG_STOP);
			}
		}

		private void DoFrameLogic()
		{
			MovementLogic movementLogic = m_MovementLogic;
			if (movementLogic != null && movementLogic.IsValid)
			{
				m_MovementLogic.DoOnFrameStart();
				Vector3 frameVelocity = m_MovementLogic.GetFrameVelocity();
				m_CachedTransform.position += frameVelocity;
				m_RotationVector = Vector3.Lerp(m_RotationVector, frameVelocity, m_ConfigInternal.RotationVectorLerpFactor);
				if (m_RotationVector != Vector3.zero)
				{
					m_CachedTransform.rotation = Quaternion.RotateTowards(m_CachedTransform.rotation, Quaternion.LookRotation(m_RotationVector), m_ConfigInternal.MaxAgentDegreesRotationPerTick);
				}
				m_MovementLogic.DoOnFrameEnd();
			}
		}

		private void DisposeCurrentMovementLogic()
		{
			m_MovementLogic?.Dispose();
			m_MovementLogic = null;
		}

		private void InvokeOnPathUpdated(PathfindingResult _Result)
		{
			OnPathUpdated?.Invoke(_Result);
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
