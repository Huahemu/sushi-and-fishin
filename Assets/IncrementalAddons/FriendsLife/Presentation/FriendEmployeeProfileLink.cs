using UnityEngine;
using UnityEngine.EventSystems;
namespace SushiAndFish.FriendsLife
{
    public sealed class FriendEmployeeProfileLink:MonoBehaviour,IPointerClickHandler
    {
        string personId;
        public static void Bind(GameObject target,string id)
        {if(!target)return;var link=target.GetComponent<FriendEmployeeProfileLink>();if(!link&&!string.IsNullOrEmpty(id))link=target.AddComponent<FriendEmployeeProfileLink>();if(link){link.personId=id;link.enabled=!string.IsNullOrEmpty(id);}}
        public void OnPointerClick(PointerEventData eventData)
        {if(eventData.button==PointerEventData.InputButton.Left&&!string.IsNullOrEmpty(personId))FriendTabletPresenter.Instance?.OpenPerson(personId);}
    }
}
