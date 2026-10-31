using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [Serializable] public sealed class C004NativeCameraPoseData
    {
        public int schema=1;
        public string worldId,episodeId,executionKey,sourceAddress,phase="Captured";
        public Vector3 position,inputLookAt,lookAt,up;
        public C004NativeCameraVector rawPosition,rawInputLookAt,rawLookAt;
        public bool onlyOnEnter;
    }
    [Serializable] public sealed class C004NativeCameraVector
    {
        public uint x,y,z;
        [StructLayout(LayoutKind.Explicit)] struct VectorWords
        {
            [FieldOffset(0)]public Vector3 vector;
            [FieldOffset(0)]public uint x;[FieldOffset(4)]public uint y;[FieldOffset(8)]public uint z;
        }
        [StructLayout(LayoutKind.Explicit)] struct FloatWord{[FieldOffset(0)]public uint raw;[FieldOffset(0)]public float value;}
        public static C004NativeCameraVector FromVector(Vector3 value){var words=new VectorWords{vector=value};return new C004NativeCameraVector{x=words.x,y=words.y,z=words.z};}
        public Vector3 ToVector()=>new VectorWords{x=x,y=y,z=z}.vector;
        internal static float Float(uint raw)=>new FloatWord{raw=raw}.value;
        internal static uint Bits(float value)=>new FloatWord{value=value}.raw;
    }
    public static class C004NativeCameraPoseBody
    {
        public const string Source="Original SetCameraLookAtAndPosAction getter01874ed0/entry01874bbc/publisher01874c5c/update01874da8: exact component FCMP equality adjusts only lookAt.z by -1; copied source position/look target/up; Demo camera-position feedback0174f7ac";
        public static Vector3 AdjustLookAt(Vector3 cameraPosition,Vector3 inputLookAt)
        {
            // Vector3.operator== is approximate. Original FCMP is exact and
            // unordered for NaN; use its component comparisons instead.
            if(cameraPosition.x==inputLookAt.x&&cameraPosition.y==inputLookAt.y&&cameraPosition.z==inputLookAt.z)
                inputLookAt.z=inputLookAt.z-1f;
            return inputLookAt;
        }
        public static C004NativeCameraVector AdjustRaw(C004NativeCameraVector cameraPosition,C004NativeCameraVector inputLookAt)
        {
            if(cameraPosition==null||inputLookAt==null)throw new OriginalBindingException("Exact original camera raw vector words required");
            var result=new C004NativeCameraVector{x=inputLookAt.x,y=inputLookAt.y,z=inputLookAt.z};
            if(C004NativeCameraVector.Float(cameraPosition.x)==C004NativeCameraVector.Float(inputLookAt.x)&&
               C004NativeCameraVector.Float(cameraPosition.y)==C004NativeCameraVector.Float(inputLookAt.y)&&
               C004NativeCameraVector.Float(cameraPosition.z)==C004NativeCameraVector.Float(inputLookAt.z))
                result.z=C004NativeCameraVector.Bits(C004NativeCameraVector.Float(inputLookAt.z)-1f);
            return result;
        }
    }
}
