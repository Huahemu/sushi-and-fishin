using System;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        private bool CanScheduleGameplayEvent(NativeLifeEventRule rule,LifeSimulationResident focus,LifeSimulationResident target,LifeSimulationResident another)
        {
            foreach(var branch in rule.branches??Array.Empty<NativeLifeBranchRule>())
            {
                var results=branch.results??Array.Empty<NativeLifeResultRule>();
                if((branch.changeOnesideLove01||results.Any(r=>r.becomeOnesideLove01))&&!CanFamilyRomance(focus.id,target.id,out _))return false;
                if(results.Any(r=>r.becomeOnesideLove02)&&(another==null||!CanFamilyRomance(focus.id,another.id,out _)))return false;
            }
            return true;
        }
        private static string GameplayEventTitle(NativeLifeEventRule rule)
        {
            if(!string.IsNullOrEmpty(rule.simulationLabel)&&rule.simulationLabel.Any(c=>c>=0x4e00&&c<=0x9fff))return rule.simulationLabel;
            string name=rule.name??"";
            if(name.Contains("FallInLove"))return"心动";
            if(name.Contains("Disillusion"))return"失望";
            if(name.Contains("DropFromTree"))return"树上掉东西";
            if(name.Contains("LiveTogether"))return"同住";
            if(name.Contains("Fight"))return"吵架";
            var results=(rule.branches??Array.Empty<NativeLifeBranchRule>()).SelectMany(b=>b.results??Array.Empty<NativeLifeResultRule>()).ToArray();
            if(results.Any(r=>r.fightNormal01))return"意见不合";
            if(results.Any(r=>r.becomeOnesideLove01||r.becomeOnesideLove02))return"暗恋";
            if(results.Any(r=>r.becomeFriend01))return"认识";
            if(results.Any(r=>r.rewardMoney0>0||r.rewardMoney1>0))return"一起玩";
            return"相遇";
        }
        private static string GameplayEventChoice(string branch,string result)
        {
            string combined=(branch??"")+" "+(result??"");
            if(combined.IndexOf("Failure",StringComparison.OrdinalIgnoreCase)>=0||combined.IndexOf("Miss",StringComparison.OrdinalIgnoreCase)>=0)return"继续";
            if(combined.IndexOf("Success",StringComparison.OrdinalIgnoreCase)>=0)return"继续";
            if(combined.IndexOf("Cancel",StringComparison.OrdinalIgnoreCase)>=0)return"结束";
            return"继续";
        }
    }
}
