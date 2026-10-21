using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Localization.Components;
using TMPro;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Core.DressingSceneSystem;
using SushiAndFish.FrontEnd;

public static class IslandFrontEndBuilder
{
    public const string Root="Assets/SushiAndFish";
    public const string GameScene=Root+"/Modules/OriginalIsland/Scenes/GameState.unity";
    public const string DressingRoot=Root+"/Modules/OriginalIsland/Scenes/DressingScenes";
    public const string CreatorScene=Root+"/Modules/CharacterCreation/Scenes/StylizedCharacterCreator.unity";
    public const string MenuScene=Root+"/FrontEnd/Scenes/MainMenuState.unity";
    public const string BackdropScene=Root+"/FrontEnd/Scenes/IslandMenuBackdrop.unity";
    static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class PreviewReport {public string utc;public int transforms,meshRenderers,terrains,lodGroups;public Vector3 centre;public float halfHeight,altitude;public string[] sources;}
    [Serializable] public class BuildReport {public string utc,gameScene,creatorScene,menuScene,backdropScene,hiddenPauseGraphic;public string[] gameScenes,mainButtons;public bool gameplaySceneGuidRetained,creatorSceneGuidRetained;}
    static T Ref<T>(UnityEngine.Object target,string field) where T:UnityEngine.Object=>new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    static void SetRef(UnityEngine.Object target,string field,UnityEngine.Object value){var state=new SerializedObject(target);state.FindProperty(field).objectReferenceValue=value;state.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);}
    static void EnsureFolders()
    {
        foreach(var path in new[]{Root+"/Modules/OriginalIsland/Scenes",Root+"/Modules/CharacterCreation/Scenes",Root+"/FrontEnd/Scenes"})Directory.CreateDirectory(path);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
    static void MoveIfNeeded(string before,string after)
    {
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(after))return;
        string error=AssetDatabase.MoveAsset(before,after);if(!string.IsNullOrEmpty(error))throw new IOException(error);
    }
    [MenuItem("Tools/Sushi & Fish/Build composed island front end")]
    public static void Build()
    {
        EnsureFolders();
        string gameGuid=AssetDatabase.AssetPathToGUID(File.Exists(GameScene)?GameScene:"Assets/Scenes/GameState.unity");
        string creatorGuid=AssetDatabase.AssetPathToGUID(File.Exists(CreatorScene)?CreatorScene:"Assets/BoZo_StylizedModularCharacters/Scenes/StylizedCharacterCreator.unity");
        MoveIfNeeded("Assets/Scenes/GameState.unity",GameScene);
        MoveIfNeeded("Assets/BoZo_StylizedModularCharacters/Scenes/StylizedCharacterCreator.unity",CreatorScene);
        if(!Directory.Exists(DressingRoot))
        {string error=AssetDatabase.MoveAsset("Assets/Scenes/DressingScenes",DressingRoot);if(!string.IsNullOrEmpty(error))throw new IOException(error);}
        var game=EditorSceneManager.OpenScene(GameScene,OpenSceneMode.Single);
        var pause=game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PauseScreen>(true)).Single();
        var schema=pause.transform.Find("ControlSchema");if(!schema)throw new InvalidOperationException("Original pause keyboard diagram not found");
        string hiddenPauseGraphic=IslandFrontEndInventory.PathOf(schema);
        schema.gameObject.SetActive(false);EditorSceneManager.SaveScene(game);
        PreviewReport preview=File.Exists(BackdropScene)?JsonUtility.FromJson<PreviewReport>(File.ReadAllText(Path.Combine(Reports,"island-menu-preview-build.json"))):BuildBackdrop();
        BuildMenu(preview);BuildCreatorConfirm();
        var old=EditorBuildSettings.scenes;
        var updated=new List<EditorBuildSettingsScene>();
        foreach(var entry in old)
        {
            string path=entry.path;
            if(path=="Assets/Scenes/MainMenuState.unity")path=MenuScene;
            else if(path=="Assets/Scenes/GameState.unity")path=GameScene;
            else if(path.StartsWith("Assets/Scenes/DressingScenes/",StringComparison.Ordinal))path=DressingRoot+path.Substring("Assets/Scenes/DressingScenes".Length);
            else if(Path.GetFileNameWithoutExtension(path)=="StylizedCharacterCreator")path=CreatorScene;
            if(!updated.Any(s=>s.path==path))updated.Add(new EditorBuildSettingsScene(path,entry.enabled));
        }
        if(!updated.Any(s=>s.path==BackdropScene))updated.Add(new EditorBuildSettingsScene(BackdropScene,true));
        EditorBuildSettings.scenes=updated.ToArray();
        var gameplay=new[]{GameScene}.Concat(Directory.GetFiles(DressingRoot,"*.unity").OrderBy(s=>s).Select(s=>s.Replace('\\','/'))).ToArray();
        Module(Root+"/Modules/OriginalIsland/OriginalIslandModule.asset","原始岛屿与完整玩法","GameState 与 14 个按原逻辑加载的布景场景；保留人物、经营、钓鱼、升级、任务及原有资源引用。",gameplay);
        Module(Root+"/Modules/CharacterCreation/CharacterCreationModule.asset","原版角色创建","原版完整捏人 Scene 与原控件，增加与开始菜单同风格的确认并开始按钮。",new[]{CreatorScene});
        Module(Root+"/FrontEnd/IslandFrontEndModule.asset","岛屿俯视开始界面","复用原岛屿视觉，俯视镜头只绕世界 Y 轴旋转。",new[]{MenuScene,BackdropScene});
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Reports,"island-front-end-build.json"),JsonUtility.ToJson(new BuildReport{utc=DateTime.UtcNow.ToString("o"),gameScene=GameScene,creatorScene=CreatorScene,menuScene=MenuScene,backdropScene=BackdropScene,hiddenPauseGraphic=hiddenPauseGraphic,gameScenes=gameplay,mainButtons=new[]{"开始游戏","读取存档","设置","退出游戏"},gameplaySceneGuidRetained=gameGuid==AssetDatabase.AssetPathToGUID(GameScene),creatorSceneGuidRetained=creatorGuid==AssetDatabase.AssetPathToGUID(CreatorScene)},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        Debug.Log("ISLAND_FRONT_END_BUILD_COMPLETE");EditorApplication.Exit(0);
    }
    public static void RebuildBackdropAndFinish(){BuildBackdrop();Build();}
    public static void RefreshMenuPresentation()
    {
        var preview=BuildBackdrop();BuildMenu(preview);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        Debug.Log("ISLAND_MENU_PRESENTATION_REFRESHED");
    }
    static void Module(string path,string title,string description,string[] scenes)
    {
        var module=AssetDatabase.LoadAssetAtPath<SceneModuleDefinition>(path);
        if(!module){module=ScriptableObject.CreateInstance<SceneModuleDefinition>();AssetDatabase.CreateAsset(module,path);}
        module.displayName=title;module.description=description;module.bootstrapScenePath="Assets/Scenes/Main.unity";module.scenePaths=scenes;module.sceneNames=scenes.Select(Path.GetFileNameWithoutExtension).ToArray();module.sceneGuids=scenes.Select(AssetDatabase.AssetPathToGUID).ToArray();EditorUtility.SetDirty(module);
    }
    static PreviewReport BuildBackdrop()
    {
        var sources=new[]{GameScene}.Concat(new[]{"100_Dressing_City_01","110_Dressing_City_Folliage_01","120_Dressing_City_Grass_01","130_Dressing_City_Rocks_01"}.Select(n=>DressingRoot+"/"+n+".unity")).ToArray();
        var loaded=new List<Scene>();
        for(int i=0;i<sources.Length;i++)loaded.Add(EditorSceneManager.OpenScene(sources[i],i==0?OpenSceneMode.Single:OpenSceneMode.Additive));
        // Streaming groups start disabled in the authoring scenes. Activate
        // their original visuals in memory before extracting the menu view.
        foreach(var sceneRoot in loaded.SelectMany(s=>s.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<DressingSceneRoot>(true)))sceneRoot.ActivateAllImmediate();
        var terrains=loaded.SelectMany(s=>s.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).ToArray();
        Bounds land=FindLandBounds(terrains);
        var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var root=new GameObject("OriginalIslandVisuals");SceneManager.MoveGameObjectToScene(root,preview);
        var transforms=new Dictionary<Transform,Transform>();var objects=new Dictionary<UnityEngine.Object,UnityEngine.Object>();var copied=new List<Component>();
        var lodMembers=new HashSet<Renderer>();var chosenLods=new HashSet<Renderer>();
        foreach(var group in loaded.SelectMany(s=>s.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<LODGroup>(true)))
        {
            var lods=group.GetLODs();foreach(var lod in lods)foreach(var member in lod.renderers)if(member)lodMembers.Add(member);
            for(int level=lods.Length-1;level>=0;level--)
            {
                var candidates=lods[level].renderers.Where(r=>r && r.TryGetComponent<MeshFilter>(out var filter) && filter && filter.sharedMesh && filter.sharedMesh.vertexCount>12).ToArray();
                if(candidates.Length==0)continue;foreach(var candidate in candidates)chosenLods.Add(candidate);break;
            }
        }
        var renderers=loaded.SelectMany(s=>s.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.transform.root.name!="---PostProcess" && !r.GetComponentsInParent<Transform>(true).Any(t=>t.name=="AR_CinemaAuditorium") && !r.GetComponentInParent<Canvas>() && r.TryGetComponent<MeshFilter>(out var filter) && filter && filter.sharedMesh && Vector3.Scale(filter.sharedMesh.bounds.size,r.transform.lossyScale).magnitude>=3f && (!lodMembers.Contains(r)||chosenLods.Contains(r)) && !r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name.IndexOf("Stylized Water",StringComparison.OrdinalIgnoreCase)>=0) && !r.name.StartsWith("OceanGrid",StringComparison.OrdinalIgnoreCase)).ToArray();
        Transform CopyTransform(Transform source)
        {
            if(transforms.TryGetValue(source,out var result))return result;
            var go=new GameObject(source.name);go.SetActive(false);SceneManager.MoveGameObjectToScene(go,preview);go.layer=source.gameObject.layer;
            go.transform.SetParent(source.parent?CopyTransform(source.parent):root.transform,false);
            go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;
            transforms[source]=go.transform;objects[source]=go.transform;objects[source.gameObject]=go;return go.transform;
        }
        Component CopyComponent(Component source)
        {
            if(objects.TryGetValue(source,out var found))return found as Component;
            var target=CopyTransform(source.transform).gameObject.AddComponent(source.GetType());
            EditorUtility.CopySerialized(source,target);objects[source]=target;copied.Add(target);return target;
        }
        foreach(var renderer in renderers){CopyComponent(renderer.GetComponent<MeshFilter>());var copiedRenderer=(MeshRenderer)CopyComponent(renderer);copiedRenderer.enabled=true;if(renderer.probeAnchor)CopyTransform(renderer.probeAnchor);}
        foreach(var terrain in terrains)CopyComponent(terrain);
        foreach(var component in copied)
        {
            var state=new SerializedObject(component);var property=state.GetIterator();
            while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue)
            {
                if(objects.TryGetValue(property.objectReferenceValue,out var mapped))property.objectReferenceValue=mapped;
                else if(property.objectReferenceValue is Component reference && reference.gameObject.scene.IsValid() && reference.gameObject.scene!=preview)property.objectReferenceValue=null;
                else if(property.objectReferenceValue is GameObject referenceObject && referenceObject.scene.IsValid() && referenceObject.scene!=preview)property.objectReferenceValue=null;
            }
            state.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var pair in transforms)pair.Value.gameObject.SetActive(pair.Key.gameObject.activeSelf);
        root.AddComponent<IslandBackdropPresentation>();
        var casinoLightRig=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SushiAndFish/FrontEnd/CasinoMenuLightRig.prefab");
        var casino=loaded.SelectMany(s=>s.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<SushiAndFish.Casino.ParkCasinoIntegration>(true)).FirstOrDefault();
        if(casinoLightRig&&casino&&casino.permanentExterior){var rig=(GameObject)PrefabUtility.InstantiatePrefab(casinoLightRig,preview);rig.transform.SetParent(root.transform,false);rig.GetComponent<SushiAndFish.FrontEnd.CasinoMenuLighting>().nativeMapPose=CopyTransform(casino.permanentExterior.transform);}

        EditorSceneManager.SaveScene(preview,BackdropScene);
        var report=new PreviewReport{utc=DateTime.UtcNow.ToString("o"),sources=sources,transforms=transforms.Count,meshRenderers=renderers.Length,terrains=terrains.Length,lodGroups=copied.Count(c=>c is LODGroup),centre=new Vector3(land.center.x,0,land.center.z),altitude=Mathf.Max(650,land.max.y+400),halfHeight=Mathf.Sqrt(land.size.x*land.size.x+land.size.z*land.size.z)*0.56f};
        File.WriteAllText(Path.Combine(Reports,"island-menu-preview-build.json"),JsonUtility.ToJson(report,true));return report;
    }
    static Bounds FindLandBounds(Terrain[] terrains)
    {
        bool initial=true;Bounds bounds=default;
        foreach(var terrain in terrains)
        {
            var data=terrain.terrainData;var heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
            for(int y=0;y<data.heightmapResolution;y+=2)for(int x=0;x<data.heightmapResolution;x+=2)
            {
                float altitude=terrain.transform.position.y+heights[y,x]*data.size.y;if(altitude<-3.5f)continue;
                var point=terrain.transform.position+new Vector3(x/(float)(data.heightmapResolution-1)*data.size.x,heights[y,x]*data.size.y,y/(float)(data.heightmapResolution-1)*data.size.z);
                if(initial){bounds=new Bounds(point,Vector3.zero);initial=false;}else bounds.Encapsulate(point);
            }
        }
        if(initial)throw new InvalidOperationException("Original island has no above-water terrain samples");return bounds;
    }
    static void BuildMenu(PreviewReport preview)
    {
        if(!File.Exists(MenuScene) && !AssetDatabase.CopyAsset("Assets/Scenes/MainMenuState.unity",MenuScene))throw new IOException("Could not copy original menu controls");
        var scene=EditorSceneManager.OpenScene(MenuScene,OpenSceneMode.Single);
        var menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuManager>(true)).Single();
        foreach(var name in new[]{"Boat","Background","MainMenuCamera"})foreach(var go in scene.GetRootGameObjects().Where(r=>r.name==name))go.SetActive(false);
        foreach(var probe in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReflectionProbe>(true)))probe.enabled=false;
        foreach(var light in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)))light.shadows=LightShadows.None;
        var previous=Ref<GameObject>(menu,"_mainCamera");
        var cameraObject=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="IslandOverheadCamera")??new GameObject("IslandOverheadCamera");
        SceneManager.MoveGameObjectToScene(cameraObject,scene);
        var camera=cameraObject.GetComponent<Camera>();if(!camera)camera=cameraObject.AddComponent<Camera>();
        if(previous && previous!=cameraObject && previous.GetComponent<Camera>()){EditorUtility.CopySerialized(previous.GetComponent<Camera>(),camera);previous.SetActive(false);}
        foreach(var oldCamera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))if(oldCamera!=camera)oldCamera.enabled=false;
        camera.enabled=true;cameraObject.SetActive(true);cameraObject.tag="MainCamera";
        if(!cameraObject.GetComponent<AudioListener>())cameraObject.AddComponent<AudioListener>();
        var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.renderShadows=false;cameraData.requiresDepthTexture=true;cameraData.requiresColorTexture=true;
        var orbit=cameraObject.GetComponent<IslandOrbitCamera>();if(!orbit)orbit=cameraObject.AddComponent<IslandOrbitCamera>();orbit.Configure(preview.centre,preview.altitude,preview.halfHeight);
        SetRef(menu,"_mainCamera",cameraObject);
        foreach(var waterRoot in scene.GetRootGameObjects().Where(r=>r.name=="StylizedWater3_OceanGrid"))
        foreach(var behaviour in waterRoot.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>b))
        {
            var state=new SerializedObject(behaviour);var property=state.GetIterator();
            while(property.NextVisible(true))if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue is Camera)property.objectReferenceValue=camera;
            state.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var canvas in menu.GetComponentsInChildren<Canvas>(true))if(canvas.renderMode==RenderMode.ScreenSpaceCamera)canvas.worldCamera=camera;
        foreach(var field in new[]{"_customizationButton","_creditsButton","_fedbackButton","_discordButton","_steamButton","_youtubeButton"}){var value=Ref<BaseButton>(menu,field);if(value)value.gameObject.SetActive(false);}
        foreach(var field in new[]{"_socials","_members"}){var value=Ref<RectTransform>(menu,field);if(value)value.gameObject.SetActive(false);}
        foreach(var transform in menu.GetComponentsInChildren<Transform>(true))if(transform.name.IndexOf("Upcoming",StringComparison.OrdinalIgnoreCase)>=0)transform.gameObject.SetActive(false);
        var flowObject=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="IslandFrontEndFlow")??new GameObject("IslandFrontEndFlow");SceneManager.MoveGameObjectToScene(flowObject,scene);
        var flow=flowObject.GetComponent<IslandFrontEndFlow>();if(!flow)flow=flowObject.AddComponent<IslandFrontEndFlow>();
        SetRef(flow,"menuManager",menu);SetRef(flow,"orbitCamera",orbit);var saves=Ref<SaveSlotScreen>(menu,"_saveSlotScreen");SetRef(flow,"saveSlotScreen",saves);SetRef(menu,"_islandFrontEnd",flow);
        var fields=new[]{"_singleplayerButton","_playButton","_settingsButton","_quitButton"};var flowFields=new[]{"startButton","loadButton","settingsButton","quitButton"};var labels=new[]{"开始游戏","读取存档","设置","退出游戏"};
        var parent=Ref<RectTransform>(menu,"_buttons");parent.anchorMin=parent.anchorMax=new Vector2(0,0.5f);parent.pivot=new Vector2(0,0.5f);parent.anchoredPosition=new Vector2(90,-80);parent.sizeDelta=new Vector2(520,450);
        var layout=parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();if(!layout)layout=parent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleLeft;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.spacing=20;
        for(int i=0;i<fields.Length;i++)
        {var button=Ref<BaseButton>(menu,fields[i]);button.gameObject.SetActive(true);button.transform.SetSiblingIndex(i);var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(520,85);SetRef(flow,flowFields[i],button);SetButtonText(button,labels[i]);}
        var messageTransform=saves.transform.Find("FrontEndSaveMessage");var message=messageTransform?messageTransform.GetComponent<TMP_Text>():null;
        if(!message)
        {
            var source=saves.GetComponentsInChildren<TMP_Text>(true).First();var go=UnityEngine.Object.Instantiate(source.gameObject,saves.transform);go.name="FrontEndSaveMessage";message=go.GetComponent<TMP_Text>();
        }
        foreach(var localizer in message.GetComponents<LocalizeStringEvent>())localizer.enabled=false;
        var messageRect=(RectTransform)message.transform;messageRect.anchorMin=messageRect.anchorMax=new Vector2(0.5f,0);messageRect.pivot=new Vector2(0.5f,0);messageRect.anchoredPosition=new Vector2(0,65);messageRect.sizeDelta=new Vector2(1350,110);message.fontSize=34;message.alignment=TextAlignmentOptions.Center;message.raycastTarget=false;message.gameObject.SetActive(false);SetRef(flow,"saveListMessage",message);
        EditorSceneManager.SaveScene(scene);
    }
    static void SetButtonText(BaseButton button,string label)
    {
        foreach(var localizer in button.GetComponentsInChildren<LocalizeStringEvent>(true))localizer.enabled=false;
        foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))text.text=label;
        var state=new SerializedObject(button);var calls=state.FindProperty("_onClick.m_PersistentCalls.m_Calls");if(calls!=null)calls.ClearArray();state.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void BuildCreatorConfirm()
    {
        var menu=EditorSceneManager.OpenScene(MenuScene,OpenSceneMode.Single);
        var manager=menu.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuManager>(true)).Single();var template=Ref<BaseButton>(manager,"_singleplayerButton");
        var creator=EditorSceneManager.OpenScene(CreatorScene,OpenSceneMode.Additive);
        var canvas=creator.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.name=="Canvas");
        var confirmTransform=canvas.transform.Find("ConfirmAndStartButton");var button=confirmTransform?confirmTransform.GetComponent<BaseButton>():null;
        if(!button){var go=UnityEngine.Object.Instantiate(template.gameObject,canvas.transform);go.name="ConfirmAndStartButton";button=go.GetComponent<BaseButton>();}
        CharacterCreationConfirmButton.ConfigureLayout(button);
        SetButtonText(button,"确认并开始游戏");if(!button.GetComponent<CharacterCreationConfirmButton>())button.gameObject.AddComponent<CharacterCreationConfirmButton>();
        EditorSceneManager.SaveScene(creator);
    }
    [MenuItem("Tools/Sushi & Fish/Open original island module")]
    public static void OpenIsland(){EditorSceneManager.OpenScene(GameScene,OpenSceneMode.Single);}
    [MenuItem("Tools/Sushi & Fish/Open original character creator module")]
    public static void OpenCreator(){EditorSceneManager.OpenScene(CreatorScene,OpenSceneMode.Single);}
    [MenuItem("Tools/Sushi & Fish/Open island start menu")]
    public static void OpenMenu(){EditorSceneManager.OpenScene(MenuScene,OpenSceneMode.Single);}
}
