using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendCasinoActor{public string personId,progressionKey;}
    [Serializable] public sealed class FriendCasinoRound{public string roundId,personId,machinePath,sourceEpisode,leaseId,phase,sourceSnapshotJson;public int restoredCount;public long lastRestoreActiveMicros,betCoins,balanceBeforeCents,balanceAfterCents;}
    [DefaultExecutionOrder(-55)] public sealed class FriendsCasinoController:MonoBehaviour
    {
        public static FriendsCasinoController Instance{get;private set;}public static bool IsApplicationQuitting{get;private set;}public string LastFailure{get;private set;}
        readonly Dictionary<string,FriendsCasinoCloneDriver> machines=new Dictionary<string,FriendsCasinoCloneDriver>();FriendsLifeRuntime runtime;readonly Dictionary<string,Vector3> restoreDestinations=new Dictionary<string,Vector3>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;IsApplicationQuitting=false;}
        void Awake(){Instance=this;if(!GetComponent<FriendsCasinoElevatorBridge>())gameObject.AddComponent<FriendsCasinoElevatorBridge>();}
        public static string PathOf(GameBase game){string path=game.name;for(var t=game.transform.parent;t;t=t.parent)path=t.name+"/"+path;return game.gameObject.scene.name+"/"+path;}
        public Slots[] ActualAvailableSlots()=>FindObjectsByType<Slots>(FindObjectsSortMode.None).Where(g=>g&&g.isActiveAndEnabled&&!g.isPlaying).ToArray();
        public bool TryPrepareMachine(string personId,Slots template,out AddonOwnedSlots machine)
        {
            machine=null;runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority||!template)return false;
            if(machines.TryGetValue(personId,out var old)&&old){if(old.SourcePath==PathOf(template)){machine=old.Game;return true;}if(old.Game.isPlaying)return false;old.Suspend();Destroy(old.gameObject);machines.Remove(personId);}
            var person=runtime.GetPerson(personId);var actor=runtime.ActorOf(personId);if(person==null||person.controllerKind!=PersonControllerKind.Npc||!person.addedByPlayer||!actor)return false;
            var profile=FriendsEconomyService.Instance.State.casinoActors.FirstOrDefault(p=>p.personId==personId);
            if(profile==null){if(!Guid.TryParse(personId,out var id))return false;ulong key=0xF100000000000000UL|(BitConverter.ToUInt64(id.ToByteArray(),0)&0x00FFFFFFFFFFFFFFUL);while(FriendsEconomyService.Instance.State.casinoActors.Any(p=>p.progressionKey==key.ToString(CultureInfo.InvariantCulture)))key++;profile=new FriendCasinoActor{personId=personId,progressionKey=key.ToString(CultureInfo.InvariantCulture)};FriendsEconomyService.Instance.State.casinoActors.Add(profile);}
            var renderers=template.GetComponentsInChildren<Renderer>();var bounds=renderers.Length>0?renderers[0].bounds:new Bounds(template.transform.position,Vector3.one);foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.enabled||!agent.isOnNavMesh)return false;Vector3 placement=default;bool found=false;
            foreach(float side in new[]{1f,-1f,2f,-2f})
            {
                var point=template.transform.position+template.transform.right*(bounds.size.x+.4f)*side;
                if(!NavMesh.SamplePosition(point,out var hit,.6f,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))continue;
                var center=bounds.center+(hit.position-template.transform.position);var box=bounds.extents;box.y=Mathf.Max(.05f,box.y-.06f);center.y+=.06f;
                if(Physics.OverlapBox(center,box,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.bounds.max.y>bounds.min.y+.1f&&!c.transform.IsChildOf(actor.transform)))continue;
                placement=new Vector3(hit.position.x,template.transform.position.y,hit.position.z);found=true;break;
            }
            if(!found){LastFailure="附近原导航上没有可放置新增专属机台的净空。";return false;}
            var driver=FriendsCasinoCloneDriver.Create(template,personId,PathOf(template),placement,template.transform.rotation,ulong.Parse(profile.progressionKey,CultureInfo.InvariantCulture),person.displayName);
            machines[personId]=driver;machine=driver.Game;return true;
        }
        public bool TryStartSlotsAtStation(string personId,GameBase machine,long quoteCoins,string sourceEpisode,out string roundId)
        {
            roundId=null;runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!economy||!Mirror.NetworkServer.active||!runtime.IsWorldRunning)return false;
            if(!(machine is AddonOwnedSlots owned)||!owned.Driver||!owned.Driver.Owned||owned.Driver.PersonId!=personId){LastFailure="需要新增 NPC 专属机台。";return false;}
            var actor=runtime.ActorOf(personId);if(!actor||Vector3.Distance(actor.transform.position,machine.transform.position)>3.5f||machine.isPlaying)return false;
            if(runtime.State.leases.Any(l=>l.personId==personId&&l.ownerEpisode!=sourceEpisode))return false;
            long bet=quoteCoins>0?quoteCoins:machine.MinBet;if(bet<machine.MinBet||bet>machine.MaxBet||bet>economy.AvailableCents(personId)/100)return false;
            string id="slots/"+sourceEpisode+"/"+personId;var prior=economy.State.casinoRounds.FirstOrDefault(x=>x.roundId==id);if(prior!=null){roundId=id;return prior.phase=="Playing"||prior.phase=="Settled";}
            var state=new FriendCasinoRound{roundId=id,personId=personId,machinePath=owned.Driver.SourcePath,sourceEpisode=sourceEpisode,leaseId="casino-native-round/"+id,phase="Starting",betCoins=bet,balanceBeforeCents=economy.BalanceCents(personId)};
            using(economy.DeferExternalCommit())
            {
                economy.State.casinoRounds.Add(state);runtime.State.leases.Add(new ActivityLease{leaseId=state.leaseId,personId=personId,ownerEpisode=sourceEpisode,facilityId="Casino",phase="NativeRound",safeBoundary=false});
                if(!owned.Driver.StartRound(id,bet)){state.phase="RejectedBySource";runtime.State.leases.RemoveAll(l=>l.leaseId==state.leaseId);LastFailure=owned.Driver.Failure??"原机台尚不能开始。";return false;}
                state.phase="Playing";try{state.sourceSnapshotJson=owned.Driver.Capture();}catch(NotSupportedException e){LastFailure=e.Message;}roundId=id;runtime.NotifyChanged();return true;
            }
        }
        void Update()
        {
            runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||!runtime.IsAuthority)return;var economy=FriendsEconomyService.Instance;if(!economy)return;
            foreach(var round in economy.State.casinoRounds.Where(x=>x.phase=="Playing"||x.phase=="Restoring").ToArray())
            {
                if(!machines.TryGetValue(round.personId,out var driver)||!driver){if(string.IsNullOrEmpty(round.sourceSnapshotJson))continue;TryRestore(round);continue;}
                if(driver.RoundId!=round.roundId){TryRestore(round);continue;}
                if(driver.Failure!=null){round.phase="AwaitSourceSnapshot";LastFailure=driver.Failure;runtime.NotifyChanged();continue;}
                if(driver.Game.isPlaying)continue;
                if(driver.Phase!="NativeComplete")continue;round.phase="Settled";round.balanceAfterCents=economy.BalanceCents(round.personId);runtime.State.leases.RemoveAll(l=>l.leaseId==round.leaseId);runtime.NotifyChanged();runtime.SaveNow();
            }
        }
        void TryRestore(FriendCasinoRound round)
        {
            try
            {
                var template=FriendsCasinoElevatorBridge.FindMachine(round.machinePath);if(!(template is Slots source)||!source.isActiveAndEnabled)return;
                if(!TryPrepareMachine(round.personId,source,out var game))return;
                var snapshot=JObject.Parse(round.sourceSnapshotJson);var saved=(JArray)snapshot["position"];
                game.transform.SetPositionAndRotation(new Vector3((float)saved[0],(float)saved[1],(float)saved[2]),Quaternion.Euler(0,(float)snapshot["yaw"],0));
                var actor=runtime.ActorOf(round.personId);if(!actor||actor.Brain==null)return;
                if(!restoreDestinations.TryGetValue(round.roundId,out var resolved))
                {
                    var target=game.keypadSpawnPoint?game.keypadSpawnPoint.position:game.transform.position;actor.Brain.Pause();
                    if(!actor.Brain.Movement.MoveTo(target,actor.Brain.EffectiveMoveSpeed,.55f,true))return;resolved=actor.Brain.Movement.Destination;restoreDestinations[round.roundId]=resolved;
                }
                if(!actor.Brain.Movement.HasArrived()||Vector3.Distance(actor.transform.position,resolved)>.9f||Vector3.Distance(actor.transform.position,game.transform.position)>3.5f){round.phase="Restoring";return;}
                if(game.Driver.Restore(round.sourceSnapshotJson,round.roundId)){round.restoredCount++;round.lastRestoreActiveMicros=runtime.ActiveMicros;round.phase="Playing";restoreDestinations.Remove(round.roundId);runtime.NotifyChanged();}
            }
            catch(Exception e){LastFailure=e.GetBaseException().Message;round.phase="AwaitSourceSnapshot";runtime.NotifyChanged();}
        }
        public FriendsCasinoCloneDriver DriverFor(string personId)=>machines.TryGetValue(personId,out var d)?d:null;
        public bool IsAtSafeBoundary(string personId)=>!FriendsEconomyService.Instance||!FriendsEconomyService.Instance.State.casinoRounds.Any(r=>r.personId==personId&&(r.phase=="Starting"||r.phase=="Playing"||r.phase=="Restoring"||r.phase=="AwaitSourceSnapshot"));
        public bool IsAtAllSafeBoundaries()=>!FriendsEconomyService.Instance||!FriendsEconomyService.Instance.State.casinoRounds.Any(r=>r.phase=="Starting"||r.phase=="Playing"||r.phase=="Restoring"||r.phase=="AwaitSourceSnapshot");
        public void CaptureSourceRounds(){if(!FriendsEconomyService.Instance)return;foreach(var round in FriendsEconomyService.Instance.State.casinoRounds.Where(r=>r.phase=="Playing"))if(machines.TryGetValue(round.personId,out var driver)&&driver&&driver.RoundId==round.roundId&&driver.Failure==null){try{round.sourceSnapshotJson=driver.Capture();}catch(NotSupportedException e){LastFailure=e.Message;}}}
        public void OnEconomyRestored(){foreach(var m in machines.Values)if(m){m.Suspend();Destroy(m.gameObject);}machines.Clear();restoreDestinations.Clear();}
        public bool PreserveQuittingRound(GameBase game)=>false;
        void OnApplicationQuit(){IsApplicationQuitting=true;CaptureSourceRounds();foreach(var d in machines.Values)if(d)d.Suspend();}
        void OnDestroy(){foreach(var d in machines.Values)if(d){d.Suspend();Destroy(d.gameObject);}if(Instance==this)Instance=null;}
    }
}
