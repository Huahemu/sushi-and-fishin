using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class UiReturnFrame
    {
        public string app="Friends",page,personId,tab,selectedRelation,focusName,purpose;
        public string entityId;
        public FriendMapViewState mapView;
        public float scroll=1;public Vector3 mapPosition;public float mapZoom;
        [NonSerialized] public GameObject focus;
        [NonSerialized] public UiReturnFrame[] pageHistory;
    }
    public sealed class TabletNavigationStack
    {
        readonly Stack<UiReturnFrame> visits=new Stack<UiReturnFrame>();
        public bool HasVisit=>visits.Count>0;
        public int Count=>visits.Count;
        static string Entity(UiReturnFrame frame)=>frame.entityId??frame.personId??"";
        static bool Matches(UiReturnFrame frame,string app,string page,string entity)=>frame.app==app&&frame.page==page&&Entity(frame)==(entity??"");
        public bool TryReturnTo(string app,string page,string entity,out UiReturnFrame frame)
        {
            frame=null;foreach(var candidate in visits)if(Matches(candidate,app,page,entity)){frame=candidate;break;}
            if(frame==null)return false;while(visits.Count>0){var popped=visits.Pop();if(ReferenceEquals(popped,frame))break;}return true;
        }
        public void PushVisit(UiReturnFrame frame)
        {
            if(frame==null)throw new ArgumentNullException(nameof(frame));UiReturnFrame old;TryReturnTo(frame.app,frame.page,Entity(frame),out old);visits.Push(frame);
        }
        public UiReturnFrame Back()=>visits.Count>0?visits.Pop():null;
        public void CloseTablet()=>visits.Clear();
        public void OpenNormalApp()=>visits.Clear();
        public static void RestoreFocus(UiReturnFrame frame,Transform root)
        {
            if(EventSystem.current==null||frame==null)return;var target=frame.focus;
            if(!target&&!string.IsNullOrEmpty(frame.focusName))foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==frame.focusName){target=t.gameObject;break;}
            if(target&&target.activeInHierarchy)EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
