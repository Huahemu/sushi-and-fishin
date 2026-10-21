using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using StylizedWater3;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfboardWorld : NetworkBehaviour
	{
		public WaveProfile waveProfile;

		public Material waterMaterial;

		public Material surfEffectMaterial;

		public Surfboard boardPrefab;

		public Vector3[] spawnPositions;

		public Quaternion[] spawnRotations;

		public Vector3[] spawnScales;

		public Material[] spawnMaterials;

		private readonly NetworkVariable<SurfWave> _wave = new NetworkVariable<SurfWave>();

		private readonly List<Surfboard> _boards = new List<Surfboard>();

		private HeightQuerySystem.Sampler _sampler;

		private Material _samplingMaterial;

		private GameObject _waveVisual;

		private DynamicEffect _effect;

		private float _oldWaterTime;

		private double _nextWaveTime;

		private uint _nextWaveId;

		private int _waveBoardCursor;

		public static SurfboardWorld Instance { get; private set; }

		public double TimeNow
		{
			get
			{
				if (!base.IsSpawned)
				{
					return Time.timeAsDouble;
				}
				return base.NetworkManager.ServerTime.Time;
			}
		}

		public SurfWave ActiveWave => _wave.Value;

		public float WaterLevel
		{
			get
			{
				if (!(OceanFollowBehaviour.Instance != null))
				{
					return 0f;
				}
				return OceanFollowBehaviour.Instance.transform.position.y;
			}
		}

		public override void OnNetworkSpawn()
		{
			Instance = this;
			_oldWaterTime = WaterObject.CustomTime;
			_sampler = new HeightQuerySystem.Sampler();
			_sampler.SetSampleCount(1);
			if (waterMaterial != null)
			{
				_samplingMaterial = new Material(waterMaterial);
			}
			CreateWaveVisual();
			if (base.IsServer)
			{
				_nextWaveTime = TimeNow + 5.0;
				for (int i = 0; i < spawnPositions.Length; i++)
				{
					Surfboard surfboard = UnityEngine.Object.Instantiate(boardPrefab, spawnPositions[i], spawnRotations[i]);
					surfboard.transform.localScale = spawnScales[i];
					surfboard.SpawnIndex = i;
					surfboard.NetworkObject.DontDestroyWithOwner = true;
					surfboard.NetworkObject.Spawn();
					_boards.Add(surfboard);
				}
			}
		}

		private void Update()
		{
			if (!base.IsSpawned)
			{
				return;
			}
			WaterObject.CustomTime = Mathf.Max(0.001f, (float)TimeNow);
			if (base.IsServer && TimeNow >= _nextWaveTime)
			{
				TryScheduleWave();
			}
			SurfWave value = _wave.Value;
			if (!(_waveVisual == null))
			{
				bool flag = value.Envelope(TimeNow) > 0.001f;
				_waveVisual.SetActive(flag);
				if (flag)
				{
					Vector3 position = value.Center(TimeNow);
					position.y = WaterLevel;
					_waveVisual.transform.SetPositionAndRotation(position, Quaternion.LookRotation(value.Direction, Vector3.up));
					_waveVisual.transform.localScale = new Vector3(value.Width, 1f, value.Length);
					_effect.materialInstance.SetFloat("_HeightScale", value.Height * value.Envelope(TimeNow));
				}
			}
		}

		private void TryScheduleWave()
		{
			_nextWaveTime = TimeNow + 2.0;
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || restaurantManager.WorldAnchor == null)
			{
				return;
			}
			for (int i = 0; i < _boards.Count; i++)
			{
				int num = (_waveBoardCursor + i) % _boards.Count;
				Surfboard board = _boards[num];
				if (!(board == null) && board.IsRiding)
				{
					float waterLevel = WaterLevel;
					if (SurfWaveRoute.TryCreate(restaurantManager.WorldAnchor.position, board.transform.position, waterLevel, (Vector3 position) => board.Depth(position, waterLevel), TimeNow, _nextWaveId + 1, out var wave))
					{
						_nextWaveId = wave.Id;
						_wave.Value = wave;
						_waveBoardCursor = (num + 1) % _boards.Count;
						_nextWaveTime = wave.StartTime + (double)wave.Duration + 4.0;
						break;
					}
				}
			}
		}

		public float SampleHeight(Vector3 position)
		{
			float num = WaterLevel;
			if (_sampler != null && waveProfile != null && _samplingMaterial != null)
			{
				_sampler.SetSamplePosition(0, position);
				_sampler.heightValues[0] = num;
				Gerstner.ComputeHeight(_sampler, waveProfile, num, _samplingMaterial);
				num = _sampler.heightValues[0];
			}
			return num + _wave.Value.Sample(position, TimeNow);
		}

		private void CreateWaveVisual()
		{
			if (!(surfEffectMaterial == null))
			{
				_waveVisual = new GameObject("Surf wave surface");
				_waveVisual.transform.SetParent(base.transform, worldPositionStays: false);
				Mesh mesh = new Mesh
				{
					name = "Surf wave footprint"
				};
				mesh.vertices = new Vector3[4]
				{
					new Vector3(-0.5f, 0f, -0.5f),
					new Vector3(-0.5f, 0f, 0.5f),
					new Vector3(0.5f, 0f, 0.5f),
					new Vector3(0.5f, 0f, -0.5f)
				};
				mesh.uv = new Vector2[4]
				{
					Vector2.zero,
					Vector2.up,
					Vector2.one,
					Vector2.right
				};
				mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
				mesh.RecalculateBounds();
				_waveVisual.AddComponent<MeshFilter>().sharedMesh = mesh;
				MeshRenderer meshRenderer = _waveVisual.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = surfEffectMaterial;
				_effect = _waveVisual.AddComponent<DynamicEffect>();
				_effect.renderer = meshRenderer;
				_effect.templateMaterial = surfEffectMaterial;
				_effect.foamAmount = 1.2f;
				_effect.normalStrength = 1f;
				_effect.UpdateMaterial();
			}
		}

		public void ResetBoards(string reason)
		{
			if (!base.IsServer)
			{
				return;
			}
			_wave.Value = default;
			_nextWaveTime = TimeNow + 10.0;
			foreach (Surfboard board in _boards)
			{
				if (board != null && board.IsSpawned)
				{
					board.ServerRelease(SurfExitReason.EndDay);
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			if (Instance == this)
			{
				Instance = null;
				WaterObject.CustomTime = _oldWaterTime;
			}
			if (base.IsServer && base.NetworkManager != null && !base.NetworkManager.ShutdownInProgress)
			{
				foreach (Surfboard board in _boards)
				{
					if (board != null && board.IsSpawned)
					{
						board.NetworkObject.Despawn();
					}
				}
			}
			_boards.Clear();
			_sampler?.Dispose();
			_sampler = null;
			if (_samplingMaterial != null)
			{
				UnityEngine.Object.Destroy(_samplingMaterial);
			}
			if (_waveVisual != null)
			{
				UnityEngine.Object.Destroy(_waveVisual.GetComponent<MeshFilter>().sharedMesh);
				UnityEngine.Object.Destroy(_waveVisual);
			}
			base.OnNetworkDespawn();
		}

		protected override void __initializeVariables()
		{
			if (_wave == null)
			{
				throw new Exception("SurfboardWorld._wave cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_wave.Initialize(this);
			__nameNetworkVariable(_wave, "_wave");
			NetworkVariableFields.Add(_wave);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "SurfboardWorld";
		}
	}
}
