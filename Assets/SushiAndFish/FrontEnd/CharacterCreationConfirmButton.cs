using Cysharp.Threading.Tasks;
using PaperPixelGames.Framework.UI;
using UnityEngine;
using TMPro;

namespace SushiAndFish.FrontEnd
{
    [RequireComponent(typeof(BaseButton))]
    public sealed class CharacterCreationConfirmButton : MonoBehaviour
    {
        private BaseButton button;
        public static void ConfigureLayout(BaseButton target)
        {
            var rect=(RectTransform)target.transform;
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;
            rect.anchoredPosition=new Vector2(30,35);rect.sizeDelta=new Vector2(390,90);rect.localScale=Vector3.one;
            var hit=target.GetComponent<UnityEngine.UI.Image>();
            if(!hit)hit=target.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color=Color.clear;hit.raycastTarget=true;
            foreach(var label in target.GetComponentsInChildren<TMP_Text>(true))
            {label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=44;}
        }
        private void Awake(){button=GetComponent<BaseButton>();ConfigureLayout(button);}
        private void OnEnable()
        {
            if(!button)button=GetComponent<BaseButton>();
            button.Clicked+=Confirm;RefreshLabel();
        }
        private void OnDisable(){if(button)button.Clicked-=Confirm;}
        private void Update()
        {
            var flow=IslandFrontEndFlow.Active;
            if(button)button.interactable=flow && flow.IsCreatingCharacter && !flow.IsBusy;
        }
        public void RefreshLabel(){if(button)button.SetText(IslandFrontEndFlow.Label("确认并开始游戏","CONFIRM & START"));}
        private void Confirm(){if(IslandFrontEndFlow.Active)IslandFrontEndFlow.Active.ConfirmNewCharacterAsync().Forget();}
    }
}
