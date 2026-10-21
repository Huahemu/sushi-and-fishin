using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ai.NPCs;

public static class CityExpansionInspection
{
    [Serializable] public class Receipt { public string utc; public List<string> ui=new List<string>(); public List<string> billboards=new List<string>(); public List<string> map=new List<string>(); public List<string> residents=new List<string>(); }
    public static string Reports => IslandFrontEndInventory.Reports;
    public static T Field<T>(object o,string name) => (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o);
    public static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    public static void Inspect()
    {
        var r=new Receipt{utc=DateTime.UtcNow.ToString("o")};
        EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var all=Resources.FindObjectsOfTypeAll<Transform>().Where(t=>t.gameObject.scene.IsValid()).ToArray();
        foreach(var panel in all.Select(t=>t.GetComponent<DialoguePanelController>()).Where(t=>t))
        {
            r.ui.Add(EditorJsonUtility.ToJson(panel,true));
            foreach(var t in panel.GetComponentsInChildren<Transform>(true))
            {
                var rect=t as RectTransform;
                r.ui.Add(PathOf(t)+" active="+t.gameObject.activeSelf+" scale="+t.localScale+" rect="+(rect?rect.rect.ToString():"-")+" anchor="+(rect?rect.anchoredPosition.ToString():"-")+" components="+string.Join(",",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)));
            }
        }
        foreach(var b in all.Select(t=>t.GetComponent<ResidentBillboardText>()).Where(b=>b))r.billboards.Add(PathOf(b.transform)+" "+EditorJsonUtility.ToJson(b,true));
        foreach(var text in all.Select(t=>t.GetComponent<TMP_Text>()).Where(t=>t&&t.text!=null&&t.text.IndexOf("park",StringComparison.OrdinalIgnoreCase)>=0))r.map.Add(PathOf(text.transform)+" text="+text.text);
        foreach(var c in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c&&(c.GetType().Name.Contains("Map")||c.GetType().Name=="ResidentManager")))
        {
            var json=EditorJsonUtility.ToJson(c,true);
            if(c.GetType().Name=="ResidentManager")r.residents.Add(PathOf(c.transform)+" "+json);else r.map.Add(PathOf(c.transform)+" "+json);
        }
        // Resident billboard templates are normally under the spawned resident prefab.
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/SushiAndFish/Modules/OriginalIsland","Assets/Resources"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if(path.IndexOf("Resident",StringComparison.OrdinalIgnoreCase)<0&&path.IndexOf("NPC",StringComparison.OrdinalIgnoreCase)<0)continue;
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var b in go.GetComponentsInChildren<ResidentBillboardText>(true))r.billboards.Add(path+" -> "+PathOf(b.transform)+" "+EditorJsonUtility.ToJson(b,true));
        }
        File.WriteAllText(System.IO.Path.Combine(Reports,"city-expansion-inspection.json"),JsonUtility.ToJson(r,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Debug.Log("CITY_INSPECTION_DONE ui="+r.ui.Count+" billboard="+r.billboards.Count);
    }
}
