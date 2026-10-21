using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Nav3D.Common
{
	public class ThreadDispatcher : MonoBehaviour
	{
		private readonly ConcurrentQueue<Action> m_ActionQueue = new ConcurrentQueue<Action>();

		public static ThreadDispatcher Instance => Singleton<ThreadDispatcher>.Instance;

		public static bool Doomed { get; private set; }

		public void Initialize()
		{
		}

		public void Uninitialize()
		{
			UtilsCommon.SmartDestroy(this);
		}

		public static void BeginInvoke(Action _Action)
		{
			if (_Action != null)
			{
				Instance.m_ActionQueue.Enqueue(_Action);
			}
		}

		public static void BeginInvoke<T>(Action<T> _Action, T _Arg)
		{
			BeginInvoke(() =>
			{
				_Action(_Arg);
			});
		}

		public static void BeginInvoke<T0, T1>(Action<T0, T1> _Action, T0 _Arg0, T1 _Arg1)
		{
			BeginInvoke(() =>
			{
				_Action(_Arg0, _Arg1);
			});
		}

		public static void BeginInvoke<T0, T1, T2>(Action<T0, T1, T2> _Action, T0 _Arg0, T1 _Arg1, T2 _Arg2)
		{
			BeginInvoke(() =>
			{
				_Action(_Arg0, _Arg1, _Arg2);
			});
		}

		private void Update()
		{
			Action result;
			while (Instance.m_ActionQueue.TryDequeue(out result))
			{
				if (result != null)
				{
					try
					{
						result();
					}
					catch (Exception ex)
					{
						Debug.LogErrorFormat("Error on event invocation: {0}", ex);
					}
				}
			}
		}

		private void Awake()
		{
			Doomed = false;
		}

		private void OnDestroy()
		{
			Doomed = true;
		}
	}
}
