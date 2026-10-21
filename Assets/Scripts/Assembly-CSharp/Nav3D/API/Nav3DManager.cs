using System;
using System.Collections.Generic;
using Nav3D.Agents;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.Obstacles;
using Nav3D.Pathfinding;
using UnityEngine;

namespace Nav3D.API
{
	public static class Nav3DManager
	{
		public class Nav3DManagerNotInitializedException : Exception
		{
			public Nav3DManagerNotInitializedException()
				: base(NEED_INIT_ERROR)
			{
			}
		}

		private const int PATH_STORAGE_BUCKET_SIZE_FACTOR = 3;

		private static readonly string NEED_INIT_ERROR = "Nav3DManager needs to be initialized before! All your work with Nav3D entities should begin on the Nav3DManager.OnNav3DInit event execution.";

		private static readonly string ON_INIT_UNSUBSCRIBE_ERROR = "There is no need to unsubscribe from the OnNav3DInit event. All subscriptions will be invoked and unsubscribed after Nav3DManager is initialized.";

		private static readonly string ON_PRE_INIT_UNSUBSCRIBE_ERROR = "There is no need to unsubscribe from the OnNav3DPreInit event. All subscriptions will be unsubscribed after Nav3DManager is initialized.";

		private static readonly string NOT_INITIALIZED_WARNING = "Nav3DManager is not initialized yet!";

		private static readonly List<Action> m_OnInitSubscribers = new List<Action>();

		private static readonly List<Action> m_OnInitInternalSubscribers = new List<Action>();

		private static readonly List<Action> m_OnPreInitSubscribers = new List<Action>();

		public static bool Inited { get; private set; }

		public static bool PreInited { get; private set; }

		private static event Action m_OnInit;

		private static event Action m_OnPreInit;

		private static event Action m_OnInitInternal;

		public static event Action OnNav3DInit
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
				m_OnInit += action;
			}
			remove
			{
				Debug.LogError(ON_INIT_UNSUBSCRIBE_ERROR);
			}
		}

		public static event Action OnNav3DInitInternal
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
					m_OnInitInternal += value;
				}
			}
			remove
			{
				m_OnInitInternalSubscribers.Remove(value);
				m_OnInitInternal -= value;
			}
		}

		public static event Action OnNav3DPreInit
		{
			add
			{
				if (value == null)
				{
					return;
				}
				if (PreInited)
				{
					ThreadDispatcher.BeginInvoke(value);
					return;
				}
				Action action = () =>
				{
					ThreadDispatcher.BeginInvoke(value);
				};
				m_OnPreInitSubscribers.Add(action);
				m_OnPreInit += action;
			}
			remove
			{
				Debug.LogError(ON_PRE_INIT_UNSUBSCRIBE_ERROR);
			}
		}

		public static bool IsPointInsideOccupiedVolume(Vector3 _Point)
		{
			if (!Inited)
			{
				return false;
			}
			return ObstacleManager.Instance.IsPointInsideObstacle(_Point);
		}

		public static void InitNav3DRuntime(float _MinBucketSize)
		{
			if (_MinBucketSize <= 0f)
			{
				throw new ArgumentException("[Nav3DManager]: Init error. _MinBucketSize value must be greater than zero.");
			}
			ThreadDispatcher.Instance.Initialize();
			ObstacleManager.Instance.Initialize(_MinBucketSize);
			PathfindingManager.Instance.Initialize(_MinBucketSize * 3f);
			AgentManager.Instance.Initialize(new List<Nav3DAgentMover>());
			ObstacleParticularResolutionManager.Instance.Initialize();
			MarksAsPreInited();
			Nav3DObstacleLoader nav3DObstacleLoader = UnityEngine.Object.FindObjectOfType<Nav3DObstacleLoader>();
			if (nav3DObstacleLoader != null && nav3DObstacleLoader.enabled)
			{
				nav3DObstacleLoader.OnLoadingFinished += MarksAsInited;
			}
			else
			{
				MarksAsInited();
			}
		}

		public static void InitNav3DEditMode(float _MinBucketSize)
		{
			if (_MinBucketSize <= 0f)
			{
				throw new ArgumentException("[Nav3DManager]: Init error. _MinBucketSize value must be greater than zero.");
			}
			ThreadDispatcher.Instance.Initialize();
			ObstacleManager.Instance.Initialize(_MinBucketSize);
			ObstacleParticularResolutionManager.Instance.Initialize();
			PathfindingManager.Instance.Initialize(_MinBucketSize * 3f);
			AgentManager.Instance.Initialize(new List<Nav3DAgentMover>());
			UnityEngine.Object.FindObjectsOfType<Nav3DParticularResolutionRegion>().ForEach((Nav3DParticularResolutionRegion _Region) =>
			{
				_Region.InitializeEditMode();
			});
		}

		public static void Dispose3DNav()
		{
			Inited = false;
			PreInited = false;
			UnsubscribeOnInitSubscribers();
			UnsubscribeOnInitInternalSubscribers();
			UnsubscribeOnPreInitSubscribers();
			m_OnInit = null;
			m_OnInitInternal = null;
			m_OnPreInit = null;
			if (!ThreadDispatcher.Doomed)
			{
				ThreadDispatcher.Instance.Uninitialize();
			}
			if (!AgentManager.Doomed)
			{
				AgentManager.Instance.Uninitialize();
			}
			if (!ObstacleManager.Doomed)
			{
				ObstacleManager.Instance.Uninitialize();
			}
			if (!PathfindingManager.Doomed)
			{
				PathfindingManager.Instance.Uninitialize();
			}
			if (!ObstacleParticularResolutionManager.Doomed)
			{
				ObstacleParticularResolutionManager.Instance.Uninitialize();
			}
		}

		public static void Dispose3DNavEditMode()
		{
			ThreadDispatcher.Instance.Uninitialize();
			ObstacleManager.Instance.Uninitialize();
			PathfindingManager.Instance.Uninitialize();
			ObstacleParticularResolutionManager.Instance.Uninitialize();
			AgentManager.Instance.Uninitialize();
		}

		public static void CheckInitedSoft()
		{
			if (!Inited)
			{
				Debug.LogWarning(NOT_INITIALIZED_WARNING);
			}
		}

		public static void CheckInitedHard()
		{
			if (!Inited)
			{
				throw new Nav3DManagerNotInitializedException();
			}
		}

		private static void MarksAsInited()
		{
			Inited = true;
			m_OnInitInternal?.Invoke();
			m_OnInit?.Invoke();
			UnsubscribeOnInitInternalSubscribers();
			UnsubscribeOnInitSubscribers();
		}

		private static void MarksAsPreInited()
		{
			PreInited = true;
			m_OnPreInit?.Invoke();
			UnsubscribeOnPreInitSubscribers();
		}

		private static void UnsubscribeOnInitSubscribers()
		{
			foreach (Action onInitSubscriber in m_OnInitSubscribers)
			{
				m_OnInit -= onInitSubscriber;
			}
			m_OnInitSubscribers.Clear();
		}

		private static void UnsubscribeOnInitInternalSubscribers()
		{
			foreach (Action onInitInternalSubscriber in m_OnInitInternalSubscribers)
			{
				m_OnInitInternal -= onInitInternalSubscriber;
			}
			m_OnInitInternalSubscribers.Clear();
		}

		private static void UnsubscribeOnPreInitSubscribers()
		{
			foreach (Action onPreInitSubscriber in m_OnPreInitSubscribers)
			{
				m_OnPreInit -= onPreInitSubscriber;
			}
			m_OnPreInitSubscribers.Clear();
		}
	}
}
