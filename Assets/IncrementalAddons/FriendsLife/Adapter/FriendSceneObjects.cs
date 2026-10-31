using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.ResidentialIsland;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // A scene-wide FindObjectsByType costs 3-7 ms per call in the merged world.
    // Tablets, their map app and the added tablet access all live on player
    // rigs, so a session reads them from the original player registry. Outside
    // a session the original scene search is kept.
    public static class FriendSceneObjects
    {
        static bool Registry(out PlayersManager players)
        {
            players=null;var network=NetworkManager.Singleton;
            return network&&network.IsListening&&Singleton<PlayersManager>.TryGetInstance(out players);
        }
        public static IEnumerable<TabletController> Tablets()
            =>Registry(out var players)?players.Players.Values.Where(p=>p&&p.TabletController).Select(p=>p.TabletController)
                :Object.FindObjectsByType<TabletController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        public static IEnumerable<HousingMap> Maps()
            =>Registry(out _)?Tablets().SelectMany(t=>t.GetComponentsInChildren<HousingMap>(true))
                :Object.FindObjectsByType<HousingMap>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        public static IEnumerable<FriendTabletAccess> TabletAccess()
            =>Registry(out _)?Tablets().Select(t=>t.GetComponent<FriendTabletAccess>()).Where(a=>a)
                :Object.FindObjectsByType<FriendTabletAccess>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    }
}
