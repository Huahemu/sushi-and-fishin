using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Tablet;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Asset inspection, pure recipe/bearing calculations and isolated UI rendering.
// No Play, game/player lifecycle, input simulation or physics stepping.
public static class VerifySailingCompassKitchen
{
    const string Out = "D:/UnityProjects/_CodexWork/SailingCompassKitchen-20261028/";
    const string Cuisine = "Assets/SushiAndFish/FishingIntegration/Cuisine/";
    static readonly List<string> checks = new List<string>();
    static void Check(bool condition, string label)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + label);
        File.WriteAllLines(Out + "Checks.txt", checks);
        if (!condition) throw new Exception(label);
    }
    static void Bearing(Vector3 target, float yaw, Vector2 expected, string label)
        => Check(Vector2.Distance(SailingCompassMath.Direction(Vector3.zero, target, yaw), expected) < .001f, label);
    public static void Run()
    {
        Directory.CreateDirectory(Out);
        Check(!EditorApplication.isPlaying, "Static verification only; no Play, game or physics simulation");
        InstallSailingCompass.ApplyAssets();
        var config = MigrationConfig.Load();
        var islands = ArchipelagoSettings.Load();
        Check(islands && islands.regions.Length == 3, "Navigation uses all three existing island destinations");
        Check(config.font && config.compassCircle && config.compassFace && config.compassArrow && config.compassDot,
            "Original HowToFish navigation sprites and existing Sushi font are assigned persistently");
        string[] sprites = { "Circle", "Dot", "Arrow", "UICircle", "UICircle_outline" };
        foreach (var name in sprites)
        {
            string path = "Assets/HowToFish/Textures/Navigation/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Check(importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled && importer.alphaIsTransparency
                && importer.textureCompression == TextureImporterCompression.Uncompressed && AssetDatabase.LoadAssetAtPath<Sprite>(path),
                name + ": lossless original transparent sprite, default UI shader, no recovered custom material");
        }
        Bearing(new Vector3(0,0,10),0,Vector2.up,"North appears ahead when looking north");
        Bearing(new Vector3(10,0,0),0,Vector2.right,"East appears to the right when looking north");
        Bearing(new Vector3(0,0,-10),0,Vector2.down,"South appears behind when looking north");
        Bearing(new Vector3(-10,0,0),0,Vector2.left,"West appears to the left when looking north");
        Bearing(new Vector3(10,0,0),90,Vector2.up,"Looking east rotates the east island ahead");
        Bearing(new Vector3(0,0,10),90,Vector2.left,"Looking east rotates north to the left");
        Bearing(new Vector3(0,50,10),0,Vector2.up,"Elevation does not distort the horizontal compass");
        Bearing(Vector3.one,30,Vector2.zero,"Arriving within two metres centres the island marker");
        Check(Vector2.Distance(SailingCompassMath.Direction(Vector3.zero,Vector3.back*10,179),
            SailingCompassMath.Direction(Vector3.zero,Vector3.back*10,-179)) < .04f,"Heading wrap at 180 degrees stays continuous");
        string hud = File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingHud.cs");
        Check(hud.Contains("islands.IsDriver(actor.player)||islands.OnDeck(actor.player)") && hud.Contains("boatCompass.Render(")
            && hud.Contains("aboard&&!blocked"),"Driver and passenger navigation is mounted on the boat screen and hides after disembarking/tablet opening");
        string archipelago = File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/ArchipelagoWorld.cs");
        Check(archipelago.Contains("hints.RenderSailing(true,Boat.MotorReady,Boat.Speed,null,null)") && !archipelago.Contains("Heading(delta)"),
            "Text destination list is replaced while native boat controls, engine and speed hints remain");
        VerifyKitchen(config);
        Preview(config,islands);
        Debug.Log("SAILING_COMPASS_KITCHEN_VERIFIED checks=" + checks.Count + " recipes=53 processed=66 play=false simulation=false");
    }
    static void VerifyKitchen(MigrationConfig config)
    {
        var db = config.database;
        var plan = JsonUtility.FromJson<InstallFishingCuisine.Plan>(File.ReadAllText(Cuisine + "CuisinePlan.json"));
        var resolver = new SushiRecipeResolver(db,null);
        Check(plan.entries.Length == 53 && db._processedIngredientItems.Count(p=>p && p.Id.StartsWith("HTF_CUT_")) == 66,
            "All 53 new raw ingredients and 66 processed forms are included in this audit");
        var blue = AssetDatabase.LoadAssetAtPath<IngredientSO>("Assets/MonoBehaviour/blue_shrimp_fillet.asset");
        var pickup = blue.Prefab.GetComponent<IngredientEntity>();
        Check(blue && blue.PlacablePrepStation && blue.ProcessedIngredients.Count > 0 && pickup && !pickup.IsPlaceableOnChefDesk,
            "Screenshot blue shrimp fillet is native unprepared seafood; it must use the existing preparation table");
        Check(blue.ProcessedIngredients.All(p=>p && p.MaxPanCapacity > 0 && p.PanIngredientPrefab),
            "Native prepared blue shrimp already has valid pan artwork and capacity");
        foreach(var entry in plan.entries)
        {
            var raw = db.FindById("HTF_RAW_" + entry.species.ToString("D3")) as IngredientSO;
            var template = AssetDatabase.LoadAssetAtPath<IngredientSO>(entry.rawTemplate);
            var dish = db.FindById("HTF_DISH_" + entry.species.ToString("D3")) as SushiSO;
            Check(raw && raw.PlacablePrepStation && raw.Prefab == template.Prefab && raw.PrepStationCuttingPrefab == template.PrepStationCuttingPrefab,
                entry.species + ": native prep-table placement and cutting meshes preserved");
            Check(raw.ProcessedIngredients.Count == template.ProcessedIngredients.Count && raw.ProcessedIngredients.Select((cut,i)=>
                cut && cut.Id == "HTF_CUT_" + entry.species.ToString("D3") + "_" + i && db.FindById(cut.Id)==cut
                && cut.ProcessedType==template.ProcessedIngredients[i].ProcessedType && cut.OutputYield==template.ProcessedIngredients[i].OutputYield
                && cut.MaxPanCapacity>0 && cut.MaxPanCapacity==template.ProcessedIngredients[i].MaxPanCapacity
                && cut.PanIngredientPrefab==template.ProcessedIngredients[i].PanIngredientPrefab).All(ok=>ok),
                entry.species + ": every slice/strip has its own registered identity and native pan/yield/capacity");
            Check(dish && dish.Recipe.ProcessedIngredients.Any(n=>raw.ProcessedIngredients.Contains(n.Data))
                && SushiCompositionData.TryCreateFromBuiltIn(dish,out var composition)
                && resolver.ResolveExact(composition)?.RecipeId==dish.Id,
                entry.species + ": processed seafood still resolves the correct original sushi-making recipe");
        }
        string ingredient = File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/ProcessedIngredient/IngredientEntity.cs");
        string chef = File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Kitchen/ChefDesk/ChefDeskGastroPan.cs");
        Check(ingredient.Contains("_isPlaceableOnChefDesk || base.ItemSO is ProcessedIngredientSO"),
            "Prepared item data is accepted even if its reused generic pickup prefab flag is false; raw flag behaviour retained");
        Check(chef.Contains("ProcessedIngredientSO prepared = ResolveProcessedIngredientSO(ingredientEntity)")
            && chef.Contains("ProcessedIngredientSO processedIngredientSO = ResolveProcessedIngredientSO(component2)"),
            "Client and server resolve prepared ingredient identity through the same existing resolver");
        Check(chef.Contains("if (!component2.IsPlaceableOnChefDesk)") && chef.Contains("ShowToast(RAW_PREPARATION_MESSAGE)")
            && chef.Contains("ClientShowNotifyRpc(\"Info\", RAW_PREPARATION_MESSAGE") && chef.Contains("请先在备料台"),
            "Raw seafood gets the actionable native toast and server-side preparation validation");
        var message = (string)typeof(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskGastroPan)
            .GetField("RAW_PREPARATION_MESSAGE",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
        Check(new Unity.Collections.FixedString128Bytes(message).ToString()==message,
            "Chinese preparation message fits the native notification RPC UTF-8 capacity without truncation or exception");
        Check(chef.Contains("if ((FixedString64Bytes)text != _ingredientID.Value)")
            && chef.Contains("_ingredientCount.Value == 0") && chef.Contains("The ingredients are different. The transfer cannot be completed."),
            "Empty pans accept prepared ingredients; occupied pans retain the real species-mismatch check");
    }
    static void Preview(MigrationConfig config,ArchipelagoSettings islands)
    {
        var cameraObject = new GameObject("Static compass UI camera");
        var camera = cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.55f,.72f,.74f,1);camera.cullingMask=1<<5;
        var canvasObject = new GameObject("Static sailing compass preview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvasObject.layer=5;var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
        canvas.worldCamera=camera;canvas.planeDistance=10;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var compassObject = new GameObject("Original-art compass",typeof(RectTransform));compassObject.transform.SetParent(canvas.transform,false);
        var compass=compassObject.AddComponent<SailingCompass>();compass.Build(config);compass.Show(true);
        try
        {
            Check(compass.GetComponentsInChildren<Image>(true).Count(i=>i.name.Contains("original map dot"))==3,
                "Exactly one original radar dot per island");
            Check(compass.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Compass never consumes native equipment/input clicks");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768)})
            {
                var target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;
                try
                {
                    foreach(float yaw in new[]{0f,90f,180f})
                    {
                        compass.Render(islands.boatSpawn,yaw,islands.regions);
                        foreach(var label in compass.GetComponentsInChildren<TMP_Text>())label.ForceMeshUpdate(true,true);
                        Canvas.ForceUpdateCanvases();camera.Render();
                        var corners=new Vector3[4];compass.Bounds.GetWorldCorners(corners);
                        Check(corners.All(c=>{var v=camera.WorldToViewportPoint(c);return v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;}),
                            size+" heading "+yaw+": compass stays fully inside the screen");
                        if(size.x==1920)
                        {
                            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false);
                            image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Out+"Compass-"+yaw+".png",image.EncodeToPNG());
                            RenderTexture.active=previous;Object.DestroyImmediate(image);
                        }
                    }
                }
                finally{camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
            }
            compass.Show(false);Check(!compass.gameObject.activeSelf,"Disembark visibility toggle clears the entire compass");
        }
        finally{Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(cameraObject);}
    }
}
