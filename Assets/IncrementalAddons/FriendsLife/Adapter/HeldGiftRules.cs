using System;
namespace SushiAndFish.FriendsLife
{
    public enum HeldGiftProof { Wait, Witness, PreserveUnknown }
    public struct HeldGiftObservation
    {
        public bool requested,epochMatches,contextValid,sourceExists,sameObject,wasSpawned,nowSpawned,ownerMatches,heldStateUnchanged,sceneValid,ownerConnected,timedOut;
    }
    // These are proof gates, not a replacement for the original inventory.
    // An absent object, a sent RPC or a new load never means consumption.
    public static class HeldGiftRules
    {
        public static string Operation(string request)=>"world-talk/"+request+"/input";
        public static bool IsHeld(string action)=>action!=null&&action.StartsWith("present:",StringComparison.Ordinal);
        public static bool BlocksOther(string currentOp,string otherOp,string human,string otherHuman,string phase,string action,bool reconciled)
            =>otherOp!=currentOp&&human==otherHuman&&phase=="Unknown"&&IsHeld(action)&&!reconciled;
        public static bool CanRequest(bool exactItem,bool otherUnknown,bool alreadyRequested)=>exactItem&&!otherUnknown&&!alreadyRequested;
        public static bool Recorded(bool observed,bool completionSaved,bool successfulReceipt)=>observed&&completionSaved&&successfulReceipt;
        public static HeldGiftProof Observe(HeldGiftObservation state)
        {
            if(!state.requested||!state.epochMatches||!state.contextValid||!state.sourceExists||!state.sameObject||!state.wasSpawned||
                !state.ownerMatches||!state.heldStateUnchanged||!state.sceneValid||!state.ownerConnected||state.timedOut)return HeldGiftProof.PreserveUnknown;
            return state.nowSpawned?HeldGiftProof.Wait:HeldGiftProof.Witness;
        }
    }
}
