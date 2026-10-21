using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.States.Game;
using Bozo.ModularCharacters;

public static class IslandFrontEndInventory
{
    [Serializable] public class Item{public string scene,path,type,details;public bool active;public Vector3 position;}
    [Serializable] public class Report{public string utc;public Item[] objects;public string[] buildScenes;}
    public static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));
    public static string PathOf(Transform value){return value.parent?PathOf(value.parent)+"/"+value.name:value.name;}
    static IEnumerable<GameObject> All(UnityEngine.SceneManagement.Scene scene){return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject);}
    public static void Run()
    {
        var rows=new List<Item>();
        foreach(var path in new[]{"Assets/Scenes/MainMenuState.unity","Assets/Scenes/GameState.unity","Assets/BoZo_StylizedModularCharacters/Scenes/StylizedCharacterCreator.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var root in scene.GetRootGameObjects())rows.Add(new Item{scene=scene.name,path=root.name,type="Root",active=root.activeSelf,position=root.transform.position});
            foreach(var go in All(scene))
            {
                if(go.GetComponent<MainMenuManager>() || go.GetComponent<SaveSlot>() || go.GetComponent<PauseScreen>() || go.GetComponent<CharacterCreator>())
                foreach(var component in go.GetComponents<MonoBehaviour>().Where(c=>c && (c is MainMenuManager || c is SaveSlot || c is PauseScreen || c is CharacterCreator)))
                {
                    var state=new SerializedObject(component);var iterator=state.GetIterator();var fields=new List<string>();
                    while(iterator.NextVisible(true))if(iterator.propertyType==SerializedPropertyType.ObjectReference && iterator.objectReferenceValue)
                    {var value=iterator.objectReferenceValue;fields.Add(iterator.propertyPath+"="+(value is Component c?PathOf(c.transform):value is GameObject g?PathOf(g.transform):AssetDatabase.GetAssetPath(value)));}
                    rows.Add(new Item{scene=scene.name,path=PathOf(go.transform),type=component.GetType().Name,active=go.activeInHierarchy,details=string.Join("; ",fields),position=go.transform.position});
                }
                if(go.GetComponent<Canvas>() || go.name.IndexOf("Key",StringComparison.OrdinalIgnoreCase)>=0 || go.name.IndexOf("Control",StringComparison.OrdinalIgnoreCase)>=0 || go.name.IndexOf("Pause",StringComparison.OrdinalIgnoreCase)>=0)
                {var image=go.GetComponent<UnityEngine.UI.Image>();rows.Add(new Item{scene=scene.name,path=PathOf(go.transform),type=string.Join(",",go.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)),active=go.activeInHierarchy,details=image&&image.sprite?image.sprite.name:"",position=go.transform.position});}
            }
        }
        File.WriteAllText(Path.Combine(Reports,"island-front-end-inventory.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),objects=rows.ToArray(),buildScenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);EditorApplication.Exit(0);
    }
}
