using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Framework.UI;
using SushiAndFish.FrontEnd;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    public static class FriendCharacterCreatorSession
    {
        static bool busy,closing,ready;static int sessionVersion;static Scene previous,scene;static CharacterCreator creator;static Action<CharacterData> confirm;static Action cancel;
        static readonly List<Camera> cameras=new List<Camera>();static readonly List<Canvas> overlays=new List<Canvas>();static GameObject[] roots;
        static readonly List<GraphicRaycaster> externalRaycasters=new List<GraphicRaycaster>();
        static FriendCharacterCreatorBridge bridge;static bool ownsScene;static FriendCreatorNetworkSceneScope networkSceneScope;
        static BaseButton confirmation;static CharacterCreationConfirmButton confirmationFlow;
        static GameObject actionButtons,nativeBack;static bool nativeBackActive;
        static bool confirmationActive,confirmationInteractable,confirmationFlowEnabled;
        static readonly Dictionary<TMP_Text,string> confirmationLabels=new Dictionary<TMP_Text,string>();
        static readonly List<Behaviour> confirmationLocalizers=new List<Behaviour>();
        static bool[] rootActive;static bool cursorVisible;static CursorLockMode cursorLock;
        public static async Task Open(CharacterData data,Action<CharacterData> onConfirmed,Action onCancelled)
        {
            if(busy)throw new InvalidOperationException("外貌编辑器正在使用");busy=true;ready=false;int version=++sessionVersion;confirm=onConfirmed;cancel=onCancelled;previous=SceneManager.GetActiveScene();cursorVisible=Cursor.visible;cursorLock=Cursor.lockState;
            try
            {
                scene=default(Scene);ownsScene=false;
                networkSceneScope=new FriendCreatorNetworkSceneScope("StylizedCharacterCreator");
                var previousHandles=new HashSet<int>();for(int i=0;i<SceneManager.sceneCount;i++)previousHandles.Add(SceneManager.GetSceneAt(i).handle);
                var operation=SceneManager.LoadSceneAsync("StylizedCharacterCreator",LoadSceneMode.Additive);if(operation==null)throw new InvalidOperationException("无法打开外貌编辑器");while(!operation.isDone)await Task.Yield();
                scene=default(Scene);for(int i=0;i<SceneManager.sceneCount;i++){var candidate=SceneManager.GetSceneAt(i);if(candidate.name=="StylizedCharacterCreator"&&!previousHandles.Contains(candidate.handle)){scene=candidate;break;}}
                if(!scene.IsValid())throw new InvalidOperationException("Owned friend editor scene was not created");ownsScene=true;networkSceneScope.Claim(scene);bool loadedHere=true;
                if(!IsActive(version))return;
                roots=scene.GetRootGameObjects();rootActive=roots.Select(root=>!loadedHere&&root.activeSelf).ToArray();foreach(var root in roots)root.SetActive(true);
                creator=roots.SelectMany(r=>r.GetComponentsInChildren<CharacterCreator>(true)).FirstOrDefault();if(!creator)throw new InvalidOperationException("外貌编辑器缺失");
                creator.BackRequested+=OnCancelled;
                foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(camera.enabled&&camera.gameObject.scene!=scene){cameras.Add(camera);camera.enabled=false;}
                // Disabling a gameplay camera does not hide its Overlay HUD.
                // Preserve the exact enabled set for this modal editor session.
                foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if(canvas.enabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay&&canvas.gameObject.scene!=scene)
                    {overlays.Add(canvas);canvas.enabled=false;}
                // WorldSpace tablet raycasters also remain active when their
                // event camera is disabled. Only this editor owns UI input.
                foreach(var raycaster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
                    if(raycaster.enabled&&raycaster.gameObject.scene!=scene){externalRaycasters.Add(raycaster);raycaster.enabled=false;}
                // Keep the real world active: NGO initial synchronization reads
                // Unity's active scene even before its public scene filter.
                for(int frame=0;IsActive(version)&&frame<1200&&(!creator.character||!creator.character.initalized||creator.character.isloading);frame++)await Task.Yield();
                if(!IsActive(version))return;
                if(!creator.character||!creator.character.initalized||creator.character.isloading)throw new InvalidOperationException("角色外貌尚未载入");
                if(data==null){var preset=Resources.Load<CharacterObject>("Default");if(!preset)throw new InvalidOperationException("外貌编辑无法打开");data=preset.GetCharacterData();}
                bridge=FriendCharacterCreatorBridge.CreateForOwnedInstance(creator);await bridge.Apply(data);bridge.BeginObserving();
                if(!IsActive(version))return;
                BindConfirmation();
                Cursor.visible=true;Cursor.lockState=CursorLockMode.None;ready=true;
            }
            catch{if(IsActive(version)){await Close();throw;}}
        }
        static void BindConfirmation()
        {
            var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CharacterCreationConfirmButton>(true)).FirstOrDefault();
            var button=flow?flow.GetComponent<BaseButton>():null;
            var back=button?button.transform.parent.Find("BackButton"):null;
            if(!button||!back)throw new InvalidOperationException("外貌编辑器确认或返回按钮缺失");
            confirmationFlow=flow;confirmation=button;nativeBack=back.gameObject;nativeBackActive=nativeBack.activeSelf;
            confirmationActive=confirmation.gameObject.activeSelf;confirmationInteractable=confirmation.interactable;confirmationFlowEnabled=confirmationFlow.enabled;
            foreach(var label in confirmation.GetComponentsInChildren<TMP_Text>(true))confirmationLabels[label]=label.text;
            var source=(RectTransform)confirmation.transform;float width=source.rect.width,height=source.rect.height;
            // Both friend actions occupy only the old Start button's region.
            // Keep its actual anchors/position so the native bottom tools stay clear.
            var row=FriendUi.Rect("FriendCreatorActions",source.parent,0,0,width,height);
            row.anchorMin=source.anchorMin;row.anchorMax=source.anchorMax;row.pivot=source.pivot;row.anchoredPosition=source.anchoredPosition;
            row.sizeDelta=source.sizeDelta;row.localRotation=source.localRotation;row.localScale=source.localScale;row.SetSiblingIndex(source.GetSiblingIndex()+1);
            actionButtons=row.gameObject;networkSceneScope?.MoveIfUnparented(actionButtons);actionButtons.SetActive(false);
            float gap=Mathf.Min(20,width*.06f),padding=Mathf.Min(10,height*.12f),buttonWidth=(width-gap)*.5f;
            FriendUi.Button("ConfirmFriend",row,0,padding,buttonWidth,height-padding*2,"完成",ConfirmCurrent,true);
            FriendUi.Button("CancelFriend",row,buttonWidth+gap,padding,buttonWidth,height-padding*2,"返回",OnCancelled);
            // Normal New Game controls remain untouched outside this session.
            confirmationFlow.enabled=false;
            foreach(var component in confirmation.GetComponentsInChildren<Behaviour>(true))
                if(component.enabled&&component.GetType().Name=="LocalizeStringEvent"){confirmationLocalizers.Add(component);component.enabled=false;}
            confirmation.gameObject.SetActive(false);nativeBack.SetActive(false);actionButtons.SetActive(true);
        }
        static void ConfirmCurrent(){if(busy&&ready&&!closing&&creator)OnConfirmed(bridge.Export());}
        static void RestoreConfirmation()
        {
            if(actionButtons){actionButtons.SetActive(false);UnityEngine.Object.Destroy(actionButtons);}actionButtons=null;
            if(confirmation){confirmation.interactable=confirmationInteractable;confirmation.gameObject.SetActive(confirmationActive);}
            foreach(var pair in confirmationLabels)if(pair.Key)pair.Key.text=pair.Value;confirmationLabels.Clear();
            foreach(var component in confirmationLocalizers)if(component)component.enabled=true;confirmationLocalizers.Clear();
            if(confirmationFlow)confirmationFlow.enabled=confirmationFlowEnabled;confirmation=null;confirmationFlow=null;
            if(nativeBack)nativeBack.SetActive(nativeBackActive);nativeBack=null;
        }
        static bool IsActive(int version)=>busy&&!closing&&sessionVersion==version;
        static async void OnConfirmed(CharacterData data){if(data==null||!busy||!ready||closing)return;var callback=confirm;await Close();callback?.Invoke(data);}
        static async void OnCancelled(){if(!busy||closing)return;var callback=cancel;await Close();callback?.Invoke();}
        static async Task Close()
        {
            if(!busy||closing)return;closing=true;ready=false;sessionVersion++;
            try
            {
                if(actionButtons)actionButtons.SetActive(false);
                if(creator)creator.BackRequested-=OnCancelled;if(bridge){bridge.End();UnityEngine.Object.Destroy(bridge);bridge=null;}
            }
            catch(Exception error){Debug.LogWarning("[FriendsLife] Character editor restore failed: "+error.Message);}
            finally
            {
                try{RestoreConfirmation();if(roots!=null)for(int i=0;i<roots.Length;i++)if(roots[i])roots[i].SetActive(rootActive!=null&&i<rootActive.Length&&rootActive[i]);}
                finally
                {
                    foreach(var camera in cameras)if(camera)camera.enabled=true;cameras.Clear();
                    foreach(var canvas in overlays)if(canvas)canvas.enabled=true;overlays.Clear();
                    foreach(var raycaster in externalRaycasters)if(raycaster)raycaster.enabled=true;externalRaycasters.Clear();
                    if(scene.IsValid()&&SceneManager.GetActiveScene()==scene&&previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                    try
                    {
                        if(ownsScene&&scene.IsValid()&&scene.isLoaded){var unload=SceneManager.UnloadSceneAsync(scene);if(unload!=null)while(!unload.isDone)await Task.Yield();}
                    }
                    finally{ownsScene=false;networkSceneScope?.Dispose();networkSceneScope=null;}
                    Cursor.visible=cursorVisible;Cursor.lockState=cursorLock;busy=false;closing=false;creator=null;roots=null;rootActive=null;confirm=null;cancel=null;
                }
            }
        }
    }
}
