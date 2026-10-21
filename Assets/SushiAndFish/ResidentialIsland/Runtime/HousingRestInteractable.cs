using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingRestInteractable : MonoBehaviour,IBasicInteractable,IToolActiveInteractable
    {
        public HousingRestKind kind;
        public Transform seatHip,sleepHip,handSupport,exit,cameraPoint;
        public Renderer[] bedSurfaces;
        public HousingToilet toilet;
        [System.NonSerialized] public EmoteArea area;
        public bool CanGaze=>area&&!area.IsOccupied;
        public bool CanInteractable=>CanGaze;
        public void GazeEnter()=>Singleton<HintsManager>.Instance?.AddHint("Interact","key_interact");
        public void GazeExit()=>Singleton<HintsManager>.Instance?.RemoveHint("Interact");
        public void TriggerInput(Interactor interactor,InputKind inputKind,InputActionPhase phase)
        {
            if(inputKind!=InputKind.Interact||phase!=InputActionPhase.Canceled||!CanInteractable)return;
            var session=interactor.Player.GetComponent<HousingRestSession>();
            if(!session)session=interactor.Player.gameObject.AddComponent<HousingRestSession>();
            session.Begin(interactor.Player,this);
        }
        public void TriggerCrossInput(Interactor interactor,BaseHoldable held,InputKind inputKind,InputActionPhase phase)=>TriggerInput(interactor,inputKind,phase);
    }
}
