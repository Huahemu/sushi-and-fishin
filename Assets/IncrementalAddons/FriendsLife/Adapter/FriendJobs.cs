using System;
using System.Linq;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Patterns;

namespace SushiAndFish.FriendsLife
{
    // Jobs contract for the other modules, plus the friend shift rule that the
    // original StaffRuntime asks about. Labels are short plain Chinese.
    public static class FriendJobs
    {
        // Casino, cinema and town restaurant jobs stay off until the core is
        // stable. Off means no new public shift and no public station scans.
        public static bool PublicJobs=>FriendsLifeFeatureFlags.PublicJobs;
        public static readonly StaffRole[] SushiRoles={StaffRole.Stocker,StaffRole.Waitress,StaffRole.PrepCook,StaffRole.SushiChef};
        public static bool ShopOpen=>Singleton<GameProgressManager>.Instance?.RestaurantManager?.IsRestaurantOpen==true;
        public static string RoleTitle(StaffRole role)=>role==StaffRole.SushiChef?"寿司师傅":role==StaffRole.PrepCook?"备料员":role==StaffRole.Waitress?"服务员":"补货员";
        // The friend's job ("寿司店补货员"), or null when the friend has none.
        public static string Current(string personId)
        {
            var economy=FriendsEconomyService.Instance;if(!economy||string.IsNullOrEmpty(personId))return null;
            var sushi=economy.State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);
            if(sushi!=null)return sushi.role==StaffRole.SushiChef?"寿司师傅":"寿司店"+RoleTitle(sushi.role);
            if(!economy.HasActiveJob(personId))return null;var title=economy.JobTitle(personId);return title=="暂无工作"?null:title;
        }
        // True while the friend is at work (walking in, on duty or leaving).
        public static bool Working(string personId)=>FriendsEconomyService.Instance?.HasActiveJob(personId)==true;
        // Shown after the E menu or the tablet hired the friend.
        public static string HireFeedback()=>ShopOpen?"已录用，忙完就去上班":"已录用，开店后上班";
        // Roles the friend can take now: unlocked, not full, not already theirs.
        public static StaffRole[] OpenRoles(string personId)
        {
            var manager=StaffManager.Instance;if(!manager)return Array.Empty<StaffRole>();
            var current=FriendsEconomyService.Instance?.State.sushiAuthorizations.FirstOrDefault(a=>a.personId==personId);
            return SushiRoles.Where(r=>manager.IsRoleUnlocked(r)&&!manager.IsRoleFull(r)&&(current==null||current.role!=r)).ToArray();
        }
        // StaffRuntime hook: a friend's employee entry stays in the team between
        // shifts, but its body only exists while a shift contract is live.
        public static bool OffShift(int employeeId)
        {
            try{if(!FriendStaffPort.IsOwnedEntry(employeeId))return false;}catch(Exception e){UnityEngine.Debug.LogWarning("[FriendsLife] Friend staff marker: "+e.Message);return false;}
            var economy=FriendsEconomyService.Instance;
            return !economy||!economy.State.jobs.Any(j=>j.employeeId==employeeId&&(j.state==FriendJobPhase.Travel||j.state==FriendJobPhase.OnDuty));
        }
    }
}
