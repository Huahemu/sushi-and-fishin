using System;
using System.Collections.Generic;
using ECM2.Examples.FirstPerson;
using HoaxGames;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Player
{
	public sealed class SurfaceChecker : MonoBehaviour
	{
		public static class Tags
		{
			public const string Tile = "Tile";

			public const string Grass = "Grass";

			public const string Sand = "Sand";

			public const string Water = "Water";
		}

		public enum SurfaceType
		{
			Tile = 0,
			Grass = 1,
			Sand = 2,
			Water = 3
		}

		[Serializable]
		private struct TerrainLayerMapping
		{
			[Tooltip("Exact TerrainLayer asset name from the Terrain.")]
			public string terrainLayerName;

			public SurfaceType surface;
		}

		[Serializable]
		private struct SurfaceFxSet
		{
			public EffectsPlayer Walk;

			public EffectsPlayer Run;

			public EffectsPlayer Jump;

			public EffectsPlayer Land;
		}

		[Header("References")]
		[SerializeField]
		private PPG_Player _player;

		[SerializeField]
		private FirstPersonCharacterInput _character;

		[Header("Tile FX")]
		[SerializeField]
		private SurfaceFxSet _tileFx;

		[Header("Grass FX")]
		[SerializeField]
		private SurfaceFxSet _grassFx;

		[Header("Sand FX")]
		[SerializeField]
		private SurfaceFxSet _sandFx;

		[Header("Water FX")]
		[SerializeField]
		private SurfaceFxSet _waterFx;

		[Header("Terrain Mapping")]
		[SerializeField]
		private TerrainLayerMapping[] _terrainMappings;

		[Header("Fallback")]
		[SerializeField]
		private SurfaceType _defaultSurface;

		[Header("Debug")]
		[SerializeField]
		private bool _debugLogs;

		private readonly Dictionary<string, SurfaceType> _terrainNameToSurface = new Dictionary<string, SurfaceType>(StringComparer.Ordinal);

		private Terrain[] _activeTerrains = Array.Empty<Terrain>();

		private Terrain _cachedTerrain;

		private TerrainData _cachedTerrainData;

		private SurfaceType[] _cachedLayerIndexToSurface;

		private SurfaceType _currentSurface;

		private int _lastMapX = -1;

		private int _lastMapZ = -1;

		private SurfaceType _lastTerrainSampleSurface;

		private bool _hasLastTerrainSample;

		private const string DEAD_FISH = "DeadFish";

		private void Awake()
		{
			BuildTerrainMappings();
			RefreshTerrainArray();
			_currentSurface = _defaultSurface;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (_player.IsOwner && other.CompareTag("DeadFish"))
			{
				_player.HandleFishSlip(other);
			}
		}

		public void Initialize(FirstPersonCharacterInput firstPersonCharacter)
		{
			_character = firstPersonCharacter;
		}

		public void HandleFootstepLeft(IKResult result)
		{
			LogIKResult("Left", result);
			HandleFootstep(result, "Left");
		}

		public void HandleFootstepRight(IKResult result)
		{
			LogIKResult("Right", result);
			HandleFootstep(result, "Right");
		}

		public void HandleGroundedEntered(GroundedResult result)
		{
			SurfaceType resolvedSurface = ResolveSurface(result.groundedTransform, result.groundedPosition, out var foundSurface, out var resolveSource);
			UpdateCurrentSurface(resolvedSurface, foundSurface);
			if (_debugLogs)
			{
				Debug.Log("[SurfaceChecker] grounded hit=" + (result.groundedTransform?.name ?? "null") + " " + $"surface={ToTag(_currentSurface)} found={foundSurface} source={resolveSource} " + $"point={result.groundedPosition}");
			}
			EffectsPlayer land = GetFxSet(_currentSurface).Land;
			if (!(land == null))
			{
				land.play(result);
			}
		}

		public void HandleJump()
		{
			EffectsPlayer jump = GetFxSet(_currentSurface).Jump;
			if (!(jump == null))
			{
				jump.play();
			}
		}

		public string GetCurrentSurfaceTag()
		{
			return ToTag(_currentSurface);
		}

		private void HandleFootstep(IKResult result, string footLabel)
		{
			SurfaceType resolvedSurface = ResolveSurface(result.primaryHitTransform, result.surfacePoint, out var foundSurface, out var resolveSource);
			UpdateCurrentSurface(resolvedSurface, foundSurface);
			bool flag = IsRunning();
			EffectsPlayer bestFootstepFx = GetBestFootstepFx(GetFxSet(_currentSurface), flag);
			if (_debugLogs)
			{
				Debug.Log("[SurfaceChecker] foot=" + footLabel + " hit=" + (result.primaryHitTransform?.name ?? "null") + " " + $"surface={ToTag(_currentSurface)} found={foundSurface} source={resolveSource} " + $"running={flag} point={result.surfacePoint}");
			}
			if (!(bestFootstepFx == null))
			{
				bestFootstepFx.play(result);
			}
		}

		private SurfaceType ResolveSurface(Transform hitTransform, Vector3 worldPos, out bool foundSurface, out string resolveSource)
		{
			foundSurface = false;
			resolveSource = "Fallback";
			if (TryGetExplicitTaggedSurface(hitTransform, out var surface))
			{
				foundSurface = true;
				resolveSource = "Tag";
				return surface;
			}
			if (TryGetTerrainSurfaceFromHit(hitTransform, worldPos, out var surface2, out var terrainLayerName))
			{
				foundSurface = true;
				resolveSource = "Terrain:" + terrainLayerName;
				return surface2;
			}
			return GetFallbackSurface();
		}

		private bool TryGetExplicitTaggedSurface(Transform hitTransform, out SurfaceType surface)
		{
			surface = _defaultSurface;
			Transform transform = hitTransform;
			while (transform != null)
			{
				GameObject gameObject = transform.gameObject;
				if (gameObject.CompareTag("Grass"))
				{
					surface = SurfaceType.Grass;
					return true;
				}
				if (gameObject.CompareTag("Sand"))
				{
					surface = SurfaceType.Sand;
					return true;
				}
				if (gameObject.CompareTag("Water"))
				{
					surface = SurfaceType.Water;
					return true;
				}
				if (gameObject.CompareTag("Tile"))
				{
					surface = SurfaceType.Tile;
					return true;
				}
				transform = transform.parent;
			}
			return false;
		}

		public SurfaceType TryGetTerrainSurface(Transform hit, Vector3 worldPos)
		{
			if (!TryGetTerrainSurfaceFromHit(hit, worldPos, out var surface, out var _))
			{
				return SurfaceType.Tile;
			}
			return surface;
		}

		private bool TryGetTerrainSurfaceFromHit(Transform hitTransform, Vector3 worldPos, out SurfaceType surface, out string terrainLayerName)
		{
			surface = _defaultSurface;
			terrainLayerName = string.Empty;
			if (!TryGetTerrainFromHit(hitTransform, out var terrain))
			{
				return false;
			}
			CacheTerrain(terrain);
			if (_cachedTerrainData == null)
			{
				return false;
			}
			Vector3 vector = worldPos - _cachedTerrain.transform.position;
			int num = Mathf.Clamp(Mathf.FloorToInt(vector.x / _cachedTerrainData.size.x * (float)_cachedTerrainData.alphamapWidth), 0, _cachedTerrainData.alphamapWidth - 1);
			int num2 = Mathf.Clamp(Mathf.FloorToInt(vector.z / _cachedTerrainData.size.z * (float)_cachedTerrainData.alphamapHeight), 0, _cachedTerrainData.alphamapHeight - 1);
			if (_hasLastTerrainSample && num == _lastMapX && num2 == _lastMapZ)
			{
				int dominantLayerIndex = GetDominantLayerIndex(num, num2);
				surface = _lastTerrainSampleSurface;
				terrainLayerName = GetTerrainLayerNameSafe(_cachedTerrainData, dominantLayerIndex);
				return true;
			}
			int dominantLayerIndex2 = GetDominantLayerIndex(num, num2);
			surface = GetSurfaceForTerrainLayerIndex(dominantLayerIndex2);
			terrainLayerName = GetTerrainLayerNameSafe(_cachedTerrainData, dominantLayerIndex2);
			_lastMapX = num;
			_lastMapZ = num2;
			_lastTerrainSampleSurface = surface;
			_hasLastTerrainSample = true;
			return true;
		}

		private bool TryGetTerrainFromHit(Transform hitTransform, out Terrain terrain)
		{
			terrain = null;
			if (hitTransform == null)
			{
				return false;
			}
			if (hitTransform.TryGetComponent<Terrain>(out var component))
			{
				terrain = component;
				return true;
			}
			if (hitTransform.TryGetComponent<TerrainCollider>(out var component2))
			{
				terrain = component2.GetComponent<Terrain>();
				if (terrain != null)
				{
					return true;
				}
			}
			terrain = hitTransform.GetComponentInParent<Terrain>();
			if (terrain != null)
			{
				return true;
			}
			TerrainCollider componentInParent = hitTransform.GetComponentInParent<TerrainCollider>();
			if (componentInParent == null)
			{
				return false;
			}
			terrain = componentInParent.GetComponent<Terrain>();
			return terrain != null;
		}

		private int GetDominantLayerIndex(int mapX, int mapZ)
		{
			if (_cachedTerrainData == null)
			{
				return 0;
			}
			float[,,] alphamaps = _cachedTerrainData.GetAlphamaps(mapX, mapZ, 1, 1);
			int result = 0;
			float num = -1f;
			int alphamapLayers = _cachedTerrainData.alphamapLayers;
			for (int i = 0; i < alphamapLayers; i++)
			{
				float num2 = alphamaps[0, 0, i];
				if (num2 > num)
				{
					num = num2;
					result = i;
				}
			}
			return result;
		}

		private void BuildTerrainMappings()
		{
			_terrainNameToSurface.Clear();
			if (_terrainMappings == null)
			{
				return;
			}
			for (int i = 0; i < _terrainMappings.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(_terrainMappings[i].terrainLayerName))
				{
					_terrainNameToSurface[_terrainMappings[i].terrainLayerName] = _terrainMappings[i].surface;
				}
			}
		}

		private void RefreshTerrainArray()
		{
			Terrain[] activeTerrains = Terrain.activeTerrains;
			_activeTerrains = activeTerrains ?? Array.Empty<Terrain>();
		}

		private void CacheTerrain(Terrain terrain)
		{
			if (!(_cachedTerrain == terrain))
			{
				_cachedTerrain = terrain;
				_cachedTerrainData = ((terrain != null) ? terrain.terrainData : null);
				RebuildTerrainLayerCache();
				ResetTerrainSampleCache();
			}
		}

		private void RebuildTerrainLayerCache()
		{
			if (_cachedTerrainData == null)
			{
				_cachedLayerIndexToSurface = null;
				return;
			}
			TerrainLayer[] terrainLayers = _cachedTerrainData.terrainLayers;
			if (terrainLayers == null || terrainLayers.Length == 0)
			{
				_cachedLayerIndexToSurface = null;
				return;
			}
			_cachedLayerIndexToSurface = new SurfaceType[terrainLayers.Length];
			for (int i = 0; i < terrainLayers.Length; i++)
			{
				TerrainLayer terrainLayer = terrainLayers[i];
				SurfaceType value;
				if (terrainLayer == null)
				{
					_cachedLayerIndexToSurface[i] = _defaultSurface;
				}
				else if (_terrainNameToSurface.TryGetValue(terrainLayer.name, out value))
				{
					_cachedLayerIndexToSurface[i] = value;
				}
				else
				{
					_cachedLayerIndexToSurface[i] = _defaultSurface;
				}
			}
		}

		private void ResetTerrainSampleCache()
		{
			_lastMapX = -1;
			_lastMapZ = -1;
			_lastTerrainSampleSurface = _defaultSurface;
			_hasLastTerrainSample = false;
		}

		private SurfaceType GetSurfaceForTerrainLayerIndex(int index)
		{
			if (_cachedLayerIndexToSurface == null)
			{
				return _defaultSurface;
			}
			if (index < 0 || index >= _cachedLayerIndexToSurface.Length)
			{
				return _defaultSurface;
			}
			return _cachedLayerIndexToSurface[index];
		}

		private static string GetTerrainLayerNameSafe(TerrainData terrainData, int index)
		{
			if (terrainData == null)
			{
				return string.Empty;
			}
			TerrainLayer[] terrainLayers = terrainData.terrainLayers;
			if (terrainLayers == null || index < 0 || index >= terrainLayers.Length)
			{
				return string.Empty;
			}
			if (!(terrainLayers[index] != null))
			{
				return string.Empty;
			}
			return terrainLayers[index].name;
		}

		private void UpdateCurrentSurface(SurfaceType resolvedSurface, bool foundSurface)
		{
			_currentSurface = (foundSurface ? resolvedSurface : _defaultSurface);
		}

		private SurfaceType GetFallbackSurface()
		{
			return _defaultSurface;
		}

		private bool IsRunning()
		{
			if (_character != null && _character.SprintAbility != null)
			{
				return _character.SprintAbility.IsSprinting();
			}
			return false;
		}

		private SurfaceFxSet GetFxSet(SurfaceType surface)
		{
			return surface switch
			{
				SurfaceType.Grass => _grassFx, 
				SurfaceType.Sand => _sandFx, 
				SurfaceType.Water => _waterFx, 
				_ => _tileFx, 
			};
		}

		private static EffectsPlayer GetBestFootstepFx(SurfaceFxSet fxSet, bool isRunning)
		{
			if (isRunning)
			{
				if (!(fxSet.Run != null))
				{
					return fxSet.Walk;
				}
				return fxSet.Run;
			}
			if (!(fxSet.Walk != null))
			{
				return fxSet.Run;
			}
			return fxSet.Walk;
		}

		private static string ToTag(SurfaceType surface)
		{
			return surface switch
			{
				SurfaceType.Grass => "Grass", 
				SurfaceType.Sand => "Sand", 
				SurfaceType.Water => "Water", 
				_ => "Tile", 
			};
		}

		private void LogIKResult(string footLabel, IKResult result)
		{
			if (_debugLogs && result != null)
			{
				string text = ((result.primaryHitTransform != null) ? result.primaryHitTransform.name : "null");
				string text2 = ((result.secondaryHitTransform != null) ? result.secondaryHitTransform.name : "null");
				UtilityMethods.Log("[SurfaceChecker] " + footLabel + " IKResult | Primary=" + text + " | Secondary=" + text2 + " | " + $"SurfacePoint={result.surfacePoint} | Normal={result.normal}");
			}
		}
	}
}
