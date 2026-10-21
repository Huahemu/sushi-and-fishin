using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StylizedWater3;
public static class IslandOceanGridRepair {
[Serializable]public class Result{public string utc,before,after;public int renderers;}
public static void Run(){var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);var ocean=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<OceanFollowBehaviour>(true)).Single();string before=EditorJsonUtility.ToJson(ocean);typeof(OceanFollowBehaviour).GetMethod("BuildLODs",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ocean,null);var result=new Result{utc=DateTime.UtcNow.ToString("o"),before=before,after=EditorJsonUtility.ToJson(ocean),renderers=ocean.GetComponentsInChildren<MeshRenderer>(true).Length};EditorUtility.SetDirty(ocean);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"ocean-grid-repair.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("OCEAN_GRID_REBUILT");}}
