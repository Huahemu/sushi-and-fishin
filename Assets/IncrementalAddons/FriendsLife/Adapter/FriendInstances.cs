using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Live instances of an added component, registered from its Awake and
    // removed in OnDestroy. Active() is the set FindObjectsByType<T>() returns
    // (components on active GameObjects) without the scene-wide search, which
    // costs 3-7 ms per call in the merged world.
    public static class FriendInstances<T> where T:Component
    {
        static readonly List<T> items=new List<T>();
        public static void Add(T item){if(item&&!items.Contains(item))items.Add(item);}
        public static void Remove(T item){items.Remove(item);}
        public static T[] Active(){items.RemoveAll(x=>!x);return items.Where(x=>x.gameObject.activeInHierarchy).ToArray();}
    }
}
