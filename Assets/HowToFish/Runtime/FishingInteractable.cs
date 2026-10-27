using UnityEngine;
namespace HowToFish {
    public enum FishingInteraction{RodRack,IceBox}
    public sealed class FishingInteractable:MonoBehaviour {
        public FishingModule fishing;
        public FishingInteraction kind;
        public string Hint=>kind==FishingInteraction.RodRack?"E 取鱼竿":"E 放入已击败的鱼";
        public void Use(FishingHand hand){if(kind==FishingInteraction.RodRack){if(hand.Held==fishing.rod.item)fishing.ReturnRod();else{fishing.rig.SelectHand(hand);fishing.rig.Take(fishing.rod.item);}}else fishing.Deposit(hand);}
    }
}
