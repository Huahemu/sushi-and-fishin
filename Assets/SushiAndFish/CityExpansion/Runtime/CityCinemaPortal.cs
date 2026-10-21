using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.WorldUISystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.CityExpansion
{
    public sealed class CityCinemaPortal : MonoBehaviour,IBasicInteractable,IToolActiveInteractable
    {
        public Transform destination;
        public WorldUIAnchor anchor;
        public string prompt="进入放映厅";
        float nextUse;
        public bool CanGaze=>true;
        public bool CanInteractable=>destination&&Time.unscaledTime>=nextUse;
        public WorldUIAnchor WorldUIAnchor=>anchor;
        public void GazeEnter(){if(CanInteractable)Singleton<HintsManager>.Instance?.AddHint("Interact","key_interact");}
        public void GazeExit(){Singleton<HintsManager>.Instance?.RemoveHint("Interact");}
        public void TriggerCrossInput(Interactor interactor,BaseHoldable held,InputKind kind,InputActionPhase phase)=>TriggerInput(interactor,kind,phase);
        public void TriggerInput(Interactor interactor,InputKind kind,InputActionPhase phase)
        {
            if(kind!=InputKind.Interact||phase!=InputActionPhase.Canceled||!CanInteractable)return;
            var player=interactor.Player;
            if(!player||player.IsUsingEmoteArea||interactor.IsUsing)return;
            nextUse=Time.unscaledTime+1;
            player.TeleportPlayer(destination.position,destination.rotation,true);
            player.FirstPersonCharacter.SetVelocity(Vector3.zero);
            player.FirstPersonCharacter.PitchInstant(0);
            GazeExit();
        }
    }
}
