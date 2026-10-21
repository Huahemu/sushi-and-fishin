using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nav3D.Common;
using Nav3D.Obstacles;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.API
{
	[ExecuteInEditMode]
	public class Nav3DObstacleLoader : MonoBehaviour
	{
		[Flags]
		public enum LoaderState
		{
			NONE = 0,
			INITIALIZER_NOT_FOUND = 1,
			INITIALIZER_INVALID_SETTINGS = 2,
			SERIALIZABLE_OBSTACLES_FOUND = 4,
			SERIALIZABLE_OBSTACLES_IS_NOT_FOUND = 8,
			SERIALIZED_OBSTACLES_HAS_CHANGED = 0x10,
			SERIALIZED_INITIALIZER_PARAMETERS_HAS_CHANGED = 0x20,
			NOT_SERIALIZED_OBSTACLES_FOUND = 0x40,
			SERIALIZATION_IN_PROGRESS = 0x80,
			SERIALIZATION_COMPLETED = 0x100
		}

		private readonly string INITIALIZER_NOT_FOUND_ERROR = "Serialization error. Nav3DInitializer component is not found on scene.";

		private readonly string NO_SERIALIZABLE_OBSTACLES = "There are no Nav3DObstacle on scene to be serialized.For obstacles that you want to serialize, set the DataSource parameter to LOAD_FROM_BINARY.";

		private const string BINARY_DATA_IS_NULL_OR_EMPTY_ERROR = "Unable to deserialize obstacle. Binary data is empty or corrupt. Rebake binary.";

		private readonly string BINARY_DATA_IS_NOT_ATTACHED_ERROR = "Unable to deserialize obstacle. There is no binary data attached into the field m_BinaryData. Attach a binary.";

		private const string NO_SERIALIZED_OBSTACLES = "No serialized obstacles. Bake obstacles in editor mode.";

		private const string SERIALIZED_OBSTACLES_HAS_CHANGED = "The last serialized data is not valid because one of the baked obstacles was changed or removed. Re-bake obstacles in editor mode.";

		private const string IMPRINT_MISMATCH_ERROR = "Mismatch between the actual obstacles on the scene and the loaded data. The obstacles on the scene were likely changed after baking and serialization. Re-bake scene obstacles.";

		private readonly string MIN_BUCKET_MISMATCH_ERROR = "Mismatch between the actual Nav3DInitializer.MinBucketSize: {0} and loaded MinBucketSizeSerialized: {1}. Re-bake obstacles with the current minimum bucket size.";

		private const string MENU_PATH = "Nav3D/Create Obstacle loader";

		private readonly string INSTANCE_HAS_EXIST_ON_SCENE_ERROR = "An instance of Nav3DObstacleLoader is already present in the scene, duplication is not possible.";

		private static readonly string ON_INIT_UNSUBSCRIBE_ERROR = "There is no need to unsubscribe from the OnLoadingFinished event. All subscriptions will be invoked and unsubscribed after Nav3DObstacleLoader performs obstacles loading.";

		private readonly List<Action> m_OnLoadSubscribers = new List<Action>();

		[SerializeField]
		private TextAsset m_BinaryData;

		[SerializeField]
		private float m_MinBucketSizeSerialized;

		[SerializeField]
		private Nav3DObstacle[] m_SerializedObstacles;

		[SerializeField]
		private bool m_ValidateSerializedData = true;

		public ObstacleDeserializingProgress DeserializingProgress { get; private set; }

		public bool IsObstaclesSerialized
		{
			get
			{
				if (m_BinaryData != null)
				{
					return !m_SerializedObstacles.IsNullOrEmpty();
				}
				return false;
			}
		}

		public bool IsSerializedBinaryAttached => m_BinaryData != null;

		public Nav3DObstacle[] SerializedObstacles => m_SerializedObstacles;

		public float MinBucketSizeSerialized => m_MinBucketSizeSerialized;

		public bool LoadIsFinished { get; private set; }

		private event Action m_OnLoadingFinished;

		public event Action OnLoadingFinished
		{
			add
			{
				if (value == null)
				{
					return;
				}
				if (LoadIsFinished)
				{
					ThreadDispatcher.BeginInvoke(value);
					return;
				}
				Action action = () =>
				{
					ThreadDispatcher.BeginInvoke(value);
				};
				m_OnLoadSubscribers.Add(action);
				m_OnLoadingFinished += action;
			}
			remove
			{
				Debug.LogError(ON_INIT_UNSUBSCRIBE_ERROR);
			}
		}

		public bool IsObstacleSerialized(Nav3DObstacle _Obstacle)
		{
			return m_SerializedObstacles.Contains(_Obstacle);
		}

		public Nav3DObstacle[] GetAllSerializableObstacles()
		{
			return (from _Obstacle in UnityEngine.Object.FindObjectsOfType<Nav3DObstacle>()
				where _Obstacle.gameObject.activeInHierarchy && _Obstacle.DataSource == Nav3DObstacle.ObstacleDataSource.LOAD_FROM_BINARY
				select _Obstacle).ToArray();
		}

		public ObstacleSerializingProgress SerializeObstacles(string _FilePath, Action<Nav3DObstacle[]> _OnFinish)
		{
			Nav3DInitializer nav3DInitializer = UnityEngine.Object.FindObjectOfType<Nav3DInitializer>();
			if (nav3DInitializer == null)
			{
				throw new Exception(INITIALIZER_NOT_FOUND_ERROR);
			}
			Nav3DObstacle[] obstaclesToSerialize = GetAllSerializableObstacles();
			if (!obstaclesToSerialize.Any())
			{
				throw new Exception(NO_SERIALIZABLE_OBSTACLES);
			}
			Nav3DManager.InitNav3DEditMode(nav3DInitializer.MinBucketSize);
			ObstacleSerializingProgress obstacleSerializingProgress = ObstacleSerializingProgress.INITIAL;
			List<ObstacleInfoBase> preparedObstacleInfos = new List<ObstacleInfoBase>(obstaclesToSerialize.Length);
			List<string> resultingImprint = new List<string>(obstaclesToSerialize.Length);
			for (int i = 0; i < obstaclesToSerialize.Length; i++)
			{
				Nav3DObstacle obstacle = obstaclesToSerialize[i];
				List<ObstacleInfoBase> list = obstacle.PrepareObstacleInfos(out var _ProcessablesImprint);
				list.ForEach((ObstacleInfoBase _Info) =>
				{
					_Info.ReplaceID(obstacle.InstanceID, i);
				});
				preparedObstacleInfos.AddRange(list);
				resultingImprint.AddRange(_ProcessablesImprint);
			}
			Task.Factory.StartNew(() =>
			{
				try
				{
					obstacleSerializingProgress.SetStatus(ObstacleSerializingProgress.ObstacleSerializingStatus.BAKING_OBSTACLES);
					ObstacleAdditionProgress obstacleAdditionProgress = null;
					obstacleAdditionProgress = ObstacleManager.Instance.AddObstacles(preparedObstacleInfos, () =>
					{
						if (!obstacleSerializingProgress.CancellationToken.IsCancellationRequested)
						{
							obstacleSerializingProgress.SetStatus(ObstacleSerializingProgress.ObstacleSerializingStatus.PACKING_BAKED_DATA);
							ObstacleDataSerializable obstacleData = new ObstacleDataSerializable(ObstacleManager.Instance.GetObstacleDatas(), resultingImprint.ToArray(), obstacleSerializingProgress);
							if (!obstacleSerializingProgress.CancellationToken.IsCancellationRequested)
							{
								try
								{
									UtilsSerialization.SerializeObstacleData(obstacleData, _FilePath, obstacleSerializingProgress);
								}
								catch (Exception exception2)
								{
									Debug.LogException(exception2);
								}
								if (!obstacleSerializingProgress.CancellationToken.IsCancellationRequested)
								{
									obstacleSerializingProgress.SetStatus(ObstacleSerializingProgress.ObstacleSerializingStatus.FINISHED);
									Debug.Log(obstacleSerializingProgress.GetResultStats());
									_OnFinish?.Invoke(obstaclesToSerialize);
								}
							}
						}
					}, _EditMode: true);
					obstacleSerializingProgress.SetObstacleAdditionProgress(obstacleAdditionProgress);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}, TaskCreationOptions.LongRunning);
			return obstacleSerializingProgress;
		}

		public bool ValidateSerializedObstacles(bool _OnAwake = false)
		{
			return !m_SerializedObstacles.Any((Nav3DObstacle _Obstacle) => _Obstacle == null || !_Obstacle.gameObject.activeInHierarchy || (!_OnAwake && _Obstacle.TransformHasChanged) || _Obstacle.DataSource != Nav3DObstacle.ObstacleDataSource.LOAD_FROM_BINARY);
		}

		private void LoadObstacles()
		{
			if (!m_SerializedObstacles.Any())
			{
				Debug.LogError("No serialized obstacles. Bake obstacles in editor mode.");
				FinishLoading(_Failed: true);
				return;
			}
			if (!ValidateSerializedObstacles(_OnAwake: true))
			{
				Debug.LogError("The last serialized data is not valid because one of the baked obstacles was changed or removed. Re-bake obstacles in editor mode.");
				FinishLoading(_Failed: true);
				return;
			}
			if (m_BinaryData == null)
			{
				Debug.LogError(BINARY_DATA_IS_NOT_ATTACHED_ERROR);
				FinishLoading(_Failed: true);
				return;
			}
			if (m_BinaryData.bytes.IsNullOrEmpty())
			{
				Debug.LogError("Unable to deserialize obstacle. Binary data is empty or corrupt. Rebake binary.");
				FinishLoading(_Failed: true);
				return;
			}
			m_SerializedObstacles.ForEach((Nav3DObstacle _Obstacle) =>
			{
				_Obstacle.Init();
			});
			Nav3DManager.OnNav3DPreInit += () =>
			{
				if (!ValidateInitializerParameters(out var _InitializerMinBucketSize))
				{
					throw new Exception(string.Format(MIN_BUCKET_MISMATCH_ERROR, _InitializerMinBucketSize, m_MinBucketSizeSerialized));
				}
				byte[] binary = m_BinaryData.bytes;
				List<string> actualImprint = new List<string>();
				if (m_ValidateSerializedData)
				{
					Nav3DObstacle[] serializedObstacles = m_SerializedObstacles;
					foreach (Nav3DObstacle nav3DObstacle in serializedObstacles)
					{
						actualImprint.AddRange(nav3DObstacle.GetImprint());
					}
				}
				Task.Factory.StartNew(() =>
				{
					try
					{
						if (!ObstacleManager.Doomed)
						{
							Dictionary<ObstacleInfoBase, Obstacle> dictionary = LoadObstacleData(binary, actualImprint.ToArray());
							foreach (ObstacleInfoBase key in dictionary.Keys)
							{
								for (int j = 0; j < m_SerializedObstacles.Length; j++)
								{
									key.ReplaceID(j, m_SerializedObstacles[j].InstanceID);
								}
							}
							ObstacleManager.Instance.AddBakedObstacles(dictionary, () =>
							{
								Nav3DObstacle[] serializedObstacles2 = m_SerializedObstacles;
								for (int k = 0; k < serializedObstacles2.Length; k++)
								{
									serializedObstacles2[k].InvokeOnObstacleAdded();
								}
								FinishLoading();
							});
						}
					}
					catch (Exception exception)
					{
						DeserializingProgress.Fail();
						Debug.LogException(exception);
						FinishLoading(_Failed: true);
					}
				}, TaskCreationOptions.LongRunning);
			};
		}

		private void FinishLoading(bool _Failed = false)
		{
			if (_Failed)
			{
				DeserializingProgress = ObstacleDeserializingProgress.FAILED;
			}
			MarkAsLoaded();
		}

		private void MarkAsLoaded()
		{
			LoadIsFinished = true;
			m_OnLoadingFinished?.Invoke();
			UnsubscribeOnLoadSubscribers();
		}

		private bool ValidateImprints(string[] _ActualImprint, ObstacleDataSerializable _LoadedData)
		{
			List<string> list = new List<string>(_LoadedData.ImprintData);
			foreach (string imprintHunk in _ActualImprint)
			{
				if (list.RemoveAll((string _String) => string.CompareOrdinal(_String, imprintHunk) == 0) == 0)
				{
					return false;
				}
			}
			return list.Count == 0;
		}

		private bool ValidateInitializerParameters(out float _InitializerMinBucketSize)
		{
			Nav3DInitializer nav3DInitializer = UnityEngine.Object.FindObjectOfType<Nav3DInitializer>();
			_InitializerMinBucketSize = nav3DInitializer.MinBucketSize;
			return m_MinBucketSizeSerialized == nav3DInitializer.MinBucketSize;
		}

		private Dictionary<ObstacleInfoBase, Obstacle> LoadObstacleData(byte[] _Binary, string[] _ActualImprint)
		{
			DeserializingProgress = new ObstacleDeserializingProgress();
			ObstacleDataSerializable obstacleDataSerializable = UtilsSerialization.DeserializeObstacleData(_Binary, DeserializingProgress);
			if (m_ValidateSerializedData && !ValidateImprints(_ActualImprint, obstacleDataSerializable))
			{
				throw new Exception("Mismatch between the actual obstacles on the scene and the loaded data. The obstacles on the scene were likely changed after baking and serialization. Re-bake scene obstacles.");
			}
			DeserializingProgress.SetStatus(ObstacleDeserializingProgress.ObstacleDeserializingStatus.UNPACKING);
			Dictionary<ObstacleInfoBase, Obstacle> result = obstacleDataSerializable.Unpack(DeserializingProgress);
			DeserializingProgress.SetStatus(ObstacleDeserializingProgress.ObstacleDeserializingStatus.FINISHED);
			return result;
		}

		private void UnsubscribeOnLoadSubscribers()
		{
			foreach (Action onLoadSubscriber in m_OnLoadSubscribers)
			{
				m_OnLoadingFinished -= onLoadSubscriber;
			}
			m_OnLoadSubscribers.Clear();
		}

		private void Awake()
		{
			LoadObstacles();
		}
	}
}
