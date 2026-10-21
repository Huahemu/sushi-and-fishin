using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Tables;
using PaperPixelGames.Core.DressingSceneSystem;

public static class IslandWorldIssues
{
    public static string Reports=>IslandFrontEndInventory.Reports;
    [Serializable] public class Obj {public string scene,path,type,json;public Vector3 position,size;}
    [Serializable] public class FontRow {public string path,source,material,texture,mode;public int glyphs,characters,width,height;public string missing;}
    [Serializable] public class Land {public string path,data,material;public Vector3 position,size;public int holes,solid;public string[] layers;}
    [Serializable] public class Report {public string utc;public FontRow[] fonts;public Obj[] waters,fences,beach;public Land[] lands;}
    public static IEnumerable<Transform> All(Scene scene)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true));
    public static string PathOf(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static void FontProbe()
    {
        var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MonoBehaviour/NotoSansSC-Bold SDF.asset");
        var source=f.sourceFontFile;var cloned=UnityEngine.Object.Instantiate(f);
        bool old=cloned.TryAddCharacters("保存主菜单继续设置图形声音返回游戏分辨率全屏命名餐厅",out string oldMissing);
        var fresh=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        bool added=fresh.TryAddCharacters("保存主菜单继续设置图形声音返回游戏分辨率全屏命名餐厅",out string freshMissing);
        File.WriteAllText(Path.Combine(Reports,"world-font-probe.json"),"{\"oldAdded\":"+old.ToString().ToLower()+",\"freshAdded\":"+added.ToString().ToLower()+",\"oldMissing\":\""+oldMissing+"\",\"freshMissing\":\""+freshMissing+"\",\"face\":"+JsonUtility.ToJson(f.faceInfo)+"}");
    }
    public static void Inventory()
    {
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        var fonts=new List<FontRow>();
        foreach(var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string p=AssetDatabase.GUIDToAssetPath(guid);var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);if(!f)continue;
            f.HasCharacters("保存主菜单继续设置图形声音返回游戏分辨率全屏命名餐厅",out uint[] missing,true,false);
            fonts.Add(new FontRow{path=p,source=AssetDatabase.GetAssetPath(f.sourceFontFile),material=AssetDatabase.GetAssetPath(f.material),texture=f.material&&f.material.mainTexture?AssetDatabase.GetAssetPath(f.material.mainTexture):"",mode=f.atlasPopulationMode.ToString(),glyphs=f.glyphTable.Count,characters=f.characterTable.Count,width=f.atlasWidth,height=f.atlasHeight,missing=missing==null?"":string.Concat(missing.Select(c=>char.ConvertFromUtf32((int)c)))});
        }
        var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        var water=new List<Obj>();var beach=new List<Obj>();var fences=new List<Obj>();var lands=new List<Land>();
        foreach(var t in All(game))
        {
            if(t.TryGetComponent<Terrain>(out var terrain))
            {
                var d=terrain.terrainData;var holes=d.GetHoles(0,0,d.holesResolution,d.holesResolution);int count=holes.Cast<bool>().Count(b=>!b);
                lands.Add(new Land{path=PathOf(t),data=AssetDatabase.GetAssetPath(d),position=t.position,size=d.size,holes=count,solid=holes.Length-count,material=AssetDatabase.GetAssetPath(terrain.materialTemplate),layers=d.terrainLayers.Select(l=>AssetDatabase.GetAssetPath(l)).ToArray()});
            }
            foreach(var c in t.GetComponents<Component>())if(c&&(c.GetType().Name.IndexOf("Ocean",StringComparison.OrdinalIgnoreCase)>=0||c.GetType().Name.IndexOf("Underwater",StringComparison.OrdinalIgnoreCase)>=0||c.GetType().Name=="WaterObject"))
                water.Add(new Obj{scene=game.name,path=PathOf(t),position=t.position,type=c.GetType().FullName,json=EditorJsonUtility.ToJson(c)});
        }
        foreach(var path in new[]{IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            foreach(var t in All(scene))
            {
                if(t.name.IndexOf("fence",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("railing",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("wall",StringComparison.OrdinalIgnoreCase)>=0)
                    fences.Add(new Obj{scene=scene.name,path=PathOf(t),position=t.position,size=t.TryGetComponent<Renderer>(out var r)?r.bounds.size:Vector3.zero});
                if(t.name.IndexOf("loung",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("deckchair",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("beachchair",StringComparison.OrdinalIgnoreCase)>=0)
                    beach.Add(new Obj{scene=scene.name,path=PathOf(t),position=t.position});
            }
        }
        File.WriteAllText(Path.Combine(Reports,"island-world-issues-inventory.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),fonts=fonts.ToArray(),waters=water.ToArray(),fences=fences.ToArray(),beach=beach.ToArray(),lands=lands.ToArray()},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    [Serializable] public class TerrainChange {public string path;public int changedHeights,filledHoles;public bool originalLandExact;}
    [Serializable] public class RepairReport {public string utc,fontAsset;public int bakedCharacters,atlasPages,fontsWithFallback,removedFenceObjects;public string[] removedFences,remainingDecorativeFences;public TerrainChange[] terrainChanges;public bool storesUnchanged;public float waterBase,waveHeight,seabedBase;}
    static string Fingerprint(Transform root)
    {
        var text=string.Join("\n",root.GetComponentsInChildren<Component>(true).Where(c=>c).Select(c=>c.GetType().FullName+"|"+EditorJsonUtility.ToJson(c)));
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
    }
    static TMP_FontAsset BakeChinese(out int count,out int changedFonts)
    {
        const string folder="Assets/SushiAndFish/UIFonts";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        const string asset=folder+"/ChineseInterfaceFallback.asset";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(asset))throw new InvalidOperationException("Font repair has already run; inspect existing assets before rebuilding");
        var chars=new HashSet<char>(Enumerable.Range(32,95).Select(i=>(char)i));
        foreach(var guid in AssetDatabase.FindAssets("t:StringTable"))
        {
            var table=AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(guid));
            if(!table||!table.LocaleIdentifier.Code.StartsWith("zh",StringComparison.OrdinalIgnoreCase))continue;
            foreach(var entry in table.Values)foreach(char c in entry.LocalizedValue??"")if(!char.IsControl(c)&&!char.IsSurrogate(c))chars.Add(c);
        }
        foreach(char c in "确认并开始游戏开始游戏读取存档暂无存档请返回选择开始游戏存档位已满请先删除一个不需要的存档继续保存返回主菜单设置图形声音游戏分辨率全屏控制鼠标灵敏度")chars.Add(c);
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/NotoSansSC-Bold.ttf");
        var font=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        font.name="ChineseInterfaceFallback";font.isMultiAtlasTexturesEnabled=true;
        bool added=font.TryAddCharacters(new string(chars.OrderBy(c=>c).ToArray()),out string missing);
        // Unsupported Unicode punctuation outside Chinese can remain on the original fallback stack.
        if(!added&&missing.Any(c=>c>=0x4e00&&c<=0x9fff))throw new InvalidOperationException("Chinese source font is missing required characters: "+missing);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,asset);
        font.material.name="ChineseInterfaceFallback Material";AssetDatabase.AddObjectToAsset(font.material,font);
        for(int i=0;i<font.atlasTextures.Length;i++){var texture=font.atlasTextures[i];texture.name="ChineseInterfaceFallback Atlas "+i;AssetDatabase.AddObjectToAsset(texture,font);EditorUtility.SetDirty(texture);}
        BindFontMaterial(font);
        changedFonts=0;
        foreach(var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));if(!f||f==font)continue;
            var fallbacks=f.fallbackFontAssetTable??new List<TMP_FontAsset>();fallbacks.RemoveAll(x=>x==font);fallbacks.Insert(0,font);f.fallbackFontAssetTable=fallbacks;
            if(f.name=="NotoSansSC-Bold SDF")f.atlasPopulationMode=AtlasPopulationMode.Static;
            EditorUtility.SetDirty(f);changedFonts++;
        }
        EditorUtility.SetDirty(font);count=font.characterTable.Count;return font;
    }
    public static void RepairFontMaterial()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SushiAndFish/UIFonts/ChineseInterfaceFallback.asset");
        if(!font||!font.material||font.atlasTextures.Length<1||!font.atlasTextures[0])throw new InvalidOperationException("Generated Chinese font assets are incomplete");
        BindFontMaterial(font);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
    }
    static void BindFontMaterial(TMP_FontAsset font)
    {
        // Player-only TMP has no editor font-authoring initialization. Use
        // property names so font construction does not depend on its lazy IDs.
        font.material.SetTexture("_MainTex",font.atlasTextures[0]);
        font.material.SetFloat("_TextureWidth",font.atlasWidth);font.material.SetFloat("_TextureHeight",font.atlasHeight);
        font.material.SetFloat("_GradientScale",font.atlasPadding+1);font.material.SetFloat("_WeightNormal",font.normalStyle);font.material.SetFloat("_WeightBold",font.boldStyle);
        EditorUtility.SetDirty(font.material);
    }
    public static void CalibrateSeaLevel()
    {
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        var ocean=All(scene).Select(t=>t.GetComponent<StylizedWater3.OceanFollowBehaviour>()).Single(c=>c);
        float sourceWater=RestaurantCoastSourceRepair.OriginalWaterLevel;
        float delta=sourceWater-ocean.transform.position.y;
        ocean.transform.position+=Vector3.up*delta;
        foreach(var area in All(scene).Select(t=>t.GetComponent<StylizedWater3.UnderwaterRendering.UnderwaterArea>()).Where(a=>a&&a.waterLevelSource==StylizedWater3.UnderwaterRendering.UnderwaterArea.WaterLevelSource.Ocean))
        {area.transform.position+=Vector3.up*delta;area.waterLevel=sourceWater;EditorUtility.SetDirty(area);}
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Reports,"world-sea-level-calibration.json"),"{\"waterBase\":"+sourceWater.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"volumeDelta\":"+delta.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"waveHeight\":0.15}");
        IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
    static bool DecorativeFence(Transform t)
    {
        string p=PathOf(t),n=t.name.ToLowerInvariant();
        if(p.StartsWith("Root/Comps/GabeMarket/")||p.StartsWith("Root/Comps/MarinaMarket/"))return false;
        bool lot=p.StartsWith("Root/Comps/")||p.StartsWith("Root/BambooPark/")||p.StartsWith("Root/City_Park_Forest/");
        return lot&&(n.Contains("fence")||n.Contains("railing")||n.Contains("wall")||n.StartsWith("sm_gate"));
    }
    public static void Repair()
    {
        if(!File.Exists(Path.Combine(Reports,"world-issues-backup.json")))throw new InvalidOperationException("Before-edit backup is required");
        var report=new RepairReport{utc=DateTime.UtcNow.ToString("o"),waterBase=-4,waveHeight=.15f,seabedBase=-18.3f};
        var font=BakeChinese(out report.bakedCharacters,out report.fontsWithFallback);report.fontAsset=AssetDatabase.GetAssetPath(font);report.atlasPages=font.atlasTextures.Length;
        var game=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single);
        var restaurant=All(game).Single(t=>PathOf(t)=="---Restaurant");string restaurantBefore=Fingerprint(restaurant);
        var grid=JsonUtility.FromJson<IslandSceneTrim.Grid>(File.ReadAllText(Path.Combine(Reports,"island-trim-distance-grid.json")));
        byte[] bytes=File.ReadAllBytes(Path.Combine(Reports,"island-trim-distance-grid.bytes"));var distances=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,distances,0,bytes.Length);
        Func<Vector3,float> distance=p=>{int x=Mathf.RoundToInt((p.x-grid.x0)/grid.step),z=Mathf.RoundToInt((p.z-grid.z0)/grid.step);return x<0||z<0||x>=grid.width||z>=grid.height?10000:distances[z*grid.width+x];};
        const string layerFolder="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/ShoreLayers";
        Directory.CreateDirectory(layerFolder);AssetDatabase.Refresh();
        var drySource=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainLayer/Sand_Terrain_01.terrainlayer");var wetSource=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainLayer/SWS_SandWet.terrainlayer");
        var dry=UnityEngine.Object.Instantiate(drySource);dry.name="TownSandMatte";dry.metallic=0;dry.smoothness=.04f;var dryMax=dry.diffuseRemapMax;dryMax.w=.04f;dry.diffuseRemapMax=dryMax;AssetDatabase.CreateAsset(dry,layerFolder+"/TownSandMatte.terrainlayer");
        var wet=UnityEngine.Object.Instantiate(wetSource);wet.name="TownSandDamp";wet.metallic=0;wet.smoothness=.12f;wet.specular=Color.black;var wetMax=wet.diffuseRemapMax;wetMax.w=.12f;wet.diffuseRemapMax=wetMax;var maskMax=wet.maskMapRemapMax;maskMax.w=.12f;wet.maskMapRemapMax=maskMax;AssetDatabase.CreateAsset(wet,layerFolder+"/TownSandDamp.terrainlayer");
        var changes=new List<TerrainChange>();
        foreach(var terrain in All(game).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray())
        {
            var d=terrain.terrainData;int n=d.heightmapResolution;var hs=d.GetHeights(0,0,n,n);var before=(float[,])hs.Clone();int changed=0,filled=0;bool exact=true;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                var p=terrain.transform.position+new Vector3(x*d.size.x/(n-1),hs[z,x]*d.size.y,z*d.size.z/(n-1));float dist=distance(p);
                if(p.y<-4.05f&&dist>0){float floor=Mathf.Max(-18f,-4.2f-dist*.08f);if(p.y<floor){hs[z,x]=(floor-terrain.transform.position.y)/d.size.y;changed++;}}
                if(dist==0&&hs[z,x]!=before[z,x])exact=false;
            }
            d.SetHeights(0,0,hs);
            int hr=d.holesResolution;var holes=d.GetHoles(0,0,hr,hr);
            for(int z=0;z<hr;z++)for(int x=0;x<hr;x++)if(!holes[z,x])
            {
                float nx=(x+.5f)/hr,nz=(z+.5f)/hr;float h=d.GetInterpolatedHeight(nx,nz)+terrain.transform.position.y;
                if(h<-4.05f){holes[z,x]=true;filled++;}
            }
            d.SetHoles(0,0,holes);var layers=d.terrainLayers;for(int i=0;i<layers.Length;i++){if(layers[i]==drySource)layers[i]=dry;else if(layers[i]==wetSource)layers[i]=wet;}d.terrainLayers=layers;EditorUtility.SetDirty(d);terrain.Flush();
            changes.Add(new TerrainChange{path=PathOf(terrain.transform),changedHeights=changed,filledHoles=filled,originalLandExact=exact});
        }
        // A real rendered and collidable bottom also covers water outside the original terrain tiles.
        var bottomData=new TerrainData{name="IslandOuterSeabed",heightmapResolution=33,alphamapResolution=16,baseMapResolution=16,size=new Vector3(4096,1,4096)};
        bottomData.terrainLayers=new[]{AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainLayer/SWS_OceanFloorSmooth.terrainlayer")};
        var alpha=new float[16,16,1];for(int y=0;y<16;y++)for(int x=0;x<16;x++)alpha[y,x,0]=1;bottomData.SetAlphamaps(0,0,alpha);
        AssetDatabase.CreateAsset(bottomData,"Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/IslandOuterSeabed.asset");
        var bottom=Terrain.CreateTerrainGameObject(bottomData);bottom.name="IslandOuterSeabed";SceneManager.MoveGameObjectToScene(bottom,game);bottom.transform.SetParent(All(game).Single(t=>PathOf(t)=="---World/Terrain"));bottom.transform.position=new Vector3(-2048,-18.3f,-2048);
        var terrainComponent=bottom.GetComponent<Terrain>();terrainComponent.materialTemplate=AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/TerrainLit.mat");terrainComponent.drawInstanced=true;bottom.layer=All(game).First(t=>t.GetComponent<Terrain>()&&t!=bottom.transform).gameObject.layer;
        foreach(string p in new[]{"Assets/Material/Stylized Water 3 High.mat","Assets/Material/Stylized Water 3 Medium.mat"})
        {var material=AssetDatabase.LoadAssetAtPath<Material>(p);if(!material)continue;material.SetFloat("_WaveHeight",.15f);EditorUtility.SetDirty(material);}
        report.storesUnchanged=Fingerprint(restaurant)==restaurantBefore;report.terrainChanges=changes.ToArray();EditorSceneManager.SaveScene(game);
        var city=EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/100_Dressing_City_01.unity",OpenSceneMode.Single);
        var shops=All(city).Where(t=>PathOf(t)=="Root/Comps/GabeMarket"||PathOf(t)=="Root/Comps/MarinaMarket").ToArray();var shopHashes=shops.Select(Fingerprint).ToArray();var removed=new List<string>();var ids=new HashSet<int>();
        foreach(var t in All(city).Where(DecorativeFence).ToArray())if(t)
        {
            removed.Add(PathOf(t));report.removedFenceObjects+=t.GetComponentsInChildren<Transform>(true).Length;
            foreach(var c in t.GetComponentsInChildren<Component>(true))if(c)ids.Add(c.GetInstanceID());foreach(var tr in t.GetComponentsInChildren<Transform>(true))ids.Add(tr.gameObject.GetInstanceID());UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        foreach(var c in All(city).SelectMany(t=>t.GetComponents<Component>()).Where(c=>c))
        {var so=new SerializedObject(c);var it=so.GetIterator();bool changed=false;while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&ids.Contains(it.objectReferenceInstanceIDValue)){it.objectReferenceValue=null;changed=true;}if(changed)so.ApplyModifiedPropertiesWithoutUndo();}
        foreach(var root in All(city).Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r)){var so=new SerializedObject(root);var groups=so.FindProperty("_groups");var remaining=root.Groups.Where(g=>g).ToArray();groups.arraySize=remaining.Length;for(int i=0;i<remaining.Length;i++)groups.GetArrayElementAtIndex(i).objectReferenceValue=remaining[i];so.ApplyModifiedPropertiesWithoutUndo();}
        report.storesUnchanged&=shops.Select(Fingerprint).SequenceEqual(shopHashes);report.removedFences=removed.ToArray();report.remainingDecorativeFences=All(city).Where(DecorativeFence).Select(PathOf).ToArray();EditorSceneManager.SaveScene(city);
        OriginalNativeShaderRestoration.RestoreSerializedReferences();AssetDatabase.SaveAssets();
        if(!report.storesUnchanged||changes.Any(c=>!c.originalLandExact)||report.remainingDecorativeFences.Length>0)throw new InvalidOperationException("A scoped scene repair invariant failed");
        File.WriteAllText(Path.Combine(Reports,"island-world-issues-repair.json"),JsonUtility.ToJson(report,true));
        IslandFrontEndBuilder.RefreshMenuPresentation();EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
}
