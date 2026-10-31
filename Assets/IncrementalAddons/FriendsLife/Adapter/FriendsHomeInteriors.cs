using HousingIsland=SushiAndFish.FriendsLife.FriendHousingIsland;
using HousingPlacement=SushiAndFish.FriendsLife.FriendHousingPlacement;
using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.ResidentialIsland;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.FriendsLife
{
    public sealed class HousingPrivateRoom : MonoBehaviour
    {
        public string groupId,personId,homeId;
        public HousingHouse house;
        public Transform arrival,returnToCommon;
        void Awake()=>FriendInstances<HousingPrivateRoom>.Add(this);
        void OnDestroy()=>FriendInstances<HousingPrivateRoom>.Remove(this);
    }

    // Shared homes retain the authored exterior/common room and give every
    // member a complete real room instance, including its own bed and toilet.
    // Remote interiors are reached through an actual internal door interaction.
    public sealed partial class FriendsHomeInteriors : MonoBehaviour
    {
        public static FriendsHomeInteriors Instance {get;private set;}
        sealed class Set
        {
            public string signature,homeId,groupId;public Vector3 stagingOffset;public readonly List<HousingPrivateRoom> rooms=new List<HousingPrivateRoom>();public readonly List<GameObject> doors=new List<GameObject>();
        }
        readonly Dictionary<string,Set> sets=new Dictionary<string,Set>();float next;
        readonly Dictionary<string,Set> preparedSets=new Dictionary<string,Set>();int prepareSequence;
        void Awake(){Instance=this;if(!GetComponent<FriendsHomeStyleProjection>())gameObject.AddComponent<FriendsHomeStyleProjection>();}
        void Update()
        {
            var economy=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;var housing=HousingIsland.Instance;
            if(!economy||!runtime||!runtime.Ready||!housing||!housing.CanProject||Time.unscaledTime<next)return;next=Time.unscaledTime+2;
            foreach(var key in sets.Keys.Where(k=>!economy.State.groups.Any(g=>g.groupId==k)).ToArray()){Dispose(sets[key]);sets.Remove(key);}
            foreach(var group in economy.State.groups.OrderBy(g=>g.groupId,StringComparer.Ordinal))
            {
                var common=economy.Plot(group.homeId)?.house;if(!common)continue;
                string signature=group.homeId+"/"+common.definition.id+"/"+common.GetInstanceID()+"/"+string.Join("/",group.members);
                if(sets.TryGetValue(group.groupId,out var current)&&current.signature==signature){if(!housing.CanBuild)foreach(var room in current.rooms)if(room)housing.BindClientFurniture(room.house);continue;}
                // Occupied furniture exits through its existing animation before
                // an upgraded/split interior projection is disposed.
                if(current!=null&&current.rooms.Any(r=>r&&r.house.GetComponentsInChildren<HousingRestInteractable>(true).Any(rest=>rest.area&&rest.area.IsOccupied)))continue;
                if(current!=null)Dispose(current);
                var built=Build(group,common,StableIsland(group.homeId));if(built!=null){built.signature=signature;sets[group.groupId]=built;}
            }
        }
        static int StableIsland(string homeId)=>!string.IsNullOrEmpty(homeId)&&homeId[0]=='P'&&int.TryParse(homeId.Substring(1),out int parcel)?parcel:throw new ArgumentException("Private room requires a canonical parcel ID");
        Set Build(HomeGroup group,HousingHouse common,int island,bool prepareOnly=false,Vector3 stagingOffset=default)
        {
            if(group.members.Count<2||group.members.Count>8)return null;
            var economy=FriendsEconomyService.Instance;var housing=HousingIsland.Instance;var result=new Set{homeId=group.homeId,groupId=group.groupId,stagingOffset=stagingOffset};
            var sourceDoor=common.GetComponentsInChildren<HousingDoor>(true).OrderBy(d=>Vector3.Distance(d.transform.position,common.transform.position)).FirstOrDefault();
            if(!sourceDoor)return null;
            try
            {
            RoomDirectory directory;TryCreateDirectory(common,sourceDoor,false,result.doors,out directory);
            for(int index=0;index<group.members.Count;index++)
            {
                string person=group.members[index];
                var root=housing.CreatePrivateInterior(common.definition,group.homeId,person,new Vector3(22000+island*300,50,index*40)+stagingOffset,prepareOnly);
                if(!root||!root.GetComponentsInChildren<HousingRestInteractable>(true).Any(r=>r.kind==HousingRestKind.Bed))
                {if(root)Destroy(root.gameObject);Dispose(result);return null;}
                var binding=root.gameObject.AddComponent<HousingPrivateRoom>();binding.groupId=group.groupId;binding.personId=person;binding.homeId=group.homeId;binding.house=root;
                var bed=root.GetComponentsInChildren<HousingRestInteractable>(true).First(r=>r.kind==HousingRestKind.Bed);
                binding.arrival=bed.exit?bed.exit:root.exit?root.exit:root.entry;
                binding.returnToCommon=common.chairs?.FirstOrDefault(a=>a&&a.exit)?.exit??common.entry;
                if(!binding.arrival||!binding.returnToCommon)throw new InvalidOperationException("室内出入口资源缺失");
                result.rooms.Add(binding);
                if(directory!=null)CreateDirectoryEntry(directory,index,group.members.Count,person,group.homeId,binding.arrival);
                else
                {
                    // Unknown house geometry retains the existing working portal path.
                    int side=index/4,row=index%4;float x=(side==0?-1:1)*common.definition.footprint.x*.33f;
                    float z=Mathf.Lerp(-common.definition.footprint.y*.29f,common.definition.footprint.y*.29f,(row+.5f)/4);
                    var inward=Quaternion.Euler(0,side==0?90:270,0);
                    CreateDoor(sourceDoor,common.transform,common.transform.TransformPoint(new Vector3(x,.05f,z)),common.transform.rotation*inward,person,group.homeId,binding.arrival,false,result.doors);
                }
                var returnSource=root.GetComponentsInChildren<HousingDoor>(true).First();RoomDirectory returnDirectory;
                if(TryCreateDirectory(root,returnSource,true,result.doors,out returnDirectory))
                {
                    CreateDirectoryEntry(returnDirectory,0,1,person,group.homeId,binding.returnToCommon);
                    if(!prepareOnly)returnDirectory.root.SetActive(true);
                }
                else CreateDoor(returnSource,root.transform,root.entry.position,root.entry.rotation,person,group.homeId,binding.returnToCommon,true,result.doors);
            }
            if(prepareOnly){foreach(var door in result.doors)if(door)door.SetActive(false);}
            else if(directory!=null)directory.root.SetActive(true);
            return result;
            }
            catch(Exception exception){Dispose(result);Debug.LogWarning("Residence preparation refused: "+exception.GetBaseException().Message);return null;}
        }
        public bool PrepareGroup(string token,HomeGroup group,HousingHouse commonOverride=null)
        {
            if(group==null)return true;
            var common=commonOverride?commonOverride:FriendsEconomyService.Instance.Plot(group.homeId)?.house;if(!common)return false;
            string expectedSignature=group.homeId+"/"+common.definition.id+"/"+common.GetInstanceID()+"/"+string.Join("/",group.members);
            if(preparedSets.TryGetValue(token,out var prior))return prior.signature==expectedSignature;
            var staging=new Vector3(80000+(++prepareSequence)*1000,0,0);var prepared=Build(group,common,StableIsland(group.homeId),true,staging);if(prepared==null)return false;
            prepared.signature=group.homeId+"/"+common.definition.id+"/"+common.GetInstanceID()+"/"+string.Join("/",group.members);preparedSets[token]=prepared;return true;
        }
        public void CommitPreparedGroup(string token)
        {
            if(!preparedSets.TryGetValue(token,out var prepared))return;preparedSets.Remove(token);
            foreach(var key in sets.Keys.Where(k=>k==prepared.groupId||sets[k].rooms.Any(r=>r&&prepared.rooms.Any(n=>n.personId==r.personId))).ToArray()){Dispose(sets[key]);sets.Remove(key);}
            foreach(var room in prepared.rooms)if(room){room.transform.position-=prepared.stagingOffset;HousingIsland.Instance.ActivatePrivateInterior(room.house);}
            foreach(var door in prepared.doors)if(door)door.SetActive(true);prepared.stagingOffset=Vector3.zero;sets[prepared.groupId]=prepared;
        }
        public void CancelPreparedGroup(string token){if(preparedSets.TryGetValue(token,out var prepared)){preparedSets.Remove(token);Dispose(prepared);}}
        // Known compact-house room entry directory. No original door/furniture is changed.
        // Collider triggers are visible to native Interactor.FindGazeTargets (Collide),
        // and cannot become new solid blockers in the living room.
        sealed class RoomDirectory
        {
            public GameObject root;public HousingHouse house;public Material wood;public TMPro.TMP_FontAsset font;public bool exit;public int layer;
        }
        static Vector3 DirectoryApproach=>new Vector3(2f,.02f,-.4f);
        static Vector3 DirectorySlot(int index,int count,bool exit)
        {
            if(count<1||count>8||index<0||index>=count||exit&&(count!=1||index!=0))throw new ArgumentOutOfRangeException(nameof(index));
            return exit?new Vector3(2f,1.55f,.54f):new Vector3(2f+(index%2==0?-.43f:.43f),1.87f-(index/2)*.26f,.54f);
        }
        static bool CompactDirectoryWall(HousingHouse house)
        {
            // Catalog footprint includes outside eaves. Gate the actual model instead
            // of projecting these coordinates into a different/larger house.
            if(!house||house.definition?.id!="compact_japanese")return false;
            var wall=house.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m=>m&&m.name=="AR_GuestHome_InteriorWallsConnected"&&m.sharedMesh);
            if(!wall)return false;var source=wall.sharedMesh.bounds;var local=new Bounds();bool first=true;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            {
                var p=new Vector3(x==0?source.min.x:source.max.x,y==0?source.min.y:source.max.y,z==0?source.min.z:source.max.z);
                p=house.transform.InverseTransformPoint(wall.transform.TransformPoint(p));
                if(first){local=new Bounds(p,Vector3.zero);first=false;}else local.Encapsulate(p);
            }
            return Vector3.Distance(local.min,new Vector3(-3.5f,0,.66f))<.045f&&Vector3.Distance(local.max,new Vector3(3.5f,3,4))<.045f;
        }
        bool TryCreateDirectory(HousingHouse house,HousingDoor source,bool exit,List<GameObject> allocated,out RoomDirectory directory)
        {
            directory=null;if(!CompactDirectoryWall(house))return false;
            var leaf=source?source.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r&&r.name.EndsWith("_Leaf",StringComparison.Ordinal)&&r.sharedMaterial):null;
            var font=HousingIsland.Instance?.Catalog?.font;if(!leaf||!font)return false;
            var root=new GameObject(exit?"CommonRoomReturnDirectory":"PrivateRoomDirectory");root.SetActive(false);allocated?.Add(root);
            root.layer=leaf.gameObject.layer;root.transform.SetParent(house.transform,false);
            directory=new RoomDirectory{root=root,house=house,wood=leaf.sharedMaterial,font=font,exit=exit,layer=root.layer};
            DirectoryCube(directory,root.transform,"WoodBacking",new Vector3(2f,exit?1.67f:1.65f,.615f),new Vector3(exit?1.3f:1.8f,exit?.7f:1.35f,.04f),false);
            DirectoryLabel(directory,root.transform,"RoomDirectoryTitle",new Vector3(2f,exit?1.87f:2.15f,.54f),exit?"返回公共区":"私人房间",new Vector2(exit?1.15f:1.65f,.24f),2f);
            return true;
        }
        GameObject DirectoryCube(RoomDirectory directory,Transform parent,string name,Vector3 position,Vector3 size,bool interaction)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=directory.layer;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=directory.wood;var collider=go.GetComponent<BoxCollider>();
            if(interaction)collider.isTrigger=true;else{collider.enabled=false;Destroy(collider);}
            return go;
        }
        TMPro.TMP_Text DirectoryLabel(RoomDirectory directory,Transform parent,string name,Vector3 position,string text,Vector2 size,float fontSize)
        {
            var go=new GameObject(name,typeof(TMPro.TextMeshPro));go.layer=directory.layer;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var label=go.GetComponent<TMPro.TextMeshPro>();label.font=directory.font;label.fontSize=fontSize;label.rectTransform.sizeDelta=size;
            label.alignment=TMPro.TextAlignmentOptions.Center;label.textWrappingMode=TMPro.TextWrappingModes.NoWrap;label.overflowMode=TMPro.TextOverflowModes.Ellipsis;
            label.enableAutoSizing=true;label.fontSizeMin=1f;label.fontSizeMax=fontSize;
            label.isOverlay=false;label.raycastTarget=false;label.color=new Color(.99f,.95f,.82f);label.text=text;return label;
        }
        GameObject CreateDirectoryEntry(RoomDirectory directory,int index,int count,string person,string homeId,Transform destination)
        {
            var slot=DirectorySlot(index,count,directory.exit);var approach=DirectoryApproach;
            var root=new GameObject(directory.exit?"ReturnToCommonRoom":"PrivateRoomDoor_"+person);root.layer=directory.layer;
            root.transform.SetParent(directory.root.transform,false);root.transform.localPosition=approach;
            // Keep a floor-level point for all existing NPC portal-path consumers.
            var portal=root.AddComponent<HousingRoomPortal>();portal.allowedPersonId=person;portal.homeId=homeId;portal.destination=destination;portal.leavingPrivateRoom=directory.exit;
            var tile=DirectoryCube(directory,root.transform,"RoomEntry",new Vector3(slot.x,slot.y,.57f)-approach,new Vector3(directory.exit?1.1f:.78f,.22f,.02f),true);
            portal.entryFace=tile.transform;
            string label=directory.exit?"公共区":(FriendsLifeRuntime.Instance?.GetPerson(person)?.displayName??"")+"的房间";
            DirectoryLabel(directory,root.transform,"RoomName",slot-approach,label,new Vector2(directory.exit?1.05f:.75f,.22f),1.8f);
            return root;
        }
        GameObject CreateDoor(HousingDoor source,Transform parent,Vector3 position,Quaternion rotation,string person,string homeId,Transform destination,bool exit,List<GameObject> allocated=null)
        {
            var authoredScale=source.transform.lossyScale;var parentScale=parent.lossyScale;
            // The imported door's local vertical axis is not local Y. Keep that
            // authored upright basis when placing the new portal at its yaw.
            var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
            if(forward.sqrMagnitude<.0001f)forward=Vector3.ProjectOnPlane(parent.forward,Vector3.up);
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
            var facing=Quaternion.LookRotation(forward.normalized,Vector3.up);
            var authoredUp=source.transform.InverseTransformDirection(Vector3.up).normalized;
            var clone=Instantiate(source.gameObject,parent);allocated?.Add(clone);clone.name=exit?"ReturnToCommonRoom":"PrivateRoomDoor_"+person;
            clone.transform.SetPositionAndRotation(position,facing*Quaternion.FromToRotation(authoredUp,Vector3.up));
            clone.transform.localScale=new Vector3(authoredScale.x/Mathf.Max(.00001f,Mathf.Abs(parentScale.x)),authoredScale.y/Mathf.Max(.00001f,Mathf.Abs(parentScale.y)),authoredScale.z/Mathf.Max(.00001f,Mathf.Abs(parentScale.z)));
            foreach(var old in clone.GetComponentsInChildren<HousingDoor>(true))Destroy(old);
            var portal=clone.AddComponent<HousingRoomPortal>();portal.allowedPersonId=person;portal.homeId=homeId;portal.destination=destination;portal.leavingPrivateRoom=exit;
            // Keep the original child collider/trigger configuration. The pivot
            // has no root Collider, but its leaf already has real mesh collision.
            if(!clone.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&!c.isTrigger&&(!(c is MeshCollider mesh)||mesh.sharedMesh!=null)))
            {
                var target=new GameObject("RoomDoorInteraction");SetWorldUnitChild(target,position);
                var collider=target.AddComponent<BoxCollider>();collider.center=Vector3.up;collider.size=new Vector3(1.1f,2.1f,.2f);
            }
            var sign=new GameObject("RoomName",typeof(TMPro.TextMeshPro));SetWorldUnitChild(sign,position+Vector3.up*2);
            var label=sign.GetComponent<TMPro.TextMeshPro>();label.font=HousingIsland.Instance.Catalog.font;label.fontSize=2;
            label.rectTransform.sizeDelta=new Vector2(2.6f,.4f);label.alignment=TMPro.TextAlignmentOptions.Center;
            label.enableWordWrapping=false;label.overflowMode=TMPro.TextOverflowModes.Ellipsis;label.isOverlay=false;label.raycastTarget=false;
            label.color=new Color(.22f,.17f,.10f);label.text=exit?"公共区":(FriendsLifeRuntime.Instance?.GetPerson(person)?.displayName??"")+"的房间";
            return clone;

            void SetWorldUnitChild(GameObject child,Vector3 worldPosition)
            {
                child.layer=clone.layer;child.transform.SetParent(clone.transform,false);
                child.transform.SetPositionAndRotation(worldPosition,facing);
                var scale=clone.transform.lossyScale;
                child.transform.localScale=new Vector3(1/Mathf.Max(.00001f,Mathf.Abs(scale.x)),1/Mathf.Max(.00001f,Mathf.Abs(scale.y)),1/Mathf.Max(.00001f,Mathf.Abs(scale.z)));
            }
        }
        public HousingPrivateRoom RoomFor(string personId)=>sets.Values.SelectMany(s=>s.rooms).FirstOrDefault(r=>r&&r.personId==personId);
        public bool PrepareHomeChange(string homeId)
        {
            bool safe=true;
            foreach(var session in FindObjectsByType<HousingRestSession>(FindObjectsSortMode.None))
                if(session.Active&&session.Target&&session.Target.GetComponentInParent<HousingHouse>()?.plot==homeId){session.RequestExit();safe=false;}
            foreach(var set in sets.Values.Where(s=>s.homeId==homeId))foreach(var room in set.rooms)
                if(room&&room.house.GetComponentsInChildren<HousingRestInteractable>(true).Any(r=>r.area&&r.area.IsOccupied))safe=false;
            var common=FriendsEconomyService.Instance?.Plot(homeId)?.house;
            if(common&&common.GetComponentsInChildren<HousingRestInteractable>(true).Any(r=>r.area&&r.area.IsOccupied))safe=false;
            return safe;
        }
        public bool HasAllPrivateRooms(string groupId)
        {
            var economy=FriendsEconomyService.Instance;var group=economy?.State.groups.FirstOrDefault(g=>g.groupId==groupId);
            return group!=null&&sets.TryGetValue(groupId,out var set)&&set.rooms.Count==group.members.Count&&set.rooms.All(r=>r&&r.arrival&&r.house.GetComponentsInChildren<HousingRestInteractable>(true).Any(b=>b.kind==HousingRestKind.Bed));
        }
        public bool EnterPrivateRoom(string personId)
        {
            var room=RoomFor(personId);var runtime=FriendsLifeRuntime.Instance;var actor=runtime?.ActorOf(personId);
            if(!room||!actor||actor.Brain==null||!FriendsEconomyService.Instance.CanEnter(room.house,personId))return false;
            var portal=FriendInstances<HousingRoomPortal>.Active().FirstOrDefault(p=>p.allowedPersonId==personId&&!p.leavingPrivateRoom&&p.destination&&p.destination.GetComponentInParent<HousingHouse>()==room.house);
            if(!portal||Vector3.Distance(actor.transform.position,portal.transform.position)>1.25f)return false;
            if(!FriendsPrivateRoomNavigation.Ensure(room,actor,out _))return false;
            var agent=actor.GetComponent<UnityEngine.AI.NavMeshAgent>();if(!agent)return false;
            var filter=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!UnityEngine.AI.NavMesh.SamplePosition(room.arrival.position,out var nav,.25f,filter)||Mathf.Abs(nav.position.y-room.arrival.position.y)>.15f)return false;
            actor.Brain.Movement.Stop();if(!actor.Brain.Movement.Warp(nav.position,Quaternion.Euler(0,room.arrival.eulerAngles.y,0)))return false;
            actor.Brain.Movement.MatchAgentPositionAtHeight(agent.nextPosition.y);
            return Vector3.Distance(actor.transform.position,nav.position)<=.15f&&Vector3.Distance(actor.transform.position,agent.nextPosition)<=.15f;
        }
        void Dispose(Set set)
        {
            var economy=FriendsEconomyService.Instance;var common=economy?.Plot(set.homeId)?.house;Transform safe=common?(common.exit?common.exit:common.entry):null;
            foreach(var room in set.rooms)if(room)
            {
                if(safe)
                {
                    foreach(var player in FindObjectsByType<PaperPixelGames.Core.Player.PPG_Player>(FindObjectsSortMode.None))
                        if(Vector3.Distance(player.transform.position,room.transform.position)<20&&!player.IsUsingEmoteArea)player.TeleportPlayer(safe.position,safe.rotation,true);
                    foreach(var person in FriendsLifeRuntime.Instance.People)
                    {var actor=FriendsLifeRuntime.Instance.ActorOf(person.personId);if(actor&&actor.Brain!=null&&Vector3.Distance(actor.transform.position,room.transform.position)<20)actor.Brain.Movement.Warp(safe.position,safe.rotation);}
                }
                if(HousingIsland.Instance)HousingIsland.Instance.DisposeOwnedProjection(room.house);else Destroy(room.gameObject);
            }
            foreach(var door in set.doors)if(door)Destroy(door);
        }
        void OnDestroy(){foreach(var set in sets.Values)Dispose(set);foreach(var set in preparedSets.Values)Dispose(set);sets.Clear();preparedSets.Clear();if(Instance==this)Instance=null;}
    }

    public sealed class HousingRoomPortal : MonoBehaviour,IBasicInteractable,IToolActiveInteractable
    {
        public string allowedPersonId,homeId;public Transform destination;public bool leavingPrivateRoom;
        [NonSerialized]public Transform entryFace;
        bool FromDirectoryFront(PaperPixelGames.Core.Player.PPG_Player player)=>!entryFace||player&&Vector3.Dot(player.transform.position-entryFace.position,-entryFace.forward)>.02f;
        void Awake()=>FriendInstances<HousingRoomPortal>.Add(this);
        void OnDestroy()=>FriendInstances<HousingRoomPortal>.Remove(this);
        public bool CanGaze=>destination&&(!entryFace||FromDirectoryFront(Singleton<PlayersManager>.Instance?.LocalPlayer));
        public bool CanInteractable=>destination;
        public void GazeEnter()=>Singleton<HintsManager>.Instance?.AddHint("Interact","key_interact");
        public void GazeExit()=>Singleton<HintsManager>.Instance?.RemoveHint("Interact");
        public void TriggerInput(Interactor interactor,InputKind kind,InputActionPhase phase)
        {
            if(kind!=InputKind.Interact||phase!=InputActionPhase.Canceled||!destination||!FromDirectoryFront(interactor?.Player))return;
            var economy=FriendsEconomyService.Instance;string person=economy?.PersonId(interactor);
            var room=leavingPrivateRoom?GetComponentInParent<HousingHouse>():destination.GetComponentInParent<HousingHouse>();
            if(!economy||!room||!economy.CanEnter(room,person))return;
            var player=interactor.Player;if(!player||player.IsUsingEmoteArea||interactor.IsUsing)return;
            player.TeleportPlayer(destination.position,destination.rotation,true);player.FirstPersonCharacter.SetVelocity(Vector3.zero);GazeExit();
        }
        public void TriggerCrossInput(Interactor interactor,BaseHoldable held,InputKind kind,InputActionPhase phase)=>TriggerInput(interactor,kind,phase);
    }
}
