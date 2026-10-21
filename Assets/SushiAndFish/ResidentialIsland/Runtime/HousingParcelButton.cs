using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingParcelButton : Button
    {
        public Action<bool> hover;
        public Action click;
        public override void OnPointerEnter(PointerEventData e){base.OnPointerEnter(e);hover?.Invoke(true);}
        public override void OnPointerExit(PointerEventData e){base.OnPointerExit(e);hover?.Invoke(false);}
        public override void OnPointerClick(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;base.OnPointerClick(e);click?.Invoke();e.Use();}
    }
}
