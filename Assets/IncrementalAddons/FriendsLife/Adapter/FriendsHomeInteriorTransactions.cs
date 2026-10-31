using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsHomeInteriors
    {
        sealed class InteriorSwitch
        {
            public Set incoming;
            public readonly List<KeyValuePair<string,Set>> previous=new List<KeyValuePair<string,Set>>();
            public readonly List<(GameObject go,bool active)> activation=new List<(GameObject,bool)>();
        }
        readonly Dictionary<string,InteriorSwitch> pendingSwitches=new Dictionary<string,InteriorSwitch>();
        public HousingHouse[] TransactionRooms(IEnumerable<string> groupIds)
        {
            var ids=new HashSet<string>(groupIds??Enumerable.Empty<string>(),StringComparer.Ordinal);
            return sets.Where(p=>ids.Contains(p.Key)).SelectMany(p=>p.Value.rooms).Where(r=>r&&r.house).Select(r=>r.house).ToArray();
        }
        public bool CanPrepareHomeChange(string homeId,out string failure)
        {
            failure=null;
            var homes=sets.Values.Where(s=>s.homeId==homeId).SelectMany(s=>s.rooms).Where(r=>r&&r.house).Select(r=>r.house).ToList();
            var common=FriendsEconomyService.Instance?.Plot(homeId)?.house;if(common)homes.Add(common);
            if(FindObjectsByType<HousingRestSession>(FindObjectsSortMode.None).Any(s=>s.Active&&s.Target&&homes.Contains(s.Target.GetComponentInParent<HousingHouse>()))||
               homes.Any(h=>h.GetComponentsInChildren<FriendsHomeFurnitureClaim>(true).Any(c=>c.Active)||h.GetComponentsInChildren<HousingRestInteractable>(true).Any(r=>r.area&&r.area.IsOccupied||!string.IsNullOrEmpty(r.GetComponent<FriendRestOccupancy>()?.friendOccupantPersonId))||h.spawnedSeats.Any(go=>go&&go.GetComponent<EmoteArea>()?.IsOccupied==true)))
            {failure="请等待住户起身后再调整住宅";return false;}
            return true;
        }
        public bool ActivatePreparedChange(string token,IEnumerable<string> oldGroupIds,out string failure)
        {
            failure=null;if(pendingSwitches.ContainsKey(token)){failure="正在处理……";return false;}
            preparedSets.TryGetValue(token,out var incoming);var ids=new HashSet<string>(oldGroupIds??Enumerable.Empty<string>(),StringComparer.Ordinal);
            var change=new InteriorSwitch{incoming=incoming};
            var alreadyClaimed=new HashSet<string>(pendingSwitches.Values.SelectMany(s=>s.previous).Select(p=>p.Key),StringComparer.Ordinal);
            foreach(var pair in sets.Where(p=>!alreadyClaimed.Contains(p.Key)&&(ids.Contains(p.Key)||incoming!=null&&(p.Key==incoming.groupId||p.Value.rooms.Any(r=>r&&incoming.rooms.Any(n=>n&&n.personId==r.personId))))).ToArray())change.previous.Add(pair);
            pendingSwitches[token]=change;
            try
            {
                void Hide(GameObject go){if(!go)return;change.activation.Add((go,go.activeSelf));go.SetActive(false);}
                foreach(var pair in change.previous)
                {
                    foreach(var room in pair.Value.rooms)if(room){Hide(room.gameObject);foreach(var seat in room.house.spawnedSeats)Hide(seat);}
                    foreach(var door in pair.Value.doors)Hide(door);
                }
                if(incoming!=null)
                {
                    foreach(var room in incoming.rooms)if(room){room.transform.position-=incoming.stagingOffset;FriendHousingIsland.Instance.ActivatePrivateInterior(room.house);}
                    incoming.stagingOffset=Vector3.zero;foreach(var door in incoming.doors)if(door)door.SetActive(true);
                }
                return true;
            }
            catch(Exception exception){Debug.LogWarning("[FriendsLife] Home interior switch failed: "+exception);failure="住宅操作失败，已恢复原样。请重试。";RollbackPreparedChange(token);return false;}
        }
        public void RollbackPreparedChange(string token)
        {
            if(pendingSwitches.TryGetValue(token,out var change))
            {
                pendingSwitches.Remove(token);foreach(var item in change.activation)if(item.go)item.go.SetActive(item.active);
            }
            CancelPreparedGroup(token);
        }
        public void FinalizePreparedChange(string token)
        {
            if(!pendingSwitches.TryGetValue(token,out var change))return;pendingSwitches.Remove(token);preparedSets.Remove(token);
            foreach(var old in change.previous){sets.Remove(old.Key);Dispose(old.Value);}
            if(change.incoming!=null)sets[change.incoming.groupId]=change.incoming;
        }
    }
}
