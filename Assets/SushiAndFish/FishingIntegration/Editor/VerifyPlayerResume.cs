using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Pure serialization/file checks in a temporary work folder and static
// collision queries. No player lifecycle, Play or physics simulation.
public static class VerifyPlayerResume {
    const string Out="D:/UnityProjects/_CodexWork/PlayerResume-20261027/";
    const string Runtime="Assets/SushiAndFish/FishingIntegration/Runtime/";
    static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!ok)throw new Exception(label);}
    public static void Run(){
        Check(!EditorApplication.isPlaying,"Static verification only; no gameplay or physics stepping");
        string root=Out+"FileCases-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(root);
        var data=new PlayerResumeData{host="1",slot=1,steam=1,valid=true,position=new Vector3(32.123f,4.456f,78.789f),rotation=Quaternion.Euler(0,137,0),pitch=-31.25f};
        Check(PlayerResumeFiles.Valid(data,"1",1,1),"Valid exact pose and slot identity");
        var clone=JsonUtility.FromJson<PlayerResumeData>(JsonUtility.ToJson(data));
        Check(clone.position==data.position&&clone.rotation==data.rotation&&clone.pitch==data.pitch,"Position, yaw and pitch JSON roundtrip without snapping");
        Check(PlayerResumeFiles.Key("1",1,1)!=PlayerResumeFiles.Key("1",0,1)&&PlayerResumeFiles.Key("1",1,1)!=PlayerResumeFiles.Key("2",1,1)&&PlayerResumeFiles.Key("1",1,1)!=PlayerResumeFiles.Key("1",1,2),"Host, slot and player have independent records");
        Check(!PlayerResumeFiles.ValidIdentity("../escape",1,1)&&!PlayerResumeFiles.ValidIdentity("1",-1,1),"Invalid file identities rejected");
        Check(PlayerResumeFiles.Read(root,"1",1,1,out var protect)==null&&!protect,"Old saves without a pose keep native spawn and can begin recording");
        string path=PlayerResumeFiles.PathFor(root,"1",1,1);PlayerResumeFiles.WriteAtomic(path,JsonUtility.ToJson(data));
        Check(PlayerResumeFiles.Read(root,"1",1,1,out protect)?.position==data.position&&!protect,"Written pose reads for correct save");
        string original=File.ReadAllText(path);data.pitch=22;PlayerResumeFiles.WriteAtomic(path,JsonUtility.ToJson(data));
        Check(File.ReadAllText(path+".bak")==original&&Directory.GetFiles(root,"*.tmp").Length==0,"Atomic update retains previous file and leaves no temporary file");
        PlayerResumeFiles.WriteAtomic(PlayerResumeFiles.EpochPath(root,"1",1),"new-slot-generation");
        Check(PlayerResumeFiles.Read(root,"1",1,1,out protect)==null&&!protect,"Deleted/recreated save cannot resurrect a previous generation position");
        data.epoch="new-slot-generation";data.version=2;File.WriteAllText(path,JsonUtility.ToJson(data));original=File.ReadAllText(path);
        Check(PlayerResumeFiles.Read(root,"1",1,1,out protect)==null&&protect&&File.ReadAllText(path)==original,"Newer pose format preserved unchanged");
        File.WriteAllText(path,"{corrupt");Check(PlayerResumeFiles.Read(root,"1",1,1,out protect)==null&&protect&&File.ReadAllText(path)=="{corrupt","Corrupt pose preserved unchanged");
        data.version=1;data.position=new Vector3(float.NaN,0,0);Check(!PlayerResumeFiles.Valid(data,"1",1,1),"Nonfinite saved position rejected");
        StaticDestinations();Sources();
        InstallFishingMigrationAudit.Verify();
        Debug.Log("PLAYER_RESUME_VERIFIED checks="+checks.Count+" play=false simulation=false realSaveWrites=false");
    }
    static void StaticDestinations(){
        var floor=new GameObject("Resume static floor");var box=floor.AddComponent<BoxCollider>();floor.transform.position=new Vector3(3000,-.5f,3000);box.size=new Vector3(20,1,20);
        var wall=new GameObject("Resume static wall");var collider=wall.AddComponent<BoxCollider>();wall.transform.position=new Vector3(3000,1,3000);collider.size=new Vector3(.3f,2,.3f);wall.SetActive(false);
        try{
            var data=new PlayerResumeData{position=new Vector3(3000,0,3000)};Physics.SyncTransforms();
            Check(PlayerResumeController.TryDestination(data,null,-4,.4f,1.85f,null,out var point)&&point==data.position,"Clear supported land location is restored exactly");
            wall.SetActive(true);Physics.SyncTransforms();Check(!PlayerResumeController.Clear(data.position,.4f,1.85f,null),"Static wall overlap rejected");
            Check(PlayerResumeController.TryDestination(data,null,-4,.4f,1.85f,null,out point)&&point!=data.position&&Vector3.Distance(point,data.position)<2,"Obstructed position uses a nearby clear supported point");
            wall.SetActive(false);data.position=new Vector3(4000,-9,4000);data.swimming=true;Physics.SyncTransforms();
            Check(PlayerResumeController.TryDestination(data,null,-4,.4f,1.85f,null,out point)&&point==data.position,"Valid saved swimming point preserved");
            data.swimming=false;Check(!PlayerResumeController.TryDestination(data,null,-4,.4f,1.85f,null,out point),"Unsupported land point cannot place player in empty space");
            Check(!PlayerResumeController.TryDestination(null,null,-4,.4f,1.85f,null,out point),"Missing data safely rejected");
        }finally{Object.DestroyImmediate(floor);Object.DestroyImmediate(wall);}
    }
    static void Sources(){
        string source=File.ReadAllText(Runtime+"PlayerResumeController.cs");
        Check(source.Contains("!player.IsOwner")&&source.Contains("!player.IsLocalGameplayPreparationReady")&&source.Contains("!Islands"),"Restore waits for owned prepared character and installed island world");
        Check(source.Contains("player.IsTrapped||player.FirstPersonCharacter.HasActiveMotion||player.FirstPersonCharacter.IsPaused()")&&source.Contains("pending=null;ready=true"),"Restore waits through setup/cinematics and runs once");
        Check(source.Contains("public static void EndLocalSession")&&source.Contains("OnApplicationPause")&&source.Contains("OnApplicationQuit")&&source.Contains("Time.unscaledTime+2"),"Exit, background and periodic saves covered");
        string player=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Player/PPG_Player.cs");
        Check(player.Contains("PlayerResumeController.Ensure(this)")&&player.Contains("PlayerResumeController.EndLocalSession(this)"),"Native player spawn/despawn hooks compile");
        string coordinator=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Networking/MultiplayerCoordinator.cs");
        Check(coordinator.Contains("PlayerResumeController.EndLocalSession()"),"Session return captures before world teardown");
        string saves=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/SaveSystem/SaveManager.cs");
        Check(saves.Contains("PlayerResumeController.SaveLocalNow()")&&saves.Contains("PlayerResumeController.InvalidateSlot(item, num)"),"Host/client save and deleted-slot generation hooks installed");
        var validate=typeof(FishingMigration).GetMethod("ValidBoatHomeRequest",BindingFlags.NonPublic|BindingFlags.Static);
        var request=new BoatHomeRequest{slotKey="1_1",id=Guid.NewGuid().ToString("N")};
        Check((bool)validate.Invoke(null,new object[]{request,"1_1"})&&!(bool)validate.Invoke(null,new object[]{request,"1_0"}),"One-shot boat reset applies only to requested save");
        request.id="bad";Check(!(bool)validate.Invoke(null,new object[]{request,"1_1"}),"Invalid boat reset marker rejected");
        source=File.ReadAllText(Runtime+"MigrationBoatHome.cs");Check(source.Contains("if(SaveWorld()){File.Move")&&source.Contains("appliedBoatHomeId!=request.id"),"Reset request archived only after successful world save; retries do not relocate repeatedly");
        source=File.ReadAllText(Runtime+"ArchipelagoWorld.cs");Check(source.Contains("var target=Settings.boatSpawn")&&source.Contains("RelocateBoat(target,rotation)"),"Boat returns through existing safe relocation and passenger/cargo handling");
        var settings=ArchipelagoSettings.Load();Check(settings&&Vector3.Distance(settings.boatSpawn,new Vector3(49.62593f,-3.75f,93.01851f))<.01f,"Boat-home location matches original restaurant port asset");
    }
}
