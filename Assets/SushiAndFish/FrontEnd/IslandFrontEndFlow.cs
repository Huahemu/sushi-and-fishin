using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FrontEnd
{
    public sealed class IslandFrontEndFlow : MonoBehaviour
    {
        public static IslandFrontEndFlow Active {get;private set;}
        [SerializeField] private MainMenuManager menuManager;
        [SerializeField] private SaveSlotScreen saveSlotScreen;
        [SerializeField] private BaseButton startButton,loadButton,settingsButton,quitButton;
        [SerializeField] private TMP_Text saveListMessage;
        [SerializeField] private IslandOrbitCamera orbitCamera;
        [SerializeField] private string backdropSceneName="IslandMenuBackdrop";
        private Scene backdropScene;
        private SaveSlot[] slots=Array.Empty<SaveSlot>();
        private bool busy,creatingCharacter,showingLoads,launching;
        private string saveWarning;
        private int pendingSlot=-1;
        public bool IsBusy=>busy||launching;
        public bool IsCreatingCharacter=>creatingCharacter;
        public int PendingSlot=>pendingSlot;
        public IslandOrbitCamera OrbitCamera=>orbitCamera;
        public bool BackdropLoaded=>backdropScene.IsValid()&&backdropScene.isLoaded;

        private void Awake()
        {
            Active=this;
            slots=saveSlotScreen.GetComponentsInChildren<SaveSlot>(true);
            LocalizationSettings.SelectedLocaleChanged+=OnLocaleChanged;
            RefreshLabels();
        }
        private void Start(){LoadBackdropAsync().Forget();}
        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged-=OnLocaleChanged;
            if(Active==this)Active=null;
        }
        private void OnLocaleChanged(UnityEngine.Localization.Locale locale){RefreshLabels();}
        public static string Label(string chinese,string english)
        {
            var locale=LocalizationSettings.SelectedLocale;
            return locale && !locale.Identifier.Code.StartsWith("zh",StringComparison.OrdinalIgnoreCase)?english:chinese;
        }
        public void RefreshLabels()
        {
            SetLabel(startButton,Label("开始游戏","START GAME"));
            SetLabel(loadButton,Label("读取存档","LOAD GAME"));
            SetLabel(settingsButton,Label("设置","SETTINGS"));
            SetLabel(quitButton,Label("退出游戏","QUIT"));
            foreach(var confirm in Resources.FindObjectsOfTypeAll<CharacterCreationConfirmButton>())if(confirm)confirm.RefreshLabel();
        }
        private static void SetLabel(BaseButton button,string text)
        {
            if(!button)return;
            foreach(var localizer in button.GetComponentsInChildren<LocalizeStringEvent>(true))localizer.enabled=false;
            button.SetText(text);
        }
        private async UniTaskVoid LoadBackdropAsync()
        {
            var operation=SceneManager.LoadSceneAsync(backdropSceneName,LoadSceneMode.Additive);
            if(operation==null)throw new InvalidOperationException("Island menu backdrop is not in Build Settings");
            await operation.ToUniTask();
            if(!this)return;
            backdropScene=SceneManager.GetSceneByName(backdropSceneName);
            SushiAndFish.Casino.Original.RecoveryShaderSupport.InstallForHost();
            SushiAndFish.Casino.Original.RecoveryShaderSupport.BeginRendering();
            SetBackdropVisible(!creatingCharacter);
        }
        private void SetBackdropVisible(bool visible)
        {
            if(BackdropLoaded)foreach(var root in backdropScene.GetRootGameObjects())root.SetActive(visible);
        }
        private void Update()
        {
            if(creatingCharacter&&!busy&&!menuManager.CustomizationSceneHost.IsOpen)
            {
                creatingCharacter=false;pendingSlot=-1;SetBackdropVisible(true);RefreshLabels();
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
            if(showingLoads)
            {
                if(saveSlotScreen.IsVisible)RefreshSaveList();
                else{showingLoads=false;RestoreSlotVisibility();}
            }
        }
        private bool SelectSingleplayer()
        {
            var coordinator=Singleton<MultiplayerCoordinator>.Instance;
            return coordinator&&coordinator.TrySelectSingleplayer();
        }
        public void BeginNewGame(){BeginNewGameAsync().Forget();}
        public async UniTask BeginNewGameAsync()
        {
            if(IsBusy||creatingCharacter||!SelectSingleplayer())return;
            var saves=Singleton<SaveManager>.Instance;
            if(!saves)return;
            pendingSlot=-1;
            for(int i=0;i<saves.SaveSlotsData.Count;i++)
                if(!saves.IsSaveSlotCorrupted(i) && !(saves.SaveSlotsData[i]?.GameProgress?.HasSave??false)){pendingSlot=i;break;}
            if(pendingSlot<0)
            {
                ShowLoadGames();
                ShowSaveMessage(Label("存档位已满，请先删除一个不需要的存档。","All save slots are full. Delete an unwanted save first."));
                return;
            }
            busy=true;
            try
            {
                showingLoads=false;RestoreSlotVisibility();saveSlotScreen.Hide();
                SetBackdropVisible(false);
                await menuManager.CustomizationSceneHost.OpenAsync();
                if(!menuManager.CustomizationSceneHost.IsOpen)throw new InvalidOperationException("Original character creator did not open");
                creatingCharacter=true;
                foreach(var confirm in Resources.FindObjectsOfTypeAll<CharacterCreationConfirmButton>())if(confirm)confirm.RefreshLabel();
                ClearSelection();
            }
            catch(Exception exception)
            {
                pendingSlot=-1;creatingCharacter=false;SetBackdropVisible(true);Debug.LogException(exception);
            }
            finally{busy=false;}
        }
        public void ShowLoadGames()
        {
            if(IsBusy||creatingCharacter||!SelectSingleplayer())return;
            saveWarning=null;showingLoads=true;menuManager.SwitchMenuStatus(MainMenuType.Play);RefreshSaveList();ClearSelection();
        }
        private void RefreshSaveList()
        {
            bool any=false;
            foreach(var slot in slots)
            {
                bool visible=slot.ContainsSave || Singleton<SaveManager>.Instance.IsSaveSlotCorrupted(slot.SlotIndex);
                if(slot.gameObject.activeSelf!=visible)slot.gameObject.SetActive(visible);
                any|=visible;
            }
            if(saveListMessage)
            {
                saveListMessage.gameObject.SetActive(!any||!string.IsNullOrEmpty(saveWarning));
                if(!string.IsNullOrEmpty(saveWarning))saveListMessage.text=saveWarning;
                else if(!any)saveListMessage.text=Label("暂无存档，请返回并选择「开始游戏」。","No saved games yet. Return and choose START GAME.");
            }
        }
        private void RestoreSlotVisibility(){foreach(var slot in slots)if(slot)slot.gameObject.SetActive(true);if(saveListMessage)saveListMessage.gameObject.SetActive(false);}
        private void ShowSaveMessage(string text){saveWarning=text;if(saveListMessage){saveListMessage.gameObject.SetActive(true);saveListMessage.text=text;}}
        public bool HandleSaveSlotClick(SaveSlot slot)
        {
            if(IsBusy||creatingCharacter)return true;
            if(!slot.ContainsSave)return true;
            ApplyCharacterForSlot(slot.SlotIndex);
            return false;
        }
        public async UniTask ConfirmNewCharacterAsync()
        {
            if(IsBusy||!creatingCharacter||pendingSlot<0)return;
            busy=true;
            try
            {
                var saves=Singleton<SaveManager>.Instance;
                if(saves.IsSaveSlotCorrupted(pendingSlot)||(saves.SaveSlotsData[pendingSlot]?.GameProgress?.HasSave??false))
                    throw new InvalidOperationException("Reserved new save slot is no longer empty; no existing save was overwritten");
                if(!await menuManager.CustomizationSceneHost.ConfirmAndCloseAsync())return;
                StoreCharacterForSlot(pendingSlot);
                var slot=slots.First(value=>value.SlotIndex==pendingSlot);
                creatingCharacter=false;SetBackdropVisible(true);
                slot.LaunchOriginalSlot();
            }
            catch(Exception exception){Debug.LogException(exception);}
            finally{busy=false;}
        }
        private static string CharacterPath(int index)=>Path.Combine(Application.persistentDataPath,"CharacterCustomization","SaveSlot_"+index+".json");
        private static void StoreCharacterForSlot(int index)
        {
            var data=PlayerCustomizationSession.GetCurrentCharacterData();
            if(data==null)throw new InvalidOperationException("No confirmed character data is available");
            string path=CharacterPath(index);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(data,true));
        }
        private static void ApplyCharacterForSlot(int index)
        {
            string path=CharacterPath(index);if(!File.Exists(path))return;
            var data=JsonUtility.FromJson<CharacterData>(File.ReadAllText(path));
            if(!PlayerCustomizationSession.SetCurrentCharacterData(data,true))throw new InvalidOperationException("Saved character data failed original character validation");
        }
        public void MarkLaunching(){launching=true;creatingCharacter=false;ClearSelection();}
        private static void ClearSelection(){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);}
    }
}
