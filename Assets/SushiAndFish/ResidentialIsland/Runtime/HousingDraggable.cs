using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingDraggable : MonoBehaviour,IBeginDragHandler,IDragHandler,IPointerDownHandler
    {
        RectTransform rect,parent;
        Vector2 start,original;
        bool allowed;
        public void OnPointerDown(PointerEventData e){transform.SetAsLastSibling();}
        public void OnBeginDrag(PointerEventData e)
        {
            allowed=e.button==PointerEventData.InputButton.Left && e.pointerPressRaycast.gameObject.GetComponentInParent<Selectable>()==null;
            if(!allowed)return;rect=(RectTransform)transform;parent=(RectTransform)rect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out start);original=rect.anchoredPosition;e.Use();
        }
        public void OnDrag(PointerEventData e)
        {
            if(!allowed)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out var p);
            var next=original+p-start;var half=rect.rect.size/2;
            next.x=Mathf.Clamp(next.x,parent.rect.xMin+half.x,parent.rect.xMax-half.x);
            next.y=Mathf.Clamp(next.y,parent.rect.yMin+half.y,parent.rect.yMax-half.y);
            rect.anchoredPosition=next;e.Use();
        }
    }
}
