using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Numeric bodies recovered from the original main, not inferred from node names.
    public static class NativeRecoveredQueries
    {
        public static void Register(OriginalNodeRegistry registry,Func<OriginalNodeContext,byte> sourceHeight)
        {
            registry.Query("AddVec3Query",(c,o)=>
            {
                bool onlyOnEnter=c.HasProperty("Prop_OnlyOnEnter")&&c.Property("Prop_OnlyOnEnter").AsBool();
                var saved=onlyOnEnter?c.Read("nativeOrderedSum"):null;if(saved!=null)return saved;
                var values=c.InputValues("Vec3s");float x=0,y=0,z=0;
                // 002301cc consumes the original array in its stored order.
                foreach(var value in values){var vector=value.AsVector();x=x+vector.x;y=y+vector.y;z=z+vector.z;}
                var result=OriginalValue.Vector(new Vector3(x,y,z));if(onlyOnEnter)c.Write("nativeOrderedSum",result);return result;
            },"Original main 002301cc / update009cf7f0; registered0045ec68; ordered Vec3s/Result and Prop_OnlyOnEnter");
            registry.Query("MiiHeightLinearInterpolationQuery",(c,o)=>
            {
                if(sourceHeight==null)throw new OriginalBindingException(c.Address+": original height owner is unavailable");
                return OriginalValue.Number(Height(sourceHeight(c),(float)c.Input("FloatMin").AsNumber(),(float)c.Input("FloatMax").AsNumber()));
            },"Original main 01935234, raw height getter 01716638; min+(rawHeight/127)*(max-min), native f32 FMADD");
        }
        public static float Height(byte rawHeight,float minimum,float maximum)
        {
            float ratio=rawHeight/127f,delta=maximum-minimum;
            return NativeFloat32.FusedMultiplyAdd(delta,ratio,minimum);
        }
    }

    // ARM64 FMADD rounds once. Unity Mono does not expose a portable f32 FMA,
    // so preserve its finite binary arithmetic with an exact integer sum.
    public static class NativeFloat32
    {
        [StructLayout(LayoutKind.Explicit)] struct Bits
        {
            [FieldOffset(0)] public float value;
            [FieldOffset(0)] public uint bits;
        }
        static uint Raw(float value)=>new Bits{value=value}.bits;
        static float Value(uint bits)=>new Bits{bits=bits}.value;
        static System.Numerics.BigInteger Significand(uint bits,out int exponent)
        {
            int field=(int)((bits>>23)&255);uint fraction=bits&0x7fffff;
            exponent=field==0?-149:field-150;
            var value=new System.Numerics.BigInteger(field==0?fraction:fraction|0x800000);
            return (bits&0x80000000)!=0?-value:value;
        }
        static System.Numerics.BigInteger RoundEven(System.Numerics.BigInteger value,int shift)
        {
            if(shift<=0)return value<<-shift;
            var quotient=value>>shift;var remainder=value-(quotient<<shift);var halfway=System.Numerics.BigInteger.One<<(shift-1);
            return remainder>halfway||remainder==halfway&&!quotient.IsEven?quotient+1:quotient;
        }
        static int Length(System.Numerics.BigInteger value)
        {
            var bytes=value.ToByteArray();int last=bytes.Length-1;while(last>0&&bytes[last]==0)last--;
            int length=last*8;for(byte high=bytes[last];high!=0;high>>=1)length++;return length;
        }
        public static float FusedMultiplyAdd(float a,float b,float c)
        {
            uint aa=Raw(a),bb=Raw(b),cc=Raw(c),productSign=(aa^bb)&0x80000000;
            if(float.IsNaN(a)||float.IsNaN(b)||float.IsNaN(c))return float.NaN;
            if(float.IsInfinity(a)||float.IsInfinity(b))
            {
                if(a==0||b==0)return float.NaN;
                if(float.IsInfinity(c)&&(cc&0x80000000)!=productSign)return float.NaN;
                return Value(productSign|0x7f800000);
            }
            if(float.IsInfinity(c))return c;
            var left=Significand(aa,out int aExponent)*Significand(bb,out int bExponent);
            var right=Significand(cc,out int cExponent);int productExponent=aExponent+bExponent,common=Math.Min(productExponent,cExponent);
            var sum=(left<<(productExponent-common))+(right<<(cExponent-common));
            if(sum.IsZero)return Value(left.IsZero&&right.IsZero&&productSign!=0&&(cc&0x80000000)!=0?0x80000000U:0);
            uint sign=sum.Sign<0?0x80000000U:0;sum=System.Numerics.BigInteger.Abs(sum);int length=Length(sum),exponent=common+length-1;
            if(exponent>127)return Value(sign|0x7f800000);
            if(exponent>=-126)
            {
                var rounded=RoundEven(sum,length-24);
                if(rounded>=(System.Numerics.BigInteger.One<<24)){rounded>>=1;exponent++;if(exponent>127)return Value(sign|0x7f800000);}
                return Value(sign|((uint)(exponent+127)<<23)|((uint)rounded&0x7fffff));
            }
            var denormal=RoundEven(sum,-149-common);return Value(sign|(uint)denormal);
        }
    }
}
