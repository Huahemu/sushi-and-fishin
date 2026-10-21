using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class AsycWorker
	{
		private class CuttingTask
		{
			public WaitCallback Callback;

			public object Arg;
		}

		public OnCut OnCut;

		private Queue<CuttingTask> _queue = new Queue<CuttingTask>();

		private Thread[] _pool;

		private MeshCutting _meshCut = new MeshCutting();

		private volatile bool _isStopping;

		private readonly object _queueLock = new object();

		private readonly object _callbackLock = new object();

		public AsycWorker(CutterBehaviour behaviour)
		{
			int num = Mathf.Max(1, SystemInfo.processorCount - 1);
			Initialize(Mathf.Clamp((behaviour.AsyncThreadAmount < 0) ? num : behaviour.AsyncThreadAmount, 1, num));
		}

		private void Initialize(int threads)
		{
			_pool = new Thread[threads];
			for (int i = 0; i < threads; i++)
			{
				_pool[i] = new Thread(Thread);
				_pool[i].IsBackground = true;
				_pool[i].Name = "thread" + i;
				_pool[i].Start();
			}
		}

		private void Thread()
		{
			while (true)
			{
				CuttingTask cuttingTask;
				lock (_queueLock)
				{
					while (!_isStopping && _queue.Count == 0)
					{
						Monitor.Wait(_queueLock);
					}
					if (_isStopping)
					{
						break;
					}
					cuttingTask = _queue.Dequeue();
					goto IL_005b;
				}
				IL_005b:
				try
				{
					cuttingTask.Callback(cuttingTask.Arg);
				}
				catch (InvalidOperationException message)
				{
					Debug.LogWarning(message);
				}
			}
			Debug.Log("Dynamic Mesh Cutter: Thread finished successfully!");
		}

		public void Enqeue(Info info)
		{
			lock (_queueLock)
			{
				if (!_isStopping)
				{
					_queue.Enqueue(new CuttingTask
					{
						Callback = AsyncCutting,
						Arg = info
					});
					Monitor.Pulse(_queueLock);
				}
			}
		}

		private void AsyncCutting(object args)
		{
			Info info = args as Info;
			if (info == null)
			{
				return;
			}
			try
			{
				VirtualMesh[] array = _meshCut.Cut(ref info);
				if (!_isStopping)
				{
					info.CreatedMeshes = array;
					PublishResult(array != null, info);
				}
			}
			catch (Exception ex)
			{
				PublishResult(success: false, info);
				Debug.LogError("Async cutter failed." + ex);
			}
		}

		private void PublishResult(bool success, Info info)
		{
			lock (_callbackLock)
			{
				if (!_isStopping)
				{
					OnCut?.Invoke(success, info);
				}
			}
		}

		public void Terminate()
		{
			lock (_callbackLock)
			{
				_isStopping = true;
				OnCut = null;
			}
			lock (_queueLock)
			{
				_queue.Clear();
				Monitor.PulseAll(_queueLock);
			}
		}
	}
}
