using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Staff;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using IncrementalAddons.Casino;
using UnityEngine;
using UnityEngine.AI;

namespace SushiAndFish.FriendsLife
{
    // Owns only new transient NavMeshData and one physically verified source
    // entrance seam. Original surfaces/assets/actors/settings are read-only.
    public sealed partial class FriendCasinoGroundNavigation:MonoBehaviour
    {
        public static FriendCasinoGroundNavigation Instance{get;private set;}
        public string Phase{get;private set;}="Waiting";
        public string LastFailure{get;private set;}
        public int SourceCount{get;private set;}
        public string[] CandidateDiagnostics{get;private set;}=Array.Empty<string>();
        public string[] ApproachDiagnostics{get;private set;}=Array.Empty<string>();
        public Vector3 SeamStart{get;private set;}
        public Vector3 SeamEnd{get;private set;}
        public Vector3 Approach{get;private set;}
        public Vector3 DutyPoint{get;private set;}
        NavMeshData ownedData;NavMeshDataInstance ownedMesh;NavMeshLinkInstance ownedLink;
        Transform module;Vector3 mapPosition,mapScale;Quaternion mapRotation;Bounds region;
        FriendJobStation boundStation;int type;bool attempted;float next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {if(Instance)return;var go=new GameObject("Friend source floor 0 navigation");DontDestroyOnLoad(go);Instance=go.AddComponent<FriendCasinoGroundNavigation>();}
        bool OriginalReady(ParkCasinoIntegration casino)
        {
            if(!casino||!casino.Ready||!NetworkSingleton<ElevatorManager>.TryGetInstance(out var original)||!original)return false;
            return GroundWorkPolicy.OriginalGroundReady(casino.Ready,casino.CurrentFloor,NativeCasinoButtonAccess.ReadNativeFloor(original),original.isTeleporting,original.IsLocked);
        }
        public static bool OriginalGroundAvailable()=>Instance&&Instance.OriginalReady(ParkCasinoIntegration.Instance);
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;var casino=ParkCasinoIntegration.Instance;
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!economy)return;
            bool moved=attempted&&(!module||!casino||casino.ModuleRoot!=module||module.position!=mapPosition||Quaternion.Angle(module.rotation,mapRotation)>.01f||Vector3.Distance(module.lossyScale,mapScale)>.001f);
            if(!OriginalReady(casino)||moved){Release();attempted=false;Phase="Waiting";return;}
            if(attempted||!runtime.IsWorldRunning||Time.unscaledTime<next)return;next=Time.unscaledTime+1;
            var auth=economy.State.publicJobAuthorizations.FirstOrDefault(a=>a.kind=="CasinoElevator"&&
                FriendsCasinoElevatorBridge.Instance&&FriendsCasinoElevatorBridge.Instance.State.journeys.Any(j=>j.personId==a.personId&&j.workKind=="CasinoElevator"&&!j.cancelRequested&&j.phase!="Done"&&j.phase!="Cancelled"));
            var returning=economy.State.jobs.FirstOrDefault(j=>j.cabinReturnRequired&&j.state!=FriendJobPhase.Done);
            string personId=auth?.personId??returning?.personId;if(string.IsNullOrEmpty(personId))return;var actor=runtime.ActorOf(personId);
            bool ownPause=runtime.State.leases.Any(l=>l.personId==personId&&(l.leaseId.StartsWith("casino-native-travel/",StringComparison.Ordinal)||l.leaseId.StartsWith("casino-cabin/",StringComparison.Ordinal)))||economy.HasActiveJob(personId);
            if(!actor||!FriendResidentBindings.IsOwned(actor)||actor.Brain==null||actor.Runtime==null||actor.Runtime.IsSnapshotApplying&&!ownPause)return;
            var station=FindObjectsByType<FriendJobStation>(FindObjectsSortMode.None).FirstOrDefault(s=>s.sourceKind=="CasinoElevator"&&(s.Available||returning!=null&&s.slotId==returning.slotId)&&s.stationTransform);
            if(!station)return;
            attempted=true;module=casino.ModuleRoot;mapPosition=module.position;mapRotation=module.rotation;mapScale=module.lossyScale;
            try{Build(actor,station);}
            catch(Exception error){LastFailure=error.GetBaseException().Message;Phase="Rejected";Release();Debug.LogWarning("[FriendsLife] Original ground route retained: "+LastFailure);}
        }
        void Build(ResidentController actor,FriendJobStation station,bool visitorPhysicalExit=false)
        {
            Phase="Preparing";var physics=new FriendGroundRoutePhysics(actor,station.stationTransform);type=physics.Agent.agentTypeID;boundStation=station;
            var supports=module.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&
                (c.name=="polySurface239"||c.name=="polySurface240"||c.name=="SM_PillarLobby01 (8)"||c.name=="SM_PillarLobby01 (6)"||
                 c.name=="Ground"&&Path(c.transform).Contains("EntranceCasinoGRP"))).ToArray();
            var entrance=supports.FirstOrDefault(c=>c.name=="polySurface239");
            if(!entrance||supports.Length<4)throw new InvalidOperationException("Actual original front support geometry is incomplete.");
            var bounds=supports[0].bounds;foreach(var c in supports.Skip(1))bounds.Encapsulate(c.bounds);
            float low=Mathf.Min(bounds.min.y,actor.transform.position.y)-.2f,high=bounds.max.y+physics.Height+physics.Climb+.3f;
            bounds.SetMinMax(new Vector3(bounds.min.x-.2f,low,bounds.min.z-.2f),new Vector3(bounds.max.x+.2f,high,bounds.max.z+.2f));region=bounds;
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>!s.component||!(s.component is Collider c)||!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger||c is TerrainCollider||physics.IsCharacter(c));
            SourceCount=sources.Count;if(SourceCount==0)throw new InvalidOperationException("No actual source colliders collected.");
            var settings=NavMesh.GetSettingsByID(type);
            // Preserve this agent type's original navigation erosion radius.
            // Actual capsule Foot/Clear and full surface traces still use physics.Radius.
            settings.agentHeight=Mathf.Max(settings.agentHeight,physics.Height);
            settings.overrideVoxelSize=true;settings.voxelSize=Mathf.Min(.04f,NavMesh.GetSettingsByID(type).agentRadius/3);
            ownedData=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(!ownedData)throw new InvalidOperationException("Owned source-collider projection produced no data.");
            ownedData.name="Friend original floor0 physical projection";ownedMesh=NavMesh.AddNavMeshData(ownedData);
            if(!ownedMesh.valid)throw new InvalidOperationException("Owned physical projection registration rejected.");
            Phase="CheckingInternalRoute";
            Vector3 oldPoint=default,newPoint=default,goal=default;bool linked=false;
            var diagnostics=new List<string>();
            // Search only the measured low front tip. Every candidate is
            // re-read from real support; no fixed invented step is accepted.
            foreach(float zOffset in new[]{0f,.5f,-.5f,1f,-1f})foreach(float inset in new[]{.55f,.65f,.75f})
            {
                var inside=new Vector3(entrance.bounds.max.x-inset,3.35f,station.stationTransform.position.z+zOffset);
                var outside=new Vector3(entrance.bounds.max.x+physics.Radius*2+.1f,3,inside.z);
                string key="z="+zOffset+"/inset="+inset+":";
                if(!physics.Foot(inside,4.1f,out var inFloor)){diagnostics.Add(key+"inside-floor-missing");continue;}
                if(inFloor.collider!=entrance){diagnostics.Add(key+"inside-floor-owner="+inFloor.collider.name+"@"+inFloor.point.ToString("F4"));continue;}
                if(!physics.Foot(outside,4.1f,out var outFloor)){diagnostics.Add(key+"outside-floor-missing");continue;}
                if(!(outFloor.collider is TerrainCollider)){diagnostics.Add(key+"outside-not-terrain="+outFloor.collider.name);continue;}
                key+="physical="+outFloor.point.ToString("F4")+"->"+inFloor.point.ToString("F4")+":";
                if(!physics.ShortStep(outFloor.point,inFloor.point,out var stepReason)){diagnostics.Add(key+"short-step-rejected/"+stepReason);continue;}
                if(!physics.SamplePhysical(outFloor.point,.15f,out var oldNav)){diagnostics.Add(key+"old-sample/"+SampleReason(physics,outFloor.point,.15f));continue;}
                if(!physics.SamplePhysical(inFloor.point,.2f,out var newNav)){diagnostics.Add(key+"new-sample/"+SampleReason(physics,inFloor.point,.2f));continue;}
                if(!TryApproach(physics,station,out var candidate)){diagnostics.Add(key+"approach-rejected");continue;}
                var inner=new NavMeshPath();
                if(!NavMesh.CalculatePath(newNav,candidate,physics.Filter,inner)){diagnostics.Add(key+"inner-calculate-false/"+PathFacts(inner,newNav,candidate,physics.Filter));continue;}
                if(!FriendGroundSurfacePath.TryPlan(physics,inner,region,default,default,false,out _,out var reason)){diagnostics.Add(key+"inner-rejected/"+reason+"/"+PathFacts(inner,newNav,candidate,physics.Filter));continue;}
                diagnostics.Add(key+"inner-pass");
                oldPoint=oldNav;newPoint=newNav;goal=candidate;linked=true;break;
            }
            CandidateDiagnostics=diagnostics.ToArray();
            if(!linked)throw new InvalidOperationException("Actual body/climb/source supports or complete internal physical route rejected; no entrance link created.");
            var link=new NavMeshLinkData{agentTypeID=type,startPosition=oldPoint,endPosition=newPoint,width=0,bidirectional=true,area=0,costModifier=-1};
            ownedLink=NavMesh.AddLink(link,Vector3.zero,Quaternion.identity);
            if(!ownedLink.valid)throw new InvalidOperationException("Verified short source seam rejected by navigation system.");
            string person=actor.GetComponent<FriendWorldActor>()?.personId;
            bool restoringExit=visitorPhysicalExit||FriendsEconomyService.Instance?.State.jobs.Any(j=>j.personId==person&&j.cabinReturnRequired&&j.state!=FriendJobPhase.Done)==true;
            if(!TryPlanExplicitApproach(actor,station.stationTransform,oldPoint,newPoint,goal,restoringExit,out var stage,out _,out var physicalRegion,out var fullReason))
                throw new InvalidOperationException("Explicit original public/source route rejected / "+fullReason);
            ExteriorStage=stage;PhysicalRegion=physicalRegion;
            SeamStart=oldPoint;SeamEnd=newPoint;Approach=goal;LastFailure=null;Phase="Ready";
        }
        bool TryApproach(FriendGroundRoutePhysics physics,FriendJobStation station,out Vector3 destination)
        {
            var diagnostics=new List<string>();
            destination=default;var target=station.stationTransform;
            if(!physics.Foot(target.position,target.position.y+.1f,out var floor)){ApproachDiagnostics=new[]{"target-floor-missing"};return false;}
            var elevator=target.GetComponentInParent<ElevatorManager>();var carving=elevator?elevator.GetComponent<NavMeshObstacle>():null;
            if(!carving||!carving.enabled||!carving.gameObject.activeInHierarchy||!carving.carving||carving.shape!=NavMeshObstacleShape.Box)
            {ApproachDiagnostics=new[]{"exact-original-elevator-box-carving-required"};return false;}
            var forward=Vector3.ProjectOnPlane(target.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.99f)return false;
            var right=Vector3.Cross(Vector3.up,forward);var scale=carving.transform.lossyScale;
            float half=Mathf.Abs(Vector3.Dot(forward,carving.transform.right))*carving.size.x*Mathf.Abs(scale.x)*.5f+
                Mathf.Abs(Vector3.Dot(forward,carving.transform.forward))*carving.size.z*Mathf.Abs(scale.z)*.5f;
            var center=carving.transform.TransformPoint(carving.center);
            var front=center+forward*(half+physics.Radius+.15f);front.y=floor.point.y;
            foreach(float side in new[]{0f,.25f,-.25f,.5f,-.5f})
            {
                var wanted=front+right*side;
                if(!physics.Foot(wanted,floor.point.y+physics.Climb,out var support)||Mathf.Abs(support.point.y-floor.point.y)>physics.Climb+.005f||
                    !physics.Clear(support.point)||!physics.SamplePhysical(support.point,.15f,out var gate))continue;
                foreach(float distance in new[]{.8f,1.1f,1.35f})foreach(float angle in new[]{0f,45f,-45f,90f,-90f,180f})
                {
                    var direction=Vector3.ProjectOnPlane(Quaternion.AngleAxis(angle,Vector3.up)*target.forward,Vector3.up).normalized;
                    var duty=target.position+direction*distance;duty.y=floor.point.y;
                    if(!physics.Foot(duty,floor.point.y+.12f,out var ground)||Mathf.Abs(ground.point.y-floor.point.y)>.12f||!physics.Clear(ground.point))continue;
                    if(!FriendCasinoCabinMotor.ValidateSegment(physics.Actor,target,gate,ground.point,out var reason))
                    {diagnostics.Add("gate="+gate.ToString("F4")+"/duty="+ground.point.ToString("F4")+"/physical="+reason);continue;}
                    DutyPoint=ground.point;destination=gate;diagnostics.Add("original-carving-retained/gate="+gate.ToString("F4")+"/duty="+DutyPoint.ToString("F4"));
                    ApproachDiagnostics=diagnostics.ToArray();return true;
                }
            }
            ApproachDiagnostics=diagnostics.Concat(new[]{"no-complete-physical-front-opening-traversal"}).ToArray();return false;
        }
        static string SampleReason(FriendGroundRoutePhysics physics,Vector3 foot,float radius)
        {
            if(!NavMesh.SamplePosition(foot+Vector3.up*.04f,out var nav,radius,physics.Filter))return "nav-missing";
            return "nav="+nav.position.ToString("F4")+"/delta="+Mathf.Abs(nav.position.y-foot.y)+"/bodyClear="+physics.Clear(nav.position);
        }
        static string PathFacts(NavMeshPath path,Vector3 start,Vector3 goal,NavMeshQueryFilter filter)
        {
            var corners=path.corners;
            string facts="start="+start.ToString("F5")+"/goal="+goal.ToString("F5")+"/status="+path.status+"/corners=["+string.Join(";",corners.Select(p=>p.ToString("F5")))+"]";
            if(corners.Length>0&&NavMesh.FindClosestEdge(corners[corners.Length-1],out var edge,filter))
                facts+="/lastEdge="+edge.position.ToString("F5")+"/normal="+edge.normal.ToString("F5")+"/distance="+edge.distance;
            if(NavMesh.FindClosestEdge(goal,out var targetEdge,filter))
                facts+="/goalEdge="+targetEdge.position.ToString("F5")+"/normal="+targetEdge.normal.ToString("F5")+"/distance="+targetEdge.distance;
            return facts;
        }
        public static bool TryResolve(FriendJobStation station,ResidentController actor,out Vector3 destination)
        {
            destination=default;var self=Instance;
            if(!self||self.Phase!="Ready"||self.boundStation!=station||!self.ownedMesh.valid||!self.ownedLink.valid||!actor||actor.Brain==null)return false;
            var actual=actor.GetComponent<NavMeshAgent>();if(!actual||!actual.enabled||!actual.isOnNavMesh)return false;
            var physics=new FriendGroundRoutePhysics(actor,station.stationTransform);
            if(physics.Agent.agentTypeID!=self.type||!self.TryApproach(physics,station,out var goal))return false;
            var path=new NavMeshPath();if(!physics.Agent.CalculatePath(goal,path)||!physics.ValidatePath(path,self.region,self.SeamStart,self.SeamEnd,true)||
                path.corners.Length==0||Vector3.Distance(path.corners[0],actor.transform.position)>.25f||Vector3.Distance(path.corners.Last(),goal)>.2f)return false;
            var privacy=FriendHousingIsland.Instance?.GetComponent<FriendHousingPrivacy>();if(privacy&&privacy.ExcludesPathFor(actor,path))return false;
            destination=goal;return true;
        }
        public static bool TryGetCabinRoute(FriendJobStation station,ResidentController actor,out Vector3 gate,out Vector3 duty)
        {
            gate=default;duty=default;var self=Instance;
            if(!self||self.Phase!="Ready"||self.boundStation!=station||!self.ownedMesh.valid||!self.ownedLink.valid||!actor||actor.Brain==null||
                !self.OriginalReady(ParkCasinoIntegration.Instance))return false;
            var physics=new FriendGroundRoutePhysics(actor,station.stationTransform);
            if(physics.Agent.agentTypeID!=self.type||!self.TryApproach(physics,station,out gate))return false;
            duty=self.DutyPoint;return true;
        }
        static string Path(Transform t)=>t?(t.parent?Path(t.parent)+"/":"")+t.name:"";
        void Release(){visitorValidated.Clear();if(ownedLink.valid)ownedLink.Remove();if(ownedMesh.valid)ownedMesh.Remove();if(ownedData)Destroy(ownedData);ownedData=null;}
        void OnDestroy(){Release();if(Instance==this)Instance=null;}
    }
}
