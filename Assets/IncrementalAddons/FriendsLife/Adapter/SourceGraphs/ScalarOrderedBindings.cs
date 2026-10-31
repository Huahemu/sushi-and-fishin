using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Exact current ordered-input query subset. No random/clock/scene owner,
    // invented array default, relation result or physical actor is written.
    public static class ScalarOrderedBindings
    {
        public const string Source="Original AddFloat00201188/018d7c0c; AverageVec3018dab34/018dac00; Max01932d28/01932d5c; Build01934f28/017166bc";
        static uint Bits(float x)=>BitConverter.ToUInt32(BitConverter.GetBytes(x),0);
        static float Float(uint x)=>BitConverter.ToSingle(BitConverter.GetBytes(x),0);
        static bool NaN(uint x)=>(x&0x7fffffffU)>0x7f800000U;
        static float NaNs(uint a,uint b)
        {
            bool an=NaN(a),bn=NaN(b);
            if(an&&(a&0x400000U)==0)return Float(a|0x400000U);
            if(bn&&(b&0x400000U)==0)return Float(b|0x400000U);
            return Float((an?a:b)|0x400000U);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static float Add(float a,float b)
        {
            uint aa=Bits(a),bb=Bits(b);if(NaN(aa)||NaN(bb))return NaNs(aa,bb);
            if(float.IsInfinity(a)&&float.IsInfinity(b)&&((aa^bb)&0x80000000U)!=0)return Float(0x7fc00000);
            return (float)(a+b);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static float Multiply(float a,float b)
        {
            uint aa=Bits(a),bb=Bits(b);if(NaN(aa)||NaN(bb))return NaNs(aa,bb);
            if(float.IsInfinity(a)&&b==0||float.IsInfinity(b)&&a==0)return Float(0x7fc00000);
            return (float)(a*b);
        }
        static void FiniteInput(float value)
        {
            // The packaged sources use finite f32 fields. Managed float
            // argument transport can quiet a signaling NaN before its payload
            // reaches this handler, so that wider domain is unsupported here.
            // It must never become an invented zero or an inaccurate success.
            if(float.IsNaN(value)||float.IsInfinity(value))
                throw new OriginalBindingException("Unsupported native scalar input: non-finite source f32 is outside the verified authored field subset");
        }
        public static float Sum(IReadOnlyList<float> values)
        {if(values==null)throw new ArgumentNullException(nameof(values));float sum=0;foreach(float value in values){FiniteInput(value);sum=Add(sum,value);}return sum;}
        public static float Maximum(IReadOnlyList<float> values)
        {
            if(values==null)throw new ArgumentNullException(nameof(values));float result=-float.MaxValue;
            // Native FCMP/FCSEL GT ignores unordered comparisons and retains
            // the old operand on equal values (including signed-zero ties).
            foreach(float value in values){FiniteInput(value);if(value>result)result=value;}return result;
        }
        public static Vector3 Average(IReadOnlyList<Vector3> values)
        {
            if(values==null)throw new ArgumentNullException(nameof(values));float x=0,y=0,z=0,count=0;
            foreach(var value in values)
            {
                FiniteInput(value.x);FiniteInput(value.y);FiniteInput(value.z);
                float next=(float)(count+1f),old=(float)(count/next),current=(float)(1f/next);
                // Actual018dab38 uses separate FMUL/FMUL/FADD for each axis,
                // a sequential online mean, not sum/divide or a fused FMA.
                x=Add(Multiply(x,old),Multiply(current,value.x));
                y=Add(Multiply(y,old),Multiply(current,value.y));
                z=Add(Multiply(z,old),Multiply(current,value.z));count=next;
            }
            return new Vector3(x,y,z);
        }
        public static float Build(byte rawBuild,float minimum,float maximum)
        {
            // Current authored3 Build consumers have finite FloatMin/Max.
            // Keep missing/non-finite source inputs explicit instead of using
            // a guessed body morph or fabricated default appearance.
            if(float.IsNaN(minimum)||float.IsNaN(maximum)||float.IsInfinity(minimum)||float.IsInfinity(maximum))throw new OriginalBindingException("Build interpolation left the current finite authored input subset");
            float ratio=(float)(rawBuild/127f),range=(float)(maximum-minimum);
            if(float.IsInfinity(range)&&ratio==0)return Float(0x7fc00000);
            return NativeFloat32.FusedMultiplyAdd(range,ratio,minimum);
        }
        static IReadOnlyList<OriginalValue> Ordered(OriginalNodeContext context,string name)
        {
            var input=(context.Node.inputs??Array.Empty<OriginalParameter>()).SingleOrDefault(p=>p.name==name);
            if(input?.sources==null||input.sources.Length==0)throw new OriginalBindingException(context.Address+": the actual authored ordered "+name+" sources are absent");
            return context.InputValues(name);
        }
        static float SourceFloat(OriginalValue value,string address)
        {
            if(value==null||value.kind!="Number")
                throw new OriginalBindingException(address+": expected an owned source Float, not a null/unknown/text fallback");
            return (float)value.number;
        }
        static OriginalValue Cached(OriginalNodeContext context,Func<OriginalValue> calculate,bool alwaysOnEnter=false)
        {
            bool once=alwaysOnEnter||context.Property("Prop_OnlyOnEnter").AsBool();
            // Query nodes in distinct module instances can share their caller
            // frame and node index. Include the persisted source scope so one
            // module's OnEnter result cannot become another module's output.
            string key="native-scalar/v1/scope:"+context.Scope.id+"/node:"+context.Node.index+"/"+context.Node.name;
            var saved=context.Read(key);
            if(saved!=null&&once)
            {
                string expected=context.Node.name=="AverageVec3Query"?"Vector":"Number";
                if(saved.kind!=expected)throw new OriginalBindingException(context.Address+": saved native scalar output has an unknown type");
                return saved;
            }
            var result=calculate();context.Write(key,result);return result;
        }
        public static void Register(OriginalNodeRegistry registry,Func<OriginalNodeContext,byte> actualSourceBuild)
        {
            registry.Query("AddFloatQuery",(c,o)=>Cached(c,()=>OriginalValue.Number(Sum(Ordered(c,"Floats").Select(v=>SourceFloat(v,c.Address)).ToArray()))),Source);
            registry.Query("AverageVec3Query",(c,o)=>Cached(c,()=>OriginalValue.Vector(Average(Ordered(c,"Vec3s").Select(v=>v.AsVector()).ToArray()))),Source);
            registry.Query("MaxFloatQuery",(c,o)=>Cached(c,()=>OriginalValue.Number(Maximum(Ordered(c,"Floats").Select(v=>SourceFloat(v,c.Address)).ToArray()))),Source);
            registry.Query("MiiBuildLinearInterpolationQuery",(c,o)=>Cached(c,()=>
            {
                if(actualSourceBuild==null)throw new OriginalBindingException(c.Address+": actual source raw build owner is unavailable");
                return OriginalValue.Number(Build(actualSourceBuild(c),SourceFloat(c.Input("FloatMin"),c.Address+"/FloatMin"),SourceFloat(c.Input("FloatMax"),c.Address+"/FloatMax")));
            },true),Source+"; native Build OnUpdate01935014 returns without recomputing, this activation retains its first actual output");
        }
    }
}
