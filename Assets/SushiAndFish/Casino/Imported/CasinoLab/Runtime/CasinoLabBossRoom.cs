namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Linq;
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

namespace CasinoLab
{
    public sealed class CasinoLabBossRoom:MonoBehaviour
    {
        public CasinoFloor floor;
        sealed class ItemPose
        {
            public Item item;public Transform parent;public Vector3 position,scale;public Quaternion rotation;public bool interactable;
        }
        ItemPose[] items;
        void Awake()
        {
            items=GetComponentsInChildren<Item>(true).Select(i=>new ItemPose{item=i,parent=i.transform.parent,position=i.transform.localPosition,rotation=i.transform.localRotation,scale=i.transform.localScale,interactable=i.IsInteractable}).ToArray();
        }
        public void EndVisit()
        {
            foreach(var pose in items)if(pose.item!=null && pose.item.NetworkHolder!=null)pose.item.ServerDrop();
            CasinoLabBootstrap.Instance.StartCoroutine(RestoreItemsAfterDrop());
        }
        IEnumerator RestoreItemsAfterDrop()
        {
            // Host RPCs may finish reparenting a dropped item on the next network tick.
            yield return null;yield return null;
            foreach(var pose in items)
            {
                var item=pose.item;if(item==null||pose.parent==null)continue;
                item.transform.SetParent(pose.parent,true);
                item.ServerTeleport(pose.parent.TransformPoint(pose.position));
                item.ServerRotate(pose.parent.rotation*pose.rotation);
                item.transform.localScale=pose.scale;item.IsInteractable=pose.interactable;
            }
        }
        public void PrepareVisit()
        {
            EndVisit();
        }
    }
}

}
