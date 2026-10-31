using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.ML.OnnxRuntime;

namespace SushiAndFish.FriendsLife.PhotoProcessing
{
    [Serializable] public sealed class NativePhotoFeatures
    {
        public string version="native-photo-image-v1";
        public int width,height,maskWidth=96,maskHeight=96;
        public float[] crop,face,hair,upper,lower,skinRgb,hairRgb,upperRgb,lowerRgb,shoeRgb,landmarks;
        public byte[] personAlpha,labels;
        public float poseConfidence,shoulderTorso,waistShoulder,mouthEar,headBody,hairTexture,seconds;
        public bool fullBody,shortHair,shortSleeves,openNeckline,glasses;
    }
    // Pure image/CPU input. No geometry, GLB, camera, game state or HTTP access.
    public static class NativePhotoImageCpu
    {
        static readonly object sync=new object();static IntPtr handle;static string loaded;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryExW(string path,IntPtr reserved,uint flags);
        static float Clamp(float v,float lo,float hi)=>Math.Max(lo,Math.Min(hi,v));
        static float Sigmoid(float v)=>1/(1+(float)Math.Exp(-v));
        static void Native(string file)
        {lock(sync){file=Path.GetFullPath(file);if(handle!=IntPtr.Zero){if(loaded!=file)throw new InvalidOperationException("Native photo runtime path changed");return;}if(!File.Exists(file))throw new FileNotFoundException("本地照片分析组件缺失",file);handle=LoadLibraryExW(file,IntPtr.Zero,8);if(handle==IntPtr.Zero)throw new InvalidOperationException("本地照片分析组件无法启动："+Marshal.GetLastWin32Error());loaded=file;}}
        static InferenceSession Session(byte[] model)
        {using(var options=new SessionOptions{IntraOpNumThreads=1,InterOpNumThreads=1,EnableCpuMemArena=false,EnableMemoryPattern=false,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_EXTENDED})return new InferenceSession(model,options);}
        static float Pixel(byte[] rgba,int w,int h,float x,float y,int channel)
        {
            if(!(x>=0&&y>=0&&x<=w-1&&y<=h-1))return .5f;// also rejects NaN, which would cast to a negative index
            int x0=(int)x,y0=(int)y,x1=Math.Min(w-1,x0+1),y1=Math.Min(h-1,y0+1);float dx=x-x0,dy=y-y0;
            Func<int,int,float> sample=(a,b)=>{int i=(b*w+a)*4;float alpha=rgba[i+3]/255f;return rgba[i+channel]/255f*alpha+.5f*(1-alpha);};
            return (sample(x0,y0)*(1-dx)+sample(x1,y0)*dx)*(1-dy)+(sample(x0,y1)*(1-dx)+sample(x1,y1)*dx)*dy;
        }
        static float[] Box(List<int> pixels,int width,float[] crop,int originalW,int originalH)
        {if(pixels.Count==0)return null;int x0=pixels.Min(i=>i%width),x1=pixels.Max(i=>i%width),y0=pixels.Min(i=>i/width),y1=pixels.Max(i=>i/width);return new[]{(crop[0]+x0*crop[2]/width)/originalW,(crop[1]+y0*crop[2]/width)/originalH,(x1-x0+1)*crop[2]/width/originalW,(y1-y0+1)*crop[2]/width/originalH};}
        static float[] Color(List<int> set,byte[] rgba,int w,int h,float[] crop)
        {
            var channels=new[]{new List<float>(),new List<float>(),new List<float>()};
            foreach(int pixel in set){float x=crop[0]+(pixel%96+.5f)*crop[2]/96,y=crop[1]+(pixel/96+.5f)*crop[2]/96;float r=Pixel(rgba,w,h,x,y,0),g=Pixel(rgba,w,h,x,y,1),b=Pixel(rgba,w,h,x,y,2);if(Math.Max(r,Math.Max(g,b))<.06f)continue;channels[0].Add(r);channels[1].Add(g);channels[2].Add(b);}
            if(channels[0].Count<3)return null;return channels.Select(c=>{c.Sort();return c[c.Count/2];}).ToArray();
        }
        public static NativePhotoFeatures Analyze(byte[] rgba,int width,int height,byte[] segmentation,byte[] pose,string library,CancellationToken cancel)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();if(rgba==null||rgba.Length!=width*height*4||width<16||height<16)throw new InvalidDataException("照片像素无效");Native(library);cancel.ThrowIfCancellationRequested();
            int x0=width,y0=height,x1=-1,y1=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(rgba[(y*width+x)*4+3]>24){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
            if(x1<x0||y1<y0)throw new InvalidDataException("请保留可见人像轮廓");float size=Math.Max(x1-x0+1,y1-y0+1)*1.08f;var crop=new[]{(x0+x1+1)*.5f-size*.5f,(y0+y1+1)*.5f-size*.5f,size};
            var input=new float[3*384*384];float[] mean={.485f,.456f,.406f},std={.229f,.224f,.225f};
            for(int y=0;y<384;y++){if((y&31)==0)cancel.ThrowIfCancellationRequested();for(int x=0;x<384;x++)for(int c=0;c<3;c++)input[(c*384+y)*384+x]=(Pixel(rgba,width,height,crop[0]+(x+.5f)*size/384,crop[1]+(y+.5f)*size/384,c)-mean[c])/std[c];}
            float[] logits;
            using(var session=Session(segmentation))using(var tensor=OrtValue.CreateTensorValueFromMemory(input,new long[]{1,3,384,384}))using(var run=new RunOptions())using(cancel.Register(()=>run.Terminate=true))using(var output=session.Run(run,new[]{"pixel_values"},new[]{tensor},new[]{"logits"}))logits=output.First().GetTensorDataAsSpan<float>().ToArray();
            if(logits.Length!=18*96*96)throw new InvalidDataException("本地照片分析版本不匹配");
            var result=new NativePhotoFeatures{width=width,height=height,crop=crop,personAlpha=new byte[96*96],labels=new byte[96*96]};var groups=Enumerable.Range(0,18).Select(_=>new List<int>()).ToArray();
            for(int i=0;i<96*96;i++)
            {
                int best=0;float max=logits[i];for(int c=1;c<18;c++)if(logits[c*96*96+i]>max){max=logits[c*96*96+i];best=c;}float sum=0;for(int c=0;c<18;c++)sum+=(float)Math.Exp(logits[c*96*96+i]-max);float human=1-(float)Math.Exp(logits[i]-max)/sum;
                result.labels[i]=(byte)best;result.personAlpha[i]=(byte)Math.Round(Clamp((human-.15f)/.7f,0,1)*255);groups[best].Add(i);
            }
            result.face=Box(groups[11],96,crop,width,height);result.hair=Box(groups[2],96,crop,width,height);var tops=groups[4].Concat(groups[7]).ToList();var bottoms=groups[5].Concat(groups[6]).ToList();
            result.upper=Box(tops,96,crop,width,height);result.lower=Box(bottoms,96,crop,width,height);result.skinRgb=Color(groups[11].Concat(groups[14]).Concat(groups[15]).ToList(),rgba,width,height,crop);result.hairRgb=Color(groups[2],rgba,width,height,crop);result.upperRgb=Color(tops,rgba,width,height,crop);result.lowerRgb=Color(bottoms,rgba,width,height,crop);result.shoeRgb=Color(groups[9].Concat(groups[10]).ToList(),rgba,width,height,crop);
            result.glasses=groups[3].Count>Math.Max(2,groups[11].Count*.01f);result.shortHair=result.hair==null||result.face==null||result.hair[1]+result.hair[3]<result.face[1]+result.face[3]*1.2f;
            if(result.hair!=null&&groups[2].Count>5){float total=0;int count=0;foreach(int i in groups[2]){float x=crop[0]+(i%96+.5f)*size/96,y=crop[1]+(i/96+.5f)*size/96;total+=Math.Abs(Pixel(rgba,width,height,x,y,0)-Pixel(rgba,width,height,x+2,y,0));count++;}result.hairTexture=total/Math.Max(1,count);}
            // A full-body image makes spectacles/skin only a few input pixels.
            // Refine the observed head in 2D, retaining the full-body mask/pose.
            if(result.face!=null)
            {
                float extent=Math.Max(result.face[2]*width,result.face[3]*height)*2,fx=(result.face[0]+result.face[2]*.5f)*width,fy=(result.face[1]+result.face[3]*.5f)*height;var headCrop=new[]{fx-extent*.5f,fy-extent*.5f,extent};var headInput=new float[3*384*384];
                for(int y=0;y<384;y++)for(int x=0;x<384;x++)for(int c=0;c<3;c++)headInput[(c*384+y)*384+x]=(Pixel(rgba,width,height,headCrop[0]+(x+.5f)*extent/384,headCrop[1]+(y+.5f)*extent/384,c)-mean[c])/std[c];
                float[] detail;using(var session=Session(segmentation))using(var tensor=OrtValue.CreateTensorValueFromMemory(headInput,new long[]{1,3,384,384}))using(var run=new RunOptions())using(cancel.Register(()=>run.Terminate=true))using(var output=session.Run(run,new[]{"pixel_values"},new[]{tensor},new[]{"logits"}))detail=output.First().GetTensorDataAsSpan<float>().ToArray();
                var headGroups=Enumerable.Range(0,18).Select(_=>new List<int>()).ToArray();if(detail.Length==18*96*96)for(int i=0;i<96*96;i++){int best=0;for(int c=1;c<18;c++)if(detail[c*96*96+i]>detail[best*96*96+i])best=c;headGroups[best].Add(i);}
                if(headGroups[11].Count>10){result.face=Box(headGroups[11],96,headCrop,width,height);result.skinRgb=Color(headGroups[11],rgba,width,height,headCrop)??result.skinRgb;}
                if(headGroups[2].Count>10){result.hair=Box(headGroups[2],96,headCrop,width,height);result.hairRgb=Color(headGroups[2],rgba,width,height,headCrop)??result.hairRgb;}
                result.glasses|=headGroups[3].Count>Math.Max(3,headGroups[11].Count*.01f);result.shortHair=result.hair==null||result.face==null||result.hair[1]+result.hair[3]<result.face[1]+result.face[3]*1.2f;
            }
            if(pose!=null&&pose.Length>0)
            {
                var tensorData=new float[256*256*3];for(int y=0;y<256;y++)for(int x=0;x<256;x++)for(int c=0;c<3;c++)tensorData[(y*256+x)*3+c]=Pixel(rgba,width,height,crop[0]+(x+.5f)*size/256,crop[1]+(y+.5f)*size/256,c);
                using(var session=Session(pose))using(var tensor=OrtValue.CreateTensorValueFromMemory(tensorData,new long[]{1,256,256,3}))using(var run=new RunOptions())using(cancel.Register(()=>run.Terminate=true))using(var output=session.Run(run,new[]{"input_1"},new[]{tensor},new[]{"Identity","Identity_1"}))
                {var arrays=output.ToArray();var values=arrays[0].GetTensorDataAsSpan<float>().ToArray();var confidence=arrays[1].GetTensorDataAsSpan<float>();result.poseConfidence=confidence.Length>0?confidence[0]:0;if(values.Length>=165&&result.poseConfidence>.35f){result.landmarks=new float[99];for(int j=0;j<33;j++){result.landmarks[j*3]=(crop[0]+values[j*5]*size/256)/width;result.landmarks[j*3+1]=(crop[1]+values[j*5+1]*size/256)/height;result.landmarks[j*3+2]=Sigmoid(values[j*5+3])*Sigmoid(values[j*5+4])*result.poseConfidence;}}}
            }
            if(result.landmarks!=null)
            {
                Func<int,int,float> distance=(a,b)=>(float)Math.Sqrt(Math.Pow((result.landmarks[a*3]-result.landmarks[b*3])*width,2)+Math.Pow((result.landmarks[a*3+1]-result.landmarks[b*3+1])*height,2));
                float shoulders=distance(11,12),torso=Math.Abs((result.landmarks[23*3+1]+result.landmarks[24*3+1]-result.landmarks[11*3+1]-result.landmarks[12*3+1])*height*.5f);
                result.fullBody=new[]{11,12,23,24,27,28}.All(i=>result.landmarks[i*3+2]>.4f);result.shoulderTorso=shoulders/Math.Max(1,torso);result.mouthEar=distance(9,10)/Math.Max(1,distance(7,8));
                if(result.face!=null)result.headBody=(result.face[3]*height)/(y1-y0+1f);
                // Class-driven exposed-arm proportion; conservative sleeve selection.
                result.shortSleeves=groups[14].Count+groups[15].Count>tops.Count*.07f;
                result.openNeckline=result.face!=null&&result.upper!=null&&(result.upper[1]-result.face[1]-result.face[3])<result.face[3]*.6f;
            }
            else{result.shortSleeves=groups[14].Count+groups[15].Count>tops.Count*.1f;result.openNeckline=false;}
            result.seconds=(float)clock.Elapsed.TotalSeconds;cancel.ThrowIfCancellationRequested();return result;
        }
    }
}
