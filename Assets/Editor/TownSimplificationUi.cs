using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.States.MainMenu;

public static partial class IslandRevision20261018
{
    [Serializable] public class TownUiResult {public string utc,stage;public int rendererIndex;public string[] hidden,renderPipelines;public bool runnerUnregistered,projectionRoundTrip;}
    static void HideUiReference(MonoBehaviour component,string field,List<string> hidden,bool wholeTile=false) {
        var state=new SerializedObject(component);var property=state.FindProperty(field);var value=property?.objectReferenceValue;
        var target=value is Component c?c.gameObject:value as GameObject;if(!target)return;
        if(wholeTile)target=target.transform.parent.gameObject;
        target.SetActive(false);hidden.Add(component.gameObject.scene.path+":"+P(target.transform));EditorUtility.SetDirty(target);
    }
    static void HideTownUi(GameObject root,List<string> hidden) {
        foreach(var menu in root.GetComponentsInChildren<MainMenuManager>(true)){HideUiReference(menu,"_versionText",hidden);HideUiReference(menu,"_fedbackButton",hidden);}
        foreach(var pause in root.GetComponentsInChildren<PauseScreen>(true)){HideUiReference(pause,"_versionText",hidden);HideUiReference(pause,"_debugButton",hidden);}
        foreach(var tablet in root.GetComponentsInChildren<TabletController>(true)){HideUiReference(tablet,"_runnerGameAppButton",hidden,true);HideUiReference(tablet,"_runnerGameApp",hidden);}
        foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if(Regex.IsMatch(text.name,@"version",RegexOptions.IgnoreCase)||Regex.IsMatch(text.text??"",@"^\s*(?:v(?:ersion)?\s*)?\d+\.\d+\.\d+(?:\s.*)?$",RegexOptions.IgnoreCase))
            {text.text="";text.gameObject.SetActive(false);EditorUtility.SetDirty(text);hidden.Add("Version:"+P(text.transform));}
    }
    public static void ApplyTownUi() {
        var hidden=new List<string>();var pipelines=new List<string>();int rendererIndex=-1;
        const string rendererPath="Assets/SushiAndFish/FrontEnd/LiveTabletMapRenderer.asset";
        foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")) {
            var path=AssetDatabase.GUIDToAssetPath(guid);if(!path.StartsWith("Assets/"))continue;
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);var state=new SerializedObject(pipeline);var list=state.FindProperty("m_RendererDataList");if(list==null||list.arraySize==0)continue;
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(!renderer) {
                var source=list.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;if(!source)throw new Exception("Expected URP forward renderer");
                renderer=UnityEngine.Object.Instantiate(source);renderer.name="LiveTabletMapRenderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,rendererPath);
                var rs=new SerializedObject(renderer);var map=rs.FindProperty("m_RendererFeatureMap");if(map!=null){map.arraySize=0;rs.ApplyModifiedPropertiesWithoutUndo();}EditorUtility.SetDirty(renderer);
            }
            int found=-1;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)found=i;
            if(found<0){found=list.arraySize;list.arraySize++;list.GetArrayElementAtIndex(found).objectReferenceValue=renderer;state.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);}
            if(rendererIndex>=0&&rendererIndex!=found)throw new Exception("Map renderer indices differ across quality pipelines");rendererIndex=found;pipelines.Add(path);
        }
        if(rendererIndex<0)throw new Exception("Live map requires a verified URP renderer");
        const string playerPath="Assets/GameObject/PlayerNGO.prefab";var prefab=PrefabUtility.LoadPrefabContents(playerPath);
        try {
            HideTownUi(prefab,hidden);var app=prefab.GetComponentInChildren<MapApp>(true);if(!app)throw new Exception("MapApp missing");
            var map=app.GetComponent<LiveSceneTabletMap>();if(!map)map=app.gameObject.AddComponent<LiveSceneTabletMap>();var serialized=new SerializedObject(map);serialized.FindProperty("_rendererIndex").intValue=rendererIndex;serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab,playerPath);
        } finally {PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(var path in new[]{IslandFrontEndBuilder.GameScene,"Assets/Scenes/MainMenuState.unity","Assets/SushiAndFish/FrontEnd/Scenes/MainMenuState.unity"}) {
            var scene=EditorSceneManager.OpenScene(path);foreach(var root in scene.GetRootGameObjects())HideTownUi(root,hidden);EditorSceneManager.SaveScene(scene);
        }
        var projection=new MapProjection();projection.FitWorldRect(new Rect(-100,-60,240,135));bool roundTrip=true;
        foreach(var point in new[]{new Vector3(-100,0,-60),new Vector3(140,0,75),new Vector3(21.5f,0,67.3f)}){roundTrip&=projection.TryWorldToMapUV(point,out var uv)&&projection.TryMapUVToWorldXZ(uv,out var world)&&Vector2.Distance(new Vector2(point.x,point.z),world)<.0001f;}
        if(!roundTrip)throw new Exception("Live map inverse projection failed");
        AssetDatabase.SaveAssets();var result=new TownUiResult{utc=DateTime.UtcNow.ToString("o"),stage="SAVED_NEEDS_RUNTIME_VERIFICATION",rendererIndex=rendererIndex,hidden=hidden.Distinct().ToArray(),renderPipelines=pipelines.ToArray(),runnerUnregistered=!File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Tablet/TabletController.cs").Contains("_runnerGameApp.Initialize()"),projectionRoundTrip=roundTrip};
        File.WriteAllText(Path.Combine(Reports,"town-simplification-ui.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_UI_AND_LIVE_MAP_SAVED");
    }
}
