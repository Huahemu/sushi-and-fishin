using System;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Original released System.actor__HeadSystemParam input owner. These two
    // explicit sparse entries exist in that source file; no profile slider,
    // personality or a guessed speed=1 supplies the query.
    public static class C004NativeHeadParameterBindings
    {
        public const string Source="Original MotionSpeedTypeQuery009ea42c/enum order029ecb18/typed HeadSystemParam accessor0003d6dc; released System.actor__HeadSystemParam MotionSpeeds Normal[0]=-1 and Quick[1]=10000";
        [Serializable] sealed class Entry{public int index;public uint nativeHash;public string rawF32,name;}
        [Serializable] sealed class InputData{public int schema;public string source,owner;public Entry[] motionSpeeds;}
        [StructLayout(LayoutKind.Explicit)]struct Word{[FieldOffset(0)]public uint bits;[FieldOffset(0)]public float value;}
        static InputData data;
        static InputData Data
        {
            get
            {
                if(data!=null)return data;var text=Resources.Load<TextAsset>("FriendsLife/C004Native/Parameters/HeadSystem");
                if(!text)throw new OriginalBindingException("Actual original HeadSystemParam resource is unavailable");
                var value=JsonUtility.FromJson<InputData>(text.text);
                if(value==null||value.schema!=1||value.owner!="System.actor__HeadSystemParam"||value.source!="Parameter/HeadSystemParam/System.actor__HeadSystemParam.bgyml"||value.motionSpeeds==null||value.motionSpeeds.Length!=2||
                   value.motionSpeeds.Count(x=>x.index==0&&x.nativeHash==0x390b012d&&x.rawF32=="bf800000")!=1||value.motionSpeeds.Count(x=>x.index==1&&x.nativeHash==0x05d11e4c&&x.rawF32=="461c4000")!=1)
                    throw new OriginalBindingException("Original released HeadSystemParam input identity/fields differ");
                return data=value;
            }
        }
        public static uint SelectRaw(int type,uint normal,uint quick)
        {
            if((uint)type>=2)throw new OriginalBindingException("Original MotionSpeedTypeQuery error2: enum index outside0..1");
            return type==0?normal:quick;
        }
        public static void Register(OriginalNodeRegistry registry)
        {
            registry.Query("MotionSpeedTypeQuery",(context,output)=>
            {
                if(output!="MotionSpeed")throw new OriginalBindingException(context.Address+": original MotionSpeed output is absent: "+output);
                var input=context.Input("MotionSpeedType");
                if(input.kind!="Number"||double.IsNaN(input.number)||double.IsInfinity(input.number)||Math.Truncate(input.number)!=input.number||input.number<int.MinValue||input.number>int.MaxValue)
                    throw new OriginalBindingException(context.Address+": exact original S32 MotionSpeedType required");
                int type=(int)input.number;var source=Data;
                uint normal=uint.Parse(source.motionSpeeds.Single(x=>x.index==0).rawF32,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
                uint quick=uint.Parse(source.motionSpeeds.Single(x=>x.index==1).rawF32,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
                uint raw=SelectRaw(type,normal,quick);return OriginalValue.Number(new Word{bits=raw}.value);
            },Source+"; explicit original released system-parameter domain; native debug/hot-reload override chains are not claimed");
        }
    }
}
