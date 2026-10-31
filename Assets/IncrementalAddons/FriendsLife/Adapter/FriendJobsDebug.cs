using System;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Staff;
using UnityEngine;
namespace SushiAndFish.FriendsLife
{
    public static class FriendJobsDebug
    {
        public static string Dump()
        {
            var life=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;var manager=StaffManager.Instance;var text=new StringBuilder();
            text.AppendLine("shopOpen="+FriendJobs.ShopOpen+" day="+(manager?manager.CurrentDay:-1));
            if(!life||!life.Ready||!economy)return text.Append("not ready").ToString();
            text.AppendLine("playerCents="+economy.BalanceCents("player")+" reserved="+economy.ReservedPlayerCents);
            foreach(var auth in economy.State.sushiAuthorizations)
            {
                int id=auth.retainedEmployee?.EmployeeId??0;var npc=economy.StaffNpcFor(id,false);
                string native=manager&&manager.TryGetEmployee(id,out var employee)?" status="+employee.Status+" seq="+employee.HireSequence+" departure="+employee.Departure:" no-entry";
                var job=economy.State.jobs.LastOrDefault(j=>j.personId==auth.personId);var actor=life.ActorOf(auth.personId);
                var arrival=economy.State.staffEntries?.LastOrDefault(e=>e.personId==auth.personId);
                text.AppendLine(auth.personId+" employee="+id+" workedDay="+auth.workedDay+" npc="+(npc?npc.transform.position.ToString():"absent")+
                    " resident="+(actor?actor.transform.position.ToString():"absent")+native+" wallet="+economy.BalanceCents(auth.personId)+
                    (job==null?"":" contract="+job.contractId+" phase="+job.state+" micros="+job.workedMicros+" paid="+job.paidCents+"/"+job.budgetCents+" reason="+job.finishReason)+
                    (arrival==null?"":" arrival="+arrival.phase+" sourceObserved="+arrival.sourceBodyObserved+" exitObserved="+arrival.exitObserved));
            }
            return text.ToString();
        }
    }
}
