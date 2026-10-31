using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable]
    public sealed class HomeStyleProjectionSnapshot
    {
        public string personId, homeId, homeInstanceId, styleId, floorMaterial, wallMaterial, failure;
        public int floorRenderers, wallRenderers;
        public bool projected;
    }

    // Maps the original committed room ID to the existing host art templates.
    // Price, ownership, mood, request matching and style selection stay in the
    // original BuyCreativeRoom method; polling never invokes that method.
    public sealed class FriendsHomeStyleProjection : MonoBehaviour
    {
        sealed class Binding
        {
            public HousingHouse house;
            public Material floor, wall;
            public Renderer[] floors, walls;
        }
        public static FriendsHomeStyleProjection Instance {get;private set;}
        readonly Dictionary<string,HomeStyleProjectionSnapshot> observations=new Dictionary<string,HomeStyleProjectionSnapshot>(StringComparer.Ordinal);
        float next;
        void Awake(){Instance=this;}
        static bool Floor(Renderer renderer)=>renderer.name.IndexOf("_Floorboard_",StringComparison.Ordinal)>=0||renderer.name.EndsWith("BathTileFloor",StringComparison.Ordinal);
        static bool Wall(Renderer renderer)=>renderer.name.EndsWith("InteriorWallsConnected",StringComparison.Ordinal);
        static Material HostMaterial(string name)=>Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m&&(m.name=="AR_GuestHome_"+name||m.name=="AR_PlayerHome_"+name));
        static bool TryPrepare(string personId,string styleId,string ownerEpisode,out Binding binding,out string failure)
        {
            binding=null;failure=null;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||!economy||runtime.Core.CreativeCatalog.Room(styleId)==null)
            {failure="The exact source room style is unavailable";return false;}
            var person=runtime.GetPerson(personId);if(person==null){failure="The person is not bound";return false;}
            if(!string.IsNullOrEmpty(ownerEpisode)&&runtime.State.leases.Any(l=>l.personId==personId&&l.ownerEpisode!=ownerEpisode))
            {failure="Another source owner controls this person";return false;}
            var room=FriendsHomeInteriors.Instance?.RoomFor(personId);
            var house=room?room.house:economy.MembersOf(person.currentHomeId).Count()<=1?economy.Plot(person.currentHomeId)?.house:null;
            if(!house||!house.gameObject.activeInHierarchy||house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone!=true||!economy.CanEnter(house,personId))
            {failure="The actual owned room or private interior is not ready";return false;}
            if(!FriendsHostVisualBindings.HasRoomTheme(styleId))
            {failure="The source room has no registered host material mapping: "+styleId;return false;}
            // Loading an existing host prefab makes its shared art materials
            // available without instantiating or editing any baseline asset.
            Resources.Load<GameObject>("Housing/AR_PlayerHome");Resources.Load<GameObject>("Housing/AR_GuestHome");
            var names=FriendsHostVisualBindings.RoomMaterials(styleId);var floor=HostMaterial(names[0]);var wall=HostMaterial(names[1]);
            if(!floor||!wall){failure="The registered host floor or wall material is not loaded";return false;}
            var renderers=house.GetComponentsInChildren<Renderer>(true);var floors=renderers.Where(Floor).ToArray();var walls=renderers.Where(Wall).ToArray();
            if(floors.Length==0||walls.Length==0){failure="The actual host room has no bound floor and wall surfaces";return false;}
            binding=new Binding{house=house,floor=floor,wall=wall,floors=floors,walls=walls};return true;
        }
        public bool CanApplySourceRoomStyle(string personId,string styleId,string ownerEpisode)
        {return TryPrepare(personId,styleId,ownerEpisode,out _,out _);}
        public string PreparationFailure(string personId,string styleId,string ownerEpisode)
        {TryPrepare(personId,styleId,ownerEpisode,out _,out var failure);return failure;}
        public HomeStyleProjectionSnapshot Observe(string personId)
        {
            if(!observations.TryGetValue(personId??"",out var s))return null;
            return new HomeStyleProjectionSnapshot{personId=s.personId,homeId=s.homeId,homeInstanceId=s.homeInstanceId,styleId=s.styleId,
                floorMaterial=s.floorMaterial,wallMaterial=s.wallMaterial,floorRenderers=s.floorRenderers,wallRenderers=s.wallRenderers,projected=s.projected,failure=s.failure};
        }
        public bool IsSourceRoomStyleProjected(string personId,string styleId,string ownerEpisode)
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);
            var creative=person==null?null:runtime.Core.State.creative?.residents.FirstOrDefault(p=>p.residentId==person.nativeResidentId);
            if(creative==null||creative.roomStyleId!=styleId||!string.IsNullOrEmpty(creative.roomDesignId)||
                !TryPrepare(personId,styleId,ownerEpisode,out var binding,out _))return false;
            return MaterialsMatch(binding.floors,binding.floor)&&MaterialsMatch(binding.walls,binding.wall);
        }
        static bool MaterialsMatch(IEnumerable<Renderer> renderers,Material expected)
        {
            return renderers.All(renderer=>
            {
                if(!renderer||renderer.sharedMaterials.Length==0||renderer.sharedMaterials.Any(m=>m!=expected))return false;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                var baseMap=block.GetTexture("_BaseMap");var main=block.GetTexture("_MainTex");
                return (!baseMap||baseMap==expected.mainTexture)&&(!main||main==expected.mainTexture);
            });
        }
        void Update()
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            foreach(var person in runtime.People)
            {
                var creative=runtime.Core.State.creative?.residents.FirstOrDefault(p=>p.residentId==person.nativeResidentId);
                if(creative==null||string.IsNullOrEmpty(creative.roomStyleId)||!string.IsNullOrEmpty(creative.roomDesignId))continue;
                if(!TryPrepare(person.personId,creative.roomStyleId,null,out var binding,out var failure))
                {observations[person.personId]=new HomeStyleProjectionSnapshot{personId=person.personId,styleId=creative.roomStyleId,failure=failure};continue;}
                if(!MaterialsMatch(binding.floors,binding.floor)||!MaterialsMatch(binding.walls,binding.wall))
                {Apply(binding.floors,binding.floor);Apply(binding.walls,binding.wall);}
                observations[person.personId]=new HomeStyleProjectionSnapshot{personId=person.personId,homeId=binding.house.plot,
                    homeInstanceId=FriendHousingIsland.HomeIdentity(binding.house),styleId=creative.roomStyleId,floorMaterial=binding.floor.name,wallMaterial=binding.wall.name,
                    floorRenderers=binding.floors.Length,wallRenderers=binding.walls.Length,projected=MaterialsMatch(binding.floors,binding.floor)&&MaterialsMatch(binding.walls,binding.wall)};
            }
            foreach(var id in observations.Keys.Where(id=>runtime.GetPerson(id)==null).ToArray())observations.Remove(id);
        }
        static void Apply(IEnumerable<Renderer> renderers,Material material)
        {
            foreach(var renderer in renderers)
            {
                renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetTexture("_BaseMap",material.mainTexture);block.SetTexture("_MainTex",material.mainTexture);renderer.SetPropertyBlock(block);
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;observations.Clear();}
    }
    public sealed partial class FriendsHomeInteriors
    {
        public bool CanApplySourceRoomStyle(string personId,string styleId,string ownerEpisode)
            =>FriendsHomeStyleProjection.Instance&&FriendsHomeStyleProjection.Instance.CanApplySourceRoomStyle(personId,styleId,ownerEpisode);
        public bool IsSourceRoomStyleProjected(string personId,string styleId,string ownerEpisode)
            =>FriendsHomeStyleProjection.Instance&&FriendsHomeStyleProjection.Instance.IsSourceRoomStyleProjected(personId,styleId,ownerEpisode);
    }
}
