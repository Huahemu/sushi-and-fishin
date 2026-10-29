using System.Collections;
using UnityEngine;
using PaperPixelGames.Core.Player;

namespace SushiAndFish.FishingIntegration
{
    public sealed partial class ArchipelagoWorld
    {
        Coroutine recalling;float recallAfter;
        internal void Recall(FishingActor requester)
        {
            if(!Server||!Boat||requester==null||requester.IsDefeated)return;
            if(recalling!=null||Time.unscaledTime<recallAfter){world.Notify(requester.player.OwnerClientId,"船只召回中，请稍候");return;}
            foreach(var other in world.actors.Values)if(other.player&&other!=requester&&OnDeck(other.player)){world.Notify(requester.player.OwnerClientId,"船上还有其他玩家，暂时无法召回");return;}
            world.Notify(requester.player.OwnerClientId,"正在寻找附近可停船的海面…");recalling=StartCoroutine(FindAndRecall(requester));
        }
        IEnumerator FindAndRecall(FishingActor requester)
        {
            // Search outward from the player's position. A per-frame query
            // budget avoids a single large burst of shoreline collision tests.
            yield return null;int budget=0;var origin=requester.player.transform.position;float yaw=requester.player.transform.eulerAngles.y;
            for(float radius=8;radius<=320;radius+=4)for(int i=0;i<48;i++)
            {
                if(!Server||!Boat||!requester.player||!requester.player.IsSpawned||requester.IsDefeated){recalling=null;yield break;}
                if(++budget>=24){budget=0;yield return null;}
                float angle=(yaw+i*7.5f)*Mathf.Deg2Rad;
                var p=origin+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);p.y=SeaHeight+.25f;
                if(!world.IsWater(p-Vector3.up*.25f))continue;
                if(!ArchipelagoTravelArea.Contains(Settings,p,SeaHeight)&&Vector3.ProjectOnPlane(p-Settings.regions[0].center,Vector3.up).sqrMagnitude>250*250)continue;
                foreach(float turn in new[]{yaw,yaw+90})
                {
                    var q=Quaternion.Euler(0,turn,0);if(!ArchipelagoBoatSafety.ClearWater(p,q,SeaHeight,Boat.transform))continue;
                    // Recheck occupancy at completion, after asynchronous search.
                    bool occupied=false;foreach(var other in world.actors.Values)if(other!=requester&&other.player&&OnDeck(other.player))occupied=true;
                    if(occupied){world.Notify(requester.player.OwnerClientId,"船上有其他玩家，已取消召回");recalling=null;yield break;}
                    Boat.SetDriver(ulong.MaxValue);RelocateBoat(p,q);world.SaveWorld();recallAfter=Time.unscaledTime+3;recalling=null;
                    world.Notify(requester.player.OwnerClientId,"船已召回到附近海面 · 约 "+Mathf.RoundToInt(Vector3.Distance(origin,p))+" 米");yield break;
                }
            }
            recalling=null;world.Notify(requester.player.OwnerClientId,"附近暂时没有能容纳整艘船的海面，请靠近海岸后再召回");
        }
    }
}
