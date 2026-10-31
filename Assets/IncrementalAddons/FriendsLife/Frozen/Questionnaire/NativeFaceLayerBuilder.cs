using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace TomodachiLife
{
    public enum NativeFaceLayerSlot
    {
        MustacheLeft, MustacheRight, Mouth, MouthAlternate, EyebrowLeft, EyebrowRight,
        HighlightLeft, HighlightRight, EyelidUpperLeft, EyelidUpperRight,
        EyelidLowerLeft, EyelidLowerRight, EyelashUpperLeft, EyelashUpperRight,
        EyelashLowerLeft, EyelashLowerRight, EyeLeft, EyeRight, EyeAlternateLeft, EyeAlternateRight,
        Mole, Clear
    }

    // Direct descriptor branches of0016facc. Descriptor fields are x,y,width,
    // height,angle,relativeScale,relativeAngle,pivotX,pivotY. Faceline wrinkles
    // and makeup belong to the head's separate render target, never these slots.
    public static class NativeFaceLayerBuilder
    {
        public static float[] Mustache(int rawY,int rawFlip,int rawScale,int rawAspect,bool right)
        {
            int y=Math.Min(rawY,16),scale=Math.Min(rawScale,8),aspect=Math.Min(rawAspect,4);
            float step=Fma(.4000000059604645f,scale,1);
            float width=step*4.5f,height=step*9*(.6399999856948853f+.11999999731779099f*aspect);
            float cy=Fma(1.0760942697525024f,y,31.763553619384766f);
            float angle=(rawFlip==0?0:180)*(right?-1:1);
            return new[]{32f,cy,width,height,angle,1f,0f,32f,cy};
        }
        public static float[] Mole(int rawX,int rawY,int rawScale)
        {
            int x=Math.Min(rawX,16),y=Math.Min(rawY,30),scale=Math.Min(rawScale,8);
            float cx=Fma(1.7792292833328247f,x,17.766164779663086f);
            float cy=Fma(1.0760942697525024f,y,17.959861755371094f);
            float size=Fma(.4000000059604645f,scale,1);
            return new[]{cx,cy,size,size,0f,1f,0f,cx,cy};
        }
        private static float Fma(float a,float b,float c)=>(float)((double)a*b+c);
        public static float[] EyeRelative(NativeFaceLayout math,string kind,float[] eye,
            int rawX,int rawY,int rawRotation,int rawScale,int rawAspect,int referenceRotation,
            int offsetRotate,float[] axis,float[] eyeAxis,bool right,bool limitUpperLashWidth=false,float partSizeRatio=1)
        {
            if(eye==null || eye.Length!=9 || axis==null || eyeAxis==null)throw new ArgumentException("Explicit native eye/part accessor data required.");
            int offset=offsetRotate<0?0:32-offsetRotate;
            int rotation=Math.Min(rawRotation,31),scale=Math.Min(rawScale,14),aspect=Math.Min(rawAspect,6);
            float initial=((referenceRotation+offset)&31)*11.25f,angle=((rotation+offset)&31)*11.25f;
            float delta=angle-initial;
            var d=new float[9];float ax=axis[0],ay=axis[1];
            if(kind=="Highlight")
            {
                int x=Math.Min(rawX,32),y=Math.Min(rawY,32);
                float basis=limitUpperLashWidth?Math.Min(eye[2],eye[3]/.8421052694320679f):eye[2];
                float width=partSizeRatio*Fma(.034547898918390274f,scale,.15546560287475586f)*basis;
                d[0]=eye[7]+((x*.03125f-.5f)*eye[2])/-1.1240822076797485f;
                d[1]=eye[8]+((y*.03125f-.5f)*eye[3])/1.077972412109375f;
                d[2]=width;d[3]=(.64f+.12f*aspect)*width;d[4]=right?-initial:initial;d[5]=1;
            }
            else
            {
                bool upperLash=kind=="EyelashUpper",lowerLash=kind=="EyelashLower";
                if(kind!="EyelidUpper"&&kind!="EyelidLower"&&!lowerLash&&!upperLash)throw new ArgumentException("Unknown native eye-relative part "+kind);
                int x=Math.Min(rawX,31),y=Math.Min(rawY,lowerLash||upperLash?36:31);
                float factor=lowerLash||upperLash?(scale+8)*.03999999910593033f:(scale+5)*.0357142873108387f;
                math.SampleTable(eye[4],out float cosine,out float sine);
                float dx=eye[2]*(x/31f-eyeAxis[0])*(right?1:-1);
                float dy=-(eye[3]*(eyeAxis[1]+(y-(upperLash?5:0))/31f-.5f));
                d[0]=eye[7]+(sine*dy+cosine*dx)/1.1240822076797485f;
                d[1]=eye[8]+(cosine*dy-sine*dx)/-1.077972412109375f;
                d[2]=partSizeRatio*factor*eye[2]*(right?-1:1);
                d[3]=(.64f+.12f*aspect)*.8421052694320679f*d[2]*(right?-1:1);
                d[4]=right?-initial:initial;d[5]=eye[3]/(eye[2]*.8421052694320679f);d[6]=eye[4];
            }
            Pivot(math,d,right?ax:-ax,ay,right?-delta:delta);
            return d;
        }
        //001721e0: preserve the authored expression pivot while adding delta.
        public static void Pivot(NativeFaceLayout math,float[] d,float axisX,float axisY,float delta)
        {
            float ax=d[2]*axisX,ay=d[3]*axisY,old=d[4];
            if(ax==0&&ay==0){d[4]=old+delta;d[7]=d[0];d[8]=d[1];return;}
            math.SampleTable(old,out float c,out float s);
            float px=d[0]+(ay*s+c*ax)/1.1240822076797485f;
            float py=d[1]-(ay*c-s*ax)/1.077972412109375f;
            d[4]=old+delta;d[7]=px;d[8]=py;
            math.SampleTable(d[4],out c,out s);
            d[0]=px-(ay*s+c*ax)/1.1240822076797485f;
            d[1]=py+(ay*c-s*ax)/1.077972412109375f;
        }
#if UNITY_5_3_OR_NEWER
        public static string[] AvailableExpressionNames()=>NativeFaceSourceData.Load().expressions.Select(p=>p.id).ToArray();
        public static NativeFaceLayerInput[] ApplyExpression(NativeFaceLayerInput[] neutral,AvatarCatalog catalog,string name)
        {
            var data=NativeFaceSourceData.Load();var expression=data.expressions.FirstOrDefault(e=>e.id==name);
            if(expression==null)throw new ArgumentException("Unknown source MiiExpression "+name);
            var output=new List<NativeFaceLayerInput>();
            foreach(var input in neutral)
            {
                var old=input.raw;string partName=old.kind=="Eye"?(old.right?expression.EyePartsRName:expression.EyePartsLName):old.kind=="Eyebrow"?(old.right?expression.EyebrowPartsRName:expression.EyebrowPartsLName):expression.MouthPartsName;
                string locationName=old.kind=="Eye"?expression.EyeLocationName:old.kind=="Eyebrow"?expression.EyebrowLocationName:expression.MouthLocationName;
                AvatarPart part=string.IsNullOrEmpty(partName)?null:catalog.parts.FirstOrDefault(p=>p.id==partName);
                if(!string.IsNullOrEmpty(partName)&&part==null)throw new InvalidOperationException("Source expression part is unavailable: "+partName);
                if(part!=null&&string.IsNullOrEmpty(part.texture))continue;
                var p=new NativeFaceRawParameters {kind=old.kind,x=old.x,y=old.y,scale=old.scale,aspect=old.aspect,rotation=old.rotation,
                    axis=part==null?old.axis:part.expressionAxis,offsetRotate=part==null?old.offsetRotate:part.NativeRotationOffset,right=old.right,
                    rawSource=old.rawSource+"; explicit source MiiExpression "+name+" / MiiPartsLocation "+locationName};
                var location=data.locations.FirstOrDefault(v=>v.id==locationName);
                if(location!=null)
                {
                    p.x=Byte(location.IsOffsetPosition?p.x+location.PositionX:location.PositionX);p.y=Byte(location.IsOffsetPosition?p.y+location.PositionY:location.PositionY);
                    p.rotation=Byte(location.IsOffsetRotate?p.rotation+location.Rotate:location.Rotate);
                    p.scale=Byte(location.IsOffsetScale?p.scale+location.Scale:location.Scale);p.aspect=Byte(location.IsOffsetAspect?p.aspect+location.Aspect:location.Aspect);
                }
                output.Add(new NativeFaceLayerInput {id=input.id+" expression "+name,textureResource=part==null?input.textureResource:part.texture,raw=p,
                    modulate=input.modulate,rawParametersVerified=false,gameInitialPresetVerified=false});
            }
            return output.ToArray();
        }
        private static int Byte(int value)=>unchecked((byte)value);
        public static NativeFaceLayerInput[] AppendOptional(NativeFaceLayerInput[] neutral,ResidentAppearance appearance,AvatarCatalog catalog)
        {
            var result=new List<NativeFaceLayerInput>(neutral);
            var source=NativeFaceSourceData.Load();var eyeSelection=appearance.Selection("Eye");
            var eyeSource=eyeSelection==null?null:source.parts.FirstOrDefault(p=>p.id==eyeSelection.part);
            var accessory=eyeSource==null?null:source.eyeAccessories.FirstOrDefault(p=>p.id==eyeSource.eyeAccessory);
            var math=NativeFaceLayout.Load();
            foreach(string kind in new[]{"Highlight","EyelidUpper","EyelidLower","EyelashUpper","EyelashLower"})
            {
                var selected=appearance.Selection(kind);int[] offsets=accessory?.Offsets(kind);
                AvatarPart part=selected==null?catalog.parts.FirstOrDefault(p=>p.category==kind&&p.nativeIndex==(accessory?.DefaultIndex(kind)??0)):catalog.Part(selected.part);
                if(part==null||string.IsNullOrEmpty(part.texture))continue;
                int[] raw=selected==null?new int[5]:new[]{selected.positionX,selected.positionY,selected.rotation,selected.scale,selected.aspect};
                if(offsets!=null)for(int i=0;i<5;i++)raw[i]=Math.Max(0,(raw[i]+offsets[i])%(i==0?kind=="Highlight"?33:32:i==1?kind=="Highlight"?33:kind.StartsWith("Eyelash")?37:32:i==2?32:i==3?15:7));
                for(int side=0;side<2;side++)
                {
                    var eye=neutral.FirstOrDefault(p=>p.raw.kind=="Eye"&&p.raw.right==(side==1));if(eye==null)continue;
                    if(!math.TryEvaluateNeutral(eye.raw,"NativeFlippedY",out var layout,out string error))throw new InvalidOperationException(error);
                    var descriptor=EyeRelative(math,kind,layout.native9,raw[0],raw[1],raw[2],raw[3],raw[4],selected?.rotation??0,part.NativeRotationOffset,part.expressionAxis,eye.raw.axis,side==1);
                    float value=kind=="Highlight"?1:0;
                    result.Add(new NativeFaceLayerInput {id=part.id+" native relative "+side,textureResource=part.texture,inspectionDescriptor=descriptor,inspectionQuadIndex=0,
                        raw=new NativeFaceRawParameters {kind=kind,right=side==1,rawSource="0016facc relative eye branch; source MiiEyeAccessoryParam offsets, accessor01e68ac8..01e68f74 modulo domains; caller part values"},rawParametersVerified=true,
                        modulate=new NativeMiiMaskParameters {mode=3,color0=new[]{value,value,value,1f},color1=new[]{0f,0f,0f,1f},color2=new[]{0f,0f,0f,1f},bindingVerified=true,colorsVerified=true,source="0016facc cases6..15 source white Highlight/black eye accessory mask bindings"} });
                }
            }
            foreach(string kind in new[]{"Mustache","Mole"})
            {
                var selected=appearance.Selection(kind);if(selected==null)continue;
                var part=catalog.Part(selected.part);if(string.IsNullOrEmpty(part.texture))continue;
                bool mole=kind=="Mole";Color color=mole?
                    new Color(.07058829814195633f,.0588236004114151f,.0588236004114151f,1):catalog.Color("common",selected.color,false);
                for(int side=0;side<(mole?1:2);side++)
                    result.Add(new NativeFaceLayerInput {id=part.id+" native slot "+(mole?20:side),textureResource=part.texture,
                        raw=new NativeFaceRawParameters {kind=kind,right=side==1,rawSource=mole?"001764f0 neutral descriptor; SDK MoleColor GammaType1":"0016facc cases0/1 neutral Mustache descriptor"},
                        inspectionDescriptor=mole?Mole(selected.positionX,selected.positionY,selected.scale):Mustache(selected.positionY,selected.rotation,selected.scale,selected.aspect,side==1),
                        inspectionQuadIndex=mole?0:side==0?1:2,rawParametersVerified=true,
                        modulate=new NativeMiiMaskParameters {mode=3,color0=new[]{color.r,color.g,color.b,1f},color1=new[]{0f,0f,0f,1f},color2=new[]{0f,0f,0f,1f},
                            bindingVerified=true,colorsVerified=true,source=mole?"0016facc case20 + SDK MoleColor@0097e27c GammaType1":"0016facc cases0/1 + SDK GetCommonColor GammaType1"} });
            }
            return result.ToArray();
        }
#endif
    }

#if UNITY_5_3_OR_NEWER
    [Serializable] public sealed class NativeFaceSourcePart {public string id;public uint eyeAccessory;public float[] rotateAxis;public bool lipDefault,mouthOpen;}
    [Serializable] public sealed class NativeFaceSourcePosition {public int X,Y;}
    [Serializable] public sealed class NativeFaceEyeAccessory
    {
        public uint id;
        public int HighlightAspect,HighlightRotate,HighlightScale,HighlightDefaultPartsIndex;
        public int EyelidUpperAspect,EyelidUpperRotate,EyelidUpperScale,EyelidUpperDefaultPartsIndex;
        public int EyelidLowerAspect,EyelidLowerRotate,EyelidLowerScale,EyelidLowerDefaultPartsIndex;
        public int EyelashUpperAspect,EyelashUpperRotate,EyelashUpperScale,EyelashUpperDefaultPartsIndex;
        public int EyelashLowerAspect,EyelashLowerRotate,EyelashLowerScale,EyelashLowerDefaultPartsIndex;
        public NativeFaceSourcePosition HighlightPos,EyelidUpperPos,EyelidLowerPos,EyelashUpperPos,EyelashLowerPos;
        public int DefaultIndex(string kind)=>kind=="Highlight"?HighlightDefaultPartsIndex:kind=="EyelidUpper"?EyelidUpperDefaultPartsIndex:kind=="EyelidLower"?EyelidLowerDefaultPartsIndex:kind=="EyelashUpper"?EyelashUpperDefaultPartsIndex:EyelashLowerDefaultPartsIndex;
        public int[] Offsets(string kind)
        {
            var p=kind=="Highlight"?HighlightPos:kind=="EyelidUpper"?EyelidUpperPos:kind=="EyelidLower"?EyelidLowerPos:kind=="EyelashUpper"?EyelashUpperPos:EyelashLowerPos;
            return new[]{p.X,p.Y,kind=="Highlight"?HighlightRotate:kind=="EyelidUpper"?EyelidUpperRotate:kind=="EyelidLower"?EyelidLowerRotate:kind=="EyelashUpper"?EyelashUpperRotate:EyelashLowerRotate,
                kind=="Highlight"?HighlightScale:kind=="EyelidUpper"?EyelidUpperScale:kind=="EyelidLower"?EyelidLowerScale:kind=="EyelashUpper"?EyelashUpperScale:EyelashLowerScale,
                kind=="Highlight"?HighlightAspect:kind=="EyelidUpper"?EyelidUpperAspect:kind=="EyelidLower"?EyelidLowerAspect:kind=="EyelashUpper"?EyelashUpperAspect:EyelashLowerAspect};
        }
    }
    [Serializable] public sealed class NativeFaceExpression
    {
        public string id,EyeLocationName,EyePartsLName,EyePartsRName,EyebrowLocationName,EyebrowPartsLName,EyebrowPartsRName;
        public string MouthLocationName,MouthPartsName,MustacheLocationName,MustachePartsName,MoleLocationName,MolePartsName;
    }
    [Serializable] public sealed class NativeFaceLocation
    {
        public string id;public int PositionX,PositionY,Rotate,Scale,Aspect;
        public bool IsOffsetPosition,IsOffsetRotate,IsOffsetScale,IsOffsetAspect;
    }
    [Serializable] public sealed class NativeFaceSourceData
    {
        public NativeFaceSourcePart[] parts;public NativeFaceEyeAccessory[] eyeAccessories;public NativeFaceExpression[] expressions;public NativeFaceLocation[] locations;
        private static NativeFaceSourceData cached;
        public static NativeFaceSourceData Load()
        {if(cached==null){var text=Resources.Load<TextAsset>("Avatar/face-runtime");if(text==null)throw new InvalidOperationException("Native face parameter tables missing.");cached=JsonUtility.FromJson<NativeFaceSourceData>(text.text);}return cached;}
    }
#endif
}
