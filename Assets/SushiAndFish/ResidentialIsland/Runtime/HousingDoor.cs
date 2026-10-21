using PaperPixelGames.Core;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingDoor : MonoBehaviour,IBasicInteractable,IToolActiveInteractable
    {
        public float openAngle=105,duration=1.15f;
        public Transform pivot;
        Quaternion closed;
        float openness,target;
        public bool IsOpen=>target>.5f;
        public bool CanGaze=>true;
        public bool CanInteractable=>true;
        void Awake(){if(!pivot)pivot=transform;closed=pivot.localRotation;}
        void Update()
        {
            if(Mathf.Approximately(openness,target))return;
            float next=Mathf.MoveTowards(openness,target,Time.deltaTime/Mathf.Max(.2f,duration));
            // Stop a closing leaf when the player occupies its sweep. It remains
            // solid and never closes through the character's controller.
            var player=HousingIsland.Instance?.Player;
            if(target<openness&&player&&Vector3.Distance(player.transform.position,pivot.position)<1.2f)return;
            openness=next;var axis=pivot.parent?pivot.parent.InverseTransformDirection(Vector3.up):Vector3.up;
            pivot.localRotation=Quaternion.AngleAxis(Mathf.SmoothStep(0,1,openness)*openAngle,axis)*closed;
        }
        public void Toggle(){target=target<.5f?1:0;}
        public void GazeEnter()=>Singleton<HintsManager>.Instance?.AddHint("Interact","key_interact");
        public void GazeExit()=>Singleton<HintsManager>.Instance?.RemoveHint("Interact");
        public void TriggerInput(Interactor interactor,InputKind inputKind,InputActionPhase phase)
        {if(inputKind==InputKind.Interact&&phase==InputActionPhase.Canceled)Toggle();}
        public void TriggerCrossInput(Interactor interactor,BaseHoldable held,InputKind inputKind,InputActionPhase phase)=>TriggerInput(interactor,inputKind,phase);
    }
}
