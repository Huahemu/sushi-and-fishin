using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.Visuals;
using SushiAndFish.FriendsLife.SourceGraphs;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // R: IslandLifeHudActivities.CaptureActivitiesPhoto and
    // IslandLifeGameFeatures.CapturePhoto -> original RecordActivityPhoto.
    // The host supplies an owned Bozo photo rig rather than moving a live
    // actor or borrowing the player's camera. Unlocks, rewards, pose indices,
    // framing zoom factor, PNG dimensions and record rules remain source owned.
    public sealed class FriendActivityPhotography:MonoBehaviour
    {
        [Serializable] public sealed class Input {public string presetId;public int pose,angle,framing;}
        [Serializable] sealed class CaptureReceipt
        {
            public string world,actor,selected,token,operation,path,presetId,phase,error,photoId;
            public int pose,angle,framing,width=1600,height=1000;
        }
        public static FriendActivityPhotography Instance {get;private set;}
        readonly Dictionary<string,CaptureReceipt> captures=new Dictionary<string,CaptureReceipt>();
        readonly Dictionary<string,CaptureReceipt> operations=new Dictionary<string,CaptureReceipt>();
        FriendsLifeRuntime runtime;string world;float next;
        public string LastError {get;private set;}
        public int Pending=>captures.Values.Count(x=>x.phase=="Rendering");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {if(Instance)return;var go=new GameObject("Friends activity photography");DontDestroyOnLoad(go);Instance=go.AddComponent<FriendActivityPhotography>();}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority)return;
            if(runtime!=r||world!=r.State.worldId)
            {
                runtime=r;world=r.State.worldId;
                runtime.RegisteredPhotoCaptureCommand=Begin;
                runtime.RegisteredPhotoRecordCommand=Record;
                captures.Clear();operations.Clear();LoadReceipts();
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        void LoadReceipts()
        {
            string folder=Path.Combine(PhotoFriendPaths.Folder("Photos"),"CaptureReceipts",world);if(!Directory.Exists(folder))return;
            foreach(string path in Directory.EnumerateFiles(folder,"*.json"))
            {
                try
                {
                    var record=JsonUtility.FromJson<CaptureReceipt>(File.ReadAllText(path));
                    if(record==null||record.world!=world||!Guid.TryParseExact(record.token,"N",out _)||string.IsNullOrEmpty(record.operation))continue;
                    var photo=runtime.Core.State.activities.photos.FirstOrDefault(x=>x.id==record.photoId||!string.IsNullOrEmpty(record.path)&&x.path==Path.GetFullPath(record.path));
                    if(photo!=null){record.photoId=photo.id;record.phase="Recorded";}
                    else if(record.phase=="Rendering"||record.phase=="Captured"||record.phase=="RecordRequested"){record.phase="Unknown";record.error="上次拍摄尚未确认完成，请查看相册。";}
                    captures[record.token]=record;operations[record.operation]=record;
                }
                catch(Exception error){Debug.LogWarning("[FriendsLife] Photograph receipt: "+error.Message);}
            }
        }
        string Begin(string actorId,string selectedId,string optionJson,string op)
        {
            var r=runtime;var actor=r?.GetPerson(actorId);var person=r?.GetPerson(selectedId);
            if(!r||!r.Ready||!r.IsAuthority||world!=r.State.worldId||actor==null||person==null)return "人物尚未准备好。";
            if(actor.controllerKind==PersonControllerKind.Human&&person.controllerKind==PersonControllerKind.Human&&actorId!=selectedId)return "该人物不属于当前玩家。";
            if(operations.TryGetValue(op,out var previous))return previous.phase=="Recorded"?"照片已加入相册。":previous.phase=="Rendering"?"正在拍摄…":previous.error??"照片尚未完成。";
            Input input;try{input=JsonUtility.FromJson<Input>(optionJson??"{}");}catch(Exception){return "请选择摄影选项。";}
            if(input==null||input.pose<0||input.pose>4||input.angle<0||input.angle>7||input.framing<0||input.framing>2||!r.Core.IsPhotoUnlocked(input.presetId))return "请选择已解锁的摄影场景与有效构图。";
            if(r.Core.IsResidentAway(person.nativeResidentId))return "朋友还在旅行或梦中，请等回来后再合影。";
            if(captures.Values.Any(x=>x.world==world&&x.selected==selectedId&&x.phase=="Rendering"))return "正在拍摄…";
            var receipt=new CaptureReceipt{world=world,actor=actorId,selected=selectedId,token=Guid.NewGuid().ToString("N"),operation=op,presetId=input.presetId,pose=input.pose,angle=input.angle,framing=input.framing,phase="Rendering"};
            captures[receipt.token]=receipt;operations[op]=receipt;Persist(receipt);Render(receipt,r,person);return "正在拍摄…";
        }
        async void Render(CaptureReceipt receipt,FriendsLifeRuntime owner,PersonBinding person)
        {
            Texture2D image=null;
            try
            {
                var creative=owner.Core.State.creative.residents.FirstOrDefault(x=>x.residentId==person.nativeResidentId);
                CharacterData data=person.controllerKind==PersonControllerKind.Human&&owner.LocalHuman()?.personId==person.personId
                    ?PlayerCustomizationSession.GetCurrentCharacterData():FriendsLifeVisualProjection.ProjectCharacter(owner,person,creative);
                if(data==null)throw new InvalidOperationException("人物外貌尚未准备好。");
                // Exact original R zoom multiplier; the base framing belongs
                // to this independent host rig, in host character units.
                float framing=Mathf.Pow(.88f,receipt.framing==1?2:receipt.framing==2?-2:0);
                image=await FriendCharacterPreview.Render(data,1600,false,system=>
                {
                    var motion=system.gameObject.AddComponent<OriginalMotionRetargeter>();motion.ProjectRootPose=true;
                    if(!motion.PlayPhotoSourcePose(receipt.pose,"photo/"+receipt.token,out var error))throw new InvalidOperationException(error);
                },receipt.angle*45,1000,framing);
                if(!owner||owner!=FriendsLifeRuntime.Instance||!owner.Ready||!owner.IsAuthority||owner.State.worldId!=receipt.world)
                    throw new InvalidOperationException("场景已切换，照片没有保存。");
                string directory=Path.Combine(PhotoFriendPaths.Folder("Photos"),receipt.world);Directory.CreateDirectory(directory);
                string safe=string.Concat((receipt.presetId+"-"+person.displayName).Where(c=>!Path.GetInvalidFileNameChars().Contains(c)));if(safe.Length>40)safe=safe.Substring(0,40);
                receipt.path=Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+safe+".png");
                File.WriteAllBytes(receipt.path,image.EncodeToPNG());receipt.phase="Captured";Persist(receipt);
                string message=Record(receipt.actor,receipt.selected,receipt.token,receipt.operation);
                if(receipt.phase!="Recorded")throw new InvalidOperationException(message);
                owner.NotifyChanged();owner.SaveNow();LastError=null;
            }
            catch(Exception error){receipt.error="照片没有成功保存，请重试。";receipt.phase="Failed";LastError=error.ToString();Persist(receipt);if(owner)owner.NotifyChanged();Debug.LogWarning("[FriendsLife] Activity photograph: "+error);}
            finally{if(image)Destroy(image);}
        }
        string Record(string actorId,string selectedId,string token,string op)
        {
            var r=runtime;
            if(!r||!r.Ready||!r.IsAuthority||!captures.TryGetValue(token,out var capture)||capture.world!=r.State.worldId||capture.actor!=actorId||capture.selected!=selectedId||capture.operation!=op)return "照片尚未准备好。";
            if(capture.phase=="Recorded")return "照片已加入相册。";
            if(capture.phase!="Captured"||string.IsNullOrEmpty(capture.path)||!File.Exists(capture.path)||new FileInfo(capture.path).Length==0)return "照片没有成功保存，请重试。";
            var selected=r.GetPerson(selectedId);if(selected==null)return "人物已经离岛。";
            LifeActivitiesResult result=null;
            capture.phase="RecordRequested";Persist(capture);
            var committed=r.ExecuteKernel("photo-result/"+token,actorId,scope=>
            {
                result=scope.RecordActivityPhoto(selected.nativeResidentId,capture.presetId,capture.path,pose:capture.pose,angle:capture.angle);
                return result.success;
            },token);
            if(!committed.success)return result?.message??committed.message;
            var photo=r.Core.State.activities.photos.FirstOrDefault(x=>x.residentId==selected.nativeResidentId&&x.path==Path.GetFullPath(capture.path));
            if(photo==null)return "照片记录尚未准备好。";
            capture.photoId=photo.id;capture.phase="Recorded";capture.error=null;Persist(capture);
            r.LinkEcologyPhotoRecord(actorId,selectedId,photo.id,op);r.NotifyChanged();r.SaveNow();return "照片已加入相册。";
        }
        static void Persist(CaptureReceipt receipt)
        {string folder=Path.Combine(PhotoFriendPaths.Folder("Photos"),"CaptureReceipts",receipt.world);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,receipt.token+".json"),JsonUtility.ToJson(receipt,true));}
    }
}
