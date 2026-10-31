using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed class NativeMiiEventRelationSnapshot
    {
        // Native 002641b4 mask: Other0, Know1, Friend2, ExFriend3, Lover4,
        // ExLover5, Couple6, Divorce7, Parent8, Child9, OlderSibling10,
        // YoungerSibling11, GrandParent12, GrandChild13, Relative14.
        public int? nativeType,meter,reverseMeter;
        public bool? oneSideLove,reverseOneSideLove,fighting;
    }
    /// <summary>002641b4 flattens reference rows into at most ten OR leaves.
    /// 000513f4 accepts the first passing leaf; 0005146c ANDs its type-mask,
    /// directional love/fight bits, and inclusive forward/reverse meter bounds.
    /// Rank labels are not substituted for these actual source meter gates.</summary>
    public sealed class NativeMiiEventRelations
    {
        public const string Source="main 002641b4,000513f4,0005146c";
        private readonly Dictionary<uint,NativeMiiEventRelation> rows;
        private readonly Dictionary<uint,NativeMiiEventMeter> meters;
        public NativeMiiEventRelations(NativeMiiEventDatabase database)
        {
            rows=(database.Data.relations??Array.Empty<NativeMiiEventRelation>()).ToDictionary(r=>r.id);
            meters=(database.Data.meters??Array.Empty<NativeMiiEventMeter>()).ToDictionary(r=>r.id);
        }
        public NativeMiiEventDecision Evaluate(uint id,NativeMiiEventRelationSnapshot state)
        {
            if(state==null)return NativeMiiEventDecision.Unsupported("Native relation snapshot is unavailable",Source);
            var leaves=new List<NativeMiiEventRelation>();var visiting=new HashSet<uint>();
            if(!Flatten(id,leaves,visiting))return NativeMiiEventDecision.Unsupported("Relation reference is missing or cyclic",Source);
            NativeMiiEventDecision unknown=null;
            foreach(var leaf in leaves)
            {
                var result=EvaluateLeaf(leaf,state);
                if(result.IsPass)return result;
                if(result.outcome==NativeMiiEventOutcome.Unsupported&&unknown==null)unknown=result;
            }
            return unknown??NativeMiiEventDecision.Fail("No native OR relation leaf passed",Source);
        }
        private bool Flatten(uint id,List<NativeMiiEventRelation> output,HashSet<uint> visiting)
        {
            if(output.Count>=10)return true;
            if(!rows.TryGetValue(id,out var row)||!visiting.Add(id))return false;
            bool success=true;
            if(row.IsRefarence)
            {
                foreach(uint reference in row.RefarenceArrayId??Array.Empty<uint>())if(!Flatten(reference,output,visiting)){success=false;break;}
            }
            else output.Add(row);
            visiting.Remove(id);return success;
        }
        private NativeMiiEventDecision EvaluateLeaf(NativeMiiEventRelation row,NativeMiiEventRelationSnapshot s)
        {
            bool[] types={row.IsTypeOther,row.IsTypeKnow,row.IsTypeFriend,row.IsTypeExFriend,row.IsTypeLover,row.IsTypeExLover,row.IsTypeCouple,row.IsTypeDivorce,row.IsTypeParent,row.IsTypeChild,row.IsTypeBrotherSisterOlder,row.IsTypeBrotherSisterYounger,row.IsTypeGrandParent,row.IsTypeGrandChild,row.IsTypeRelative};
            uint mask=0;for(int i=0;i<types.Length;i++)if(types[i])mask|=1U<<i;
            if(mask==0)return NativeMiiEventDecision.Fail("Native relation type mask is empty",Source);
            NativeMiiEventDecision unknown=null;
            if(!s.nativeType.HasValue)unknown=NativeMiiEventDecision.Unsupported("Native relation type is unavailable",Source);
            else if((mask>>(s.nativeType.Value&31)&1)==0)return NativeMiiEventDecision.Fail("Native relation type mask rejected",Source);
            foreach(var gate in new[]{CheckBits(s.oneSideLove,row.IsOnesideLove,row.IsOnesideLoveNot,"forward one-side love"),CheckBits(s.reverseOneSideLove,row.ReverseIsOnesideLove,row.ReverseIsOnesideLoveNot,"reverse one-side love"),CheckBits(s.fighting,row.IsFight,row.IsFightNot,"pair fight"),CheckMeter(s.meter,row.MeterOrHigherId,true),CheckMeter(s.meter,row.MeterOrLowerId,false),CheckMeter(s.reverseMeter,row.ReverseMeterOrHigherId,true),CheckMeter(s.reverseMeter,row.ReverseMeterOrLowerId,false)})
            {
                if(gate.outcome==NativeMiiEventOutcome.Fail)return gate;
                if(gate.outcome==NativeMiiEventOutcome.Unsupported&&unknown==null)unknown=gate;
            }
            return unknown??NativeMiiEventDecision.Pass(Source);
        }
        private static NativeMiiEventDecision CheckBits(bool? actual,bool required,bool forbidden,string name)
        {
            if(!required&&!forbidden)return NativeMiiEventDecision.Pass(Source);
            if(required&&forbidden)return NativeMiiEventDecision.Fail("Contradictory native "+name+" flags",Source);
            if(!actual.HasValue)return NativeMiiEventDecision.Unsupported("Native "+name+" is unavailable",Source);
            return actual.Value==required?NativeMiiEventDecision.Pass(Source):NativeMiiEventDecision.Fail("Native "+name+" gate rejected",Source);
        }
        private NativeMiiEventDecision CheckMeter(int? actual,uint thresholdId,bool lowerBound)
        {
            if(thresholdId==0)return NativeMiiEventDecision.Pass(Source);
            if(!meters.TryGetValue(thresholdId,out var threshold))return NativeMiiEventDecision.Unsupported("Source relation-meter threshold row missing",Source);
            if(!actual.HasValue)return NativeMiiEventDecision.Unsupported("Native directional relation meter unavailable",Source);
            bool pass=lowerBound?threshold.Meter<=actual.Value:actual.Value<=threshold.Meter;
            return pass?NativeMiiEventDecision.Pass(Source):NativeMiiEventDecision.Fail("Native inclusive relation-meter bound rejected",Source);
        }
    }
}
