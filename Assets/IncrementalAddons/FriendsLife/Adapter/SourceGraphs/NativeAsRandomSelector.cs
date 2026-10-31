using System;
using System.Collections.Generic;
using TomodachiLife.Simulation;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeAsHistoryPair {public int node=-1,child=-1;}
    [Serializable] public sealed class NativeAsActorHistory
    {
        public NativeAsHistoryPair[] recent={new NativeAsHistoryPair(),new NativeAsHistoryPair(),new NativeAsHistoryPair(),new NativeAsHistoryPair()};
    }
    [Serializable] public sealed class NativeAsSharedHistory
    {
        public int capacity,cursor;
        public List<int> values=new List<int>();
    }
    [Serializable] public sealed class NativeAsRandomHistory
    {
        // Four source actor-instance history pairs, retained across Date entry.
        public int[] nodes={-1,-1,-1,-1};
        public int[] children={-1,-1,-1,-1};
    }
    // Direct scalar port of original main 00a95f30. Shared mask/ring follow
    // 0234fcfc / 0234fd3c. Callers own and serialize both history objects and
    // the original four-word RNG; no implicit startup seed is generated here.
    public static class NativeAsRandomSelector
    {
        public const string Source="main 00a95f30 / 0234fcfc / 0234fd3c";
        public static int Choose(int node,float[] weights,int historyCount,NativeAsActorHistory actor,NativeAsSharedHistory shared,ref NativeMiiEventRandomState random)
        {
            if(weights==null||weights.Length<1||weights.Length>31)throw new InvalidOperationException("Recovered AS selector supports the source 1..31-child mask domain");
            if(actor?.recent==null||actor.recent.Length!=4)throw new InvalidOperationException("Original AS actor history requires exactly four records");
            if(shared!=null&&(shared.capacity<1||shared.capacity>255||shared.values==null||shared.values.Count>shared.capacity||shared.cursor<0||shared.cursor>=shared.capacity))throw new InvalidOperationException("Original AS shared history owner is invalid");
            ulong local=0;
            foreach(var record in actor.recent){if(record==null)throw new InvalidOperationException("Original AS history record is absent");if(record.node==node)local|=1UL<<(record.child&63);}
            ulong mask=local;
            if(shared!=null)foreach(int child in shared.values)mask|=1UL<<(child&63);
            uint all=unchecked(~(0xffffffffU<<(weights.Length&31)));
            if(mask==all)mask=local;
            float total=0;int eligible=0,chosen=0;
            for(int i=0;i<weights.Length;i++)
            {
                if((mask>>i&1)!=0)continue;
                total=total+weights[i];eligible++;chosen=i;
            }
            float divisor=total==0?eligible:total;
            float draw=NativeMiiEventRandom.NextUnitFloat(ref random),cumulative=0;
            for(int i=0;i<weights.Length;i++)
            {
                if((mask>>i&1)!=0)continue;
                float next=cumulative+(total==0?1f/eligible:weights[i]/divisor);
                if(cumulative<=draw&&draw<next){chosen=i;break;}
                cumulative=next;
            }
            if(historyCount!=0)
            {
                int count=0,first=-1;
                for(int i=0;i<4;i++)if(actor.recent[i].node==node){if(first<0)first=i;count++;}
                int remove=count>=historyCount?Math.Max(0,first):0;
                for(int i=remove;i<3;i++)actor.recent[i]=actor.recent[i+1];
                actor.recent[3]=new NativeAsHistoryPair{node=node,child=chosen};
            }
            if(shared!=null)
            {
                if(shared.values.Count<shared.capacity)shared.values.Add(chosen);
                else{shared.values[shared.cursor]=chosen;shared.cursor=(shared.cursor+1)%shared.capacity;}
            }
            return chosen;
        }
        // Bounded no-shared-history Date variant, checked against4096 actual
        // main00a95f30 observations. Preserve the existing general Choose API.
        public static int Select(int node,float[] weights,int historyCount,NativeAsRandomHistory history,ref NativeMiiEventRandomState random,int previousChild,out bool sameChild)
        {
            if(weights==null||weights.Length==0||weights.Length>31||history==null||history.nodes==null||history.children==null||history.nodes.Length!=4||history.children.Length!=4)
                throw new InvalidOperationException("Original AS selector requires its authored1..31 child weights and four actor history pairs");
            foreach(float weight in weights)if(float.IsNaN(weight)||float.IsInfinity(weight)||weight<0)throw new InvalidOperationException("This recovered source selector subset requires finite nonnegative authored weights");
            ulong mask=0;for(int i=0;i<4;i++)if(history.nodes[i]==node)mask|=1UL<<(history.children[i]&63);
            // The Date root has no shared-history object at node+20. Its native
            // local-history mask is retained even when every child is excluded.
            float total=0;int eligible=0,selected=0;
            for(int i=0;i<weights.Length;i++)if((mask&(1UL<<i))==0){total=(float)(total+weights[i]);eligible++;selected=i;}
            float denominator=total!=0?total:(float)eligible;
            float sample=NativeMiiEventRandom.NextUnitFloat(ref random),cumulative=0;
            for(int i=0;i<weights.Length;i++)
            {
                if((mask&(1UL<<i))!=0)continue;
                float portion=total==0?(float)(1f/eligible):(float)(weights[i]/denominator),next=(float)(cumulative+portion);
                if(cumulative<=sample&&sample<next){selected=i;break;}cumulative=next;
            }
            if(historyCount!=0)
            {
                int matching=0,first=-1;for(int i=0;i<4;i++)if(history.nodes[i]==node){matching++;if(first<0)first=i;}
                int remove=historyCount<=matching&&first>=0?first:0;
                for(int i=remove;i<3;i++){history.nodes[i]=history.nodes[i+1];history.children[i]=history.children[i+1];}
                history.nodes[3]=node;history.children[3]=selected;
            }
            sameChild=selected==previousChild;return selected;
        }
    }
}
