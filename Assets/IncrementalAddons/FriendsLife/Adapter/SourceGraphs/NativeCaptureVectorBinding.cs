using System;using System.Globalization;using System.Linq;using System.Runtime.InteropServices;using UnityEngine;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Original00a00bc8 captures the input vector once at selector enter,
    // calls006dfc54 to copy its three raw words, and returns the authored
    // Child index. Neither update, host dt nor a producer replay is involved.
    public static class NativeCaptureVectorBinding
    {
        public const string Source="Original CaptureV3fSelector01966fdc/vtable0319d798; enter00a00bc8/vector copy006dfc54/Child binding00a00bfc";
        public const string RawKey="native-capture-v3/v1/raw";
        [StructLayout(LayoutKind.Explicit)]struct VectorBits
        {
            [FieldOffset(0)]public Vector3 vector;
            [FieldOffset(0)]public uint x;[FieldOffset(4)]public uint y;[FieldOffset(8)]public uint z;
        }
        public static string Encode(Vector3 vector)
        {var raw=new VectorBits{vector=vector};return string.Join(",",new[]{raw.x,raw.y,raw.z}.Select(v=>v.ToString("x8",CultureInfo.InvariantCulture)));}
        public static Vector3 Decode(string raw)
        {
            var words=(raw??"").Split(',');if(words.Length!=3)throw new OriginalBindingException("Malformed persisted original CaptureV3f raw vector");var result=new uint[3];
            for(int i=0;i<3;i++){if(words[i].Length!=8||!words[i].All(Uri.IsHexDigit)||!uint.TryParse(words[i],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint bits))throw new OriginalBindingException("Malformed persisted original CaptureV3f raw word");result[i]=bits;}
            return new VectorBits{x=result[0],y=result[1],z=result[2]}.vector;
        }
        static void Frame(OriginalNodeContext context)
        {
            if(context.Frame==null||context.Frame.node!=context.Node.index||!context.Frame.entered)throw new OriginalBindingException(context.Address+": original CaptureV3f activation is absent");
            var outputs=context.Node.outputs??Array.Empty<OriginalParameter>();if(outputs.Length!=1||outputs[0].name!="Vector3"||OriginalValue.Canonical(outputs[0].kind)!="Vector")throw new OriginalBindingException(context.Address+": unknown original CaptureV3f output schema");
        }
        public static bool Enter(OriginalNodeContext context)
        {
            Frame(context);var saved=context.Read(RawKey);
            if(saved!=null){if(saved.kind!="String")throw new OriginalBindingException(context.Address+": invalid persisted CaptureV3f state kind");Decode(saved.text);return false;}
            if(context.Frame.children.Count>0)throw new OriginalBindingException(context.Address+": started CaptureV3f child has no persisted raw capture");
            var input=context.Input("Vector3");if(input==null||input.kind!="Vector")throw new OriginalBindingException(context.Address+": actual Vector3F input is missing");
            context.Write(RawKey,OriginalValue.String(Encode(input.AsVector())));return true;
        }
        public static OriginalValue Output(OriginalNodeContext context,string output)
        {
            Frame(context);if(output!="Vector3")throw new OriginalBindingException(context.Address+": unknown original CaptureV3f output "+output);
            var saved=context.Read(RawKey);if(saved==null||saved.kind!="String")throw new OriginalBindingException(context.Address+": no original entry capture; producer is not replayed by an output query");
            return OriginalValue.Vector(Decode(saved.text));
        }
    }
}
