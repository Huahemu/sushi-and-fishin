using System;
using System.Numerics;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Original SDK powf105a49e0, its immutable109e6f98/109e81b0 tables and
    // FRINTA helper105a8b50. Arithmetic order follows actual ARM64 instructions.
    public static class NativeSdkPowf
    {
        public const string Source="Original SDK105a49e0/105a4440/105a4470/105a44a0; tables109e6f98,109e81b0; round105a8b50";
        static uint Bits(float value)=>BitConverter.ToUInt32(BitConverter.GetBytes(value),0);
        static float Float(uint value)=>BitConverter.ToSingle(BitConverter.GetBytes(value),0);
        static ulong Bits(double value)=>unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        static double Double(ulong value)=>BitConverter.Int64BitsToDouble(unchecked((long)value));
        static bool Nan(uint value)=>(value&0x7fffffffU)>0x7f800000U;
        static uint Quiet(uint value)=>value|0x00400000U;
        static float ProcessNaN(uint a,uint b)
        {
            bool an=Nan(a),bn=Nan(b);
            // ARM FPProcessNaNs prioritizes signaling operands, then the first
            // quiet operand. DN is clear in the recovered numerical fixture.
            if(an&&(a&0x00400000U)==0)return Float(Quiet(a));
            if(bn&&(b&0x00400000U)==0)return Float(Quiet(b));
            return Float(Quiet(an?a:b));
        }
        static float Add(float a,float b)
        {uint aa=Bits(a),bb=Bits(b);if(Nan(aa)||Nan(bb))return ProcessNaN(aa,bb);if((aa&0x7fffffffU)==0x7f800000U&&(bb&0x7fffffffU)==0x7f800000U&&((aa^bb)&0x80000000U)!=0)return Float(0x7fc00000);return (float)(a+b);}
        static float Multiply(float a,float b)
        {uint aa=Bits(a),bb=Bits(b);if(Nan(aa)||Nan(bb))return ProcessNaN(aa,bb);if(((aa&0x7fffffffU)==0x7f800000U&&(bb&0x7fffffffU)==0)||((bb&0x7fffffffU)==0x7f800000U&&(aa&0x7fffffffU)==0))return Float(0x7fc00000);return (float)(a*b);}
        static float Divide(float a,float b)
        {uint aa=Bits(a),bb=Bits(b);if(Nan(aa)||Nan(bb))return ProcessNaN(aa,bb);if(((aa&0x7fffffffU)==0&&(bb&0x7fffffffU)==0)||((aa&0x7fffffffU)==0x7f800000U&&(bb&0x7fffffffU)==0x7f800000U))return Float(0x7fc00000);return (float)(a/b);}
        static bool OddInteger(uint value)
        {uint exponent=(value>>23)&255;if(exponent<127||exponent>150)return false;uint bit=1U<<(150-(int)exponent);return (value&(bit-1))==0&&(value&bit)!=0;}
        static readonly ulong[] LogTable={
            0x3ff661ec79f8f3be,0xc02efec65b963019,0x3ff571ed4aaf883d,0xc02b0b6832d4fca4,0x3ff49539f0f010b0,0xc027418b0a1fb77b,0x3ff3c995b0b80385,0xc0239de91a6dcf7b,
            0x3ff30d190c8864a5,0xc0201d9bf3f2b631,0x3ff25e227b0b8ea0,0xc0197c1d1b3b7af0,0x3ff1bb4a4a1a343f,0xc012f9e393af3c9f,0x3ff12358f08ae5ba,0xc00960cbbf788d5c,
            0x3ff0953f419900a7,0xbffa6f9db6475fce,0x3ff0000000000000,0x0000000000000000,0x3fee608cfd9a47ac,0x400338ca9f24f53d,0x3feca4b31f026aa0,0x401476a9543891ba,
            0x3feb2036576afce6,0x401e840b4ac4e4d2,0x3fe9c2d163a1aa2d,0x40240645f0c6651c,0x3fe886e6037841ed,0x40288e9c2c1b9ff8,0x3fe767dcf5534862,0x402ce0a44eb17bcc};
        static readonly ulong[] ExpTable={
            0x3ff0000000000000,0x3fefd9b0d3158574,0x3fefb5586cf9890f,0x3fef9301d0125b51,0x3fef72b83c7d517b,0x3fef54873168b9aa,0x3fef387a6e756238,0x3fef1e9df51fdee1,
            0x3fef06fe0a31b715,0x3feef1a7373aa9cb,0x3feedea64c123422,0x3feece086061892d,0x3feebfdad5362a27,0x3feeb42b569d4f82,0x3feeab07dd485429,0x3feea47eb03a5585,
            0x3feea09e667f3bcd,0x3fee9f75e8ec5f74,0x3feea11473eb0187,0x3feea589994cce13,0x3feeace5422aa0db,0x3feeb737b0cdc5e5,0x3feec49182a3f090,0x3feed503b23e255d,
            0x3feee89f995ad3ad,0x3feeff76f2fb5e47,0x3fef199bdd85529c,0x3fef3720dcef9069,0x3fef5818dcfba487,0x3fef7c97337b9b5f,0x3fefa4afa2a490da,0x3fefd0765b6e4540};
        static void Parts(ulong bits,out BigInteger significand,out int exponent)
        {
            ulong field=(bits>>52)&2047,man=bits&0xfffffffffffffUL;
            exponent=field==0?-1074:(int)field-1075;significand=new BigInteger(field==0?man:man|0x10000000000000UL);if((bits&0x8000000000000000UL)!=0)significand=-significand;
        }
        static BigInteger Round(BigInteger value,int shift)
        {if(shift<=0)return value<<-shift;BigInteger whole=value>>shift,remain=value-(whole<<shift),half=BigInteger.One<<(shift-1);if(remain>half||(remain==half&&!whole.IsEven))whole++;return whole;}
        static double Fma(double a,double b,double c)
        {
            // The powf normal core reaches this only with finite binary64
            // operands; non-finite input cases are handled before table use.
            Parts(Bits(a),out var ma,out int ea);Parts(Bits(b),out var mb,out int eb);Parts(Bits(c),out var mc,out int ec);int ep=ea+eb,e=Math.Min(ep,ec);
            BigInteger sum=((ma*mb)<<(ep-e))+(mc<<(ec-e));if(sum.IsZero)return 0d;ulong sign=sum.Sign<0?0x8000000000000000UL:0;sum=BigInteger.Abs(sum);
            byte[] bytes=sum.ToByteArray();int top=bytes.Length-1;while(top>0&&bytes[top]==0)top--;int bit=7;while(bit>0&&(bytes[top]&(1<<bit))==0)bit--;int length=top*8+bit+1,unbiased=e+length-1;
            if(unbiased< -1022)return Double(sign|(ulong)Round(sum,-1074-e));
            BigInteger mantissa=Round(sum,length-53);if(mantissa==(BigInteger.One<<53)){mantissa>>=1;unbiased++;}
            if(unbiased>1023)return Double(sign|0x7ff0000000000000UL);
            return Double(sign|((ulong)(unbiased+1023)<<52)|((ulong)mantissa&0xfffffffffffffUL));
        }
        public static float Evaluate(float x,float y)=>Float(EvaluateBits(Bits(x),Bits(y)));
        // Raw words preserve signaling NaNs and untouched +1 returns across
        // managed floating-point argument conventions. The host graph itself
        // currently stores numeric values as double; this raw API states the
        // kernel boundary without claiming its host container preserves sNaN.
        public static uint EvaluateBits(uint ix,uint iy)
        {
            uint ay=iy&0x7fffffffU;
            if(ay==0x3f800000U){if((iy&0x80000000U)==0)return ix;if(Nan(ix))return Quiet(ix);}
            if(Nan(ix)||Nan(iy))
            {
                if(ay==0)return Nan(ix)&&(ix&0x00400000U)==0?Quiet(ix):0x3f800000U;
                if(ix==0x3f800000U&&Nan(iy))return (iy&0x00400000U)==0?Quiet(iy):0x3f800000U;
                if(Nan(iy))
                {
                    if(Nan(ix)&&(ix&0x00400000U)==0)return Quiet(ix);
                    if((iy&0x00400000U)==0)return Quiet(iy);
                    return Quiet(Nan(ix)?ix:iy);
                }
                uint result=Quiet(ix);if(ay<0x7f800000U&&(ix&0x80000000U)!=0&&OddInteger(iy))result^=0x80000000U;return result;
            }
            return Bits(EvaluateNonNaN(Float(ix),Float(iy)));
        }
        static float EvaluateNonNaN(float x,float y)
        {
            uint ix=Bits(x),iy=Bits(y);if((iy&0x7fffffffU)==0x3f800000U)return (iy&0x80000000U)==0?x:Divide(1f,x);
            uint signBias=0,twiceY=unchecked(iy<<1);
            if(unchecked(ix+0x80800000U)<0x81000000U||unchecked(twiceY+0x01000000U)<=0x01000000U)
            {
                if(unchecked(twiceY-1U)>=0xfeffffffU)
                {
                    if(twiceY==0)return ((unchecked(ix<<1)^0x00800000U)<0xff800001U)?1f:Add(x,y);
                    if(ix==0x3f800000U)return ((twiceY^0x00800000U)<0xff800001U)?1f:Add(x,y);
                    uint twiceX=unchecked(ix<<1);if(twiceX>0xff000000U||twiceY>=0xff000001U)return Add(x,y);if(twiceX==0x7f000000U)return 1f;
                    bool greater=(twiceX>>24)>126,positive=(int)iy>=0;return greater!=positive?0f:Multiply(y,y);
                }
                if(unchecked((ix<<1)-1U)>=0xfeffffffU)
                {
                    float squared=Multiply(x,x);if((ix&0x80000000U)!=0&&OddInteger(iy))squared=Float(Bits(squared)^0x80000000U);
                    return (iy&0x80000000U)!=0?Divide(1f,squared):squared;
                }
                if((ix&0x80000000U)!=0)
                {
                    uint exponent=(iy>>23)&255;if(exponent<127)return Float(0x7fc00000);
                    if(exponent<=150){uint bit=1U<<(150-(int)exponent);if((iy&(bit-1))!=0)return Float(0x7fc00000);if((iy&bit)!=0)signBias=0x10000;}
                    ix&=0x7fffffffU;
                }
                if((ix>>23)==0)ix=unchecked((Bits(Multiply(x,Float(0x4b000000)))&0x7fffffffU)-0x0b800000U);
            }
            uint temp=unchecked(ix-0x3f330000U),top=temp&0xff800000U;int table=(int)((temp>>19)&15)*2;double z=Float(unchecked(ix-top));
            double r=Fma(z,Double(LogTable[table]),-1d),baseline=Double(LogTable[table+1])+(double)((int)top>>18);
            double p=Fma(Double(0x402ec70a6ca7badd),r,Double(0xc037154748bef6c8));baseline=Fma(Double(0x40471547652ab82b),r,baseline);
            double r2=r*r,q=Fma(Double(0x40227616c9496e0b),r,Double(0xc0271969a075c67a));baseline=Fma(p,r2,baseline);double scaled=Fma(q,r2*r2,baseline)*(double)y;
            if((Bits(scaled)&0x7fff800000000000UL)>=0x40af800000000000UL)
            {if(scaled>Double(0x40afffffffd1d571))return Float((signBias==0?0U:0x80000000U)|0x7f800000U);if(scaled<=-4800d)return Float(signBias==0?0U:0x80000000U);}
            double rounded=Math.Round(scaled,MidpointRounding.AwayFromZero),delta=scaled-rounded;long integer=(long)rounded;
            ulong scale=unchecked(ExpTable[(int)(integer&31)]+((ulong)unchecked((uint)((int)integer+(int)signBias))<<47));
            p=Fma(Double(0x3ebc6af84b912394),delta,Double(0x3f2ebfce50fac4f3));q=Fma(Double(0x3f962e42ff0c52d6),delta,1d);
            return (float)(Fma(p,delta*delta,q)*Double(scale));
        }
    }
}
