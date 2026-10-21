using System.Collections.Generic;
using Nav3D.API;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Common
{
	public static class ExtensionTransform
	{
		private static readonly string MESH_ERROR = "The obstacle transform (name: {0}, InstanceID: {1}) has no MeshFilter component, or it's mesh has no any triangles";

		public static bool TryGetObstacleInfo(this Transform _Transform, Nav3DObstacle _ObstacleRootController, out ObstacleInfoSingle _AdditionData)
		{
			if (_Transform.TryGetObstacleMeshData(_ObstacleRootController, out _AdditionData))
			{
				return true;
			}
			if (_Transform.TryGetObstacleTerrainData(_ObstacleRootController, out _AdditionData))
			{
				return true;
			}
			Debug.LogWarning(string.Format(MESH_ERROR, _Transform.name, _Transform.GetInstanceID()));
			return false;
		}

		public static List<Transform> GetAllChildren(this Transform _Root, bool _CheckActive = true)
		{
			List<Transform> list = new List<Transform>();
			GetAllChildren(_Root, list, _CheckActive);
			return list;
		}

		private static bool TryGetObstacleMeshData(this Transform _Transform, Nav3DObstacle _ObstacleRootController, out ObstacleInfoSingle _AdditionData)
		{
			if (!_Transform.TryGetComponent<MeshFilter>(out var component) || component.sharedMesh.triangles.Length == 0)
			{
				_AdditionData = null;
				return false;
			}
			Mesh sharedMesh = component.sharedMesh;
			_AdditionData = new ObstacleInfoMesh(_ObstacleRootController.InstanceID, sharedMesh.vertices, sharedMesh.triangles, _Transform.position, _Transform.lossyScale, _Transform.rotation);
			return true;
		}

		private static bool TryGetObstacleTerrainData(this Transform _Transform, Nav3DObstacle _ObstacleRootController, out ObstacleInfoSingle _AdditionData)
		{
			if (!_Transform.TryGetComponent<Terrain>(out var component))
			{
				_AdditionData = null;
				return false;
			}
			TerrainData terrainData = component.terrainData;
			int heightmapResolution = terrainData.heightmapResolution;
			_AdditionData = new ObstacleInfoTerrain(_ObstacleRootController.InstanceID, terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution), heightmapResolution, terrainData.size, _Transform.position);
			return true;
		}

		private static void GetAllChildren(Transform _Transform, List<Transform> _Children, bool _CheckActive)
		{
			if (_CheckActive && !_Transform.gameObject.activeInHierarchy)
			{
				return;
			}
			if (_Children != null)
			{
				_Children.Add(_Transform);
			}
			else
			{
				_Children = new List<Transform> { _Transform };
			}
			foreach (Transform item in _Transform)
			{
				GetAllChildren(item, _Children, _CheckActive);
			}
		}
	}
}
