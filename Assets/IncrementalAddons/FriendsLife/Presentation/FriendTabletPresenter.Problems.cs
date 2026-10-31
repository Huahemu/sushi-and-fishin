using System.Linq;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // 烦恼·社交 tab body (M2). This friend's problem and how to help, what it
    // likes as a gift, the other friends' problems, then the social messages.
    public sealed partial class FriendTabletPresenter
    {
        partial void RenderProblemsTab(Transform content,ref float y,ref bool handled)
        {
            var runtime=FriendsLifeRuntime.Instance;var person=runtime?.GetPerson(personId);if(person==null||!runtime.Ready)return;
            handled=true;
            var problem=FriendPlayerInteractions.Problem(runtime,personId);
            Field(content,ref y,"烦恼",problem==null?"现在没有烦恼。":problem.description+"\n走到TA身边按 E，选“帮忙”。");
            if(person.controllerKind==PersonControllerKind.Npc)
            {
                var taste=runtime.Core.GiftTaste(person.nativeResidentId);
                Field(content,ref y,"喜好","喜欢"+LifeSimulationRuntime.PlayerGiftName(taste.liked)+"，不喜欢"+LifeSimulationRuntime.PlayerGiftName(taste.disliked));
            }
            var others=runtime.People.Where(p=>p.controllerKind==PersonControllerKind.Npc&&p.personId!=personId)
                .Select(p=>new{p.displayName,request=FriendPlayerInteractions.Problem(runtime,p.personId)}).Where(x=>x.request!=null).ToArray();
            foreach(var other in others)Field(content,ref y,other.displayName,other.request.title);
            RenderSourceSocialPage("social",content,ref y);
        }
    }
}
