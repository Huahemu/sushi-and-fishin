using System;
using System.Collections.Generic;
using UnityEngine;

namespace HowToFish {
    public enum FishingState { Ready, Charging, Casting, Floating, Hooked }
    [DefaultExecutionOrder(-50)]
    public sealed partial class FishingModule:MonoBehaviour {
        public FishingRig rig;
        public bool BoundedLineMotion;
        public bool RequireManualCatchRelease;
        // Native first-person hands can render through a separate camera.
        // Map only tool-space points; gameplay physics stays in world space.
        public Func<Vector3,Vector3> ToolToWorld,WorldToTool;
        public Vector3 WorldRodTip=>ToolPoint(rod.tip.position);
        public Vector3 ToolPoint(Vector3 point)=>ToolToWorld!=null?ToolToWorld(point):point;
        public System.Func<FishingCatch,Collider,bool> TryPlayerContact;
        public int MaxLooseCatches=12;
        public System.Collections.Generic.IReadOnlyList<FishingCatch> LooseCatches=>loose;
        public FishingInventory inventory;
        public FishingRod rod;
        
        public FishingWaterZone[] zones;
        public Transform rodRest,landingPoint,waterTarget,safeFeet;
        public Rigidbody hookBody;
        public SpringJoint rope;
        public LineRenderer line;
        public TextMesh board,stockBoard,rodDisplay;
        public FishingInteractable[] interactables;
        public ParticleSystem splash;
        public FishingProfile profile;
        public FishCombat combat;
        public FishingVitals vitals;
        public FishingWorldEvents worldEvents;
        public int AvailableCatchVertices{get{int cost=0;foreach(var f in loose)if(f&&f.IsAvailable)cost+=f.definition.renderVertexCost;return Mathf.Max(0,profile.catchVertexBudget-cost);}}
        public int AvailableCatchTriangles{get{int cost=0;foreach(var f in loose)if(f&&f.IsAvailable)cost+=f.definition.renderTriangleCost;return Mathf.Max(0,profile.catchTriangleBudget-cost);}}
        public Vector3 islandCenter=new Vector3(0,0,-10);
        public float maxLine=30;
        public FishingState State{get;private set;}
        public float SurfaceHeight=>waterSurface?waterSurface.position.y:zones.Length>0?zones[0].surfaceHeight:0;
        public string BaitName=>BaitInfo.title+(bait==0?"（无限）":"（"+inventory.BaitCount(bait)+"）");
        public float Tension{get;private set;}
        public float CastPower=>charge;
        public float PlannedCastDistance=>profile?FishingCastMath.Distance(profile,charge):0;
        public float BaitDepth=>Mathf.Max(0,SurfaceHeight-hookBody.position.y);
        public float EffectiveLureTime=>wetTime;
        public event Action<FishingCatch> HookedFish,KilledFish;
        public event Action<FishingModule,Vector3> WaterCastEntered;
        void AnnounceWaterCast()=>WaterCastEntered?.Invoke(this,hookBody.position);
        public Transform waterSurface;
        public Func<Vector3,bool> WaterQuery;
        public int Bait=>bait;
        public float LineLength=>lineLength;
        public float MinimumLineLength=>profile?profile.MinimumLine(hooked):.5f;
        public FishingCatch Hooked=>hooked;
        public FishingCatch DisplayCatch {
            get {
                if(displayCatch&&displayCatch.IsAvailable&&!displayCatch.IsDead)return displayCatch;
                if(hooked&&hooked.IsAvailable&&!hooked.IsDead)return hooked;
                return null;
            }
        }
        public FishingCatch RestoreCatch(FishDefinition definition,Vector3 position,Quaternion rotation,int health,bool shiny,float weight,bool reachedShore,bool seen=false){
            var fish=Instantiate(definition.catchPrefab,position,rotation,transform);fish.fishing=this;fish.weight=weight;fish.SetDrip(shiny);fish.RestoreRuntimeState(health,reachedShore,seen);fish.item.rig=rig;rig.Register(fish.item);loose.Add(fish);return fish;
        }
        public void FocusCatch(FishingCatch fish){if(fish&&fish.IsAvailable&&!fish.IsDead)displayCatch=fish;}
        public void ForgetCatch(FishingCatch fish){
            if(!fish)return;
            loose.Remove(fish);if(displayCatch==fish)displayCatch=null;
            if(hooked==fish){ClearJoint();hooked=null;Tension=0;if(initialized)State=payingOut?FishingState.Casting:FishingState.Floating;}
        }
        public OriginalBait BaitInfo=>profile.baits[Mathf.Clamp(bait,0,profile.baits.Length-1)];
        FishingCatch hooked,displayCatch;
        FixedJoint hookJoint;
        readonly List<FishingCatch> loose=new List<FishingCatch>();
        int bait;
        float lineSteps,charge,reeling,xrReel,lineLength,lineVelocity,reelClock,reelMulti=1,wait,wetTime;
        bool payingOut,wasWet,wasRodHeld,crankReady,previousReel;
        Vector3 previousTip,tipVelocity;
        float lastCrankAngle;
        float capacityNoticeAt=-100;
        AudioSource sound;
        string oldBoard;
        MaterialPropertyBlock lineProperties;
        bool initialized,started;
        public void BindRig(FishingRig actor){rig=actor;if(combat)combat.BindRig(actor);if(Application.isPlaying&&started)Initialize();}
        public void SetReeling(float amount){reeling=Mathf.Clamp(amount,0,1.6f);}
        public bool ControlsHand(FishingHand hand)=>hand&&rod&&rod.item.owner==hand;
        void Start(){started=true;Initialize();}
        void Initialize(){
            if(initialized||!rig||!profile)return;initialized=true;
            if(!rig.Register(rod.item)){Debug.LogError("Fishing rod registration failed");enabled=false;return;}
            rod.item.SetHome(transform,rodRest.position,rodRest.rotation);rod.item.Recall();
            hookBody.isKinematic=true;hookBody.useGravity=false;hookBody.GetComponent<Collider>().enabled=false;if(rope)rope.connectedBody=rod.item.GetComponent<Rigidbody>();SetRope(false);line.enabled=false;State=FishingState.Ready;previousTip=WorldRodTip;inventory.Changed+=RefreshBoards;
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.22f;ApplyBait();RefreshBoards();
        }
        void Update(){
            if(!initialized){Initialize();if(!initialized)return;}
            if(rig.hands==null||!profile)return;
            tipVelocity=Vector3.Lerp(tipVelocity,(WorldRodTip-previousTip)/Mathf.Max(Time.deltaTime,.005f),.4f);previousTip=WorldRodTip;
            loose.RemoveAll(f=>!f||!f.IsAvailable);
            var hand=rod.item.owner;
            if(!hand){if(wasRodHeld)Abort();wasRodHeld=false;line.enabled=false;hookBody.GetComponent<Renderer>().enabled=false;RefreshBoards();return;}
            if(!wasRodHeld){previousReel=false;wasRodHeld=true;}
            line.enabled=true;hookBody.GetComponent<Renderer>().enabled=true;
            if(reeling>.01f&&!previousReel)StartReelInput();previousReel=reeling>.01f;
            if(State==FishingState.Charging)charge=Mathf.Clamp01(charge+Time.deltaTime/Mathf.Max(.1f,profile.castChargeSeconds));
            if(hooked){State=FishingState.Hooked;Tension=Mathf.Lerp(Tension,BoundedLineMotion?Mathf.Clamp01(.2f+Vector3.Distance(WorldRodTip,hookBody.position)*.015f+reeling*.12f):profile.bendResponse.Evaluate(rope.currentForce.magnitude),Time.deltaTime*10);}else Tension=Mathf.MoveTowards(Tension,0,Time.deltaTime*3);
            rod.Pose(Tension,reeling);RefreshBoards();
        }
        void FixedUpdate(){
            if(BoundedLineMotion&&LandingInProgress)TickLandingMotion();
            if(!rod||!rod.item.owner||!profile)return;
            if(BoundedLineMotion){FixedBoundedLine();return;}
            if(hookBody.isKinematic)return;
            float dt=Time.fixedDeltaTime;rope.connectedAnchor=rod.transform.InverseTransformPoint(WorldRodTip);
            if(payingOut){lineLength=Mathf.Min(profile.maxLine,Vector3.Distance(hookBody.position,WorldRodTip)+1);}
            else{
                float reelSpeed=hooked?profile.HookedReelSpeedAtDistance(Vector3.Distance(hookBody.position,WorldRodTip)):Mathf.Infinity;
                if(reeling>.01f){
                    if(hooked){
                        // Continuous distance keeps fast reeling independent of
                        // the old one-step-per-physics-tick limit and click spam.
                        lineSteps=Mathf.Max(0,lineSteps-reelSpeed*reeling*dt/Mathf.Max(.01f,profile.reelStep));reelClock=0;
                    }else{reelClock+=profile.reelFrequency*reelMulti*reeling*dt;if(reelClock>=1){lineSteps=Mathf.Max(0,lineSteps-1);reelClock=0;}}
                }
                reelMulti=Mathf.Lerp(reelMulti,1,dt);
                lineLength=Mathf.SmoothDamp(lineLength,MinimumLineLength+lineSteps*profile.reelStep,ref lineVelocity,hooked?profile.hookedReelSmooth:profile.reelSmooth,hooked?reelSpeed*Mathf.Max(1,reeling):Mathf.Infinity,dt);
            }
            rope.maxDistance=lineLength;
            bool wet=hookBody.position.y<SurfaceHeight&&IsWaterAt(hookBody.position,out _);
            hookBody.linearDamping=wet?profile.underwaterDrag:payingOut?0:Mathf.Lerp(4,0,Mathf.Clamp01(lineLength/4));
            if(wet){
                if(!wasWet)Splash();
                if(!hooked&&State==FishingState.Casting)State=FishingState.Floating;
            }
            TickCastBite(dt,wet);
            wasWet=wet;
            if(!hooked&&!payingOut&&lineSteps==0&&Vector3.Distance(hookBody.position,WorldRodTip)<.8f&&!EnsureCatchBeforeRecall())ResetHook();
        }
        void LateUpdate(){if(rod&&hookBody&&line&&rod.item.owner)DrawLine();}
        void StartReelInput(){
            if(State==FishingState.Ready||State==FishingState.Charging)return;
            if(!FishingCastMath.CanStartReeling(BoundedLineMotion,payingOut,castTouchedWater,boundedFlightBlocked))return;
            if(payingOut){lineSteps=Mathf.Max(0,(int)(Vector3.Distance(WorldRodTip,hookBody.position)/profile.reelStep));payingOut=false;lineVelocity=0;}
            if(lineVelocity<-2)reelMulti+=2;reelClock=0;lineSteps=Mathf.Max(0,lineSteps-1);
        }
        public bool IsWaterAt(Vector3 point,out FishingWaterZone zone){
            zone=null;if(WaterQuery!=null)return WaterQuery(point);foreach(var candidate in zones)if(candidate.Contains(point)){zone=candidate;break;}if(!zone)return false;
            foreach(var hit in Physics.RaycastAll(new Vector3(point.x,SurfaceHeight+2,point.z),Vector3.down,2.08f,~0,QueryTriggerInteraction.Ignore))if(!(hit.collider is CharacterController)&&!hit.collider.GetComponentInParent<FishingItem>()&&hit.collider.attachedRigidbody!=hookBody)return false;
            return true;
        }
        public void BeginCast(){if(!initialized||!rod.item.owner||vitals.Down||State!=FishingState.Ready)return;if(MaxLooseCatches>0&&loose.Count>=MaxLooseCatches||!HasRoomForCast()){rig.Notify("岸边已有较多鱼，请先处理和入库");return;}charge=0;State=FishingState.Charging;Play(profile.castClips);rig.Notify("拉竿蓄力 · 松开右键向海里抛出");}
        public void ReleaseCast(bool tracked){
            if(!initialized||!rod.item.owner||State!=FishingState.Charging)return;
            var forward=rig.view.transform.forward;var horizontal=Vector3.ProjectOnPlane(forward,Vector3.up);if(horizontal.sqrMagnitude<.01f)horizontal=Vector3.forward;horizontal.Normalize();
            var launch=horizontal*Mathf.Lerp(8,15,charge)+Vector3.up*Mathf.Lerp(3.5f,7,charge);
            if(tracked&&tipVelocity.magnitude>2)launch=tipVelocity+horizontal*4+Vector3.up*2;
            hookBody.position=WorldRodTip+horizontal*.15f;hookBody.isKinematic=false;hookBody.useGravity=true;hookBody.linearDamping=0;hookBody.GetComponent<Collider>().enabled=true;SetRope(true);lineLength=profile.maxLine;if(rope)rope.maxDistance=lineLength;if(BoundedLineMotion)BeginBoundedCast(launch);else hookBody.linearVelocity=launch;
            BeginWaterCast();payingOut=true;wetTime=0;wasWet=false;State=FishingState.Casting;previousReel=false;crankReady=false;rod.item.owner.Pulse(.2f,.06f);RefreshBoards();
        }
        void HookFish(){
            if(!castActive||castCaught||!castTouchedWater||hooked)return;
            var definition=worldEvents?worldEvents.PickCatch(this,UnityEngine.Random.value,UnityEngine.Random.value):FishingCatchPool.PickForCast(profile,BaitInfo,UnityEngine.Random.value,CastDepth,CastDistance,1,false,AvailableCatchVertices,AvailableCatchTriangles);
            if(!definition)definition=FishingCatchPool.SmallCatch(profile,BaitInfo,UnityEngine.Random.value,AvailableCatchVertices,AvailableCatchTriangles);
            if(!definition||!definition.catchPrefab){if(Time.time-capacityNoticeAt>3){rig.Notify("岸边已有较多钓获物，请先击败并入库");capacityNoticeAt=Time.time;}return;}
            PrepareBitePosition();var fish=Instantiate(definition.catchPrefab,hookBody.position,Quaternion.identity,transform);fish.fishing=this;fish.weight=definition.weight*RandomWeight();fish.SetDrip(UnityEngine.Random.value<(worldEvents?worldEvents.ShinyChance:.08f));fish.item.name=(fish.IsDrip?"稀有 ":"")+fish.DisplayName+" "+fish.weight.ToString("0.00")+" kg";
            if(!rig.Register(fish.item)){Destroy(fish.gameObject);return;}
            fish.Place(hookBody.position,hookBody.rotation*Quaternion.Euler(-90,0,0));fish.Attach();castCaught=true;HookedFish?.Invoke(fish);hooked=fish;loose.Add(fish);
            if(BoundedLineMotion){fish.SuspendOnLine();haulRaised=catchReeledIn=false;}else{hookBody.linearVelocity=Vector3.zero;hookBody.angularVelocity=Vector3.zero;hookJoint=fish.body.gameObject.AddComponent<FixedJoint>();hookJoint.autoConfigureConnectedAnchor=false;hookJoint.connectedBody=hookBody;hookJoint.massScale=.5f;hookJoint.connectedMassScale=1;hookJoint.anchor=fish.hookAnchor?fish.body.transform.InverseTransformPoint(fish.hookAnchor.position):Vector3.zero;hookJoint.connectedAnchor=BaitInfo.hookPoint;hookJoint.enableCollision=false;}
            foreach(var collider in fish.GetComponentsInChildren<Collider>(true))Physics.IgnoreCollision(collider,hookBody.GetComponent<Collider>(),true);
            if(bait>0&&UnityEngine.Random.value*100<BaitInfo.loss){inventory.UseBait(bait);}
            if(worldEvents)worldEvents.CatchSpawned(definition);FocusCatch(fish);State=FishingState.Hooked;wetTime=0;rod.item.owner.Pulse(.6f,.12f);rig.Notify(definition.seaHorse?"钓到海马了？先收线上来看看！":"钓到 "+fish.DisplayName+"！继续收线甩上岸；右键可脱钩");RefreshBoards();
        }
        static float RandomWeight(){float gaussian=Mathf.Sqrt(-2*Mathf.Log(Mathf.Max(.00001f,UnityEngine.Random.value)))*Mathf.Cos(2*Mathf.PI*UnityEngine.Random.value);return Mathf.Clamp(1+gaussian*.15f,.25f,1.75f);}
        public void ReleaseCatch(FishingCatch fish){ReleaseCatch(fish,false);}
        public void ReleaseCatch(FishingCatch fish,bool settledOnShore){
            if(!fish)return;
            if(BoundedLineMotion&&hooked==fish&&!settledOnShore&&!fish.IsDead&&fish.ArchiveLifted&&Vector3.Distance(fish.body.worldCenterOfMass,rig.view.transform.position)<=8&&TryBeginDryLanding(fish))return;
            if(hooked==fish){ClearJoint();hooked=null;fish.ResumeFromLine();fish.MarkLanded();if(settledOnShore)fish.SettleOnShore();State=payingOut?FishingState.Casting:FishingState.Floating;Play(profile.releaseClips);rig.Notify(fish.IsDead?"鱼已死亡 · E 拿起后放入冷藏箱":"鱼已脱钩！用鱼枪击败它，再按 E 拿取");}
        }
        public void OnCatchKilled(FishingCatch fish){StopLandingOnDeath(fish);KilledFish?.Invoke(fish);if(hooked==fish)ReleaseCatch(fish);rig.Notify("已击败 "+fish.DisplayName+" · 可以 E 拿取 / 入库");if(combat)combat.RegisterKill(fish);}
        void ClearJoint(){if(hookJoint){Destroy(hookJoint);hookJoint=null;}}
        void ResetHook(){ClearWaterCast();SetRope(false);hookBody.isKinematic=true;hookBody.useGravity=false;hookBody.GetComponent<Collider>().enabled=false;State=FishingState.Ready;Tension=0;reeling=0;wetTime=0;payingOut=wasWet=false;lineLength=profile?profile.minLine:.5f;rod.ResetSteering();if(bait>0&&inventory.BaitCount(bait)==0){bait=0;ApplyBait();}}
        public void Abort(string message=""){if(hooked)ReleaseCatch(hooked);ClearJoint();ResetHook();if(!string.IsNullOrEmpty(message))rig.Notify(message);RefreshBoards();}
        public void ReturnRod(){
            Abort();if(rod.item.owner)rod.item.owner.Forget(rod.item);rod.item.Recall();
            reeling=xrReel=0;previousReel=wasRodHeld=false;line.enabled=false;hookBody.GetComponent<Renderer>().enabled=false;
            previousTip=WorldRodTip;tipVelocity=Vector3.zero;
        }
        public void CycleBait(){if(!initialized)return;if(State!=FishingState.Ready){rig.Notify("先收回鱼线再换饵");return;}for(int i=0;i<profile.baits.Length;i++){bait=(bait+1)%profile.baits.Length;if(bait==0||inventory.BaitCount(bait)>0)break;}ApplyBait();rig.Notify("已换为 "+BaitName);RefreshBoards();}
        void ApplyBait(){if(!profile)return;var filter=hookBody.GetComponent<MeshFilter>();if(filter&&BaitInfo.mesh)filter.sharedMesh=BaitInfo.mesh;}
        public void Deposit(FishingHand hand){var item=hand.Held;if(!item||!item.fish){rig.Notify("拿着已击败的鱼，再放入冷藏箱");return;}if(!item.fish.IsDead){rig.Notify("活鱼不能入库，先用枪击败它");return;}if(inventory.Deposit(item.fish)){rig.Feedback(hand);rig.Notify("已击败的鱼已入库 · 仓库容量不限");}RefreshBoards();}
        public bool TriggerHand(int index,bool trigger,bool previous,bool cycle,bool restock){
            var hand=rig.hands[index];var holder=rod.item.owner;
            if(holder){
                if(hand==holder){if(cycle)CycleBait();if(restock){if(hooked)ReleaseCatch(hooked);else Abort();}if(trigger&&!previous&&State==FishingState.Ready)BeginCast();if(!trigger&&previous&&State==FishingState.Charging)ReleaseCast(true);return true;}
                xrReel=0;if(hand.Held){SetReeling(0);return false;}
                if(trigger){var local=rod.crank.InverseTransformPoint(hand.transform.position);float angle=Mathf.Atan2(local.z,local.y)*Mathf.Rad2Deg;if(local.magnitude<.25f){if(crankReady)xrReel=Mathf.Clamp(Mathf.Abs(Mathf.DeltaAngle(lastCrankAngle,angle))/Mathf.Max(Time.deltaTime,.005f)/240,0,1.6f);lastCrankAngle=angle;crankReady=true;}else{xrReel=1;crankReady=false;}}else crankReady=false;
                SetReeling(xrReel);return trigger;
            }
            xrReel=0;SetReeling(0);return false;
        }
        void SetRope(bool active){if(!rope)return;rope.spring=active?profile.jointSpring:0;rope.damper=active?profile.jointDamper:0;if(!active)rope.maxDistance=10000;}
        public FishingInteractable NearestInteraction(Vector3 point,float radius){FishingInteractable best=null;foreach(var target in interactables){float distance=Vector3.Distance(point,target.transform.position);if(distance<radius){radius=distance;best=target;}}return best;}
        void DrawLine(){
            if(lineProperties==null)lineProperties=new MaterialPropertyBlock();lineProperties.SetColor("_BaseColor",new Color(.86f,.94f,1));line.SetPropertyBlock(lineProperties);
            if(State==FishingState.Ready||State==FishingState.Charging)hookBody.transform.position=WorldRodTip+Vector3.down*.1f;
            var guides=rod.lineGuides;int guideCount=guides==null?0:guides.Length;const int count=20;line.positionCount=guideCount+count;
            for(int i=0;i<guideCount;i++)line.SetPosition(i,ToolPoint(guides[i].position));
            var start=WorldRodTip;var end=hookBody.position;float sag=Mathf.Clamp((lineLength-Vector3.Distance(start,end))*.12f,0,.5f);
            for(int i=0;i<count;i++){float t=i/(float)(count-1);line.SetPosition(guideCount+i,Vector3.Lerp(start,end,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*sag));}
        }
        void Splash(){if(splash){splash.transform.position=new Vector3(hookBody.position.x,SurfaceHeight+.03f,hookBody.position.z);splash.Play();}}
        public void Restock(){inventory.RestockBait();rig.Notify("四种原作拟饵已补充至至少八份");}
        void Play(AudioClip[] clips){if(sound&&clips!=null&&clips.Length>0)sound.PlayOneShot(clips[UnityEngine.Random.Range(0,clips.Length)]);}
        string Status=>State==FishingState.Ready?(rig.Tracked?"持竿扳机拉竿，松开抛出":"右键拉竿，松开抛出 · B 换饵"):State==FishingState.Charging?"松开抛竿":hooked?"已上钩 · 持续收线拉出水面\n落到岸边脱钩；右键也可脱钩":BiteReady?"已有鱼靠近 · 收线即可咬钩":"放线下沉 · 收线咬钩 · 浅水也有小鱼";
        public void RefreshBoards(){if(!inventory||!profile)return;string text="原作物理垂钓\n"+BaitName+"\n"+Status+"\n无限仓库 · 已存 "+inventory.Total+" 条";if(text!=oldBoard){oldBoard=text;if(board)board.text=text;}if(rodDisplay){rodDisplay.gameObject.SetActive(rig.Tracked&&rod.item.owner);rodDisplay.text=Status+"\n"+WaitingCatchNotice;}if(stockBoard)stockBoard.text="无限仓库 · E 放入已击败的鱼\n库存 "+inventory.Total+" · 已入库 "+inventory.Landed+"\nTab 图鉴 · I 仓库";}
        void OnDestroy(){if(inventory)inventory.Changed-=RefreshBoards;}
    }
}
