using UnityEngine;
namespace HowToFish {
    // Input-neutral entry points for Input Actions, UI buttons or an existing XR rig.
    public sealed class FishingControls:MonoBehaviour {
        public FishingModule module;
        readonly bool[] pressed=new bool[2];
        public void EquipRod(){module.combat.Equip(-1);}
        public void EquipWeapon(int index){module.combat.Equip(index);}
        public void BeginCast(){module.BeginCast();}
        public void ReleaseCast(){module.ReleaseCast(module.rig.Tracked);}
        public void SetReel(float amount){module.SetReeling(amount);}
        public void ReleaseFish(){if(module.Hooked)module.ReleaseCatch(module.Hooked);}
        public void SwitchBait(){module.CycleBait();}
        public void OpenCollection(){module.inventory.RequestArchive(FishingArchiveCommand.Collection);}
        public void OpenWarehouse(){module.inventory.RequestArchive(FishingArchiveCommand.Warehouse);}
        public void CloseArchive(){module.inventory.RequestArchive(FishingArchiveCommand.Close);}
        public void NextArchivePage(){module.inventory.RequestArchive(FishingArchiveCommand.NextPage);}
        public void PreviousArchivePage(){module.inventory.RequestArchive(FishingArchiveCommand.PreviousPage);}
        public void NextArchiveEntry(){module.inventory.RequestArchive(FishingArchiveCommand.NextEntry);}
        public void PreviousArchiveEntry(){module.inventory.RequestArchive(FishingArchiveCommand.PreviousEntry);}
        public void ToggleArchiveAppearance(){module.inventory.RequestArchive(FishingArchiveCommand.ToggleAppearance);}
        public void LeftTrigger(float value){Trigger(0,value>.5f);}
        public void RightTrigger(float value){Trigger(1,value>.5f);}
        void Trigger(int hand,bool down){if(!module.combat.TriggerHand(hand,down,pressed[hand],false))module.TriggerHand(hand,down,pressed[hand],false,false);else module.SetReeling(0);pressed[hand]=down;}
        public void ReloadLeft(){var item=module.rig.hands[0].Held;if(item){var weapon=item.GetComponent<FishWeapon>();if(weapon)weapon.Reload();}}
        public void ReloadRight(){var item=module.rig.hands[1].Held;if(item){var weapon=item.GetComponent<FishWeapon>();if(weapon)weapon.Reload();}}
        public void GrabLeft(){Grab(0);}
        public void GrabRight(){Grab(1);}
        void Grab(int hand){var grip=module.rig.hands[hand];FishingItem nearest=null;float distance=.35f;foreach(var item in module.rig.Items){if(!item||item.owner||item.Retired||item.fish&&!item.fish.CanGrab)continue;float d=Vector3.Distance(grip.transform.position,item.transform.position);if(d<distance){distance=d;nearest=item;}}if(nearest)grip.Grab(nearest);}
        public void ReleaseLeft(){module.rig.hands[0].Release();}
        public void ReleaseRight(){module.rig.hands[1].Release();}
        public void DepositLeft(){module.Deposit(module.rig.hands[0]);}
        public void DepositRight(){module.Deposit(module.rig.hands[1]);}
    }
}
