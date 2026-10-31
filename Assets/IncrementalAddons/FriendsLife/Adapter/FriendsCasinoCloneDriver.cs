using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using PortSlots=IncrementalAddons.Casino.SourceReuse.Slots;
using PortReel=IncrementalAddons.Casino.SourceReuse.SlotReel;
namespace SushiAndFish.FriendsLife
{
    [DefaultExecutionOrder(31030)] public sealed class FriendsCasinoCloneDriver:MonoBehaviour
    {
        public bool Owned{get;private set;}public bool Suspended{get;private set;}public string PersonId{get;private set;}public string RoundId{get;private set;}
        public AddonOwnedSlots Game{get;private set;}public PlayerInteract Input{get;private set;}public string SourcePath{get;private set;}
        public string Phase{get;private set;}="Idle";public string Failure{get;private set;}
        public List<PortReel> Reels=>(List<PortReel>)NativeCasinoCloneCodec.Get(Game,"reels");public List<SlotPattern> Patterns=>(List<SlotPattern>)NativeCasinoCloneCodec.Get(Game,"patterns");
        internal long NativeStep;internal int NativeDepth;int nextRoutine;readonly List<NativeCasinoTrackedRoutine> routines=new List<NativeCasinoTrackedRoutine>();string lastCapture;readonly Dictionary<PortReel,JObject> pendingReelStarts=new Dictionary<PortReel,JObject>();
        internal string Relative(Transform t){if(t==transform)return "";var list=new List<string>();for(;t&&t!=transform;t=t.parent)list.Add(t.name);list.Reverse();return string.Join("/",list);}
        internal Transform Find(string path)
        {
            if(!string.IsNullOrEmpty(path)&&(path.StartsWith("/",StringComparison.Ordinal)||path.Split('/').Any(p=>p=="..")))throw new InvalidOperationException("Native snapshot reference leaves the NEW owned clone");
            var result=string.IsNullOrEmpty(path)?transform:transform.Find(path);if(!result||!(result==transform||result.IsChildOf(transform)))throw new InvalidOperationException("Native snapshot owned object is missing");return result;
        }
        static void CopyFields(object source,object target,Type start,Type stop,Dictionary<SlotReel,PortReel> map=null)
        {
            for(var t=start;t!=null&&t!=stop;t=t.BaseType)foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {
                if(f.IsStatic||f.IsInitOnly||!(f.IsPublic||f.IsDefined(typeof(SerializeField),true)))continue;var dest=NativeCasinoCloneCodec.Field(target.GetType(),f.Name);if(dest==null)continue;
                if(f.Name=="reels"&&map!=null){dest.SetValue(target,((List<SlotReel>)f.GetValue(source)).Select(x=>map[x]).ToList());continue;}
                if(dest.FieldType.IsAssignableFrom(f.FieldType))dest.SetValue(target,f.GetValue(source));
            }
        }
        public static FriendsCasinoCloneDriver Create(Slots template,string actor,string sourcePath,Vector3 position,Quaternion rotation,ulong profileKey,string displayName)
        {
            if(!template||template.isPlaying||template.isGoldenChipApplied||template.isGoldenBet||!Mirror.NetworkServer.active)throw new InvalidOperationException("Idle original template and active source server required");
            var staging=new GameObject("New NPC machine staging");staging.SetActive(false);var clone=Instantiate(template.gameObject,position,rotation,staging.transform);clone.name="NpcNativeSlots_"+actor;clone.transform.localScale=template.transform.lossyScale;
            foreach(var keypad in clone.GetComponentsInChildren<Keypad>(true))DestroyImmediate(keypad.gameObject);
            var map=new Dictionary<SlotReel,PortReel>();foreach(var original in clone.GetComponentsInChildren<SlotReel>(true)){var replacement=original.gameObject.AddComponent<PortReel>();CopyFields(original,replacement,typeof(SlotReel),typeof(Mirror.NetworkBehaviour));map.Add(original,replacement);}
            var old=clone.GetComponent<Slots>();var game=clone.AddComponent<AddonOwnedSlots>();CopyFields(old,game,typeof(Slots),typeof(Mirror.NetworkBehaviour),map);
            foreach(var oldReel in map.Keys)DestroyImmediate(oldReel);DestroyImmediate(old);
            var driver=clone.AddComponent<FriendsCasinoCloneDriver>();driver.Owned=true;driver.PersonId=actor;driver.SourcePath=sourcePath;driver.Game=game;game.Driver=driver;NativeCasinoCloneCodec.Set(game,"keypad",null);game.isPlaying=false;
            var identity=clone.GetComponent<Mirror.NetworkIdentity>();if(identity){var field=NativeCasinoCloneCodec.Field(identity.GetType(),"sceneId");if(field!=null)field.SetValue(identity,0UL);}
            var profileRoot=new GameObject("NpcNativeInputProfile");profileRoot.SetActive(false);profileRoot.AddComponent<Mirror.NetworkIdentity>();var profile=profileRoot.AddComponent<NpcCasinoProfile>();profile.enabled=false;profile.playerName=displayName;profile.steamId=profileKey;
            var buff=profileRoot.AddComponent<PlayerBuff>();buff.enabled=false;driver.Input=profileRoot.AddComponent<PlayerInteract>();driver.Input.enabled=false;
            clone.transform.SetParent(null,true);Destroy(staging);profileRoot.SetActive(true);Mirror.NetworkServer.Spawn(profileRoot);clone.SetActive(true);Mirror.NetworkServer.Spawn(clone);return driver;
        }
        public bool StartRound(string round,long quote)
        {
            RoundId=round;Failure=null;NativeStep=0;Suspended=false;
            using(FriendsEconomyService.Instance.DeferExternalCommit())using(CasinoInvocation.EnterOwned(this,"begin")){Game.Keypad.Network_currentInput=quote.ToString();Game.ServerSetBet(quote);Game.TryStartGame(Input);}
            return Game.isPlaying&&routines.Any(x=>x.active);
        }
        public IEnumerator TrackNative(MonoBehaviour component,IEnumerator original)
        {
            if(!Owned||!component||!component.transform.IsChildOf(transform)||original==null)throw new InvalidOperationException("Only NEW owned clone native iterators can be tracked");
            if(component is PortReel started)pendingReelStarts.Remove(started);
            var tracked=new NativeCasinoTrackedRoutine(this,component,original,++nextRoutine);routines.Add(tracked);return tracked;
        }
        public void NoteNativeReelStart(PortReel reel,float duration,int turns,int seed)
        {if(!Owned||!reel.transform.IsChildOf(transform))throw new InvalidOperationException("Not an owned reel");pendingReelStarts[reel]=new JObject{["reel"]=Reels.IndexOf(reel),["duration"]=duration,["turns"]=turns,["seed"]=seed};}
        internal void NativeFailure(Exception e){Failure=e.GetBaseException().Message;Phase="AwaitInspection";FriendsLifeRuntime.Instance?.NotifyChanged();}
        internal void NativeAfterMove(bool checkpoint=false)
        {
            if(NativeDepth>0||Suspended||!Game)return;
            Phase=!Game.isPlaying?"NativeComplete":routines.Any(x=>x.active&&ReferenceEquals(x.component,Game)&&x.raw.GetType().Name.Contains("EndGameRoutine"))?"Result":"Spin";
            if(!checkpoint)return;
            try{lastCapture=Capture();FriendsCasinoController.Instance?.CaptureSourceRounds();}catch(Exception e){NativeFailure(e);}
        }
        void LateUpdate(){if(Game&&!Game.isPlaying&&Failure==null&&!string.IsNullOrEmpty(RoundId))Phase="NativeComplete";}
        public string Capture()
        {
            if(NativeDepth>0){if(lastCapture!=null)return lastCapture;throw new InvalidOperationException("Native MoveNext has not reached a saved boundary");}
            var reels=new JArray();foreach(var reel in Reels)
            {
                var data=new JObject();foreach(string f in new[]{"_symbolPosY","_step","_botY","_reelLenght","_currentSeed","symbols"})data[f]=NativeCasinoCloneCodec.Encode(NativeCasinoCloneCodec.Get(reel,f),this);
                data["visuals"]=new JArray(((Image[])NativeCasinoCloneCodec.Get(reel,"symbols")).Select(x=>new JObject{["image"]=NativeCasinoCloneCodec.Encode(x,this),["sprite"]=NativeCasinoCloneCodec.Encode(x.sprite,this),["x"]=x.rectTransform.anchoredPosition.x,["y"]=x.rectTransform.anchoredPosition.y}));reels.Add(data);
            }
            var buffs=new JObject();var buff=Input.GetComponent<PlayerBuff>();foreach(PlayerBuffType kind in Enum.GetValues(typeof(PlayerBuffType)))buffs[kind.ToString()]=buff.GetValue(kind);
            return new JObject{["schema"]=2,["profileBuffs"]=buffs,["keypadInput"]=Game.Keypad.Network_currentInput,["payoutText"]=(NativeCasinoCloneCodec.Get(Game,"payoutText") as TMPro.TMP_Text)?.text,["sourcePath"]=SourcePath,["phase"]=Phase,["step"]=NativeStep,["nextRoutine"]=nextRoutine,
                ["nativeSeed"]=NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed,["nativeDays"]=NetworkSingleton<GameManager>.Instance.daysPassed,
                ["position"]=new JArray(transform.position.x,transform.position.y,transform.position.z),["yaw"]=transform.eulerAngles.y,
                ["pendingReelStarts"]=new JArray(pendingReelStarts.Values),["machine"]=NativeCasinoCloneCodec.Machine(this),["routines"]=new JArray(routines.Where(x=>x.active).Select(x=>x.Capture())),["reels"]=reels}.ToString(Newtonsoft.Json.Formatting.None);
        }
        public bool Restore(string json,string round)
        {
            var o=JObject.Parse(json);if((int?)o["schema"]!=2||(string)o["sourcePath"]!=SourcePath)return false;
            if((int?)o["nativeSeed"]!=NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed||(int?)o["nativeDays"]!=NetworkSingleton<GameManager>.Instance.daysPassed)throw new InvalidOperationException("Original seed/day differs; preserved without replay");
            RoundId=round;Phase=(string)o["phase"];NativeStep=(long)o["step"];nextRoutine=(int)o["nextRoutine"];var pos=(JArray)o["position"];transform.SetPositionAndRotation(new Vector3((float)pos[0],(float)pos[1],(float)pos[2]),Quaternion.Euler(0,(float)o["yaw"],0));
            NativeCasinoCloneCodec.RestoreMachine(this,(JObject)o["machine"]);NativeCasinoCloneCodec.Set(Game,"interactingPlayer",Input.GetComponent<PlayerProfile>());Game.Keypad.Network_currentInput=(string)o["keypadInput"]??Game.currentBet.ToString();
            var payout=NativeCasinoCloneCodec.Get(Game,"payoutText") as TMPro.TMP_Text;if(payout&&o["payoutText"]!=null)payout.text=(string)o["payoutText"];
            if(o["profileBuffs"] is JObject buffs){var dict=NativeCasinoCloneCodec.Get(Input.GetComponent<PlayerBuff>(),"_buffs");var index=dict.GetType().GetProperty("Item");foreach(var pair in buffs)index.SetValue(dict,(float)pair.Value,new object[]{Enum.Parse(typeof(PlayerBuffType),pair.Key)});}
            for(int i=0;i<Reels.Count;i++){var reel=Reels[i];var d=(JObject)o["reels"][i];foreach(var pair in d.Properties().Where(p=>p.Name!="visuals")){var f=NativeCasinoCloneCodec.Field(reel.GetType(),pair.Name);f.SetValue(reel,NativeCasinoCloneCodec.Decode(pair.Value,f.FieldType,this));}
                foreach(JObject v in d["visuals"]){var image=(Image)NativeCasinoCloneCodec.Decode(v["image"],typeof(Image),this);image.sprite=(Sprite)NativeCasinoCloneCodec.Decode(v["sprite"],typeof(Sprite),this);image.rectTransform.anchoredPosition=new Vector2((float)v["x"],(float)v["y"]);}}
            routines.Clear();Suspended=false;foreach(JObject state in ((JArray)o["routines"]).OrderBy(x=>(int)x["id"])) {var tracked=NativeCasinoTrackedRoutine.Restore(state,this);routines.Add(tracked);tracked.component.StartCoroutine(tracked);}pendingReelStarts.Clear();foreach(JObject pending in (JArray)(o["pendingReelStarts"]??new JArray()))
            {var reel=Reels[(int)pending["reel"]];NativeCasinoCloneCodec.Invoke(reel,typeof(PortReel),"UserCode_RpcStartScrolling__Single__Int32__Int32",(float)pending["duration"],(int)pending["turns"],(int)pending["seed"]);}
            lastCapture=Capture();return true;
        }
        public void Suspend(){if(!Owned)return;Suspended=true;Game.isPlaying=false;Game.StopAllCoroutines();foreach(var reel in Reels)reel.StopAllCoroutines();}
        void OnDestroy(){if(Input)Destroy(Input.gameObject);}
    }
}
