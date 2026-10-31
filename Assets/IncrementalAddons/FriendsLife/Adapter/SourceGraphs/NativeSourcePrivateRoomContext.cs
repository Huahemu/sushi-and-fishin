using System;
using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // The host's real remote private interior is a declared adapter for native
    // house submaps1..8. It supplies map kind/owner, never native XZ coordinates.
    public static class NativeSourcePrivateRoomContext
    {
        public const string Source="main003ef214 private branch:+1b enabled,+1c mine;003ef910 accepts house submaps1..8;01d49840 checks target in focus member room";
        public sealed class Observation
        {
            public HousingPrivateRoom room;
            public string actorPersonId,ownerPersonId,homeId;
            public int nativeRoomType;
            public bool OwnRoom=>actorPersonId==ownerPersonId;
        }
        static int roomFrame=int.MinValue;
        static HousingPrivateRoom[] rooms=Array.Empty<HousingPrivateRoom>();
        static HousingPrivateRoom[] CurrentRooms()
        {
            if(roomFrame!=Time.frameCount){roomFrame=Time.frameCount;rooms=FriendInstances<HousingPrivateRoom>.Active();}
            return rooms;
        }
        static bool Contains(HousingPrivateRoom room,Vector3 position)
        {
            if(!room||!room.house||room.house.definition==null||!room.gameObject.activeInHierarchy)return false;
            var size=room.house.definition.footprint;if(size.x<=0||size.y<=0)return false;
            // Same installed house-local volume convention as the host's
            // HousingNpcExclusion, without its exterior safety margin.
            var volume=new Bounds(new Vector3(0,2.5f,0),new Vector3(size.x,11,size.y));
            return volume.Contains(room.house.transform.InverseTransformPoint(position));
        }
        public static Observation Observe(FriendsLifeRuntime runtime,string nativeResidentId)
        {
            if(!runtime||!runtime.Ready)return null;var person=runtime.PersonFromNative(nativeResidentId);if(person==null)return null;
            var body=runtime.TransformOf(person.personId);var economy=FriendsEconomyService.Instance;if(!body||!economy)return null;
            Observation result=null;
            foreach(var room in CurrentRooms())
            {
                if(!Contains(room,body.position))continue;
                var group=economy.State.groups.FirstOrDefault(g=>g.groupId==room.groupId&&g.homeId==room.homeId);if(group==null||group.members==null)continue;
                int index=group.members.IndexOf(room.personId);if(index<0||index>=8||FriendHouseMetadata.For(room.house).privatePersonId!=room.personId)continue;
                // More than one declared private map around one body is not a
                // unique original map owner, so leave that binding unresolved.
                if(result!=null)return null;
                result=new Observation{room=room,actorPersonId=person.personId,ownerPersonId=room.personId,homeId=room.homeId,nativeRoomType=index+1};
            }
            return result;
        }
        public static bool MatchPrivateRoom(NativeMiiEventPlace rule,bool ownsRoom)
            =>rule!=null&&rule.IsHousePrivateRoom&&(!rule.IsHousePrivateRoomMine||ownsRoom);
        public static bool? Resolve(FriendsLifeRuntime runtime,LifeSimulationRuntime kernel,NativeMiiEventQuery query)
        {
            if(!runtime||query==null||kernel?.NativeEventEngine==null)return null;
            if(query.kind=="Place")
            {
                var actual=Observe(runtime,query.actorId);if(actual==null)return null;
                var rule=kernel.NativeEventEngine.Database.Data.places.FirstOrDefault(p=>p.id==query.id);return rule==null?(bool?)null:MatchPrivateRoom(rule,actual.OwnRoom);
            }
            if(query.kind=="PrivateRoom")
            {
                // Native01d49840 tests the target's actual house/member room
                // against the focus's home/member identity, not vice versa.
                var focus=runtime.PersonFromNative(query.actorId);var target=Observe(runtime,query.otherId);
                if(focus==null||target==null)return null;
                return target.homeId==focus.currentHomeId&&target.ownerPersonId==focus.personId;
            }
            return null;
        }
    }
}
