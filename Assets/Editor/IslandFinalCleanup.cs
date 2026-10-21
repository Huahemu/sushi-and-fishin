using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class IslandFinalCleanup {
public static void Run(){var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity");var rows=IslandWorldIssues.All(scene).Where(t=>t.name.StartsWith("SM_airconditioner")&&System.Text.RegularExpressions.Regex.IsMatch(IslandFrontEndInventory.PathOf(t),@"^Root/Comps/Comp\d+(?: \(\d+\))?/")).ToArray();var paths=rows.Select(IslandFrontEndInventory.PathOf).ToArray();foreach(var t in rows)UnityEngine.Object.DestroyImmediate(t.gameObject);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(IslandComprehensiveRepair.Reports,"final-orphan-cleanup.json"),JsonUtility.ToJson(new Result{utc=DateTime.UtcNow.ToString("o"),removed=paths},true));IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");IslandFinalInspection.Run();}
[Serializable]public class Result{public string utc;public string[] removed;}
}
