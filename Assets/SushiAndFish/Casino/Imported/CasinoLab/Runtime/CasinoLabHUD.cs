namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::TMPro;
using global::UnityEngine.InputSystem;

namespace CasinoLab
{
    public sealed class CasinoLabHUD:MonoBehaviour
    {
        static CasinoLabHUD instance;
        GameObject panel;
        TextMeshProUGUI status;
        bool visible;
        public static void Toggle() { if(instance!=null)instance.SetVisible(!instance.visible); }
        void Start()
        {
            instance=this;
            var root=new GameObject("CasinoLabTestHUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            status=Text(root.transform,"UNLIMITED TEST FUNDS | F8: Test controls",new Vector2(640,45),new Vector2(18,-15),22);
            panel=new GameObject("TestControls",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);rect.sizeDelta=new Vector2(1000,710);
            panel.GetComponent<UnityEngine.UI.Image>().color=new Color(0.025f,0.03f,0.045f,0.96f);
            Text(panel.transform,"CASINO TEST CONTROLS",new Vector2(520,50),new Vector2(20,-20),28);
            Button("Return to building entrance",-85,()=>{CasinoLabBootstrap.Instance.ReturnToEntrance();SetVisible(false);},25,460);
            Button("Reset casino / next seed",-85,()=>{CasinoLabBootstrap.Instance.ResetCasino();SetVisible(false);},515,460);
            Button("Casino boss office",-150,()=>{CasinoLabBootstrap.Instance.OpenBossRoom();SetVisible(false);},25,460);
            Button("Resume",-150,()=>SetVisible(false),515,460);
            Text(panel.transform,"Choose an original cluster for the floor 4 test position",new Vector2(950,50),new Vector2(25,-220),24);
            var names=CasinoLabSelection.Names;
            for(int i=0;i<names.Length;i++)
            {
                int selected=i;
                Button(names[i],-280-(i/4)*80,()=>{CasinoLabSelection.Select(selected);SetVisible(false);},25+(i%4)*242,225);
            }
            Text(panel.transform,"Original floor layouts regenerate with Reset. F8 closes this panel.",new Vector2(950,50),new Vector2(25,-630),22);
            panel.SetActive(false);
        }
        TextMeshProUGUI Text(Transform parent,string value,Vector2 size,Vector2 pos,int fontSize)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.sizeDelta=size;r.anchoredPosition=pos;
            var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;return text;
        }
        void Button(string label,float y,UnityEngine.Events.UnityAction action,float x=25,float width=510)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(panel.transform,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,55);
            go.GetComponent<UnityEngine.UI.Image>().color=new Color(0.12f,0.16f,0.22f,1);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(action);Text(go.transform,label,new Vector2(width-20,45),new Vector2(12,-7),22);
        }
        void SetVisible(bool value)
        {
            visible=value;panel.SetActive(value);Cursor.visible=value;Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;
            if(Extensions.MonoSingleton<LocalManager>.Instance!=null)
                foreach(var p in Extensions.MonoSingleton<LocalManager>.Instance.players)p.controller.IsLocked=value;
        }
        void Update()
        {
            if(status!=null && CasinoLabBootstrap.Instance!=null)status.text="UNLIMITED TEST FUNDS | F8: Test controls\n"+CasinoLabBootstrap.Instance.status;
        }
    }
}

}
