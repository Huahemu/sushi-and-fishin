using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DynamicMeshCutter
{
	public abstract class CutterBehaviour : MonoBehaviour
	{
		public float Separation = 0.02f;

		[Tooltip("Automatically destroy the original object that is cut, when cut")]
		public bool DestroyTargets = true;

		[Tooltip("Use multiple threads to cut. Drastically reduces lag. Recommend ON")]
		public bool UseAsync = true;

		[Tooltip("If UseAsync=true, designates the amount of threads able to run the cutting algorithm. If left negative, SystemInfo.processorCount-1 will be used. Otherwise the minimum of AsyncThreadAmount and SystemInfo.processorCount-1 will be used.")]
		public int AsyncThreadAmount = -1;

		[Tooltip("Cut objects whose vertices are LESS than this will NOT be created")]
		public int VertexCreationThreshold;

		public Material DefaultMaterial;

		private bool _cutterIsEnabled;

		public static bool ApplicationHasQuit = true;

		private AsycWorker _asyncWorker;

		private List<Info> _successes = new List<Info>();

		private List<Info> _fails = new List<Info>();

		private Queue<Info> _qSuccesses = new Queue<Info>();

		private Queue<Info> _qFails = new Queue<Info>();

		private bool _isInitialized;

		public bool CutterIsEnabled => _cutterIsEnabled;

		public AsycWorker AsyncWorker
		{
			get
			{
				if (_asyncWorker == null)
				{
					InitializeWorker();
				}
				return _asyncWorker;
			}
		}

		private void InitializeWorker()
		{
			if (!_isInitialized)
			{
				_isInitialized = true;
				_asyncWorker = new AsycWorker(this);
				AsycWorker asyncWorker = _asyncWorker;
				asyncWorker.OnCut = (OnCut)Delegate.Combine(asyncWorker.OnCut, new OnCut(OnCut));
			}
		}

		private void Terminate()
		{
			_asyncWorker?.Terminate();
			_asyncWorker = null;
			_isInitialized = false;
			lock (_successes)
			{
				_successes.Clear();
			}
			lock (_fails)
			{
				_fails.Clear();
			}
			_qSuccesses.Clear();
			_qFails.Clear();
		}

		private void OnApplicationQuit()
		{
			ApplicationHasQuit = true;
			Terminate();
		}

		private void Awake()
		{
			ApplicationHasQuit = false;
		}

		private void OnEnable()
		{
			_cutterIsEnabled = true;
		}

		private void OnDisable()
		{
			_cutterIsEnabled = false;
			Terminate();
		}

		protected virtual void Update()
		{
			if (_successes.Count != 0)
			{
				lock (_successes)
				{
					for (int i = 0; i < _successes.Count; i++)
					{
						_qSuccesses.Enqueue(_successes[i]);
					}
					_successes.Clear();
				}
			}
			while (_qSuccesses.Count != 0)
			{
				Info info = _qSuccesses.Dequeue();
				info.OnCutCallback?.Invoke(success: true, info);
				CreateGameObjects(info);
			}
			if (_fails.Count != 0)
			{
				lock (_fails)
				{
					for (int j = 0; j < _fails.Count; j++)
					{
						_qFails.Enqueue(_fails[j]);
					}
					_fails.Clear();
				}
			}
			while (_qFails.Count != 0)
			{
				Info info2 = _qFails.Dequeue();
				info2.OnCutCallback?.Invoke(success: false, info2);
			}
		}

		public void Cut(MeshTarget target, Vector3 worldPosition, Vector3 worldNormal, OnCut onCut = null, OnCreated onCreated = null, object boxedUserData = null)
		{
			if (!target.isActiveAndEnabled)
			{
				return;
			}
			Matrix4x4 matrix4x = target.transform.worldToLocalMatrix;
			if (target.RequireLocal)
			{
				matrix4x = Matrix4x4.Scale(target.transform.lossyScale) * matrix4x;
			}
			Vector4 vector = new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 1f);
			Vector3 localPosition = matrix4x * vector;
			Vector3 normalized = matrix4x.inverse.transpose.MultiplyVector(worldNormal).normalized;
			VirtualPlane plane = new VirtualPlane(localPosition, normalized, worldPosition, worldNormal);
			Info info = new Info(target, plane, onCut, onCreated, boxedUserData);
			if (!UseAsync)
			{
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();
				int num = 0;
				VirtualMesh[] array = (info.CreatedMeshes = new MeshCutting().Cut(ref info));
				if (array == null)
				{
					OnCut(success: false, info);
				}
				else
				{
					OnCut(success: true, info);
					num = array.Length;
				}
				stopwatch.Stop();
				UnityEngine.Debug.Log($"Synchronus cut creating {num} meshes took {stopwatch.ElapsedMilliseconds} ms. Success ? {array != null}");
			}
			else
			{
				AsyncWorker.Enqeue(info);
			}
		}

		protected virtual void CreateGameObjects(Info info)
		{
			MeshCreationData meshCreationData = MeshCreation.CreateObjects(info, DefaultMaterial, VertexCreationThreshold);
			if (meshCreationData == null)
			{
				info.OnCreatedCallback?.Invoke(info, null);
				return;
			}
			if (DestroyTargets && (bool)info.MeshTarget)
			{
				info.MeshTarget.transform.position = new Vector3(0f, -10000f, 0f);
				if (info.MeshTarget.GameobjectRoot != null)
				{
					UnityEngine.Object.Destroy(info.MeshTarget.GameobjectRoot, 0f);
				}
				else
				{
					UnityEngine.Object.Destroy(info.MeshTarget.gameObject, 0f);
				}
			}
			for (int i = 0; i < meshCreationData.CreatedObjects.Length; i++)
			{
				if (meshCreationData.CreatedObjects[i] == null)
				{
					UnityEngine.Debug.Log("Dynamic Mesh Cutter: Cut supressed creation of object due to VertexCreationThreshold. Make sure you handle NullReferenceExceptions!");
				}
			}
			info.OnCreatedCallback?.Invoke(info, meshCreationData);
		}

		private void OnCut(bool success, Info info)
		{
			if (success)
			{
				lock (_successes)
				{
					_successes.Add(info);
					return;
				}
			}
			lock (_fails)
			{
				_fails.Add(info);
			}
		}
	}
}
