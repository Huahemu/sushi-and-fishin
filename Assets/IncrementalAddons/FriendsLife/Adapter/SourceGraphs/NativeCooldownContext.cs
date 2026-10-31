using System;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeCooldownSlot
    {
        // Original record begins at owner+0x248+8+slot*0x20.
        public bool active;
        public int handleToken,handleIndex;
        public long lastUserPosixSeconds;
        public int references;
    }

    [Serializable] public sealed class NativeCooldownOwner
    {
        public int schema=1;
        public NativeCooldownSlot[] slots=new NativeCooldownSlot[NativeCooldownContext.SlotCount];
        public NativeCooldownOwner()
        {
            // 006a2434 allocates the actual focus owner and calls006a32d0 at
            // +0x248. All70 active flags are initially false. Inactive payload
            // bytes are not read by the original readiness function.
            for(int index=0;index<slots.Length;index++)slots[index]=new NativeCooldownSlot();
        }
    }

    // Original AI.Product.100 scalar owner port. It does not start a new timer,
    // choose an event, advance a clock, assign actor slots or invent ownership.
    // The caller supplies the same original actor-slot identity and raw user
    // POSIX clock to each task acquire/release; it owns durable task idempotency.
    public static class NativeCooldownContext
    {
        public const int SlotCount=70;
        public const int OriginalGroupActionCoolingTimeSpan=10;
        public const string Source="main init006a32d0 owner006a2434; acquire007caf38/task007cae08; release0179c4cc/task0179f02c; elapsed0179c3cc/01d3e538; predicate01d491a8/01d4e684; GroupAction getter01c40ac0";
        public const string ClockSource="main00566678: nn::time::AdjustableUserSystemClock::GetCurrentTime raw PosixTime at+0x150 copied to+0x120, distinct from pause-adjusted+0xf0";

        public static void Validate(NativeCooldownOwner owner)
        {
            if(owner==null||owner.schema!=1||owner.slots==null||owner.slots.Length!=SlotCount)
                throw new InvalidOperationException("Original cooldown owner/schema or70-slot array is unavailable");
            for(int index=0;index<SlotCount;index++)if(owner.slots[index]==null)
                throw new InvalidOperationException("Original cooldown slot is unavailable: "+index);
        }
        static NativeCooldownSlot FindOrCreate(NativeCooldownOwner owner,uint actorSlot,int handleToken,int handleIndex)
        {
            Validate(owner);
            if(actorSlot>=SlotCount)return null;
            var slot=owner.slots[actorSlot];
            if(!slot.active)
            {
                // 0179c44c / inlined007caf38 initialize the record only once.
                // An active record is not silently replaced if a handle changes.
                slot.handleToken=handleToken;slot.handleIndex=handleIndex;
                slot.lastUserPosixSeconds=0;slot.references=0;slot.active=true;
            }
            return slot;
        }
        public static bool Acquire(NativeCooldownOwner owner,uint actorSlot,int handleToken,int handleIndex,long? currentUserPosixSeconds)
        {
            var slot=FindOrCreate(owner,actorSlot,handleToken,handleIndex);if(slot==null)return false;
            slot.lastUserPosixSeconds=currentUserPosixSeconds??0;
            slot.references=unchecked(slot.references+1);return true;
        }
        public static bool Release(NativeCooldownOwner owner,uint actorSlot,int handleToken,int handleIndex,long? currentUserPosixSeconds)
        {
            var slot=FindOrCreate(owner,actorSlot,handleToken,handleIndex);if(slot==null)return false;
            slot.lastUserPosixSeconds=currentUserPosixSeconds??0;
            // The source can decrement zero to-1. Adapter replay protection
            // belongs to the task receipt; clamping here would change the core.
            slot.references=unchecked(slot.references-1);return true;
        }
        public static long MinimumElapsed(NativeCooldownOwner owner,long? currentUserPosixSeconds)
        {
            Validate(owner);long result=long.MaxValue;
            foreach(var slot in owner.slots)
            {
                if(!slot.active)continue;
                if(slot.references>0)return 0;
                long difference=currentUserPosixSeconds.HasValue?unchecked(slot.lastUserPosixSeconds-currentUserPosixSeconds.Value):0;
                // Future timestamps are ignored by the original, including a
                // backwards user-clock change. Do not make this monotonic.
                if(difference<=0)
                {
                    long elapsed=unchecked(-difference);if(elapsed<result)result=elapsed;
                }
            }
            return result;
        }
        public static bool Ready(NativeCooldownOwner owner,bool nativeEventSystemAvailable,long? currentUserPosixSeconds,int groupActionCoolingTimeSpan=OriginalGroupActionCoolingTimeSpan)
        {
            if(owner==null||!nativeEventSystemAvailable)return false;
            return (long)groupActionCoolingTimeSpan<=MinimumElapsed(owner,currentUserPosixSeconds);
        }
    }
}
