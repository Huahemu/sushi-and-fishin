using System;
using System.Linq;
using PaperPixelGames.Core.Staff;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        bool RenderPublicJobs(Transform content,ref float y)
        {
            var runtime=FriendsLifeRuntime.Instance;var service=FriendsEconomyService.Instance;
            if(!runtime||!runtime.Ready||runtime.GetPerson(personId)==null||!service)return false;
            var job=service.State.jobs.FirstOrDefault(j=>j.personId==personId&&j.state!=FriendJobPhase.Done);
            var authorization=service.State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);
            var arrival=service.State.staffEntries?.LastOrDefault(e=>e.personId==personId&&e.phase!="Done"&&e.phase!="Cancelled");
            bool workedToday=service.WorkedToday(personId);
            // The shift follows the shop sign: before opening, after closing, or once today's shift is over.
            string phase=job==null?(authorization!=null?(!FriendJobs.ShopOpen?"开店后上班":workedToday?"今天已下班":arrival?.phase=="Travel"?"前往工作":arrival?.phase=="Arrived"?"等待上岗":"忙完手头的事就去"):"暂无工作"):
                job.state==FriendJobPhase.Travel?"前往工作":job.state==FriendJobPhase.OnDuty?"工作中":"下班中";
            string title=job!=null?service.JobTitle(personId):authorization!=null?FriendJobs.RoleTitle(authorization.role):null;
            FriendUi.Text("CurrentJob",content,0,y,ContentWidth(content),64,title==null?phase:title+" · "+phase,24);y+=80;
            FriendUi.Text("PersonalWallet",content,0,y,ContentWidth(content),64,"可用金币："+(service.AvailableCents(personId)/100m).ToString("0.00"),24);y+=80;
            if(job!=null)
            {
                FriendUi.Text("CurrentWages",content,0,y,ContentWidth(content),64,"本次工资："+(job.paidCents/100m).ToString("0.00")+" / "+(job.budgetCents/100m).ToString("0.00")+" 金币",24);y+=80;
            }
            else
            {
                var last=service.State.jobs.LastOrDefault(j=>j.personId==personId&&j.employeeId!=0&&j.state==FriendJobPhase.Done&&j.paidCents>0);
                if(last!=null){FriendUi.Text("LastWages",content,0,y,ContentWidth(content),64,"上一班工资："+(last.paidCents/100m).ToString("0.00")+" 金币",24);y+=80;}
            }
            if(job!=null||authorization!=null||service.State.publicJobAuthorizations.Any(a=>a.personId==personId))
            {
                var leave=SourceRow(content,ref y,job?.state==FriendJobPhase.Handover?"正在下班…":"离开工作",()=>
                {
                    bool accepted=runtime.IsAuthority?service.LeavePublicWork(personId):SendTrackedHousing("leave-work");
                    if(!accepted)Status(service.LastFailure??"暂时无法离开工作。");else if(runtime.IsAuthority)Render();
                });
                leave.interactable=(job!=null||authorization!=null||service.State.publicJobAuthorizations.Any(a=>a.personId==personId))&&job?.state!=FriendJobPhase.Handover;
                // Current work and its safe exit are the first visible actions.
                if(job!=null)return true;
            }
            if(runtime.GetPerson(personId).controllerKind!=PersonControllerKind.Npc)return true;
            // Only roles the friend can take now: unlocked and not full.
            var roles=FriendJobs.OpenRoles(personId);
            if(roles.Length==0){FriendUi.Text("NoOpenings",content,0,y,ContentWidth(content),64,authorization==null?"寿司店暂时没有空缺岗位。":"暂时没有其他空缺岗位。",24);y+=80;return true;}
            foreach(var role in roles)
            {
                var value=role;
                SourceRow(content,ref y,(authorization==null?"去当":"改当")+FriendJobs.RoleTitle(role),()=>
                {
                    bool accepted=runtime.IsAuthority?service.InviteToWork(personId,value):SendTrackedHousing("invite-work",new HousingPlanCommand{typeId=value.ToString()});
                    if(!accepted)Status(service.LastFailure??"目前无法安排这份工作。");else if(runtime.IsAuthority){Render();Status(FriendJobs.HireFeedback());}
                });
            }
            return true;
        }
    }
}
