using System;
using System.Numerics;
using System.Text;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class NativeExpressionBinary
    {
        public int version;
        public string exb;
    }

    // This value is independent of Unity and the host graph serializer. The
    // parent graph runner converts its authored raw input and the returned value.
    public struct NativeExpressionValue
    {
        public const int None=0,BoolType=2,IntType=3,FloatType=5,VectorType=7;
        public int type,integer;
        public bool boolean;
        public float x,y,z;
        public static NativeExpressionValue Bool(bool value)=>new NativeExpressionValue{type=BoolType,boolean=value};
        public static NativeExpressionValue Int(int value)=>new NativeExpressionValue{type=IntType,integer=value};
        public static NativeExpressionValue Float(float value)=>new NativeExpressionValue{type=FloatType,x=value};
        public static NativeExpressionValue Vector(float x,float y,float z)=>new NativeExpressionValue{type=VectorType,x=x,y=y,z=z};
    }

    public sealed class NativeExpressionProgram
    {
        internal struct Command
        {
            internal int first,count,staticSize,input,output;
        }
        internal byte[] bytes;
        internal Command[] commands;
        internal string[] signatures;
        internal int instructions,parameters,parameterSize;
        public int Count=>commands.Length;
    }

    // Bounded to the original 13 expressions used by the selected 12 AINB nodes.
    // Unsupported source/type/opcode/function throws; no value is synthesized.
    public static class NativeExpressionRuntime
    {
        public const string Source="Original EXB v3 bytes; BBFloat023565c0/BBBool02356500; Cast02354150; Abs02353e10; Pow0235425c/02353d34; Normalize00a98ab0/0052eba8";
        static InvalidOperationException Fault(string message)=>new InvalidOperationException("Original EXB: "+message);
        static void Range(byte[] bytes,int offset,int length)
        {
            if(offset<0||length<0||offset>bytes.Length-length)throw Fault("binary/memory bounds at "+offset+" + "+length);
        }
        static int U16(byte[] bytes,int at){Range(bytes,at,2);return bytes[at]|bytes[at+1]<<8;}
        static int I32(byte[] bytes,int at){Range(bytes,at,4);return bytes[at]|bytes[at+1]<<8|bytes[at+2]<<16|bytes[at+3]<<24;}
        static float F32(byte[] bytes,int at){Range(bytes,at,4);return BitConverter.ToSingle(bytes,at);}
        static void PutInt(byte[] bytes,int at,int value)
        {
            Range(bytes,at,4);bytes[at]=(byte)value;bytes[at+1]=(byte)(value>>8);bytes[at+2]=(byte)(value>>16);bytes[at+3]=(byte)(value>>24);
        }
        static void PutFloat(byte[] bytes,int at,float value)
        {
            var raw=BitConverter.GetBytes(value);Range(bytes,at,4);Buffer.BlockCopy(raw,0,bytes,at,4);
        }
        static int Width(int type)
        {
            switch(type){case 2:return 1;case 3:case 5:return 4;case 7:return 12;default:throw Fault("unsupported v3 data type "+type);}
        }
        static NativeExpressionValue ReadValue(byte[] bytes,int at,int type)
        {
            Range(bytes,at,Width(type));
            switch(type)
            {
                case 2:return NativeExpressionValue.Bool(bytes[at]!=0);
                case 3:return NativeExpressionValue.Int(I32(bytes,at));
                case 5:return NativeExpressionValue.Float(F32(bytes,at));
                case 7:return NativeExpressionValue.Vector(F32(bytes,at),F32(bytes,at+4),F32(bytes,at+8));
                default:throw Fault("unreachable type");
            }
        }
        static void WriteValue(byte[] bytes,int at,NativeExpressionValue value)
        {
            Range(bytes,at,Width(value.type));
            switch(value.type)
            {
                case 2:bytes[at]=(byte)(value.boolean?1:0);break;
                case 3:PutInt(bytes,at,value.integer);break;
                case 5:PutFloat(bytes,at,value.x);break;
                case 7:PutFloat(bytes,at,value.x);PutFloat(bytes,at+4,value.y);PutFloat(bytes,at+8,value.z);break;
            }
        }

        public static NativeExpressionProgram Compile(NativeExpressionBinary binary)
        {
            if(binary==null||binary.version!=3||string.IsNullOrEmpty(binary.exb))throw Fault("original v3 EXB is unavailable");
            byte[] bytes;
            try{bytes=Convert.FromBase64String(binary.exb);}catch(Exception exception){throw Fault("invalid original base64: "+exception.Message);}
            Range(bytes,0,44);
            if(I32(bytes,0)!=0x20425845||I32(bytes,4)!=3)throw Fault("magic/version differs from source v3");
            int ci=I32(bytes,24),ct=I32(bytes,28),st=I32(bytes,32),pr=I32(bytes,36),sp=I32(bytes,40);
            if(!(44<=ci&&ci<=ct&&ct<=st&&st<=pr&&pr<=sp&&sp<=bytes.Length))throw Fault("section order/bounds");
            int count=I32(bytes,ci),instructionCount=I32(bytes,ct),signatureCount=I32(bytes,st);
            if(count<0||count>4096||instructionCount<0||instructionCount>65536||signatureCount<0||signatureCount>4096)throw Fault("section count");
            if(ci+4+count*28!=ct||ct+4+instructionCount*8!=st||st+4+signatureCount*4!=pr)throw Fault("v3 section entry sizes");
            var result=new NativeExpressionProgram{bytes=bytes,commands=new NativeExpressionProgram.Command[count],signatures=new string[signatureCount],instructions=ct+4,parameters=pr,parameterSize=sp-pr};
            for(int index=0;index<signatureCount;index++)
            {
                int at=sp+I32(bytes,st+4+index*4);Range(bytes,at,1);int end=at;
                while(end<bytes.Length&&bytes[end]!=0)end++;
                if(end==bytes.Length)throw Fault("unterminated function signature");
                result.signatures[index]=Encoding.UTF8.GetString(bytes,at,end-at);
            }
            for(int index=0;index<count;index++)
            {
                int at=ci+4+index*28;
                if(I32(bytes,at)!=-1||I32(bytes,at+4)!=0||U16(bytes,at+20)!=0||U16(bytes,at+22)!=0)
                    throw Fault("pre-command or scratch owner requires its original execution context");
                var command=new NativeExpressionProgram.Command{first=I32(bytes,at+8),count=I32(bytes,at+12),staticSize=I32(bytes,at+16),output=U16(bytes,at+24),input=U16(bytes,at+26)};
                if(command.first<0||command.count<1||command.first>instructionCount-command.count||command.staticSize<0||command.staticSize>I32(bytes,8))throw Fault("command extent/storage");
                if(bytes[result.instructions+(command.first+command.count-1)*8]!=1)throw Fault("missing authored terminator");
                Width(command.output);if(command.input!=1)Width(command.input);
                result.commands[index]=command;
            }
            return result;
        }

        sealed class Execution
        {
            internal NativeExpressionProgram program;
            internal byte[] storage,input,output;
            internal bool[] initialized;
            internal bool outputWritten;
            internal Func<int,float> readFloat;
            internal Func<int,bool> readBool;
            internal NativeExpressionValue Read(int source,int offset,int type)
            {
                int width=Width(type);
                switch(source)
                {
                    case 0:
                        if(type==3)return NativeExpressionValue.Int(offset);
                        if(type==2)return NativeExpressionValue.Bool(offset!=0);
                        throw Fault("unverified immediate scalar type "+type);
                    case 2:
                        Range(storage,offset,width);
                        for(int index=offset;index<offset+width;index++)if(!initialized[index])throw Fault("uninitialized original static memory at "+index);
                        return ReadValue(storage,offset,type);
                    case 3:
                        if(offset<0||offset>program.parameterSize-width)throw Fault("parameter region bounds");
                        return ReadValue(program.bytes,program.parameters+offset,type);
                    case 6:
                        if(input==null)throw Fault("authored input owner was not supplied");
                        return ReadValue(input,offset,type);
                    default:throw Fault("unsupported read source "+source);
                }
            }
            internal void Write(int source,int offset,NativeExpressionValue value)
            {
                if(source==2)
                {
                    WriteValue(storage,offset,value);
                    for(int index=offset;index<offset+Width(value.type);index++)initialized[index]=true;
                }
                else if(source==5){WriteValue(output,offset,value);outputWritten=true;}
                else throw Fault("unsupported write source "+source);
            }
            internal void Function(int at,string signature,int resultType)
            {
                NativeExpressionValue value;
                switch(signature)
                {
                    case "GetBBCodeFloat( Float, Int )":
                        if(resultType!=5||readFloat==null)throw Fault("original typed Float blackboard owner is unavailable");
                        value=NativeExpressionValue.Float(readFloat(Read(2,at,3).integer));break;
                    case "GetBBCodeBool( Bool, Int )":
                        if(resultType!=2||readBool==null)throw Fault("original typed Bool blackboard owner is unavailable");
                        value=NativeExpressionValue.Bool(readBool(Read(2,at,3).integer));break;
                    case "CastToFloat( Float, Int )":
                        if(resultType!=5)throw Fault("CastToFloat result type");
                        value=NativeExpressionValue.Float((float)Read(2,at,3).integer);break;
                    case "Abs( Float, Float )":
                        if(resultType!=5)throw Fault("Abs result type");
                        value=NativeExpressionValue.Float(NativeExpressionFloatMath.Abs(Read(2,at,5).x));break;
                    case "Pow( Float, Float, Float )":
                        if(resultType!=5)throw Fault("Pow result type");
                        value=NativeExpressionValue.Float(NativeSdkPowf.Evaluate(Read(2,at,5).x,Read(2,at+4,5).x));break;
                    case "Normalize( Vector3F, Vector3F )":
                        if(resultType!=7)throw Fault("Normalize result type");
                        value=NativeExpressionFloatMath.Normalize(Read(2,at,7));break;
                    default:throw Fault("unsupported original function signature "+signature);
                }
                Write(2,at,value);
            }
        }

        public static NativeExpressionValue Evaluate(NativeExpressionProgram program,int expressionIndex,NativeExpressionValue input,Func<int,float> readBlackboardFloat=null,Func<int,bool> readBlackboardBool=null)
        {
            if(program==null||expressionIndex<0||expressionIndex>=program.commands.Length)throw Fault("expression index/owner unavailable");
            var command=program.commands[expressionIndex];
            var execution=new Execution{program=program,storage=new byte[command.staticSize],initialized=new bool[command.staticSize],output=new byte[Math.Max(4,Width(command.output))],readFloat=readBlackboardFloat,readBool=readBlackboardBool};
            if(command.input!=1)
            {
                if(input.type!=command.input)throw Fault("authored raw input type does not match command input");
                execution.input=new byte[Math.Max(4,Width(input.type))];WriteValue(execution.input,0,input);
            }
            for(int index=command.first;index<command.first+command.count;index++)
            {
                int at=program.instructions+index*8,opcode=program.bytes[at],type=program.bytes[at+1];
                if(opcode==1)
                {
                    if(!execution.outputWritten)throw Fault("expression terminated without its authored output store");
                    return ReadValue(execution.output,0,command.output);
                }
                if(opcode==27)
                {
                    int signature=I32(program.bytes,at+4);
                    if(signature<0||signature>=program.signatures.Length)throw Fault("function signature index");
                    execution.Function(U16(program.bytes,at+2),program.signatures[signature],type);continue;
                }
                int lhs=program.bytes[at+2],rhs=program.bytes[at+3],li=U16(program.bytes,at+4),ri=U16(program.bytes,at+6);
                NativeExpressionValue value;
                if(opcode==2)value=execution.Read(rhs,ri,type);
                else
                {
                    value=execution.Read(lhs,li,type);
                    if(opcode==3)
                    {
                        if(type==3)value.integer=unchecked(-value.integer);
                        else if(type==5)value.x=-value.x;
                        else if(type==7){value.x=-value.x;value.y=-value.y;value.z=-value.z;}
                        else throw Fault("unsupported Negate type "+type);
                    }
                    else if(opcode==4&&type==2)value.boolean=!value.boolean;
                    else if(opcode==12&&type==7)
                    {
                        float scalar=execution.Read(rhs,ri,5).x;
                        value.x=(float)(value.x*scalar);value.y=(float)(value.y*scalar);value.z=(float)(value.z*scalar);
                    }
                    else if(type==5&&(opcode==5||opcode==6||opcode==7))
                    {
                        float other=execution.Read(rhs,ri,5).x;
                        value.x=opcode==5?(float)(value.x+other):opcode==6?(float)(value.x-other):(float)(value.x*other);
                    }
                    else throw Fault("unsupported original opcode/type "+opcode+"/"+type);
                }
                execution.Write(lhs,li,value);
            }
            throw Fault("authored command did not terminate");
        }
    }

    static class NativeExpressionFloatMath
    {
        static uint Bits(float value)=>BitConverter.ToUInt32(BitConverter.GetBytes(value),0);
        static float Float(uint value)=>BitConverter.ToSingle(BitConverter.GetBytes(value),0);
        // 02353e10 is a comparison/select, not FABS: it preserves -0 and
        // negates an unordered NaN operand. Do not replace this with Math.Abs.
        internal static float Abs(float value)=>value>=0f?value:-value;

        // Exact finite binary32 fused multiply-add, using an integer significand
        // and one round-to-nearest/even step. Original Normalize uses FMADD.
        static void Parts(uint bits,out BigInteger significand,out int exponent)
        {
            uint field=(bits>>23)&255,man=bits&0x7fffff;
            exponent=field==0?-149:(int)field-150;
            significand=new BigInteger(field==0?man:man|0x800000U);
            if((bits&0x80000000U)!=0)significand=-significand;
        }
        static BigInteger Round(BigInteger value,int right)
        {
            if(right<=0)return value<<-right;
            BigInteger whole=value>>right,remainder=value-(whole<<right),half=BigInteger.One<<(right-1);
            if(remainder>half||(remainder==half&&!whole.IsEven))whole++;
            return whole;
        }
        static float Fma(float a,float b,float c)
        {
            uint aa=Bits(a),bb=Bits(b),cc=Bits(c);
            if((aa&0x7f800000U)==0x7f800000U||(bb&0x7f800000U)==0x7f800000U||(cc&0x7f800000U)==0x7f800000U)
                return (float)((double)a*b+c);
            Parts(aa,out var ma,out int ea);Parts(bb,out var mb,out int eb);Parts(cc,out var mc,out int ec);
            int ep=ea+eb,e=Math.Min(ep,ec);
            BigInteger sum=((ma*mb)<<(ep-e))+(mc<<(ec-e));
            if(sum.IsZero)return 0f;
            uint sign=sum.Sign<0?0x80000000U:0U;sum=BigInteger.Abs(sum);
            byte[] raw=sum.ToByteArray();int top=raw.Length-1;
            while(top>0&&raw[top]==0)top--;
            int bit=7;while(bit>0&&(raw[top]&(1<<bit))==0)bit--;
            int length=top*8+bit+1,unbiased=e+length-1;
            if(unbiased < -126)
            {
                BigInteger sub=Round(sum,-149-e);
                return Float(sign|(uint)sub);
            }
            BigInteger mantissa=Round(sum,length-24);
            if(mantissa==(BigInteger.One<<24)){mantissa>>=1;unbiased++;}
            if(unbiased>127)return Float(sign|0x7f800000U);
            return Float(sign|((uint)(unbiased+127)<<23)|((uint)mantissa&0x7fffffU));
        }
        internal static NativeExpressionValue Normalize(NativeExpressionValue value)
        {
            float x=value.x,y=value.y,z=value.z;
            float squared=(float)(y*y);squared=Fma(x,x,squared);squared=Fma(z,z,squared);
            float length=(float)Math.Sqrt(squared);
            if(length<=Float(0x29cccccdU))return NativeExpressionValue.Vector(0,0,0);
            if(float.IsPositiveInfinity(length))
            {
                float scale=Float(0x1f13cd3bU);x=(float)(x*scale);y=(float)(y*scale);z=(float)(z*scale);
                squared=(float)(y*y);squared=Fma(x,x,squared);squared=Fma(z,z,squared);
                length=(float)Math.Sqrt(squared);
            }
            float inverse=(float)(1f/length);
            return NativeExpressionValue.Vector((float)(x*inverse),(float)(y*inverse),(float)(z*inverse));
        }
    }
}
