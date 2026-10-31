namespace SushiAndFish.FriendsLife
{
    // Host service reconciliation only; this contains no source gameplay rule.
    public enum SushiDeliveryObservation { None, AlreadyCommitted, Placed, Delivered, Returned, Rejected, Unknown }
    public static class SushiDeliveryPolicy
    {
        public static SushiDeliveryObservation Observe(string phase,bool billCommitted,bool savedWitness,bool sameLiveSource,bool hasReservation,
            bool sourceFound,bool sameConsumer,bool foodFound,bool placed,bool sameOrder,bool eating,bool atAnchor,bool framePassed)
        {
            if(billCommitted)return SushiDeliveryObservation.AlreadyCommitted;
            if(savedWitness)return SushiDeliveryObservation.Delivered;
            if(phase=="Cancelled")return SushiDeliveryObservation.None;
            if(phase=="Committed")return SushiDeliveryObservation.Unknown;
            if(!sameLiveSource)
                return phase=="AwaitFunds"&&!hasReservation?SushiDeliveryObservation.Rejected:SushiDeliveryObservation.Unknown;
            if(sameConsumer&&placed&&eating)return SushiDeliveryObservation.Delivered;
            if(phase=="Unknown")return SushiDeliveryObservation.None;
            if(sameConsumer&&placed)return SushiDeliveryObservation.Placed;
            if(sameConsumer&&sameOrder&&foodFound&&!placed&&!atAnchor&&phase!="AwaitFunds"&&framePassed)return SushiDeliveryObservation.Returned;
            if(phase=="AwaitFunds"&&!hasReservation&&(!sourceFound||!sameConsumer||!sameOrder))return SushiDeliveryObservation.Rejected;
            if((phase=="Reserved"||phase=="Placed")&&(!sourceFound||!sameConsumer||!foodFound))return SushiDeliveryObservation.Unknown;
            return SushiDeliveryObservation.None;
        }
    }
}
