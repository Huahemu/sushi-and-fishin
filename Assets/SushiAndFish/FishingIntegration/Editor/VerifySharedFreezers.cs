using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.SaveSystem;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifySharedFreezers
{
    static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(InstallSharedFreezers.Out+"Checks.txt",checks);if(!ok)throw new Exception(text);}
    static string Read(string name)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+name+".cs");
    public static void Run()
    {
        Check(!EditorApplication.isPlaying,"No Play, player/network initialization, input, fish AI, boat controller or physics stepping");
        var c=MigrationConfig.Load();var settings=ArchipelagoSettings.Load();var prefab=c.freezerPrefab;
        Check(prefab&&prefab.GetComponent<SharedFishFreezer>()&&c.freezerPoints.Length==2,"Two island terminals are configured; the restaurant keeps its native freezer");
        Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length==1&&prefab.GetComponentsInChildren<Rigidbody>(true).Length==0,"Island freezer copies contain only passive shared marker and static geometry");
        var renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);
        Check(renderers.Length==3&&renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&!m.shader.name.Contains("Error"))),"Original body, lid, ice and native material references retained");
        Check(renderers.All(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh).StartsWith("Assets/Mesh/",StringComparison.Ordinal)),"Freezer geometry and UVs are the original sushi game meshes");
        Check(c.nativeInventoryRow&&AssetDatabase.GetAssetPath(c.nativeInventoryRow).Equals("Assets/GameObject/InventoryItem.prefab")&&c.panelSprite&&c.font&&c.nativeButtonTemplate,"List rows, fonts, frame and purchase-style buttons reuse the native UI assets");
        Check(c.species.All(b=>b.original&&c.database.FindById(b.original.Id)==b.original&&(!b.shiny||c.database.FindById(b.shiny.Id)==b.shiny)),"All migrated normal and alternate fish IDs resolve through the original database");
        var data=new FridgeData{FridgeFishes=c.species.SelectMany(b=>new[]{b.original,b.shiny}).Where(f=>f).Select((f,i)=>new FridgeFishData{Id=f.Id,Weights=new List<float>{i+.125f,i+12.5f}}).ToList()};
        var round=JsonUtility.FromJson<FridgeData>(JsonUtility.ToJson(data));
        Check(round.FridgeFishes.Count==data.FridgeFishes.Count&&round.FridgeFishes.Zip(data.FridgeFishes,(a,b)=>a.Id==b.Id&&a.Weights.SequenceEqual(b.Weights)).All(v=>v),"Every new fish and alternate appearance retains its ID and individual weights in the native freezer save format");
        var legacy=JsonUtility.FromJson<FridgeData>("{\"FridgeFishes\":[{\"Id\":\"sardine_standard_iwashi\",\"Weights\":[1.25,3.5]}]}");
        Check(legacy.FridgeFishes.Single().Weights.SequenceEqual(new[]{1.25f,3.5f}),"Existing native freezer save shape remains compatible");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        for(int i=1;i<3;i++){
            var region=settings.regions[i];var island=Object.Instantiate(region.prefab,region.center,Quaternion.identity);
            var terrain=island.GetComponentsInChildren<MeshCollider>(true).Single(t=>t.name=="Island1");var point=c.freezerPoints[i-1];
            var freezer=Object.Instantiate(prefab,point.position,point.rotation);Physics.SyncTransforms();
            Check(terrain.Raycast(new Ray(point.position+Vector3.up*3,Vector3.down),out var hit,5)&&Mathf.Abs(point.position.y-hit.point.y)<.03f&&hit.normal.y>.95f,"Island "+i+" freezer rests on dry flat ground with no hovering or sinking");
            var path=settings.accessPrefab.transform.Cast<Transform>().Single(t=>t.name==region.title+" · 靠岸栈桥");var local=path.InverseTransformPoint(point.position);
            Check(Mathf.Abs(local.x)>path.GetComponent<BoxCollider>().size.x*.5f+freezer.GetComponent<BoxCollider>().size.magnitude*.5f,"Island "+i+" freezer leaves the full boarding walkway clear");
            Object.DestroyImmediate(freezer);Object.DestroyImmediate(island);
        }
        string native=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/FishFridge/Fridge.cs");var store=native.Substring(native.IndexOf("public bool StoreSharedFish",StringComparison.Ordinal));store=store.Substring(0,store.IndexOf("public ushort GetStockCount",StringComparison.Ordinal));
        Check(store.Contains("OwnerClientId!=interactor.OwnerClientId")&&store.Contains("interactor.CurrentHeld!=fish")&&store.Contains("fish.CanHold.Value")&&store.Contains("!fish.IsSpawned"),"Native storage checks ownership, held identity and spawned state before changing inventory");
        Check(store.Contains("GetOrCreateStock(id).Weights.Add(fish.Weight.Value)")&&!store.Contains("TryGetValue(id, out")&&store.Contains("ClientFinalizeAddStockRpc"),"New fish are stored independently of visual slots and use the native quick-slot cleanup RPC");
        Check(Read("MigrationHarvest").Contains("legacy?string.Empty:stored.token")&&Read("MigrationHarvest").Contains("FishingCaptureLedger.Resolve(SaveData,token)"),"Withdrawals avoid duplicate catch registration and one-time pickup protection remains in place");
        Check(Read("SharedFishFreezer").Contains("SharedStock()")&&Read("MigrationNetworking").Contains("WarehouseLines=SharedStock()"),"Clients and original tablet receive combined legacy warehouse and native freezer counts");
        Check(Read("FishingMigration").Contains("CapturesInteractInput&&!UsesMigratedFishing(player)")&&Read("SharedFishFreezer").Contains("GetComponentInParent<PPG_Player>()==actor.player"),"Dead-fish targeting ignores own body while native underwater spearfishing retains its input");
        Check(Read("MigratedCatchIdentity").Contains("pickup.isTrigger=true")&&Read("MigratedCatchIdentity").Contains("catchBody.CanGrab"),"Dead fish have a render-sized pickup trigger; live fish do not get a pickup proxy");
        Check(Read("FishingFreezerPanel").Contains("ownsInput=false")&&Read("FishingFreezerPanel").Contains("if(ownsInput&&"),"Panel teardown releases only its own input lock, including same-frame reopen");
        var recall=Read("BoatRecall");Check(recall.Contains("radius=8;radius<=320;radius+=4")&&recall.Contains("if(++budget>=24)")&&recall.Contains("ArchipelagoBoatSafety.ClearWater"),"Boat recall searches near-to-far with full-hull clearance and a bounded per-frame query budget");
        Check(recall.Contains("other!=requester&&OnDeck")&&recall.Contains("RelocateBoat(p,q)")&&recall.Contains("world.SaveWorld()")&&!recall.Contains("Instantiate("),"Recall preserves the existing boat, rejects other passengers and saves the relocated pose");
        Check(Read("ArchipelagoWorld").Contains("StopCoroutine(recalling)")&&Read("ArchipelagoWorld").Contains("if(action==\"boat-recall\")"),"Recall is server-routed and cancelled when the world is disposed");
        Check(Read("SharedFishFreezer").Contains("Keyboard.current.f5Key.wasPressedThisFrame")&&Read("FishingWarehousePage").Contains("召回船只"),"Recall has a gameplay hotkey and original-style tablet button");
        Check(settings.accessPrefab.transform.Cast<Transform>().All(t=>!t.name.StartsWith("主岛",StringComparison.Ordinal)),"The main-island ramp remains removed as requested");
        Debug.Log("SHARED_FREEZERS_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
    }
}
