using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Framework.UI;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Added to a newly loaded friend-only creator instance. No original code,
    // private dictionary, IL or method interception is changed. The native UI
    // keeps its shape/colour/wardrobe APIs; generated-path history is owned here.
    public sealed class FriendCharacterCreatorBridge:MonoBehaviour
    {
        CharacterCreator creator;bool applying,observing;float next;string current;
        readonly List<string> undo=new List<string>(),redo=new List<string>();
        readonly List<OutfitSelector> choices=new List<OutfitSelector>();
        BaseButton undoButton,redoButton;
        public bool Applying=>applying;
        public static FriendCharacterCreatorBridge CreateForOwnedInstance(CharacterCreator owner)
        {
            if(!owner||!owner.character)throw new InvalidOperationException("Owned friend creator is missing");
            var bridge=owner.gameObject.AddComponent<FriendCharacterCreatorBridge>();bridge.creator=owner;FriendAppearancePort.MarkOwnedClone(owner.character);return bridge;
        }
        public async Task Apply(CharacterData data)
        {
            if(applying)throw new InvalidOperationException("Friend editor is loading");applying=true;
            try{await FriendAppearancePort.Load(creator.character,data);creator.RefreshAfterExternalCharacterLoad();if(FriendAppearancePort.HasGenerated(data))FriendCreatorPhotoViewport.Ensure(creator);current=JsonUtility.ToJson(FriendAppearancePort.Capture(creator.character));}
            finally{applying=false;}
        }
        public CharacterData Export()=>FriendAppearancePort.Capture(creator.character);
        public void BeginObserving()
        {
            if(observing)return;observing=true;
            var buttons=creator.GetComponentsInChildren<BaseButton>(true);
            undoButton=buttons.FirstOrDefault(b=>b.name.IndexOf("undo",StringComparison.OrdinalIgnoreCase)>=0);
            redoButton=buttons.FirstOrDefault(b=>b.name.IndexOf("redo",StringComparison.OrdinalIgnoreCase)>=0);
            // These are controls of this new friend-only scene instance. The
            // original editor and its original button methods remain intact.
            if(undoButton){undoButton.Clicked-=creator.Undo;undoButton.Clicked+=Undo;}
            if(redoButton){redoButton.Clicked-=creator.Redo;redoButton.Clicked+=Redo;}
            var sample=creator.GetComponentsInChildren<OutfitSelector>(true).FirstOrDefault();
            if(sample&&FriendAppearancePort.HasGenerated(FriendAppearancePort.Capture(creator.character)))foreach(var outfit in FriendGeneratedModels.RegisteredOutfits.Where(o=>o))
            {var choice=UnityEngine.Object.Instantiate(sample,sample.transform.parent);choice.name="Friend generated choice";choice.Init(outfit,creator);choices.Add(choice);}
        }
        void LateUpdate()
        {
            if(!observing||applying||!creator||!creator.character||Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;var system=creator.character;if(system.isloading||system.isMerging||system.isUpdatingTexture)return;
            string value=JsonUtility.ToJson(FriendAppearancePort.Capture(system));if(current!=null&&value!=current){undo.Add(current);if(undo.Count>80)undo.RemoveAt(0);redo.Clear();}current=value;
            foreach(var choice in choices)if(choice&&choice.outfit&&choice.outfit.Type)choice.gameObject.SetActive(choice.outfit.Type.name==creator.GetCurrentCatagory());
        }
        async void Undo(){await Move(undo,redo);}
        async void Redo(){await Move(redo,undo);}
        async Task Move(List<string> from,List<string> to)
        {
            if(applying||from.Count==0||!creator)return;
            try{to.Add(JsonUtility.ToJson(Export()));string value=from[from.Count-1];from.RemoveAt(from.Count-1);await Apply(JsonUtility.FromJson<CharacterData>(value));}
            catch(Exception error){Debug.LogWarning("[FriendsLife] Friend history: "+error.Message);}
        }
        public void End()
        {
            if(!observing)return;
            observing=false;GetComponent<FriendCreatorPhotoViewport>()?.End();if(undoButton){undoButton.Clicked-=Undo;undoButton.Clicked+=creator.Undo;}if(redoButton){redoButton.Clicked-=Redo;redoButton.Clicked+=creator.Redo;}
            foreach(var choice in choices)if(choice)UnityEngine.Object.Destroy(choice.gameObject);choices.Clear();undo.Clear();redo.Clear();
        }
        void OnDestroy(){if(creator)End();}
    }
}
