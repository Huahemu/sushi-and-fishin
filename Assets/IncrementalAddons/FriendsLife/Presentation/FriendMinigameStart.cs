using System;
using System.Linq;
using System.Threading.Tasks;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendTabletPresenter
    {
        bool preparingGamePortraits;
        string preparingGamePortraitsId;
        string[] MinigamePortraitCandidates(string definition)
        {
            var runtime=FriendsLifeRuntime.Instance;var selected=runtime?.GetPerson(personId);
            if(!runtime||!runtime.Ready||selected==null)return Array.Empty<string>();
            var residents=runtime.Core.State.residents.Where(r=>!runtime.IsAwaitingArrival(r.id));
            if(definition=="CafeLatteGame"||definition=="SilhouetteGameMii")
                return residents.OrderBy(r=>r.id).Take(4).Select(r=>r.id).ToArray();
            if(definition=="FindTheOnly")
                return new[]{selected.nativeResidentId}.Concat(residents.Where(r=>r.id!=selected.nativeResidentId&&!runtime.Core.IsResidentInActivity(r.id)).Take(1).Select(r=>r.id)).ToArray();
            return Array.Empty<string>();
        }
        async void StartSourceMinigame(string definition)
        {
            if(preparingGamePortraits)return;
            var candidates=MinigamePortraitCandidates(definition);
            if(candidates.Length==0){Command("minigame:"+definition);return;}
            string originalPage=page,originalPerson=personId,error=null;bool ready=false;
            preparingGamePortraits=true;preparingGamePortraitsId=definition;Render();
            try
            {
                float deadline=Time.realtimeSinceStartup+25f;
                while(Time.realtimeSinceStartup<deadline&&isActiveAndEnabled&&page==originalPage&&personId==originalPerson)
                {
                    var runtime=FriendsLifeRuntime.Instance;var actual=MinigamePortraitCandidates(definition);
                    ready=actual.Length>0;
                    foreach(string native in actual)
                    {
                        var person=runtime?.PersonFromNative(native);
                        if(person==null||!FriendPortraitCache.Get(person.personId))ready=false;
                    }
                    if(ready)break;
                    await Task.Yield();
                }
                if(!ready)error="画像还在准备，请稍后再试。";
            }
            catch(Exception exception){Debug.LogWarning("[FriendsLife] Minigame start: "+exception);error="小游戏无法开始，请稍后再试。";}
            finally
            {
                preparingGamePortraits=false;preparingGamePortraitsId=null;
                if(isActiveAndEnabled&&page==originalPage&&personId==originalPerson)Render();
            }
            if(!isActiveAndEnabled||page!=originalPage||personId!=originalPerson)return;
            if(ready)Command("minigame:"+definition);else if(!string.IsNullOrEmpty(error))Status(error);
        }
    }
}
