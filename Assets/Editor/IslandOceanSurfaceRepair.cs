using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StylizedWater3;
public static class IslandOceanSurfaceRepair{
public static void Build(bool save){if(UnityEngine.Object.FindFirstObjectByType<WaterGrid>() is WaterGrid existing&&existing.name=="TownOceanContinuousGrid")return;var ocean=UnityEngine.Object.FindFirstObjectByType<OceanFollowBehaviour>();if(!ocean)throw new Exception("Ocean missing");var scene=ocean.gameObject.scene;var root=new GameObject("TownOceanContinuousGrid");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.transform.SetParent(ocean.transform.parent,false);root.transform.position=new Vector3(-24,ocean.transform.position.y,0);var grid=root.AddComponent<WaterGrid>();grid.material=ocean.material;grid.scale=4096;grid.vertexDistance=4;grid.rowsColumns=4;grid.autoAssignCamera=true;grid.followTarget=Application.isPlaying&&Camera.main?Camera.main.transform:null;grid.Recreate();
foreach(var r in ocean.GetComponentsInChildren<MeshRenderer>(true))r.enabled=false;ocean.enabled=true;OceanFollowBehaviour.Instance=ocean;
if(save){var filter=root.GetComponentInChildren<MeshFilter>();const string path="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/TownOceanContinuousGrid.asset";if(!AssetDatabase.Contains(filter.sharedMesh))AssetDatabase.CreateAsset(filter.sharedMesh,path);EditorUtility.SetDirty(grid);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"ocean-surface-repair.json"),"{\"utc\":\""+DateTime.UtcNow.ToString("o")+"\",\"tiles\":16,\"vertexSpacing\":4,\"size\":4096,\"materialPreserved\":true}");}}
public static void Run(){EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);Build(true);IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
}

