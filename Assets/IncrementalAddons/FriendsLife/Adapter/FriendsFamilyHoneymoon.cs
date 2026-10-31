using System;
using System.Linq;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomyService
    {
        // Source StartTravel and RecordFamilyHoneymoon remain the sole owners
        // of booking, inventory, price and family-state changes. Both execute
        // in one existing KernelScope; a rejected second step rolls back both.
        public bool TryStartFamilyHoneymoon(string actorPersonId,string coupleId,string destinationId,bool useTicket,string opId,out string travelSessionId)
            =>TryStartFamilyHoneymoon(actorPersonId,actorPersonId,coupleId,destinationId,useTicket,opId,out travelSessionId);
        public bool TryStartFamilyHoneymoon(string requesterPersonId,string selectedPersonId,string coupleId,string destinationId,bool useTicket,string opId,out string travelSessionId)
        {
            travelSessionId=null;LastFailure=null;
            if(!Authority||!runtime||!runtime.Ready||!runtime.IsWorldRunning||string.IsNullOrEmpty(opId))return false;
            var requester=runtime.GetPerson(requesterPersonId);var actor=runtime.GetPerson(selectedPersonId);var couple=runtime.Core.State.family?.couples.FirstOrDefault(c=>c.id==coupleId);
            if(requester==null||actor==null||couple==null||(actor.nativeResidentId!=couple.parentAId&&actor.nativeResidentId!=couple.parentBId))
            {LastFailure="请选择参与旅行的伴侣。";return false;}
            if(requester.controllerKind==PersonControllerKind.Human?!FamilyHumanIsBound(requesterPersonId):requesterPersonId!=selectedPersonId)return false;
            var prior=runtime.State.receipts.FirstOrDefault(r=>r.opId==opId);
            if(prior!=null)
            {
                var binding=State.receipts.FirstOrDefault(r=>r.opId=="honeymoon-trip/"+opId);
                var trip=binding==null?null:runtime.Core.State.activities?.travels.FirstOrDefault(t=>t.id==binding.counterparty);
                bool same=prior.success&&prior.domain=="FrozenKernel"&&prior.payerPersonId==requesterPersonId&&prior.causeEpisode==coupleId&&
                    binding!=null&&binding.actorId==selectedPersonId&&binding.domain==(useTicket?"source-honeymoon-ticket":"source-honeymoon-paid")&&
                    trip!=null&&trip.destinationId==destinationId&&trip.residentId==actor.nativeResidentId&&
                    (trip.residentId==couple.parentAId&&trip.partnerId==couple.parentBId||trip.residentId==couple.parentBId&&trip.partnerId==couple.parentAId);
                if(same)travelSessionId=trip.id;else LastFailure="这次旅行操作已提交了其他结果。";
                return same;
            }
            if(couple.status!="Married"||!LifeActivitiesCatalog.Travels.Any(d=>d.id==destinationId&&d.canWedding))
            {LastFailure="当前没有可安排的伴侣旅行。";return false;}
            var partner=runtime.PersonFromNative(actor.nativeResidentId==couple.parentAId?couple.parentBId:couple.parentAId);
            if(partner==null||new[]{actor,partner}.Any(p=>p.controllerKind==PersonControllerKind.Human&&p.personId!=requesterPersonId))
            {LastFailure="等待玩家确认旅行。";return false;}
            bool OwnTalk(ActivityLease lease)=>requester.controllerKind==PersonControllerKind.Human&&lease.facilityId=="WorldTalk"&&
                runtime.State.worldTalks.Any(t=>t.phase=="Active"&&t.humanId==requesterPersonId&&t.personId==lease.personId&&lease.leaseId=="world-talk/"+t.requestId);
            if(HasActiveJob(selectedPersonId)||HasActiveJob(partner.personId)||runtime.State.leases.Any(l=>(l.personId==selectedPersonId||l.personId==partner.personId)&&!OwnTalk(l)))
            {LastFailure="请等待当前活动结束。";return false;}
            string created=null;string sourceFailure=null;
            var receipt=runtime.ExecuteKernel(opId,requesterPersonId,scope=>
            {
                var booking=scope.StartTravel(actor.nativeResidentId,destinationId,useTicket,partner.nativeResidentId);
                if(!booking.success){sourceFailure=booking.message;return false;}
                var family=scope.RecordFamilyHoneymoon(coupleId,booking.sessionId);
                if(!family.success){sourceFailure=family.message;return false;}
                created=booking.sessionId;return true;
            },coupleId);
            if(receipt?.success!=true){LastFailure=sourceFailure??receipt?.message;return false;}
            if(string.IsNullOrEmpty(created)){LastFailure="已出发。";return false;}
            Receipt("honeymoon-trip/"+opId,useTicket?"source-honeymoon-ticket":"source-honeymoon-paid",selectedPersonId,0,created);Commit();
            travelSessionId=created;
            return !string.IsNullOrEmpty(travelSessionId);
        }
    }
}
