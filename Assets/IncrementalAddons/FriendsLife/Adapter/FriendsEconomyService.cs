using HousingIsland=SushiAndFish.FriendsLife.FriendHousingIsland;
using HousingPlacement=SushiAndFish.FriendsLife.FriendHousingPlacement;
using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendWallet { public string personId; public long balanceCents,reservedCents,revision; }
    [Serializable] public sealed class EconomyReceipt { public string opId,domain,actorId,counterparty; public long deltaCents,revision; }
    [Serializable] public sealed class HomeGroup { public string groupId,homeId; public List<string> members=new List<string>(); public List<string> ownerHomes=new List<string>(); public List<SharedHomeReturnTemplate> returnHomes=new List<SharedHomeReturnTemplate>(); public int level,vacancySchema; public long revision; }
    [Serializable] public sealed class FriendBill { public string billId,personId,facility,sourceFoodId; public long priceCents; public bool delivered,committed,cancelled,quotePending; }
    [Serializable] public sealed class EarnForIntent { public string personId,intentId; public long targetCents,sequence; }
    [Serializable] public sealed class HousingPlanCommand { public string plotId,typeId,homeId,doorPath,privatePersonId,familySuggestionId;public float yaw;public bool open;public string splitGroupId;public long splitRevision;public List<HomeSplitChoice> splitHomes; }
    [Serializable] public sealed class QueuedHomeUpgrade {public string homeId,opId,homesSignature,homeInstanceId;public long quoteCents;}
    [Serializable] public sealed class HomeVisitPermit {public string homeId,personId,ownerEpisode,privatePersonId,homeInstanceId;}
    // workedDay is the original day+1 of the last started shift (0 = none): one shift a day.
    [Serializable] public sealed class AuthorizedSushiJob {public string personId;public StaffRole role;public StaffEmployeeData retainedEmployee;public int workedDay;}
    [Serializable] public sealed class AuthorizedPublicJob {public string authorizationId,personId,kind;public long nextShiftSequence;}
    public enum FriendJobPhase { Queued, Travel, OnDuty, Handover, Done }
    [Serializable] public sealed class FriendJobContract
    {
        public string contractId,slotId,personId,payer,finishReason; public Vector3 workPosition; public int employeeId; public long workedMicros,paidCents,wageRemainder,paySequence,budgetCents; public FriendJobPhase state;
        public int wageCentsPerMinute=200;
        public int staffWorkDay; // original day+1; persists if employment is changed or cancelled
        public bool cabinReturnRequired;public Vector3 cabinGate,cabinDuty;
    }
    [Serializable] public sealed partial class FriendsEconomySave
    {
        public string casinoTravelJson;
        public int version=1; public long revision; public List<FriendWallet> wallets=new List<FriendWallet>(); public List<EconomyReceipt> receipts=new List<EconomyReceipt>();
        public List<HomeGroup> groups=new List<HomeGroup>(); public List<FriendBill> bills=new List<FriendBill>(); public List<FriendJobContract> jobs=new List<FriendJobContract>();
        public List<HousingPlacement> placements=new List<HousingPlacement>();
        public List<HomeVacatedGeneration> vacatedHomes=new List<HomeVacatedGeneration>();
        public List<EarnForIntent> incomeIntents=new List<EarnForIntent>();
        public List<FriendFacilityVisit> facilityVisits=new List<FriendFacilityVisit>();
        public List<QueuedHomeUpgrade> queuedUpgrades=new List<QueuedHomeUpgrade>();
        public List<HomeVisitPermit> homeVisits=new List<HomeVisitPermit>();
        public List<AuthorizedSushiJob> sushiAuthorizations=new List<AuthorizedSushiJob>();
        public List<AuthorizedPublicJob> publicJobAuthorizations=new List<AuthorizedPublicJob>();
        public List<FriendCasinoActor> casinoActors=new List<FriendCasinoActor>();
        public List<FriendCasinoRound> casinoRounds=new List<FriendCasinoRound>();
    }

    // The host serializes this entire projection inside FriendsSnapshot. All
    // public commands run on the main-thread authority and use stable receipts.
    [DefaultExecutionOrder(-45)]
    public sealed partial class FriendsEconomyService : MonoBehaviour
    {
        public static FriendsEconomyService Instance {get;private set;}
        public const long InitialCents=5000,UpgradeCents=20000;
        public FriendsEconomySave State {get;private set;}=new FriendsEconomySave();
        public string LastFailure {get;private set;}
        public event Action Changed;
        FriendsLifeRuntime runtime; float nextAuto; bool projecting,portsBound,canonicalHousingLoaded,housingProjectionPending=true;int externalCommitDepth;
        readonly Dictionary<string,FriendJobStation> stations=new Dictionary<string,FriendJobStation>();
        readonly HashSet<string> pendingHousingCommands=new HashSet<string>();
        // A public shift refused at departure for lack of a route is retried
        // after this pause instead of every 2 s: each attempt adds a contract
        // and commits a full world save.
        const float PublicRouteRetrySeconds=30;
        readonly Dictionary<string,float> publicRetryAt=new Dictionary<string,float>(StringComparer.Ordinal);
        FriendsLifeNetwork housingNetwork;
        EconomyHandler PlayerWallet=>Singleton<GameProgressManager>.Instance?.EconomyHandler;
        bool Authority=>runtime&&runtime.IsAuthority;
        public bool HasCanonicalHousing=>runtime&&runtime.Ready&&canonicalHousingLoaded;
        public long ReservedPlayerCents=>State.wallets.FirstOrDefault(w=>w.personId=="player")?.reservedCents??0;
        public void RestoreHousingProjection()=>RestorePlacements();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {if(!Instance){var go=new GameObject("FriendsEconomyService");DontDestroyOnLoad(go);go.AddComponent<FriendsEconomyService>();}}
        // Facility stations are scene objects: rescan when scenes change, with
        // a slow fallback, instead of five scene-wide searches every 2 s.
        bool stationScanDirty=true;float nextStationScan;
        void MarkStationScan(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)=>stationScanDirty=true;
        void MarkStationScan(UnityEngine.SceneManagement.Scene scene)=>stationScanDirty=true;
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;UnityEngine.SceneManagement.SceneManager.sceneLoaded+=MarkStationScan;UnityEngine.SceneManagement.SceneManager.sceneUnloaded+=MarkStationScan;gameObject.AddComponent<FriendsFacilityController>();gameObject.AddComponent<FriendsHomeInteriors>();gameObject.AddComponent<FriendsCasinoController>();gameObject.AddComponent<FriendsSushiBillingAuthority>();}
        void Update()
        {
            if(runtime!=FriendsLifeRuntime.Instance)
            {
                if(runtime)runtime.WorldTick-=Tick;
                runtime=FriendsLifeRuntime.Instance;portsBound=false;
            }
            if(runtime&&runtime.Ready&&!portsBound){portsBound=true;runtime.ReadWalletCents=AvailableCents;runtime.CommitWalletDelta=(id,delta,op)=>CommitDelta(id,delta,op,"kernel");runtime.CommitWalletBatch=CommitBatch;runtime.CaptureEconomyState=Capture;runtime.CaptureLiveEconomyState=CaptureLive;runtime.RestoreEconomyState=Restore;runtime.WorldTick+=Tick;Restore(runtime.State.economyJson);}
            if(housingNetwork!=FriendsLifeNetwork.Instance){if(housingNetwork)housingNetwork.ResponseReceived-=HousingResponse;housingNetwork=FriendsLifeNetwork.Instance;if(housingNetwork)housingNetwork.ResponseReceived+=HousingResponse;}
            if(!runtime||!runtime.Ready||Time.unscaledTime<nextAuto)return;nextAuto=Time.unscaledTime+2;
            RestorePlacements();var island=HousingIsland.Instance;
            if(island&&island.HasArrivalTask&&runtime.GetPerson(island.ArrivalPersonId)?.arrivalGrantUsed==true)island.FinishArrival();
            if(!Authority||!runtime.IsWorldRunning)return;if(!AdvanceHousingVacancyMigrations())return;BindLegacyPlayerHome();PruneSettledPublicJobs();if((FriendJobs.PublicJobs||HasLivePublicJob)&&(stationScanDirty||Time.unscaledTime>=nextStationScan)){stationScanDirty=false;nextStationScan=Time.unscaledTime+10;DiscoverStations();}if(FriendJobs.PublicJobs)AdvanceAuthorizedPublicWork();AdvanceAuthorizedStaff();AssignIncomeWork();AdvanceQueuedUpgrades();
            foreach(var person in runtime.People.Where(p=>p.arrivalGrantUsed&&p.controllerKind==PersonControllerKind.Npc))
            {
                var actor=runtime.ActorOf(person.personId);
                if(actor&&actor.Runtime!=null&&actor.Runtime.IsAvailable&&!actor.Runtime.IsWorking&&actor.Brain!=null&&actor.Brain.CanAcceptRestaurantAssignment()&&runtime.IsWorldAvailable(person.personId))
                {
                    var plot=Plot(person.currentHomeId);
                    var owned=Plot(person.ownedHomeId);if(owned?.house&&owned.house.definition.level==1&&AvailableCents(person.personId)<UpgradeCents)QueueIncome(person.personId,"home-saving/"+owned.id,UpgradeCents);
                    if(plot?.house&&plot.house.definition.level==1&&OwnerOf(plot.id)==person.personId&&!IsUpgradePending(person.currentHomeId)&&AvailableCents(person.personId)>=UpgradeCents)
                        TryUpgrade(person.currentHomeId,false,"upgrade/auto/"+person.personId+"/"+plot.id+"/"+FriendHousingIsland.HomeIdentity(plot.house)+"/from/"+plot.house.definition.level);
                }
            }
        }
        FriendWallet Wallet(string id,bool create=true)
        {
            if(Human(id)&&CanUsePlayerWallet(id))id="player";
            if(string.IsNullOrEmpty(id))return null;var wallet=State.wallets.FirstOrDefault(w=>w.personId==id);
            if(wallet==null&&create){wallet=new FriendWallet{personId=id};State.wallets.Add(wallet);}return wallet;
        }
        bool Human(string id)=>id=="player"||runtime?.GetPerson(id)?.controllerKind==PersonControllerKind.Human;
        bool CanUsePlayerWallet(string id)
        {
            if(id=="player")return true;var person=runtime?.GetPerson(id);
            if(person?.controllerKind!=PersonControllerKind.Human)return false;
            var network=Unity.Netcode.NetworkManager.Singleton;
            if(Authority&&network&&network.IsServer)
                return network.ConnectedClientsIds.Any(client=>runtime.HumanForClient(client)?.personId==id);
            var player=Singleton<PlayersManager>.Instance?.LocalPlayer;if(!player)return false;
            string account=player.SteamID.Value!=0?"steam:"+player.SteamID.Value:"client:"+player.OwnerClientId;
            return person.controllerKind==PersonControllerKind.Human&&person.platformAccountKey==account;
        }
        public long BalanceCents(string id)
        {if(Human(id))return CanUsePlayerWallet(id)&&PlayerWallet?(long)Math.Min(long.MaxValue,PlayerWallet.ExactGoldBalance):0;return Wallet(id,false)?.balanceCents??0;}
        public long AvailableCents(string id)
        {if(Human(id))return CanUsePlayerWallet(id)&&PlayerWallet&&PlayerWallet.InfiniteGold?long.MaxValue:Math.Max(0,BalanceCents(id)-(Wallet(id,false)?.reservedCents??0));var w=Wallet(id,false);return w==null?0:Math.Max(0,w.balanceCents-w.reservedCents);}
        public bool HasReceipt(string op)=>State.receipts.Any(r=>r.opId==op);
        public bool CommitDelta(string id,long delta,string op,string domain="wallet",string other=null)
        {
            if(!Authority||string.IsNullOrEmpty(op)||string.IsNullOrEmpty(id)||delta==long.MinValue)return false;if(HasReceipt(op))return true;
            if(delta<0&&AvailableCents(id)<-delta)return false;
            if(Human(id)){if(!CanUsePlayerWallet(id)||!PlayerWallet||!PlayerWallet.ServerTryChangeStoredGold(delta))return false;}
            else{var w=Wallet(id);try{w.balanceCents=checked(w.balanceCents+delta);}catch(OverflowException){return false;}w.revision++;}
            Receipt(op,domain,id,delta,other);Commit();return true;
        }
        void Receipt(string op,string domain,string actor,long delta=0,string other=null)
        {State.revision++;State.receipts.Add(new EconomyReceipt{opId=op,domain=domain,actorId=actor,deltaCents=delta,counterparty=other,revision=State.revision});}
        public bool CommitBatch(IReadOnlyList<ActorWalletDelta> deltas,string op)
        {
            if(!Authority||string.IsNullOrEmpty(op)||deltas==null)return false;if(HasReceipt(op))return true;
            var combined=deltas.GroupBy(d=>d.personId).Select(g=>new {id=g.Key,delta=g.Aggregate(0L,(sum,d)=>checked(sum+d.deltaCents))}).ToArray();
            foreach(var row in combined)
            {
                if(string.IsNullOrEmpty(row.id)||Human(row.id)&&!CanUsePlayerWallet(row.id)||row.delta<0&&AvailableCents(row.id)<-row.delta)return false;
                if(row.delta==long.MinValue)return false;
                if(!(Human(row.id)&&PlayerWallet&&PlayerWallet.InfiniteGold))try{checked{long result=BalanceCents(row.id)+row.delta;if(result<0)return false;}}catch(OverflowException){return false;}
            }
            // Connected humans use the original shared operating wallet. Sender
            // validation completes before its single exact host-wallet mutation.
            long humanDelta=combined.Where(r=>Human(r.id)).Sum(r=>r.delta);
            if(humanDelta!=0&&(!PlayerWallet||!PlayerWallet.ServerTryChangeStoredGold(humanDelta)))return false;
            foreach(var row in combined)
            {
                if(!Human(row.id)){var wallet=Wallet(row.id);wallet.balanceCents+=row.delta;wallet.revision++;}
                Receipt(op+"/actor/"+row.id,"kernel",row.id,row.delta);
            }
            Receipt(op,"kernel-batch",combined.FirstOrDefault()?.id);Commit();return true;
        }
        void Commit(){Changed?.Invoke();if(runtime){runtime.NotifyChanged();if(externalCommitDepth==0)runtime.SaveNow();}}
        public IDisposable DeferExternalCommit()
        {externalCommitDepth++;return new DeferredCommit(this);}
        sealed class DeferredCommit : IDisposable
        {
            FriendsEconomyService owner;public DeferredCommit(FriendsEconomyService value){owner=value;}
            public void Dispose(){if(!owner)return;owner.externalCommitDepth=Math.Max(0,owner.externalCommitDepth-1);if(owner.externalCommitDepth==0){owner.runtime?.NotifyChanged();owner.runtime?.SaveNow();}owner=null;}
        }
        public string Capture(){CaptureLive();return JsonUtility.ToJson(State);}
        // Live state captures without encoding the ledger JSON.
        public void CaptureLive()
        {
            if(Authority)FriendsSushiBillingAuthority.Instance?.CaptureNativeWitnesses();
            if(Authority)FriendsCasinoController.Instance?.CaptureSourceRounds();
            if(Authority&&FriendsCasinoElevatorBridge.Instance)State.casinoTravelJson=FriendsCasinoElevatorBridge.Instance.Capture();
            var housing=HousingIsland.Instance;
            // A new or legacy housing world can validly be empty. Do not
            // overwrite saved placements before their projection has finished.
            if(Authority&&!projecting&&!housingProjectionPending&&housing&&housing.Plots.Count>0&&
               (canonicalHousingLoaded||housing.InitialHousingLoaded))
            {
                var live=housing.Plots.Where(p=>p.house).Select(p=>HousingIsland.CapturePlacement(p.house)).Where(row=>!IsRetiredHome(row)).ToList();
                // Destroy/unload ordering can remove an owned projection before
                // this component's final capture. A missing runtime object is
                // not an authorized housing deletion. Keep its exact canonical
                // row, including pose/doors, until it can be projected again.
                var missing=State.placements.Where(row=>!IsRetiredHome(row)&&!(row.legacyPlayerOwned&&!row.friendsOwnedClone)&&!live.Any(current=>current.plot==row.plot)).ToArray();
                live.AddRange(missing);State.placements=live;canonicalHousingLoaded=true;
                if(missing.Length>0)housingProjectionPending=true;
            }
        }
        public void Restore(string json)
        {
            canonicalHousingLoaded=!string.IsNullOrEmpty(json);housingProjectionPending=true;
            State=string.IsNullOrEmpty(json)?new FriendsEconomySave():JsonUtility.FromJson<FriendsEconomySave>(json)??new FriendsEconomySave();
            State.wallets=State.wallets??new List<FriendWallet>();State.receipts=State.receipts??new List<EconomyReceipt>();State.groups=State.groups??new List<HomeGroup>();State.bills=State.bills??new List<FriendBill>();State.jobs=State.jobs??new List<FriendJobContract>();State.placements=State.placements??new List<HousingPlacement>();State.incomeIntents=State.incomeIntents??new List<EarnForIntent>();
            State.facilityVisits=State.facilityVisits??new List<FriendFacilityVisit>();
            State.queuedUpgrades=State.queuedUpgrades??new List<QueuedHomeUpgrade>();
            State.homeVisits=State.homeVisits??new List<HomeVisitPermit>();
            State.sushiAuthorizations=State.sushiAuthorizations??new List<AuthorizedSushiJob>();
            State.publicJobAuthorizations=State.publicJobAuthorizations??new List<AuthorizedPublicJob>();
            State.casinoActors=State.casinoActors??new List<FriendCasinoActor>();State.casinoRounds=State.casinoRounds??new List<FriendCasinoRound>();
            NormalizeHousingVacancySave();
            if(Authority)FriendsCasinoController.Instance?.OnEconomyRestored();
            if(Authority)FriendsCasinoElevatorBridge.Instance?.Restore(State.casinoTravelJson);
            // No elapsed wall clock is imported as work after loading.
            if(Authority)
            {
                // Loading ends the interrupted shift without importing offline work or
                // clearing its paid-day marker. A load-only body recovery is distinct
                // from the normal, witnessed walk out through the original staff door.
                loadedStaffReturns.Clear();
                foreach(var job in State.jobs.Where(j=>j.state!=FriendJobPhase.Done))
                {job.state=FriendJobPhase.Handover;if(job.employeeId!=0)loadedStaffReturns.Add(job.contractId);}
                foreach(var visit in State.facilityVisits.Where(v=>v.phase!="Done"))
                {runtime?.State?.leases.RemoveAll(l=>l.leaseId==visit.leaseId);visit.phase="Queued";visit.leaseId=null;visit.pausedBrain=false;}
            }
            RestorePlacements();Changed?.Invoke();
        }
        void RestorePlacements()
        {
            var housing=HousingIsland.Instance;if(projecting||!housing||!housing.CanProject||housing.Plots.Count==0)return;
            if(!housingProjectionPending){if(!housing.CanBuild)foreach(var plot in housing.Plots)if(plot.house)housing.BindClientFurniture(plot.house);return;}
            projecting=true;try
            {
                bool complete=RemoveRetiredHousingProjections();
                foreach(var row in State.placements.Where(row=>!IsRetiredHome(row)))
                {
                    // Original houses remain controlled by the original housing
                    // module. Canonical addon rows may replace only addon homes.
                    if(row.legacyPlayerOwned&&!row.friendsOwnedClone)continue;
                    var plot=Plot(row.plot);var type=housing.Catalog.homes.FirstOrDefault(t=>t.id==row.type);
                    if(plot==null||type==null){complete=false;LastFailure="保存的住宅暂不可投影";continue;}
                    if(plot.house)housing.ApplyPlacementProjection(plot,row);
                    if(!plot.house)housing.Place(plot,type,row.yaw,false);
                    else if(plot.house.definition.id!=row.type)
                    {var prepared=housing.PrepareReplacement(plot,type,out _);if(prepared)housing.ActivatePrepared(plot,prepared);}
                    if(!plot.house||plot.house.definition.id!=row.type){complete=false;continue;}
                    housing.ApplyPlacementProjection(plot,row);
                    if(!housing.CanBuild)housing.BindClientFurniture(plot.house);
                }
                housingProjectionPending=!complete;
            }finally{projecting=false;}
        }
        public HousingPlot Plot(string homeId)=>HousingIsland.Instance?.Plots.FirstOrDefault(p=>p.id==homeId);
        public string OwnerOf(string homeId)=>runtime?.People.FirstOrDefault(p=>p.ownedHomeId==homeId)?.personId;
        void BindLegacyPlayerHome()
        {
            var player=runtime.LocalHuman();if(player==null||!string.IsNullOrEmpty(player.ownedHomeId)||!string.IsNullOrEmpty(player.currentHomeId))return;
            var home=HousingIsland.Instance?.Plots.FirstOrDefault(p=>p.house&&FriendHouseMetadata.For(p.house).legacyPlayerOwned&&!runtime.People.Any(other=>other.ownedHomeId==p.id));if(home==null)return;
            player.ownedHomeId=player.currentHomeId=player.returnHomeId=home.id;runtime.SyncHomeBindings(false);
            Receipt("legacy-home/"+player.personId,"home-migration",player.personId,0,home.id);Commit();
        }
        public IEnumerable<PersonBinding> MembersOf(string homeId)=>runtime?runtime.People.Where(p=>p.currentHomeId==homeId):Enumerable.Empty<PersonBinding>();
        public bool CanEnter(string homeId,string personId)
        {
            if(string.IsNullOrEmpty(personId))return false;
            if(runtime?.GetPerson(personId)?.currentHomeId==homeId)return true;
            if(State.homeVisits.Any(p=>p.homeId==homeId&&p.personId==personId&&VisitMatchesGeneration(p)))return true;
            // Original unbound housing remains the player's own legacy house.
            return CanUsePlayerWallet(personId)&&runtime&&runtime.Ready&&!runtime.People.Any(p=>p.ownedHomeId==homeId||p.currentHomeId==homeId);
        }
        public bool CanEnter(HousingHouse house,string personId)=>house&&(string.IsNullOrEmpty(FriendHouseMetadata.For(house).privatePersonId)?CanEnter(house.plot,personId):(FriendHouseMetadata.For(house).privatePersonId==personId&&CanEnter(house.plot,personId))||State.homeVisits.Any(p=>p.homeId==house.plot&&p.personId==personId&&p.privatePersonId==FriendHouseMetadata.For(house).privatePersonId&&VisitMatchesGeneration(p)));
        public bool PermitHomeVisit(string homeId,string personId,string ownerEpisode,string privateRoomOwner=null)
        {
            if(!Authority||Plot(homeId)?.house==null||runtime.GetPerson(personId)==null)return false;
            var episode=runtime.State.episodes.FirstOrDefault(e=>e.episodeId==ownerEpisode);
            if(episode==null||episode.focusPersonId!=personId&&episode.targetPersonId!=personId&&episode.anotherPersonId!=personId)return false;
            if(!State.homeVisits.Any(p=>p.homeId==homeId&&p.personId==personId&&p.ownerEpisode==ownerEpisode&&p.privatePersonId==privateRoomOwner&&VisitMatchesGeneration(p)))State.homeVisits.Add(new HomeVisitPermit{homeId=homeId,personId=personId,ownerEpisode=ownerEpisode,privatePersonId=privateRoomOwner,homeInstanceId=FriendHousingIsland.HomeIdentity(Plot(homeId).house)});
            runtime.NotifyChanged();return true;
        }
        public void RevokeHomeVisit(string ownerEpisode)
        {if(!Authority)return;State.homeVisits.RemoveAll(p=>p.ownerEpisode==ownerEpisode);runtime.NotifyChanged();HousingIsland.Instance?.GetComponent<HousingNpcExclusion>()?.ProtectNow();}
        public string PersonId(ResidentController actor)=>actor&&runtime?runtime.People.FirstOrDefault(p=>p.hostResidentId==actor.ResidentId)?.personId:null;
        public string PersonId(Component actor)
        {
            if(!actor)return null;var resident=actor.GetComponentInParent<ResidentController>();if(resident)return PersonId(resident);
            var staff=actor.GetComponentInParent<StaffNpc>();if(staff)return State.jobs.FirstOrDefault(j=>j.employeeId==staff.EmployeeId.Value&&j.state!=FriendJobPhase.Done)?.personId;
            var player=actor.GetComponentInParent<PaperPixelGames.Core.Player.PPG_Player>();
            if(player){string account=player.SteamID.Value!=0?"steam:"+player.SteamID.Value:"client:"+player.OwnerClientId;return runtime?.People.FirstOrDefault(p=>p.controllerKind==PersonControllerKind.Human&&p.platformAccountKey==account)?.personId??(player.IsOwner?"player":null);}
            return null;
        }
        public bool BeginArrival(string personId,bool selectForLocal=true)
        {
            if(!Authority)return false;var person=runtime.GetPerson(personId);if(person==null||housingTransition||housingProjectionPending)return false;
            var housing=HousingIsland.Instance;if(!housing)return false;
            if(person.controllerKind==PersonControllerKind.Human)
            {
                // The player's first home is selected on the same real map.
                // It carries no NPC arrival grant and never takes a friend's home.
                if(runtime.LocalHuman()?.personId==personId)BindLegacyPlayerHome();
                if(person.arrivalGrantUsed||!string.IsNullOrEmpty(person.ownedHomeId)||!string.IsNullOrEmpty(person.currentHomeId))return false;
                if(selectForLocal)housing.BeginArrival(personId);return true;
            }
            if(person.arrivalGrantUsed)return false;
            var free=housing.Plots.FirstOrDefault(p=>p.house&&!FriendHouseMetadata.For(p.house).legacyPlayerOwned&&!runtime.People.Any(x=>x.ownedHomeId==p.id||x.currentHomeId==p.id));
            if(free!=null)return CommitArrival(personId,free.id);
            if(selectForLocal)housing.BeginArrival(personId);return true;
        }
        public bool RequestHousing(string kind,string targetPersonId,HousingPlanCommand plan=null)
        {
            var network=FriendsLifeNetwork.Instance;var human=runtime?.LocalHuman();if(!network||human==null)return false;
            var command=new FriendsWorldCommand{commandId=Guid.NewGuid().ToString("N"),kind="housing",actorPersonId=human.personId,targetPersonId=targetPersonId,actionId=kind,optionId=plan==null?"":JsonUtility.ToJson(plan)};
            pendingHousingCommands.Add(command.commandId);network.Submit(command);return true;
        }
        void HousingResponse(FriendsWorldResponse response)
        {if(response==null||!pendingHousingCommands.Remove(response.commandId))return;LastFailure=response.accepted?null:response.message;HousingIsland.Instance?.SetFailure(LastFailure);Changed?.Invoke();}
        public bool ExecuteHousingCommand(FriendsWorldCommand command,string humanPersonId)
        {
            if(!Authority||command==null)return false;var housing=HousingIsland.Instance;var target=runtime.GetPerson(command.targetPersonId);if(!housing||target==null)return false;
            if((command.actionId=="begin-arrival"||command.actionId=="place")&&target.controllerKind==PersonControllerKind.Human&&target.personId!=humanPersonId)return false;
            var plan=string.IsNullOrEmpty(command.optionId)?new HousingPlanCommand():JsonUtility.FromJson<HousingPlanCommand>(command.optionId)??new HousingPlanCommand();
            switch(command.actionId)
            {
                case "invite-work":
                    return runtime.GetPerson(humanPersonId)?.controllerKind==PersonControllerKind.Human&&Enum.TryParse(plan.typeId,out StaffRole requestedRole)&&Enum.IsDefined(typeof(StaffRole),requestedRole)&&InviteToWork(target.personId,requestedRole);
                case "invite-public-work":return runtime.GetPerson(humanPersonId)?.controllerKind==PersonControllerKind.Human&&InviteToPublicWork(target.personId,plan.typeId);
                case "leave-work":return runtime.GetPerson(humanPersonId)?.controllerKind==PersonControllerKind.Human&&LeavePublicWork(target.personId);
                case "choose-family-home":
                    if(target.personId!=humanPersonId){LastFailure="只能选择本人的同住计划。";return false;}
                    return ChooseFamilyHome(humanPersonId,plan.familySuggestionId,plan.homeId);
                case "begin-arrival":return BeginArrival(target.personId,false);
                case "place":
                    if(target.arrivalGrantUsed)return target.currentHomeId==plan.plotId;
                    var plot=Plot(plan.plotId);var type=housing.Catalog.homes.FirstOrDefault(t=>t.id==plan.typeId&&t.level==1);if(plot==null||type==null)return false;
                    bool placed=housing.Place(plot,type,plan.yaw,arrivalPersonId:target.personId);LastFailure=housing.LastFailure;return placed;
                case "upgrade":
                    if(!CanUsePlayerWallet(humanPersonId)){LastFailure="当前玩家的钱包尚未绑定";return false;}
                    if(target.currentHomeId!=plan.homeId){LastFailure="住宅资料已变化";return false;}
                    return TryUpgrade(plan.homeId,true,"housing/"+command.commandId);
                case "split":
                    if(!CanRequestHomeSplit(target.personId,humanPersonId))return false;
                    return SeparateHomeGroup(target.personId,plan.splitGroupId,plan.splitRevision,plan.splitHomes,"housing/"+command.commandId);
                case "door":
                    var house=string.IsNullOrEmpty(plan.privatePersonId)?Plot(plan.homeId)?.house:FriendsHomeInteriors.Instance?.RoomFor(plan.privatePersonId)?.house;
                    if(!house||!CanEnter(house,humanPersonId))return false;
                    var door=house.GetComponentsInChildren<HousingDoor>(true).FirstOrDefault(d=>FriendHousingDoor.PathFromHouse(d)==plan.doorPath);if(!door)return false;
                    FriendHousingDoor.SetOpenInstant(door,plan.open);runtime.NotifyChanged();return true;
                default:return false;
            }
        }
        public bool CommitArrival(string personId,string homeId)
        {
            if(!Authority)return false;var person=runtime.GetPerson(personId);var plot=Plot(homeId);string op="arrival/"+personId;
            if(person==null||!plot?.house)return false;
            if(person.controllerKind==PersonControllerKind.Human)return CommitPlayerHome(person,plot);
            if(HasReceipt(op))return person.currentHomeId==homeId;
            if(person.arrivalGrantUsed||runtime.People.Any(p=>p.personId!=personId&&(p.ownedHomeId==homeId||p.currentHomeId==homeId)))return false;
            string oldOwned=person.ownedHomeId,oldCurrent=person.currentHomeId,oldReturn=person.returnHomeId;string coreBefore=runtime.Core.ExportJson(false);
            person.ownedHomeId=person.currentHomeId=person.returnHomeId=homeId;
            if(!runtime.RegisterArrival(personId,homeId,false))
            {person.ownedHomeId=oldOwned;person.currentHomeId=oldCurrent;person.returnHomeId=oldReturn;runtime.Core.TryImportJson(coreBefore,out _);return false;}
            long grant=person.addedByPlayer?InitialCents:0;Wallet(personId).balanceCents+=grant;person.arrivalGrantUsed=true;
            Receipt(op,"arrival",personId,grant,homeId);Capture();Commit();return true;
        }
        bool CommitPlayerHome(PersonBinding person,HousingPlot plot)
        {
            string op="player-home/"+person.personId;
            if(!string.IsNullOrEmpty(person.ownedHomeId)||HasReceipt(op))return person.ownedHomeId==plot.id&&person.currentHomeId==plot.id;
            if(runtime.People.Any(p=>p.personId!=person.personId&&(p.ownedHomeId==plot.id||p.currentHomeId==plot.id)))return false;
            string before=runtime.Core.ExportJson(false);
            string oldOwned=person.ownedHomeId,oldCurrent=person.currentHomeId,oldReturn=person.returnHomeId;
            person.ownedHomeId=person.currentHomeId=person.returnHomeId=plot.id;
            if(!runtime.RegisterArrival(person.personId,plot.id,false))
            {person.ownedHomeId=oldOwned;person.currentHomeId=oldCurrent;person.returnHomeId=oldReturn;runtime.Core.TryImportJson(before,out _);return false;}
            person.arrivalGrantUsed=true;FriendHouseMetadata.For(plot.house).legacyPlayerOwned=true;
            Receipt(op,"player-home",person.personId,0,plot.id);Capture();Commit();return true;
        }
        public long UpgradeQuote(string homeId)=>UpgradeHomes(homeId).Count*UpgradeCents;
        public bool IsUpgradePending(string homeId)=>State.queuedUpgrades.Any(q=>q.homeId==homeId);
        void AdvanceQueuedUpgrades()
        {
            foreach(var plan in State.queuedUpgrades.ToArray())
            {
                var homes=UpgradeHomes(plan.homeId);string signature=string.Join("/",homes.Select(p=>p.id).OrderBy(id=>id,StringComparer.Ordinal));
                if(HasReceipt(plan.opId)){State.queuedUpgrades.Remove(plan);continue;}
                if(homes.Count*UpgradeCents!=plan.quoteCents||signature!=plan.homesSignature||
                   (!string.IsNullOrEmpty(plan.homeInstanceId)?homes.Any(p=>FriendHousingIsland.HomeIdentity(p.house)!=plan.homeInstanceId):State.vacatedHomes.Any(v=>v.plotId==plan.homeId))){State.queuedUpgrades.Remove(plan);LastFailure="住宅方案已变化";runtime.NotifyChanged();continue;}
                if(CanUpgrade(plan.homeId,true,out _))TryUpgrade(plan.homeId,true,plan.opId);
            }
        }
        List<HousingPlot> UpgradeHomes(string homeId)
        {
            // Only the actual common house is upgraded. Vacated templates are
            // not houses and are never silently included in a quote or charge.
            var plot=Plot(homeId);return plot?.house&&plot.house.definition.level==1&&
                plot.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true
                ?new List<HousingPlot>{plot}:new List<HousingPlot>();
        }
        public bool CanUpgrade(string homeId,bool playerPays,out string reason)
        {
            reason=null;var homes=UpgradeHomes(homeId);if(homes.Count==0){reason="已是最高等级";return false;}
            if(playerPays){if(AvailableCents("player")<homes.Count*UpgradeCents){reason="金币不足";return false;}}
            else if(homes.Any(p=>string.IsNullOrEmpty(OwnerOf(p.id))||Human(OwnerOf(p.id)))){reason="由屋主确认升级";return false;}
            else if(homes.Any(p=>AvailableCents(OwnerOf(p.id))<UpgradeCents)){reason="等待屋主攒够金币";return false;}
            return true;
        }
        public bool TryUpgrade(string homeId,bool playerPays,string op)=>TryCommitHomeUpgrade(homeId,playerPays,op);
        public bool ShareHome(string[] personIds,string selectedHomeId,string sourceOp)
            =>TryCommitSharedHome(personIds,selectedHomeId,sourceOp,null);
        public bool LeaveHome(string personId,string op)
        {LastFailure="请先在地图上为分住成员选择空置地块";return false;}
        // Only new host meal quotes use this independent RNG; the original
        // Tomodachi/Unity/source casino random state is never consumed here.
        readonly System.Random townMealQuoteRandom=new System.Random();
        public bool QuoteTownMealAndReserve(string id,string billId,string foodId="Sushi")
        {
            if(!Authority||string.IsNullOrEmpty(billId)||runtime.GetPerson(id)?.controllerKind!=PersonControllerKind.Npc)return false;
            // Check persisted identity/quote before drawing. Legacy bills default
            // quotePending=false because their price was already fully reserved.
            var bill=State.bills.FirstOrDefault(b=>b.billId==billId);
            if(bill!=null)
            {
                if(bill.cancelled||bill.personId!=id||bill.facility!="TownRestaurant"||bill.sourceFoodId!=foodId)return false;
                if(!bill.quotePending)return true;
            }
            else
            {
                bill=new FriendBill{personId=id,billId=billId,facility="TownRestaurant",priceCents=townMealQuoteRandom.Next(15,31)*100L,sourceFoodId=foodId,quotePending=true};
                State.bills.Add(bill);Commit();
            }
            if(AvailableCents(id)<bill.priceCents){QueueIncome(id,billId,bill.priceCents);return false;}
            Wallet(id).reservedCents+=bill.priceCents;bill.quotePending=false;Commit();return true;
        }
        public bool QuoteAndReserve(string id,string billId,string facility,long cents,string foodId=null)
        {
            if(!Authority||cents<0||string.IsNullOrEmpty(id)||string.IsNullOrEmpty(billId)||string.IsNullOrEmpty(facility)||
                (!Human(id)&&runtime.GetPerson(id)==null)||Human(id)&&!CanUsePlayerWallet(id))return false;
            var existing=State.bills.FirstOrDefault(b=>b.billId==billId);
            if(existing!=null)
            {
                if(existing.cancelled||existing.personId!=id||existing.priceCents!=cents||existing.facility!=facility||existing.sourceFoodId!=foodId)return false;
                if(existing.committed||!existing.quotePending)return true;
                if(AvailableCents(id)<cents){QueueIncome(id,billId,cents);return false;}
                Wallet(id).reservedCents+=cents;existing.quotePending=false;Commit();return true;
            }
            if(AvailableCents(id)<cents){QueueIncome(id,billId,cents);return false;}Wallet(id).reservedCents+=cents;
            State.bills.Add(new FriendBill{personId=id,billId=billId,facility=facility,priceCents=cents,sourceFoodId=foodId});Commit();return true;
        }
        public bool DeliverBill(string billId,bool creditPlayer=false)
        {
            var bill=State.bills.FirstOrDefault(b=>b.billId==billId);if(!Authority||bill==null||bill.cancelled||bill.quotePending)return false;if(bill.committed)return true;
            if(Human(bill.personId))
            {
                if(!CanUsePlayerWallet(bill.personId)||!PlayerWallet)return false;var wallet=Wallet(bill.personId);wallet.reservedCents=Math.Max(0,wallet.reservedCents-bill.priceCents);
                if(!PlayerWallet.ServerTryChangeStoredGold(-bill.priceCents)){wallet.reservedCents+=bill.priceCents;return false;}
            }
            else{var wallet=Wallet(bill.personId);if(wallet.balanceCents<bill.priceCents)return false;wallet.reservedCents=Math.Max(0,wallet.reservedCents-bill.priceCents);wallet.balanceCents-=bill.priceCents;wallet.revision++;}
            if(creditPlayer&&PlayerWallet)PlayerWallet.ServerTryChangeStoredGold(bill.priceCents);
            bill.delivered=bill.committed=true;Receipt("bill/"+billId,"consume",bill.personId,-bill.priceCents,bill.facility);Commit();ApplyDeliveredFood(bill);return true;
        }
        void ApplyDeliveredFood(FriendBill bill)
        {
            if(string.IsNullOrEmpty(bill.sourceFoodId)||State.receipts.Any(r=>r.opId=="food-rejected/"+bill.billId))return;var person=runtime.GetPerson(bill.personId);if(person==null)return;
            var result=runtime.ExecuteKernel("food-delivery/"+bill.billId,bill.personId,scope=>
            {
                var stock=scope.State.inventory.FirstOrDefault(s=>s.category=="Food"&&s.itemName==bill.sourceFoodId);
                if(stock!=null)stock.quantity++;else scope.State.inventory.Add(new LifeSimulationInventoryStack{category="Food",itemName=bill.sourceFoodId,quantity=1});
                return scope.GiveFood(person.nativeResidentId,bill.sourceFoodId).success;
            },bill.billId);
            if(result?.success!=true){Receipt("food-rejected/"+bill.billId,"source-food-rejected",bill.personId,0,result?.message);Commit();}
        }
        public bool IsFriend(ResidentController actor)
        {var person=runtime?.GetPerson(PersonId(actor));return person!=null&&person.arrivalGrantUsed;}
        public void CancelBill(string billId)
        {
            var bill=State.bills.FirstOrDefault(b=>b.billId==billId);if(!Authority||bill==null||bill.delivered||bill.cancelled)return;
            if(!bill.quotePending){var w=Wallet(bill.personId);w.reservedCents=Math.Max(0,w.reservedCents-bill.priceCents);}bill.cancelled=true;Commit();
        }
        public bool ReleaseUnstartedBill(string billId)
        {
            var bill=State.bills.FirstOrDefault(b=>b.billId==billId);
            if(!Authority||bill==null||bill.delivered||bill.committed||bill.cancelled)return false;
            if(bill.quotePending)return true;
            var wallet=Wallet(bill.personId);wallet.reservedCents=Math.Max(0,wallet.reservedCents-bill.priceCents);
            bill.quotePending=true;Commit();return true;
        }
        public void RegisterStation(FriendJobStation station){if(station&&!string.IsNullOrEmpty(station.slotId))stations[station.slotId]=station;}
        public bool BoundEmployee(int employeeId)=>State.jobs.Any(j=>j.employeeId==employeeId&&j.state!=FriendJobPhase.Done);
        public string PersonForEmployee(int employeeId)=>State.jobs.FirstOrDefault(j=>j.employeeId==employeeId&&j.state!=FriendJobPhase.Done)?.personId;
        public string JobTitle(string personId)
        {var job=State.jobs.FirstOrDefault(j=>j.personId==personId&&j.state!=FriendJobPhase.Done);if(job==null)return "暂无工作";if(job.employeeId!=0){var auth=State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);if(auth==null)return "寿司店员工";switch(auth.role){case StaffRole.SushiChef:return "寿司师傅";case StaffRole.PrepCook:return "备料员";case StaffRole.Waitress:return "服务员";case StaffRole.Stocker:return "补货员";default:return "寿司店员工";}}return job.slotId.StartsWith("CinemaTicket")?"影院检票":job.slotId.StartsWith("TownRestaurantService")?"餐厅服务":job.slotId.StartsWith("CasinoCardDealer")?"赌场发牌员":"赌场电梯服务";}
        public GameObject EmployeeAppearance(int employeeId)
        {var job=State.jobs.FirstOrDefault(j=>j.employeeId==employeeId&&j.state!=FriendJobPhase.Done);var actor=job==null?null:runtime?.ActorOf(job.personId);return actor&&actor.VisualAnimator?actor.VisualAnimator.gameObject:null;}
        public bool HasActiveJob(string personId)=>State.jobs.Any(j=>j.personId==personId&&j.state!=FriendJobPhase.Done);
        public void QueueIncome(string personId,string intentId,long targetCents)
        {
            if(!Authority||Human(personId)||runtime.GetPerson(personId)==null)return;
            var existing=State.incomeIntents.FirstOrDefault(i=>i.personId==personId&&i.intentId==intentId);
            if(existing==null)State.incomeIntents.Add(new EarnForIntent{personId=personId,intentId=intentId,targetCents=targetCents,sequence=++State.revision});else if(targetCents>existing.targetCents)existing.targetCents=targetCents;else return;
            Commit();
        }
        void AssignIncomeWork()
        {
            State.incomeIntents.RemoveAll(i=>AvailableCents(i.personId)>=i.targetCents);
            // Savings are a motive, not permission to invent a paid public
            // shift. Current lightweight employment uses authorized native
            // sushi staff above, retaining the original progression gates.
            // Already saved public contracts still finish through Tick().
        }
        public bool InviteToWork(string personId,StaffRole role)
        {
            var manager=StaffManager.Instance;LastFailure=null;
            if(!Authority||manager==null||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc){LastFailure="现在不能安排工作。";return false;}
            var authorization=State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);if(authorization?.role==role)return true;
            if(!manager.IsRoleUnlocked(role)){LastFailure="这个岗位还没解锁。";return false;}
            if(manager.IsRoleFull(role)){LastFailure="这个岗位已经满了。";return false;}
            State.publicJobAuthorizations.RemoveAll(a=>a.personId==personId);
            foreach(var journey in FriendsCasinoElevatorBridge.Instance?.State.journeys.Where(j=>j.personId==personId&&!string.IsNullOrEmpty(j.workKind)&&j.phase!="Done"&&j.phase!="Cancelled").ToArray()??Array.Empty<FriendCasinoJourney>())FriendsCasinoElevatorBridge.Instance.CancelVisit(journey.visitId);
            // A new role ends the current shift; the old entry leaves the team once its body is gone.
            if(authorization==null)State.sushiAuthorizations.Add(new AuthorizedSushiJob{personId=personId,role=role,workedDay=LastStaffWorkDay(personId)});else{authorization.role=role;authorization.retainedEmployee=null;CancelStaffArrival(personId);RequestLeaveJob(personId);}
            Commit();AdvanceAuthorizedStaff();return true;
        }
        public AuthorizedSushiJob SushiAuthorization(string personId,StaffRole role)=>State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId&&a.role==role);
        public bool InviteToPublicWork(string personId,string kind)
        {
            if(!FriendJobs.PublicJobs){LastFailure="这份工作暂未开放。";return false;}
            if(!Authority||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc||!new[]{"CasinoCardDealer","CasinoElevator","CinemaTicket","TownRestaurantService"}.Contains(kind))return false;
            var old=State.publicJobAuthorizations.FirstOrDefault(a=>a.personId==personId);if(old!=null&&old.kind==kind)return true;
            LeavePublicWork(personId);State.sushiAuthorizations.RemoveAll(a=>a.personId==personId);
            State.publicJobAuthorizations.Add(new AuthorizedPublicJob{authorizationId="public-auth/"+Guid.NewGuid().ToString("N"),personId=personId,kind=kind});Commit();return true;
        }
        public bool LeavePublicWork(string personId)
        {
            if(!Authority||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc)return false;
            bool changed=State.publicJobAuthorizations.RemoveAll(a=>a.personId==personId)>0;changed|=State.sushiAuthorizations.RemoveAll(a=>a.personId==personId)>0;CancelStaffArrival(personId);RequestLeaveJob(personId);
            var bridge=FriendsCasinoElevatorBridge.Instance;if(bridge)foreach(var journey in bridge.State.journeys.Where(j=>j.personId==personId&&!string.IsNullOrEmpty(j.workKind)&&j.phase!="Done"&&j.phase!="Cancelled").ToArray())bridge.CancelVisit(journey.visitId);
            if(changed)Commit();return true;
        }
        void AdvanceAuthorizedPublicWork()
        {
            foreach(var authorization in State.publicJobAuthorizations.OrderBy(a=>a.personId,StringComparer.Ordinal))
            {
                if(HasActiveJob(authorization.personId)||!runtime.IsWorldAvailable(authorization.personId))continue;
                if(authorization.kind=="CasinoCardDealer"||authorization.kind=="CasinoElevator")
                {
                    var bridge=FriendsCasinoElevatorBridge.Instance;if(!bridge)continue;
                    string op=authorization.authorizationId+"/shift/"+authorization.nextShiftSequence;
                    if(bridge.RequestVisit(authorization.personId,op,authorization.kind=="CasinoCardDealer"?3:0,0,false,out _,authorization.kind)){authorization.nextShiftSequence++;Commit();}
                }
                else
                {
                    if(publicRetryAt.TryGetValue(authorization.personId,out var retry)&&Time.unscaledTime<retry)continue;
                    var actor=runtime.ActorOf(authorization.personId);if(!actor)continue;
                    var station=stations.Values.Where(s=>s&&s.sourceKind==authorization.kind&&s.Available&&s.TryResolveRoute(actor,out _)).OrderBy(s=>Vector3.Distance(s.stationTransform.position,actor.transform.position)).FirstOrDefault();
                    if(station)TryBeginPublicWorkAtStation(authorization.personId,station.slotId,out _);
                }
            }
        }
        public bool TryBeginPublicWorkAtStation(string personId,string slotId,out string contractId)
        {
            contractId=null;if(!Authority||HasActiveJob(personId)||!runtime.IsWorldAvailable(personId)||!stations.TryGetValue(slotId,out var station)||!station||!station.Available)return false;
            var actor=runtime.ActorOf(personId);if(!actor||actor.Brain==null)return false;
            var job=new FriendJobContract{contractId="public/"+Guid.NewGuid().ToString("N"),personId=personId,payer="town",budgetCents=1000,state=FriendJobPhase.Travel};
            if(!station.TryAssign(job))return false;actor.Brain.Pause();State.jobs.Add(job);contractId=job.contractId;Commit();return true;
        }
        // Original staff stay hired; a friend's body follows the shop sign. It
        // walks in once a day while the shop is open and leaves at closing,
        // keeping the same employee entry (FriendJobs.OffShift in StaffRuntime).
        const long MinShiftCents=200;
        readonly HashSet<string> loadedStaffReturns=new HashSet<string>(StringComparer.Ordinal);
        bool HasLivePublicJob=>State.jobs.Any(j=>j.employeeId==0&&j.state!=FriendJobPhase.Done);
        void AdvanceAuthorizedStaff()
        {
            var manager=StaffManager.Instance;if(!manager)return;
            FriendStaffPort.ReleaseUnclaimed(manager,ClaimsEmployee);
            bool open=FriendJobs.ShopOpen;int today=manager.CurrentDay+1;
            foreach(var auth in State.sushiAuthorizations.OrderBy(a=>a.personId,StringComparer.Ordinal).ToArray())
            {
                if(HasActiveJob(auth.personId))continue;
                int kept=KeptEmployee(auth,manager);
                if(kept<0){State.sushiAuthorizations.Remove(auth);CancelStaffArrival(auth.personId);Commit();continue;}
                if(!open||WorkedToday(auth.personId)){CancelStaffArrival(auth.personId);continue;}
                if(kept==0&&(!manager.IsRoleUnlocked(auth.role)||manager.IsRoleFull(auth.role)))continue;
                if(AvailableCents("player")>=MinShiftCents)AdvanceStaffArrival(auth,manager,kept);
            }
        }
        // The friend's own team entry: its id, 0 when there is none (first hire),
        // or -1 when the original staff tablet fired it or could not pay it.
        int KeptEmployee(AuthorizedSushiJob auth,StaffManager manager)
        {
            int id=auth.retainedEmployee?.EmployeeId??0;
            if(id<=0||!manager.TryGetEmployee(id,out var entry)||entry.Role!=auth.role||
                entry.HireSequence!=auth.retainedEmployee.HireSequence||!FriendStaffPort.IsOwnedEntry(id,auth.personId))return 0;
            if(entry.Departure!=StaffDeparture.None)return -1;
            return id;
        }
        bool ClaimsEmployee(int employeeId)=>State.jobs.Any(j=>j.employeeId==employeeId&&j.state!=FriendJobPhase.Done)||State.sushiAuthorizations.Any(a=>a.retainedEmployee!=null&&a.retainedEmployee.EmployeeId==employeeId);
        int LastStaffWorkDay(string personId)=>State.jobs.Where(j=>j.employeeId!=0&&j.personId==personId).Select(j=>j.staffWorkDay).DefaultIfEmpty(0).Max();
        public bool WorkedToday(string personId)
        {
            var manager=StaffManager.Instance;if(!manager)return false;int today=manager.CurrentDay+1;
            return State.sushiAuthorizations.Any(a=>a.personId==personId&&a.workedDay==today)||
                State.jobs.Any(j=>j.employeeId!=0&&j.personId==personId&&j.staffWorkDay==today);
        }
        public void RememberStaffProfile(string personId,StaffEmployeeData data)
        {var auth=data==null?null:State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId&&a.role==data.Role);if(auth!=null)auth.retainedEmployee=data;}
        public void RevokeSushiAuthorization(int employeeId)
        {var job=State.jobs.FirstOrDefault(j=>j.employeeId==employeeId&&j.state!=FriendJobPhase.Done);if(job==null)return;State.sushiAuthorizations.RemoveAll(a=>a.personId==job.personId);job.state=FriendJobPhase.Handover;Commit();}
        public bool BindEmployee(string personId,int employeeId)
        {
            if(!Authority||runtime.GetPerson(personId)==null||(!runtime.IsWorldAvailable(personId)&&!HasReadyStaffArrival(personId))||State.jobs.Any(j=>j.personId==personId&&j.state!=FriendJobPhase.Done))return false;
            // The shift's pay is capped at the role's original daily wage.
            var auth=State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);var manager=StaffManager.Instance;
            if(auth==null||!manager||WorkedToday(personId))return false;
            long dailyWage=Math.Max(MinShiftCents,manager.GetRoleDefinition(auth.role).BaseDailyWage*100L);
            long budget=Math.Min(AvailableCents("player"),dailyWage);if(budget<MinShiftCents)return false;Wallet("player").reservedCents+=budget;
            if(auth!=null&&manager)auth.workedDay=manager.CurrentDay+1;
            State.jobs.Add(new FriendJobContract{contractId="staff/"+personId+"/"+employeeId+"/"+Guid.NewGuid().ToString("N"),slotId="staff/"+employeeId,personId=personId,payer="player",employeeId=employeeId,budgetCents=budget,staffWorkDay=manager.CurrentDay+1,state=FriendJobPhase.Travel});Commit();return true;
        }
        public void RequestLeaveJob(string personId)
        {bool changed=false;foreach(var job in State.jobs.Where(j=>j.personId==personId&&j.state!=FriendJobPhase.Done&&j.state!=FriendJobPhase.Handover)){job.state=FriendJobPhase.Handover;changed=true;}if(changed)Commit();}
        void Tick(double gameDelta,long activeDelta)
        {
            if(!Authority||activeDelta<=0)return;
            foreach(var job in State.jobs.Where(j=>j.state!=FriendJobPhase.Done).ToArray())
            {
                if(job.employeeId!=0){TickStaff(job,activeDelta);continue;}
                if(!stations.TryGetValue(job.slotId,out var station)||!station)
                {
                    if(job.cabinReturnRequired)
                    {if(Time.unscaledTime>=nextStationScan){nextStationScan=Time.unscaledTime+10;DiscoverStations();}if(!stations.TryGetValue(job.slotId,out station)||!station){job.state=FriendJobPhase.Handover;job.finishReason="waiting-for-actual-cabin-return-station";continue;}}
                    else{job.state=FriendJobPhase.Done;RestoreActor(job);continue;}
                }
                station.TickContract(job,activeDelta,this);
                if(job.state==FriendJobPhase.Done&&job.employeeId==0&&job.finishReason=="route-unavailable")publicRetryAt[job.personId]=Time.unscaledTime+PublicRouteRetrySeconds;
            }
        }
        void TickStaff(FriendJobContract job,long activeDelta)
        {
            var manager=StaffManager.Instance;
            if(!manager||!manager.IsSpawned)return;
            // Legacy active contracts have no encoded paid-day field. Adopt the
            // now-loaded original day once; repeated loads cannot restart its cap.
            if(loadedStaffReturns.Contains(job.contractId))
            {
                if(job.staffWorkDay==0)job.staffWorkDay=manager.CurrentDay+1;
                var authorization=State.sushiAuthorizations.FirstOrDefault(a=>a.personId==job.personId);
                if(authorization!=null&&authorization.workedDay==0)authorization.workedDay=job.staffWorkDay;
            }
            if(!manager.TryGetEmployee(job.employeeId,out var entry)){job.state=FriendJobPhase.Done;RestoreActor(job);return;}
            if(!FriendStaffPort.IsOwnedEntry(job.employeeId,job.personId))
            {job.state=FriendJobPhase.Handover;job.finishReason="StaffOwnershipUnresolved";return;}
            // Fired or unpaid in the original staff tablet: the job ends for good.
            if(entry.Departure!=StaffDeparture.None){job.state=FriendJobPhase.Handover;State.sushiAuthorizations.RemoveAll(a=>a.personId==job.personId);}
            // At closing (or after quitting) the employee walks out through the
            // original departure path, but its team entry is kept: no "fired".
            else if(job.state!=FriendJobPhase.Handover&&(!FriendJobs.ShopOpen||!State.sushiAuthorizations.Any(a=>a.personId==job.personId))){job.state=FriendJobPhase.Handover;job.finishReason=FriendJobs.ShopOpen?"left":"closing";}
            var npc=StaffNpcFor(job.employeeId,job.state==FriendJobPhase.Handover);
            if(job.state==FriendJobPhase.Handover){if(!npc){RememberStaffProfile(job.personId,FriendStaffPort.Snapshot(manager,job.employeeId));job.state=FriendJobPhase.Done;RestoreActor(job);}return;}
            bool duty=npc&&entry.Departure==StaffDeparture.None&&entry.Status!=StaffStatus.Leaving&&entry.Status!=StaffStatus.Stopped&&entry.Status!=StaffStatus.MissingWorkplace&&entry.Status!=StaffStatus.Unavailable;
            if(!duty)return;StaffEmployeeSpawned(job.personId,job.employeeId);job.state=FriendJobPhase.OnDuty;var proxy=npc.GetComponent<FriendWorkProxy>();if(!proxy)proxy=npc.gameObject.AddComponent<FriendWorkProxy>();proxy.personId=job.personId;
            var actor=runtime.ActorOf(job.personId);if(actor&&!actor.GetComponent<FriendResidentDormancy>()){var sleep=actor.gameObject.AddComponent<FriendResidentDormancy>();sleep.Suspend(actor);}
            PayWork(job,activeDelta);
        }
        // The original runtime owns the employee dictionary. Read that exact
        // registry for spawn/absence; no repeated scene scans on a job tick.
        public StaffNpc StaffNpcFor(int employeeId,bool exact)=>FriendStaffPort.NpcFor(employeeId);
        public bool PayWork(FriendJobContract job,long activeDelta)
        {
            if(!Authority||job==null||activeDelta<=0||job.state!=FriendJobPhase.OnDuty||job.wageCentsPerMinute<=0||
                !State.jobs.Contains(job)||Human(job.personId)||runtime.GetPerson(job.personId)==null)return false;
            long micros;try{micros=checked(job.workedMicros+activeDelta);}catch(OverflowException){return false;}
            long earned;
            if(job.budgetCents>0)
            {
                var pay=FriendStaffPay.Plan(job.workedMicros,activeDelta,job.wageCentsPerMinute,job.budgetCents);
                micros=pay.workedMicros;earned=pay.earnedCents;
                if(pay.budgetReached){job.state=FriendJobPhase.Handover;job.finishReason="budget-paid";}
            }
            else earned=(long)((decimal)job.wageCentsPerMinute*micros/60000000m);
            long due=earned-job.paidCents;
            if(due>0&&job.payer=="player")
            {
                Wallet("player").reservedCents=Math.Max(0,Wallet("player").reservedCents-due);
                if(earned>job.budgetCents||!PlayerWallet||!PlayerWallet.ServerTryChangeStoredGold(-due)){Wallet("player").reservedCents+=due;job.state=FriendJobPhase.Handover;return false;}
            }
            job.workedMicros=micros;job.wageRemainder=(long)((decimal)job.wageCentsPerMinute*micros%60000000m);
            if(due>0)
            {
                var wallet=Wallet(job.personId);wallet.balanceCents+=due;wallet.revision++;job.paidCents=earned;
                job.paySequence=Math.Max(1,(micros-1)/60000000+1);string op=job.contractId+"/pay/"+job.paySequence;var receipt=State.receipts.FirstOrDefault(r=>r.opId==op);
                if(receipt==null)Receipt(op,"wage",job.personId,due,job.payer);else{receipt.deltaCents+=due;receipt.revision=++State.revision;}
                if(job.employeeId!=0&&StaffManager.Instance&&StaffManager.Instance.TryGetEmployee(job.employeeId,out var employee))StaffManager.Instance.Ledger.Wage(employee,(int)Math.Min(int.MaxValue,due));
                runtime.NotifyChanged();Changed?.Invoke();
            }
            if(job.employeeId==0&&job.budgetCents>0&&job.paidCents>=job.budgetCents)job.state=FriendJobPhase.Handover;
            return true;
        }
        public void RestoreActor(FriendJobContract job)
        {
            if(job==null)return;
            if(job.payer=="player"){var wallet=Wallet("player");wallet.reservedCents=Math.Max(0,wallet.reservedCents-Math.Max(0,job.budgetCents-job.paidCents));job.budgetCents=job.paidCents;}
            var actor=runtime.ActorOf(job.personId);
            if(job.employeeId!=0&&!TryRestoreStaffBody(job,actor)){job.state=FriendJobPhase.Handover;return;}
            bool otherJob=State.jobs.Any(other=>other!=job&&other.personId==job.personId&&other.state!=FriendJobPhase.Done);
            bool otherActivity=runtime.State.leases.Any(lease=>lease.personId==job.personId);
            if(actor&&!otherJob)
            {var sleep=actor.GetComponent<FriendResidentDormancy>();if(sleep)sleep.Restore(!otherActivity);else if(!otherActivity){actor.Brain?.SetModelVisible(true);actor.Brain?.Resume();actor.Brain?.RequestScheduleChange();}}
            Commit();
        }
        // Settled public attempts that never paid hold no balance, receipt or
        // page entry. Contracts a casino journey still names are kept.
        void PruneSettledPublicJobs()
        {
            var bridge=FriendsCasinoElevatorBridge.Instance;if(!bridge||bridge.State?.journeys==null)return;
            var named=new HashSet<string>(bridge.State.journeys.Select(j=>j.workContractId).Where(id=>!string.IsNullOrEmpty(id)),StringComparer.Ordinal);
            if(State.jobs.RemoveAll(j=>j.state==FriendJobPhase.Done&&j.employeeId==0&&j.paidCents==0&&j.payer!="player"&&!j.cabinReturnRequired&&!named.Contains(j.contractId))>0)runtime.NotifyChanged();
        }
        void DiscoverStations()
        {
            // The requested dealer job only replaces the authored standing NPC.
            // The original card game keeps its own rules, inputs and animations.
            foreach(var table in FindObjectsByType<SushiAndFish.Casino.Original.Poker>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var original=table.transform.Find("NPC Static");if(!original)continue;
                var station=table.GetComponent<FriendJobStation>();if(!station)station=table.gameObject.AddComponent<FriendJobStation>();
                string path=table.name;for(var parent=table.transform.parent;parent;parent=parent.parent)path=parent.name+"/"+path;
                station.slotId="CasinoCardDealer/"+table.gameObject.scene.name+"/"+path;station.sourceKind="CasinoCardDealer";station.service=table;station.stationTransform=original;station.fallbackNpc=original;
                station.IsOpen=()=>table&&table.isActiveAndEnabled;station.SafeBoundary=()=>true;RegisterStation(station);
            }
            foreach(var portal in FindObjectsByType<SushiAndFish.CityExpansion.CityCinemaPortal>(FindObjectsSortMode.None).Where(p=>FriendFacilities.IsEntrance(p)))
            {
                var station=portal.GetComponent<FriendJobStation>();if(!station)station=portal.gameObject.AddComponent<FriendJobStation>();
                station.slotId="CinemaTicket/"+portal.gameObject.scene.name+"/"+portal.name;station.sourceKind="CinemaTicket";station.service=portal;
                if(!station.stationTransform||station.stationTransform==portal.transform)
                {var anchor=new GameObject("CinemaTicketWorkerAnchor").transform;anchor.SetParent(portal.transform,false);anchor.localPosition=Vector3.right*1.6f;station.stationTransform=anchor;}
                station.IsOpen=()=>portal&&portal.isActiveAndEnabled&&portal.destination;station.SafeBoundary=()=>true;RegisterStation(station);
            }
            foreach(var scheduler in FindObjectsByType<SushiAndFish.CityExpansion.TownMealScheduler>(FindObjectsSortMode.None))
            {
                var station=scheduler.GetComponent<FriendJobStation>();if(!station)station=scheduler.gameObject.AddComponent<FriendJobStation>();
                station.slotId="TownRestaurantService/"+scheduler.gameObject.scene.name;station.sourceKind="TownRestaurantService";station.service=scheduler;
                if(!station.stationTransform&&scheduler.seats!=null)
                {var seat=scheduler.seats.FirstOrDefault(s=>s&&s.approach);if(seat){var anchor=new GameObject("TownRestaurantServiceAnchor").transform;anchor.SetParent(scheduler.transform,false);anchor.position=seat.approach.position+Vector3.right;station.stationTransform=anchor;}}
                station.IsOpen=()=>scheduler&&scheduler.isActiveAndEnabled&&!scheduler.SushiOpen;station.SafeBoundary=()=>true;if(station.stationTransform)RegisterStation(station);
            }
            foreach(var attendant in FindObjectsByType<SushiAndFish.Casino.Original.ElevatorGuy>(FindObjectsSortMode.None))
            {
                var station=attendant.GetComponent<FriendJobStation>();if(!station)station=attendant.gameObject.AddComponent<FriendJobStation>();
                string path=attendant.name;for(var parent=attendant.transform.parent;parent;parent=parent.parent)path=parent.name+"/"+path;
                station.slotId="CasinoElevator/"+attendant.gameObject.scene.name+"/"+path;station.sourceKind="CasinoElevator";station.service=attendant;station.stationTransform=attendant.transform;station.fallbackNpc=attendant.transform;
                station.IsOpen=()=>SushiAndFish.Casino.ParkCasinoIntegration.Instance?.Ready==true&&SushiAndFish.Casino.ParkCasinoIntegration.Instance.CurrentFloor==0;
                station.SafeBoundary=()=>SushiAndFish.Casino.Original.Extensions.NetworkSingleton<SushiAndFish.Casino.Original.ElevatorManager>.Instance?.isTeleporting!=true&&SushiAndFish.Casino.ParkCasinoIntegration.Instance?.elevatorConversation?.IsOpen!=true;
                RegisterStation(station);
            }
            foreach(var station in FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None))RegisterStation(station);
        }
        void OnDestroy(){UnityEngine.SceneManagement.SceneManager.sceneLoaded-=MarkStationScan;UnityEngine.SceneManagement.SceneManager.sceneUnloaded-=MarkStationScan;if(runtime)runtime.WorldTick-=Tick;if(Instance==this)Instance=null;}
    }
}
