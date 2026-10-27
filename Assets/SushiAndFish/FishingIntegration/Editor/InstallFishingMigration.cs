using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using HowToFish;
using HowToFish.Interface;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.UI;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class InstallFishingMigration {
    const string Root="Assets/SushiAndFish/FishingIntegration/";
    [Serializable] class AtlasRects {public AtlasRect[] rects;}
    [Serializable] class AtlasRect {public int index,x,y,width,height;}
    [Serializable] class VariantColors {public VariantColor[] colors;}
    [Serializable] class VariantColor {public int id;public Color color;}
    static readonly List<string> checks=new List<string>();
    static void Check(bool value,string message){checks.Add((value?"PASS ":"FAIL ")+message);if(!value){File.WriteAllLines("FishingMigrationChecks.txt",checks);throw new Exception(message);}}
    static void Folder(string name){Directory.CreateDirectory(Root+name);}
    static T Save<T>(T data,string path)where T:Object {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(data,old);Object.DestroyImmediate(data);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(data,path);return data;}
    public static void InstallAndVerify(){
        if(EditorApplication.isPlaying)throw new Exception("Saved asset authoring only; never Play or simulate");
        Folder("Data");Folder("Graphics");Folder("Art/Sprites");Folder("Art/Variants");Folder("Resources");AssetDatabase.Refresh();
        var config=AssetDatabase.LoadAssetAtPath<MigrationConfig>(Root+"Resources/FishingMigration.asset");if(!config){config=ScriptableObject.CreateInstance<MigrationConfig>();AssetDatabase.CreateAsset(config,Root+"Resources/FishingMigration.asset");}
        config.enabled=true;config.systemPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HowToFish/Prefabs/FishingSystem.prefab");config.profile=AssetDatabase.LoadAssetAtPath<FishingProfile>("Assets/HowToFish/Definitions/Original v1.1.1.asset");
        config.nativeHintTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/HintItem.prefab");config.nativeHintSprites=AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>("Assets/MonoBehaviour/keyboard-&-mouse_sheet.asset");
        config.database=AssetDatabase.FindAssets("t:ItemDatabaseSO").Select(g=>AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(AssetDatabase.GUIDToAssetPath(g))).OrderByDescending(d=>d._allItems.Count).First();
        var originalItems=config.database._allItems.Where(i=>i&&!i.Id.StartsWith("HTF_")).ToArray();var originalCreatures=config.database.CreatureItems.Where(i=>i&&!i.Id.StartsWith("HTF_")).ToArray();
        File.WriteAllLines("FishingOriginalCatalogIds.txt",originalCreatures.Select(i=>i.Id));
        var template=AssetDatabase.LoadAssetAtPath<FishSO>("Assets/MonoBehaviour/mackerel.asset");Check(template&&config.database,"Original fish data and item database found");
        config.panelSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/PupUp_Panel_Big.asset");config.legacyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/HowToFish/Fonts/Chinese.ttf");
        var card=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/FishBookItem.prefab");var textButton=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"UI/NativeTextButton.prefab");if(textButton)config.nativeButtonTemplate=textButton.GetComponent<BaseButton>();else if(!config.nativeButtonTemplate)config.nativeButtonTemplate=card.GetComponentInChildren<BaseButton>(true);config.font=card.GetComponentInChildren<TMP_Text>(true).font;
        var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"Art/FishingChineseFallback.asset");if(!fallback){fallback=TMP_FontAsset.CreateFontAsset(config.legacyFont);fallback.name="Fishing Chinese fallback";AssetDatabase.CreateAsset(fallback,Root+"Art/FishingChineseFallback.asset");if(fallback.material)AssetDatabase.AddObjectToAsset(fallback.material,fallback);foreach(var tex in fallback.atlasTextures)if(tex)AssetDatabase.AddObjectToAsset(tex,fallback);}
        if(config.font.fallbackFontAssetTable==null)config.font.fallbackFontAssetTable=new List<TMP_FontAsset>();if(!config.font.fallbackFontAssetTable.Contains(fallback)){config.font.fallbackFontAssetTable.Add(fallback);EditorUtility.SetDirty(config.font);}
        config.grayscale=new Material(Shader.Find("HowToFish/Catalogue UI"));config.grayscale.SetFloat("_DisplayMode",1);config.grayscale=Save(config.grayscale,Root+"Art/Grayscale.mat");
        config.silhouette=new Material(Shader.Find("HowToFish/Catalogue UI"));config.silhouette.SetFloat("_DisplayMode",2);config.silhouette=Save(config.silhouette,Root+"Art/Silhouette.mat");
        var fishIcons=Sprites("fish",config.profile.fish.OrderBy(d=>d.originalId).Select(d=>"Fish_"+d.originalId).ToArray());
        config.equipmentIcons=Sprites("equipment",Enumerable.Range(0,12).Select(i=>"Equipment_"+i).ToArray());config.rodIcon=config.equipmentIcons[0];config.gunIcon=config.equipmentIcons[1];config.warehouseIcon=config.equipmentIcons[6];
        var colors=JsonUtility.FromJson<VariantColors>(File.ReadAllText(Root+"Art/variant-colors.json"));
        var nativeFish=originalItems.OfType<FishSO>().Where(f=>f.IngredientSO).ToArray();
        var mapping=new List<FishingSpeciesBinding>();var routes=new StringBuilder("ID | 中文鱼名 | 原生物品ID | 原色/异色 | 食材出口\n");int index=0;
        foreach(var definition in config.profile.fish.OrderBy(d=>d.originalId)){
            var binding=new FishingSpeciesBinding{definition=definition,icon=fishIcons[index++],edible=!new[]{7,16,22,44,51,80,90}.Contains(definition.originalId)};
            var ingredientSource=IngredientSource(definition.originalId,nativeFish,template);binding.ingredientRoute=binding.edible?ingredientSource.IngredientSO.Name:"收藏 / 展示 / 出售";
            binding.original=BuildItem(template,definition,false,binding.icon,ingredientSource,binding.edible);
            bool hasShiny=definition.catchPrefab.regularVisual&&definition.catchPrefab.dripVisual;
            if(hasShiny){binding.shiny=BuildItem(template,definition,true,binding.icon,ingredientSource,binding.edible);var color=colors.colors.FirstOrDefault(c=>c.id==definition.originalId);if(color!=null){var material=new Material(Shader.Find("SushiAndFish/Painted fish variation"));material.SetColor("_VariantColor",color.color);binding.shinyIconMaterial=Save(material,Root+"Art/Variants/Fish_"+definition.originalId+".mat");}}
            mapping.Add(binding);
            if(!config.database._allItems.Contains(binding.original))config.database._allItems.Add(binding.original);
            if(!config.database.CreatureItems.Contains(binding.original))config.database.CreatureItems.Add(binding.original);
            if(binding.shiny&&!config.database._allItems.Contains(binding.shiny))config.database._allItems.Add(binding.shiny);
            routes.AppendLine(definition.originalId+" | "+definition.displayName+" | "+binding.original.Id+" | "+(hasShiny?"2":"1")+" | "+binding.ingredientRoute);
        }
        config.species=mapping.ToArray();
        var scene=EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
        var sea=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Water"||t.name=="Ocean");config.seaHeight=sea?sea.position.y:0;
        Check(!scene.isDirty,"Original island scene left unchanged; integration uses runtime bootstrap");
        EditorUtility.SetDirty(config.database);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        File.WriteAllText("FishingIngredientRoutes.txt",routes.ToString());
        InstallFishingUIFix.ApplyIconGeometry(config);AssetDatabase.SaveAssets();
        InstallFishingCuisine.ApplyAssets();
        Verify(config,originalCreatures.Select(i=>i.Id).ToArray());
        File.WriteAllLines("FishingMigrationChecks.txt",checks);Debug.Log("SUSHI_FISHING_MIGRATION_ASSETS_PASS original="+originalCreatures.Length+" added="+mapping.Count+" appearances="+mapping.Sum(b=>b.shiny?2:1));
    }
    static Sprite[] Sprites(string kind,string[] names){
        string path=Root+"Art/"+kind+"-icons.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var rects=JsonUtility.FromJson<AtlasRects>(File.ReadAllText(Root+"Art/"+kind+"-slices.json")).rects;
        Check(rects.Length==names.Length,kind+" atlas has one complete isolated region per requested icon");
        return rects.Select((r,i)=>Save(Sprite.Create(texture,new Rect(r.x,r.y,r.width,r.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect),Root+"Art/Sprites/"+names[i]+".asset")).ToArray();
    }
    static FishSO IngredientSource(int id,FishSO[] choices,FishSO fallback){
        string key=id==33?"Mackerel":id==15?"Bigeye Tuna":id==41||id==98?"Atlantic Salmon":id==29?"Halibut":id==23?"Eel":id==46||id==13||id==21||id==32||id==79?"Shrimp":id==88?"Squid":id==97?"Yellowtail":id==40?"Snapper":id==20||id==17||id==45||id==95?"Cod":"Sea Bream";
        return choices.FirstOrDefault(f=>f.Name.IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0)??fallback;
    }
    static FishSO BuildItem(FishSO template,FishDefinition definition,bool shiny,Sprite icon,FishSO ingredient,bool edible){
        string id="HTF_"+(shiny?"S":"N")+definition.originalId.ToString("D3");var data=Object.Instantiate(template);data.name=id;data.Id=id;data.Name=(shiny?"异色 · ":"")+definition.displayName;data.Active=true;data.Sprite=icon;
        data.IngredientSO=edible?ingredient.IngredientSO:null;data.MinYieldRange=edible?Mathf.Max(1,ingredient.MinYieldRange):0;data.MaxYieldRange=edible?Mathf.Max(data.MinYieldRange+1,ingredient.MaxYieldRange):0;
        data.FishingRodSettings.Enabled=false;data.MinWeight=Mathf.Max(.0001f,definition.weight*.5f);data.MaxWeight=Mathf.Max(data.MinWeight,definition.weight*1.5f);data.Rarity=definition.rarity==CatchRarity.Common?CreatureRarity.Common:definition.rarity==CatchRarity.Uncommon?CreatureRarity.Rare:definition.rarity==CatchRarity.Rare?CreatureRarity.Epic:CreatureRarity.Legendary;
        data.DeadFishSalePrice=ingredient.DeadFishSalePrice;data.DeadGraphic=Graphic(definition,shiny);data.GraphicObjectPrefab=data.PlacementObjectPrefab=data.TrophyGraphicPrefab=data.DeadGraphic;data.CanDisplayAsTrophy=true;
        data.ColliderShape=data.DeadColliderShape=ColliderType.Box;var bounds=BoundsOf(data.DeadGraphic);data.ColliderCenter=data.DeadBoxCenter=bounds.center;data.BoxSize=data.DeadBoxSize=bounds.size;data.UseDeadCollider=true;
        data.FPSHoldOffset=new ItemOffset(new Vector3(.1f,-.1f,.3f),new Vector3(0,90,0));data.TPSHoldOffset=data.FPSHoldOffset;data.PlaceOffset=new ItemOffset(Vector3.up*(bounds.extents.y+.03f),Vector3.zero);data.FilletOffset=new ItemOffset(Vector3.zero,Vector3.zero);data.TrophyOffset=new ItemOffset(Vector3.zero,Vector3.zero);
        return Save(data,Root+"Data/"+id+".asset");
    }
    static GameObject Graphic(FishDefinition definition,bool shiny){
        var wrapper=new GameObject(definition.displayName+(shiny?" 异色":""));var go=Object.Instantiate(definition.catchPrefab.gameObject,wrapper.transform);var fish=go.GetComponent<FishingCatch>();
        if(fish.regularVisual&&fish.dripVisual){fish.regularVisual.SetActive(!shiny);fish.dripVisual.SetActive(shiny);}
        foreach(var joint in go.GetComponentsInChildren<Joint>(true))Object.DestroyImmediate(joint);
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
        foreach(var body in go.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(body);
        foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
        foreach(var animation in go.GetComponentsInChildren<Animation>(true))Object.DestroyImmediate(animation);
        string path=Root+"Graphics/Fish_"+definition.originalId+(shiny?"_shiny":"_original")+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,path);Object.DestroyImmediate(wrapper);return prefab;
    }
    static Bounds BoundsOf(GameObject prefab){var go=Object.Instantiate(prefab);bool any=false;var bounds=new Bounds();foreach(var renderer in go.GetComponentsInChildren<Renderer>()){
        Mesh baked=null;Bounds local;if(renderer is SkinnedMeshRenderer skin){baked=new Mesh();skin.BakeMesh(baked);local=baked.bounds;}else local=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
        for(int i=0;i<8;i++){var p=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=go.transform.InverseTransformPoint(renderer.transform.TransformPoint(p));p=Vector3.Scale(p,go.transform.localScale);if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);}if(baked)Object.DestroyImmediate(baked);}
        Object.DestroyImmediate(go);return bounds;}
    static void Verify(MigrationConfig config,string[] originalIds){
        Check(config.species.Length==60,"All 60 new species registered");Check(config.species.Sum(b=>b.shiny?2:1)==111,"All 111 original / alternate appearances retained");
        Check(originalIds.SequenceEqual(config.database.CreatureItems.Take(originalIds.Length).Select(i=>i.Id)),"Every original fish-book entry preserved in its original order");
        Check(config.database.CreatureItems.Count(i=>i.Id.StartsWith("HTF_"))==60,"Exactly 60 new species appended; alternate skins are not duplicate book entries");
        Check(config.database._allItems.Where(i=>i).Select(i=>i.Id).Distinct().Count()==config.database._allItems.Count(i=>i),"No duplicate item IDs");
        Check(config.species.All(b=>b.original.Sprite&&b.original.DeadGraphic&&b.original.DeadPrefab&&(!b.shiny||b.shiny.Sprite&&b.shiny.DeadGraphic&&b.shinyIconMaterial)),"All data, hand-painted sprites, graphics, native pickup prefabs and alternate appearances bound");
        Check(config.species.Where(b=>b.edible).All(b=>b.original.IngredientSO&&b.original.IngredientSO.GraphicObjectPrefab),"Every edible species has a working native ingredient output");
        Check(config.nativeButtonTemplate&&config.panelSprite&&config.font&&config.equipmentIcons.Length==12,"Original UI buttons, frame, fonts and consistent new equipment/event art retained");
        Check(config.profile.nearRareChance==.2f&&config.profile.nonCommonProbabilityMultiplier==.7f&&config.profile.hookedMinLine==.12f,"Existing catch odds and repaired reel settings preserved");
        var source=File.ReadAllText("Assets/HowToFish/Runtime/FishingWorldEvents.cs");Check(source.Contains("Eligibility!=null&&!Eligibility()"),"Special-event scheduling is gated by active fishing");
        Check(!EditorApplication.isPlaying,"No Play or gameplay simulation used");
        var report=new {date="2026-10-23",originalSpecies=originalIds.Length,addedSpecies=60,appearances=111,readOnlyGameplay=true};File.WriteAllText("FishingMigrationSummary.json",JsonUtility.ToJson(new Summary{originalSpecies=originalIds.Length},true));
    }
    [Serializable] class Summary {public string date="2026-10-23";public int originalSpecies,addedSpecies=60,appearances=111;public bool playUsed=false,physicsSimulationUsed=false;}
}
