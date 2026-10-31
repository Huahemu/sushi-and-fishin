using System;
using System.Linq;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeMiiFacialSlot
    {public int expression=-1;public string owner="";public bool ignoreHabit;}
    [Serializable] public sealed class NativeMiiFacialState
    {
        public int schema=1;
        public string worldId,nativeActorId;
        public int selectedExpression,selectedPriority;
        public NativeMiiFacialSlot[] slots;
    }
    [Serializable] public sealed class NativeMiiExpressionLeaf
    {public string owner;public int rawExpression;public bool ignoreHabit,ended;}
    public sealed class NativeMiiFacialInputs
    {
        // Source86ed0 also owns environmental/model and habit overrides.
        // A caller must observe these owners, not infer them from a Host
        // expression label, personality slider or visible bed animation.
        public bool ordinaryModelGateResolved;
        public bool habitOwnerResolved;
        public int habitMode;
        public bool temporaryHabitOwnerResolved,temporaryHabitOverride;
        public int temporaryHabitMode;
        // Actual parsed RSDB expression row+6c values;75 means no redirect.
        public int[] actualNativeLessUseCodes;
    }
    public sealed class NativeMiiFacialSelection
    {public int rawExpression,expression,priority,habitMode;public bool ignoreHabit;public string owner;}
    // Faithful integer owner/state layer. Presentation and source model/habit
    // input binding are separate required ports, not a Smile fallback.
    public static class NativeMiiFacialOwner
    {
        public const string Source="Ctor0069ddc8..69de70; leaf018467f0/009a60ac; slot016ed230/016ed25c; selection00086ed0";
        static void Expression(int code){if(code< -1||code>=75)throw new InvalidOperationException("Unsupported original Mii expression code: "+code);}
        static string Self(NativeMiiFacialState state)=>"native-expression-component/"+state.worldId+"/"+state.nativeActorId;
        public static NativeMiiFacialState Construct(string actualWorldId,string actualNativeActorId)
        {
            if(string.IsNullOrEmpty(actualWorldId)||string.IsNullOrEmpty(actualNativeActorId))throw new InvalidOperationException("Actual source world/actor identity required");
            // Literal native constructor writes8 code=-1/owner0/flag0 lanes,
            // selected code/priority0. Resolve establishes the Normal base.
            return new NativeMiiFacialState{worldId=actualWorldId,nativeActorId=actualNativeActorId,slots=Enumerable.Range(0,8).Select(_=>new NativeMiiFacialSlot()).ToArray()};
        }
        public static void Validate(NativeMiiFacialState state)
        {
            if(state==null||state.schema!=1||string.IsNullOrEmpty(state.worldId)||string.IsNullOrEmpty(state.nativeActorId)||state.slots==null||state.slots.Length!=8||state.slots.Any(s=>s==null))throw new InvalidOperationException("Undeclared/malformed native expression owner");
            foreach(var slot in state.slots)Expression(slot.expression);
            if(state.selectedExpression<0||state.selectedExpression>=75||state.selectedPriority<0||state.selectedPriority>=8)throw new InvalidOperationException("Unknown saved native selected expression");
        }
        public static NativeMiiExpressionLeaf EnterCueLeaf(NativeMiiFacialState state,string actualLeafOwner,int rawExpression,bool ignoreHabit)
        {
            Validate(state);Expression(rawExpression);if(string.IsNullOrEmpty(actualLeafOwner))throw new InvalidOperationException("Actual stable source leaf owner required");
            var leaf=new NativeMiiExpressionLeaf{owner=actualLeafOwner,rawExpression=rawExpression,ignoreHabit=ignoreHabit};
            // Source -1 is the explicit cached no-effect branch, never an
            // unavailable presenter/default. It performs no slot write.
            if(rawExpression!=-1)SetSourceSlot(state,1,rawExpression,actualLeafOwner,ignoreHabit);
            return leaf;
        }
        public static void SetSourceSlot(NativeMiiFacialState state,int actualPriority,int rawExpression,string actualOwner,bool ignoreHabit)
        {
            Validate(state);Expression(rawExpression);if(actualPriority<0||actualPriority>=8)throw new InvalidOperationException("Actual source expression priority must be0..7");
            var slot=state.slots[actualPriority];slot.expression=rawExpression;slot.owner=actualOwner??"";slot.ignoreHabit=ignoreHabit;
        }
        static NativeMiiFacialSelection PlanSelection(NativeMiiFacialState state,NativeMiiFacialInputs input)
        {
            Validate(state);if(input==null||!input.ordinaryModelGateResolved)throw new InvalidOperationException("Unsupported native face model/environment owner");
            if(state.slots[4].owner==Self(state))throw new InvalidOperationException("Source self-owned environmental expression lane requires its actual model owner");
            int priority=0;for(int k=7;k>=1;k--)if(state.slots[k].expression!=-1){priority=k;break;}
            var selected=priority==0?new NativeMiiFacialSlot{expression=0,owner=Self(state)}:state.slots[priority];int mode=0,expression=selected.expression;
            if(!selected.ignoreHabit)
            {
                if(!input.habitOwnerResolved||!input.temporaryHabitOwnerResolved)throw new InvalidOperationException("Unsupported native equipped/temporary habit owner");
                mode=input.temporaryHabitOverride?input.temporaryHabitMode:input.habitMode;
                if(mode==1)
                {
                    if(input.actualNativeLessUseCodes==null||input.actualNativeLessUseCodes.Length!=75)throw new InvalidOperationException("Actual parsed native ExpressionLessUseType table unavailable");
                    int replacement=input.actualNativeLessUseCodes[expression];if(replacement<0||replacement>75)throw new InvalidOperationException("Unknown parsed native LessUse expression value");if(replacement!=75)expression=replacement;
                }
            }
            return new NativeMiiFacialSelection{rawExpression=selected.expression,expression=expression,priority=priority,owner=selected.owner,ignoreHabit=selected.ignoreHabit,habitMode=mode};
        }
        public static NativeMiiFacialSelection Resolve(NativeMiiFacialState state,NativeMiiFacialInputs actualInputs)
        {
            // Validate required owners before mutation; missing input must not
            // become a normal/Smile write or overwrite a persisted result.
            var result=PlanSelection(state,actualInputs);
            state.slots[0].expression=0;state.slots[0].owner=Self(state);state.slots[0].ignoreHabit=false;
            state.selectedExpression=result.expression;state.selectedPriority=result.priority;return result;
        }
        public static bool ClearOwnedCueLeafRaw(NativeMiiFacialState state,NativeMiiExpressionLeaf leaf)
        {Validate(state);if(leaf==null||string.IsNullOrEmpty(leaf.owner))throw new InvalidOperationException("Actual cached source leaf required");Expression(leaf.rawExpression);if(leaf.ended)return false;leaf.ended=true;if(leaf.rawExpression==-1||state.slots[1].owner!=leaf.owner)return false;state.slots[1].expression=-1;state.slots[1].owner="";return true;}
        public static bool ExitCueLeaf(NativeMiiFacialState state,NativeMiiExpressionLeaf leaf,NativeMiiFacialInputs actualInputs)
        {
            Validate(state);if(leaf==null||string.IsNullOrEmpty(leaf.owner))throw new InvalidOperationException("Actual cached leaf record required");Expression(leaf.rawExpression);if(leaf.ended)return false;
            if(leaf.rawExpression==-1||state.slots[1].owner!=leaf.owner){leaf.ended=true;return false;}
            // Native009a60ac clears only the CURRENT matching owner. Plan on
            // a local copy first so an unresolved lower owner cannot partially
            // destroy the persisted live source state in this strict port.
            var old=state.slots[1];state.slots[1]=new NativeMiiFacialSlot{expression=-1,owner="",ignoreHabit=old.ignoreHabit};
            try{Resolve(state,actualInputs);}catch{state.slots[1]=old;throw;}
            leaf.ended=true;return true;
        }
        public static bool MatchesCueEnd(int requestedToken,int requestedIndex,uint requestedKind,int candidateToken,int candidateIndex,uint candidateKind,bool candidateIsCue)
            =>requestedToken==candidateToken&&requestedIndex==candidateIndex&&requestedKind==candidateKind&&candidateIsCue;
    }
}
