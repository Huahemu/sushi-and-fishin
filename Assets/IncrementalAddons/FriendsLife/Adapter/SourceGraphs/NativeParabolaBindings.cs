using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Original018ddf98 snapshots the geometry once. Original018de098 updates
    // only Ratio, using separate f32 X/Z operations and fused negative Y step.
    public static class NativeParabolaBindings
    {
        public const string Source="Original main018ddf98 geometry entry /018de098 evaluation /018de120 update";
        public sealed class Geometry
        {
            public float startX,startZ,deltaX,deltaZ,rootSum,rootLeft,vertexY;
        }
        static uint Bits(float x)=>BitConverter.ToUInt32(BitConverter.GetBytes(x),0);
        static float Float(uint x)=>BitConverter.ToSingle(BitConverter.GetBytes(x),0);
        static void Finite(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))throw new OriginalBindingException("Unsupported native parabola input or intermediate: finite authored f32 subset required");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]static float Add(float a,float b)=>(float)(a+b);
        [MethodImpl(MethodImplOptions.NoInlining)]static float Sub(float a,float b)=>(float)(a-b);
        [MethodImpl(MethodImplOptions.NoInlining)]static float Mul(float a,float b)=>(float)(a*b);
        public static Geometry Begin(Vector3 start,Vector3 end,float vertex)
        {
            foreach(float value in new[]{start.x,start.y,start.z,end.x,end.y,end.z,vertex})Finite(value);
            // Original Enter reports error2 before any PosOut write when the
            // vertex is below an endpoint. No clamp or invented flat path.
            if(start.y>vertex||end.y>vertex)throw new OriginalBindingException("Native CalcParabolaQuery error2: VertexY is below an endpoint");
            float left=Sub(vertex,start.y),right=Sub(vertex,end.y);Finite(left);Finite(right);
            var g=new Geometry{startX=start.x,startZ=start.z,deltaX=Sub(end.x,start.x),deltaZ=Sub(end.z,start.z),rootLeft=(float)Math.Sqrt(left),vertexY=vertex};
            g.rootSum=Add(g.rootLeft,(float)Math.Sqrt(right));
            foreach(float value in new[]{g.deltaX,g.deltaZ,g.rootLeft,g.rootSum})Finite(value);
            return g;
        }
        public static Vector3 Evaluate(Geometry geometry,float ratio)
        {
            if(geometry==null)throw new ArgumentNullException(nameof(geometry));Finite(ratio);
            // FNMSUB(s4,s0,s2) is rootLeft-rootSum*ratio, rounded once.
            float offset=NativeFloat32.FusedMultiplyAdd(-geometry.rootSum,ratio,geometry.rootLeft);
            var result=new Vector3(Add(Mul(ratio,geometry.deltaX),geometry.startX),Sub(geometry.vertexY,Mul(offset,offset)),Add(Mul(ratio,geometry.deltaZ),geometry.startZ));
            foreach(float value in new[]{result.x,result.y,result.z})Finite(value);
            return result;
        }
        public static string Encode(Geometry g)=>string.Join(",",new[]{g.startX,g.startZ,g.deltaX,g.deltaZ,g.rootSum,g.rootLeft,g.vertexY}.Select(x=>Bits(x).ToString("x8",CultureInfo.InvariantCulture)));
        public static Geometry Decode(string saved)
        {
            var parts=(saved??"").Split(',');if(parts.Length!=7)throw new OriginalBindingException("Malformed persisted native parabola geometry");
            var v=new float[7];for(int i=0;i<v.Length;i++){if(parts[i].Length!=8||!uint.TryParse(parts[i],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint raw))throw new OriginalBindingException("Malformed native parabola f32 payload");v[i]=Float(raw);Finite(v[i]);}
            if(v[4]<0||v[5]<0||v[4]<v[5])throw new OriginalBindingException("Invalid persisted native parabola square-root geometry");
            return new Geometry{startX=v[0],startZ=v[1],deltaX=v[2],deltaZ=v[3],rootSum=v[4],rootLeft=v[5],vertexY=v[6]};
        }
        static Vector3 Vector(OriginalNodeContext c,string input)
        {
            var value=c.Input(input);if(value==null||value.kind!="Vector")throw new OriginalBindingException(c.Address+": missing actual "+input+" Vector3F input");return value.AsVector();
        }
        static float Scalar(OriginalNodeContext c,string input)
        {
            var value=c.Input(input);if(value==null||value.kind!="Number")throw new OriginalBindingException(c.Address+": missing actual "+input+" Float input");float number=(float)value.number;Finite(number);return number;
        }
        public static void Register(OriginalNodeRegistry registry)
        {
            registry.Query("CalcParabolaQuery",(c,o)=>
            {
                string key="native-parabola/v1/scope:"+c.Scope.id+"/node:"+c.Node.index;
                var saved=c.Read(key);Geometry g;
                if(saved!=null){if(saved.kind!="String")throw new OriginalBindingException(c.Address+": unknown saved native parabola state kind");g=Decode(saved.text);}
                else g=Begin(Vector(c,"Pos0"),Vector(c,"Pos1"),Scalar(c,"VertexY"));
                var result=Evaluate(g,Scalar(c,"Ratio"));
                // Raw f32 hex preserves cached operation results across the
                // original JsonUtility continuation, including signed zero.
                if(saved==null)c.Write(key,OriginalValue.String(Encode(g)));
                return OriginalValue.Vector(result);
            },Source+"; finite authored subset; saved scope-owned exact f32 geometry; current Ratio only on Update");
        }
    }
}
