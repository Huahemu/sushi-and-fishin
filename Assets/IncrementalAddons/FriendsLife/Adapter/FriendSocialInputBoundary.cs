using System;
using System.Linq;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendWorldTalkAuthority
    {
        sealed class DispatchContext
        {public FriendsLifeRuntime runtime;public string human,person,action,option,op;}
        static DispatchContext dispatch;
        // The in-person E actions (talk/help/present) read the validated
        // speaker, option and operation id of the current dispatch here.
        internal static bool TryCurrentDispatch(FriendsLifeRuntime runtime,string person,out string human,out string option,out string op)
        {
            bool current=dispatch!=null&&ReferenceEquals(dispatch.runtime,runtime)&&dispatch.person==person;
            human=current?dispatch.human:null;option=current?dispatch.option:null;op=current?dispatch.op:null;return current;
        }
        internal static bool FamilyInputUnresolved(FriendsLifeRuntime runtime,string human,string choiceId)
            =>runtime?.State.worldTalks.Any(r=>r.humanId==human&&r.phase=="Unknown"&&
                !string.IsNullOrEmpty(r.completedCommandId)&&r.completedAction=="family:"+choiceId)==true;
        internal static bool IsCurrentFamilyDispatch(FriendsLifeRuntime runtime,string human,string person,string action)
            =>dispatch!=null&&ReferenceEquals(dispatch.runtime,runtime)&&dispatch.human==human&&dispatch.person==person&&dispatch.action==action;
        static string DispatchSourceInput(FriendsLifeRuntime runtime,FriendWorldTalkRequest row)
        {
            // Only the already validated authority stack gets this one-call
            // scope. A new UI nonce cannot bypass an existing uncertain input.
            string op="world-talk/"+row.requestId+"/input";
            var prior=dispatch;dispatch=new DispatchContext{runtime=runtime,human=row.humanId,person=row.personId,action=row.completedAction,option=row.completedOption,op=op};
            try{FriendPlayerInteractions.Bind(runtime);return runtime.SubmitSourceActionAs(row.humanId,row.personId,row.completedAction,row.completedOption,op);}
            finally{dispatch=prior;}
        }
    }
}
