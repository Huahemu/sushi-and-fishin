using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.ML.OnnxRuntime;

namespace SushiAndFish.FriendsLife.PhotoProcessing
{
    // Pure CPU data. Texture and mask arrays use Unity's bottom-left row order.
    // No GameObject, native game model, world clock or original save is accessed.
    public sealed class PhotoSurfaceInput
    {
        public float[] positions,normals,uv;
        public int[] triangles;
        public int textureWidth,textureHeight;
        public byte[] textureRgb;
    }
    public sealed class PhotoSurfaceRegions
    {
        public byte[] rgb;
        public int width,height,covered,observed,inferred;
        public int[] observedByView;
        public float seconds;
    }
    public sealed class PhotoSurfacePose
    {public float[] joints,rms,confidence;public float seconds;}
    public static class PhotoRegionCpu
    {
        const int Size=640,InputSize=384,OutputSize=96,Classes=18,Regions=7;
        static readonly int[] Labels={0,0,1,0,2,3,3,2,0,5,5,6,4,4,4,4,0,0};
        static readonly float[] Mean={.485f,.456f,.406f},Std={.229f,.224f,.225f};
        static readonly byte[] Background={33,43,54};
        static IntPtr nativeHandle;static string nativePath;static readonly object nativeLock=new object();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryExW(string path,IntPtr reserved,uint flags);
        static void Native(string path)
        {
            path=Path.GetFullPath(path);
            lock(nativeLock)
            {
                if(nativeHandle!=IntPtr.Zero){if(!string.Equals(path,nativePath,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Photo inference library already initialized from a different location");return;}
                if(!File.Exists(path))throw new FileNotFoundException("Photo inference library is missing",path);
                nativeHandle=LoadLibraryExW(path,IntPtr.Zero,8);
                if(nativeHandle==IntPtr.Zero)throw new InvalidOperationException("Photo inference native load failed: "+Marshal.GetLastWin32Error());nativePath=path;
            }
        }
        struct V
        {
            public float x,y,z;public V(float a,float b,float c){x=a;y=b;z=c;}
            public static V operator +(V a,V b)=>new V(a.x+b.x,a.y+b.y,a.z+b.z);
            public static V operator -(V a,V b)=>new V(a.x-b.x,a.y-b.y,a.z-b.z);
            public static V operator *(V a,float b)=>new V(a.x*b,a.y*b,a.z*b);
            public float Length=>(float)Math.Sqrt(x*x+y*y+z*z);
            public V Unit(){float n=Length;return n>1e-12f?this*(1/n):new V(0,1,0);}
        }
        struct P{public float x,y;public P(float a,float b){x=a;y=b;}}
        struct Box{public float x,y,size;public bool Contains(float a,float b)=>a>=x&&b>=y&&a<x+size&&b<y+size;}
        sealed class View
        {
            public P[] screen;public V[] normal;public int[] visible;public byte[] rgb;
            public float[] body,head;public Box crop,refine;public bool hasHead;
        }
        struct Edge:IEquatable<Edge>
        {
            public int a,b;public Edge(int x,int y){a=Math.Min(x,y);b=Math.Max(x,y);}
            public bool Equals(Edge v)=>a==v.a&&b==v.b;public override bool Equals(object v)=>v is Edge&&Equals((Edge)v);
            public override int GetHashCode(){unchecked{return a*73856093^b*19349663;}}
        }
        struct Cell:IEquatable<Cell>
        {
            public long x,y,z;public Cell(V p,float tolerance){x=(long)Math.Round(p.x/tolerance);y=(long)Math.Round(p.y/tolerance);z=(long)Math.Round(p.z/tolerance);}
            public bool Equals(Cell v)=>x==v.x&&y==v.y&&z==v.z;public override bool Equals(object v)=>v is Cell&&Equals((Cell)v);
            public override int GetHashCode(){unchecked{return x.GetHashCode()*73856093^y.GetHashCode()*19349663^z.GetHashCode()*83492791;}}
        }
        struct Link{public int face;public float weight;public Link(int f,float w){face=f;weight=w;}}
        static float Clamp(float x,float min,float max)=>Math.Max(min,Math.Min(max,x));
        static int Clamp(int x,int min,int max)=>Math.Max(min,Math.Min(max,x));
        static float Dot(V a,V b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        static V Read(float[] data,int index)=>new V(data[index*3],data[index*3+1],data[index*3+2]);
        static V Rotate(V p,float c,float s)=>new V(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);
        static float Den(P a,P b,P c)=>(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
        static void Bary(P a,P b,P c,float den,float x,float y,out float u,out float v,out float w)
        {u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den;v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;w=1-u-v;}
        static void Bounds(P a,P b,P c,int width,int height,out int x0,out int x1,out int y0,out int y1)
        {x0=Clamp((int)Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x))),0,width-1);x1=Clamp((int)Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))),0,width-1);y0=Clamp((int)Math.Floor(Math.Min(a.y,Math.Min(b.y,c.y))),0,height-1);y1=Clamp((int)Math.Ceiling(Math.Max(a.y,Math.Max(b.y,c.y))),0,height-1);}
        static V Texture(PhotoSurfaceInput source,float u,float v)
        {
            int x=Clamp((int)(u*(source.textureWidth-1)),0,source.textureWidth-1),y=Clamp((int)(v*(source.textureHeight-1)),0,source.textureHeight-1),i=(y*source.textureWidth+x)*3;
            return new V(source.textureRgb[i]/255f,source.textureRgb[i+1]/255f,source.textureRgb[i+2]/255f);
        }
        static View Render(PhotoSurfaceInput source,V[] points,int yaw,CancellationToken cancel)
        {
            float angle=(float)(yaw*Math.PI/180),c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            float minY=points.Min(p=>p.y),maxY=points.Max(p=>p.y),scale=Size*.86f/Math.Max(maxY-minY,1e-5f);
            var view=new View{screen=new P[points.Length],normal=new V[points.Length],visible=Enumerable.Repeat(-1,Size*Size).ToArray(),rgb=new byte[Size*Size*3]};
            var depths=new float[points.Length];var depth=Enumerable.Repeat(float.NegativeInfinity,Size*Size).ToArray();
            for(int i=0;i<points.Length;i++){var p=Rotate(points[i],c,s);view.screen[i]=new P(p.x*scale+Size*.5f,p.y*scale+Size*.5f);depths[i]=p.z;view.normal[i]=Rotate(Read(source.normals,i),c,s);}
            for(int i=0;i<Size*Size;i++){view.rgb[i*3]=Background[0];view.rgb[i*3+1]=Background[1];view.rgb[i*3+2]=Background[2];}
            V light=new V(.3f,.7f,.65f).Unit();
            for(int fi=0;fi<source.triangles.Length/3;fi++)
            {
                if((fi&127)==0)cancel.ThrowIfCancellationRequested();int a=source.triangles[fi*3],b=source.triangles[fi*3+1],d=source.triangles[fi*3+2];var p=view.screen[a];var q=view.screen[b];var r=view.screen[d];float den=Den(p,q,r);if(Math.Abs(den)<1e-10)continue;
                Bounds(p,q,r,Size,Size,out int x0,out int x1,out int y0,out int y1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    Bary(p,q,r,den,x+.5f,y+.5f,out float u,out float v,out float w);if(u<0||v<0||w<0)continue;int pixel=y*Size+x;float z=u*depths[a]+v*depths[b]+w*depths[d];if(z<=depth[pixel])continue;
                    var n=(view.normal[a]*u+view.normal[b]*v+view.normal[d]*w).Unit();float shade=.55f+.45f*Math.Max(Dot(n,light),0);
                    float tx=source.uv[a*2]*u+source.uv[b*2]*v+source.uv[d*2]*w,ty=source.uv[a*2+1]*u+source.uv[b*2+1]*v+source.uv[d*2+1]*w;var color=Texture(source,tx,ty)*shade;
                    view.rgb[pixel*3]=(byte)Clamp((int)(color.x*255),0,255);view.rgb[pixel*3+1]=(byte)Clamp((int)(color.y*255),0,255);view.rgb[pixel*3+2]=(byte)Clamp((int)(color.z*255),0,255);depth[pixel]=z;view.visible[pixel]=fi;
                }
            }
            return view;
        }
        static float Pixel(byte[] rgb,float x,float yBottom,int channel)
        {
            if(x<0||yBottom<0||x>Size-1||yBottom>Size-1)return Background[channel]/255f;
            int x0=(int)x,y0=(int)yBottom,x1=Math.Min(x0+1,Size-1),y1=Math.Min(y0+1,Size-1);float dx=x-x0,dy=yBottom-y0;
            return ((rgb[(y0*Size+x0)*3+channel]*(1-dx)+rgb[(y0*Size+x1)*3+channel]*dx)*(1-dy)+(rgb[(y1*Size+x0)*3+channel]*(1-dx)+rgb[(y1*Size+x1)*3+channel]*dx)*dy)/255f;
        }
        static float[] Tensor(byte[] rgb,Box box,CancellationToken cancel)
        {
            var data=new float[3*InputSize*InputSize];
            for(int y=0;y<InputSize;y++)
            {
                if((y&31)==0)cancel.ThrowIfCancellationRequested();float sy=Size-1-(box.y+(y+.5f)*box.size/InputSize-.5f);
                for(int x=0;x<InputSize;x++)for(int channel=0;channel<3;channel++){float sx=box.x+(x+.5f)*box.size/InputSize-.5f;data[(channel*InputSize+y)*InputSize+x]=(Pixel(rgb,sx,sy,channel)-Mean[channel])/Std[channel];}
            }
            return data;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]static InferenceSession Session(byte[] model)
        {
            using(var options=new SessionOptions{IntraOpNumThreads=1,InterOpNumThreads=1,EnableCpuMemArena=false,EnableMemoryPattern=false,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_EXTENDED})return new InferenceSession(model,options);
        }
        static float[] Infer(InferenceSession session,float[] data,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();using(var input=OrtValue.CreateTensorValueFromMemory(data,new long[]{1,3,InputSize,InputSize}))using(var run=new RunOptions())
            using(cancel.Register(()=>run.Terminate=true))
            using(var outputs=session.Run(run,new[]{"pixel_values"},new[]{input},new[]{"logits"}))
            {var result=outputs.First().GetTensorDataAsSpan<float>().ToArray();if(result.Length!=Classes*OutputSize*OutputSize)throw new InvalidDataException("Photo segmentation output dimensions changed");return result;}
        }
        public static PhotoSurfacePose EstimatePose(PhotoSurfaceInput source,byte[] model,string library,CancellationToken cancel)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();Native(library);cancel.ThrowIfCancellationRequested();
            int count=source.positions.Length/3;var points=new V[count];for(int i=0;i<count;i++)points[i]=Read(source.positions,i);
            V min=new V(points.Min(p=>p.x),points.Min(p=>p.y),points.Min(p=>p.z)),max=new V(points.Max(p=>p.x),points.Max(p=>p.y),points.Max(p=>p.z)),center=(min+max)*.5f;
            float height=max.y-min.y;if(height<1e-5f)throw new InvalidDataException("Photo pose has no body height");
            for(int i=0;i<count;i++)points[i]=points[i]-center;
            var values=new float[4][];var detection=new float[4];
            using(var session=Session(model))for(int viewIndex=0;viewIndex<4;viewIndex++)
            {
                cancel.ThrowIfCancellationRequested();var view=Render(source,points,viewIndex*90,cancel);var tensor=new float[256*256*3];
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)for(int channel=0;channel<3;channel++)tensor[(y*256+x)*3+channel]=Pixel(view.rgb,(x+.5f)*Size/256f-.5f,Size-.5f-(y+.5f)*Size/256f,channel);
                using(var input=OrtValue.CreateTensorValueFromMemory(tensor,new long[]{1,256,256,3}))using(var run=new RunOptions())using(cancel.Register(()=>run.Terminate=true))
                using(var output=session.Run(run,new[]{"input_1"},new[]{input},new[]{"Identity","Identity_1"}))
                {
                    var arrays=output.ToArray();values[viewIndex]=arrays[0].GetTensorDataAsSpan<float>().ToArray();detection[viewIndex]=arrays[1].GetTensorDataAsSpan<float>()[0];
                    if(values[viewIndex].Length!=195||detection[viewIndex]<.5f)throw new InvalidDataException("Photo body pose could not be observed in all four model views");
                }
            }
            var joints=new float[99];var rms=new float[33];var confidence=new float[33];float projection=Size*.86f/height;
            for(int joint=0;joint<33;joint++)
            {
                float x=0,z=0,y=0,wx=0,wz=0,wy=0,total=0,error=0;
                for(int vi=0;vi<4;vi++)
                {
                    var data=values[vi];int at=joint*5;float visible=1/(1+(float)Math.Exp(-data[at+3])),present=1/(1+(float)Math.Exp(-data[at+4]));float support=visible*present*detection[vi],weight=Math.Max(.0001f,support);weight*=weight;confidence[joint]=Math.Max(confidence[joint],support);
                    float sx=(data[at]*Size/256f-Size*.5f)/projection,sy=(Size*.5f-data[at+1]*Size/256f)/projection;
                    if(vi%2==0){x+=(vi==0?sx:-sx)*weight;wx+=weight;}else{z+=(vi==1?sx:-sx)*weight;wz+=weight;}y+=sy*weight;wy+=weight;
                }
                x/=wx;z/=wz;y/=wy;
                for(int vi=0;vi<4;vi++)
                {
                    var data=values[vi];int at=joint*5;float support=1/(1+(float)Math.Exp(-data[at+3]))/(1+(float)Math.Exp(-data[at+4]))*detection[vi],weight=Math.Max(.0001f,support);weight*=weight;
                    float sx=(data[at]*Size/256f-Size*.5f)/projection,sy=(Size*.5f-data[at+1]*Size/256f)/projection;float expected=vi==0?x:vi==1?z:vi==2?-x:-z;error+=((sx-expected)*(sx-expected)+(sy-y)*(sy-y))*weight;total+=2*weight;
                }
                joints[joint*3]=x+center.x;joints[joint*3+1]=y+center.y;joints[joint*3+2]=z+center.z;rms[joint]=(float)Math.Sqrt(error/total);
            }
            foreach(int joint in new[]{11,12,13,14,15,16,23,24,25,26,27,28})if(confidence[joint]<.5f||rms[joint]>height*.06f)throw new InvalidDataException("Photo body landmarks are inconsistent across model views");
            cancel.ThrowIfCancellationRequested();return new PhotoSurfacePose{joints=joints,rms=rms,confidence=confidence,seconds=(float)clock.Elapsed.TotalSeconds};
        }
        static bool HeadCrop(float[] logits,out Box crop,out Box refine)
        {
            int x0=OutputSize,y0=OutputSize,x1=-1,y1=-1;
            for(int y=0;y<OutputSize;y++)for(int x=0;x<OutputSize;x++)
            {
                int pixel=y*OutputSize+x,best=0;float max=logits[pixel];for(int k=1;k<Classes;k++){float v=logits[k*OutputSize*OutputSize+pixel];if(v>max){max=v;best=k;}}
                if(best!=1&&best!=2&&best!=11)continue;x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);
            }
            crop=new Box();refine=new Box();if(x1<x0||y1<y0)return false;
            float cx=(x0+x1+1)*.5f*Size/OutputSize,cy=(y0+y1+1)*.5f*Size/OutputSize,extent=Math.Max(x1-x0+1,y1-y0+1)*(float)Size/OutputSize;
            float size=extent*2.2f;crop=new Box{x=cx-size*.5f,y=cy-size*.5f,size=size};size=extent*1.3f;refine=new Box{x=cx-size*.5f,y=cy-size*.5f,size=size};return true;
        }
        static void Probabilities(float[] logits,float xTop,float yTop,float[] output)
        {
            float x=Clamp(xTop,0,OutputSize-1),y=Clamp(yTop,0,OutputSize-1);int x0=(int)x,y0=(int)y,x1=Math.Min(x0+1,OutputSize-1),y1=Math.Min(y0+1,OutputSize-1);float dx=x-x0,dy=y-y0,max=float.NegativeInfinity;
            for(int k=0;k<Classes;k++){int start=k*OutputSize*OutputSize;float value=(logits[start+y0*OutputSize+x0]*(1-dx)+logits[start+y0*OutputSize+x1]*dx)*(1-dy)+(logits[start+y1*OutputSize+x0]*(1-dx)+logits[start+y1*OutputSize+x1]*dx)*dy;output[k]=value;max=Math.Max(max,value);}
            float sum=0;for(int k=0;k<Classes;k++){output[k]=(float)Math.Exp(output[k]-max);sum+=output[k];}for(int k=0;k<Classes;k++)output[k]/=sum;
        }
        static void Sample(View view,float x,float yBottom,float[] body,float[] head,float[] result)
        {
            float top=Size-1-yBottom;Probabilities(view.body,(x+.5f)*OutputSize/Size-.5f,(top+.5f)*OutputSize/Size-.5f,body);
            bool zoom=view.hasHead&&view.refine.Contains(x,top);
            if(zoom)Probabilities(view.head,(x-view.crop.x+.5f)*OutputSize/view.crop.size-.5f,(top-view.crop.y+.5f)*OutputSize/view.crop.size-.5f,head);
            Array.Clear(result,0,Regions);for(int k=0;k<Classes;k++)result[Labels[k]]+=zoom?head[k]:body[k];
        }
        static List<Link>[] Graph(PhotoSurfaceInput source,V[] points,out V[] colors)
        {
            int faceCount=source.triangles.Length/3;var graph=new List<Link>[faceCount];var centroids=new V[faceCount];colors=new V[faceCount];
            float extent=Math.Max(points.Max(p=>p.x)-points.Min(p=>p.x),Math.Max(points.Max(p=>p.y)-points.Min(p=>p.y),points.Max(p=>p.z)-points.Min(p=>p.z)));float tolerance=Math.Max(extent*1e-6f,1e-9f);
            var cells=new Dictionary<Cell,int>();var welded=new int[points.Length];
            for(int i=0;i<points.Length;i++){var key=new Cell(points[i],tolerance);if(!cells.TryGetValue(key,out int index)){index=cells.Count;cells.Add(key,index);}welded[i]=index;}
            var edges=new Dictionary<Edge,List<int>>();
            for(int fi=0;fi<faceCount;fi++)
            {
                graph[fi]=new List<Link>();int a=source.triangles[fi*3],b=source.triangles[fi*3+1],c=source.triangles[fi*3+2];centroids[fi]=(points[a]+points[b]+points[c])*(1/3f);colors[fi]=Texture(source,(source.uv[a*2]+source.uv[b*2]+source.uv[c*2])/3,(source.uv[a*2+1]+source.uv[b*2+1]+source.uv[c*2+1])/3);
                int[] ids={welded[a],welded[b],welded[c]};for(int i=0;i<3;i++){var edge=new Edge(ids[i],ids[(i+1)%3]);if(!edges.TryGetValue(edge,out var sharing))edges[edge]=sharing=new List<int>();sharing.Add(fi);}
            }
            foreach(var sharing in edges.Values)for(int i=0;i<sharing.Count;i++)for(int j=i+1;j<sharing.Count;j++)
            {int a=sharing[i],b=sharing[j];float w=1/(Math.Max((centroids[a]-centroids[b]).Length,1e-9f)*(1+10*(colors[a]-colors[b]).Length));graph[a].Add(new Link(b,w));graph[b].Add(new Link(a,w));}
            return graph;
        }
        public static PhotoSurfaceRegions Generate(PhotoSurfaceInput source,byte[] model,string library,CancellationToken cancel,Action<int> progress=null)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();Native(library);cancel.ThrowIfCancellationRequested();
            if(source.positions==null||source.positions.Length<300||source.positions.Length%3!=0||source.normals?.Length!=source.positions.Length||source.uv?.Length!=source.positions.Length/3*2||source.triangles==null||source.triangles.Length%3!=0||source.textureRgb?.Length!=source.textureWidth*source.textureHeight*3)throw new InvalidDataException("Generated photo surface is incomplete");
            int vertexCount=source.positions.Length/3,faceCount=source.triangles.Length/3,width=source.textureWidth,height=source.textureHeight,pixelCount=width*height;
            var points=new V[vertexCount];for(int i=0;i<vertexCount;i++)points[i]=Read(source.positions,i);
            V min=new V(points.Min(p=>p.x),points.Min(p=>p.y),points.Min(p=>p.z)),max=new V(points.Max(p=>p.x),points.Max(p=>p.y),points.Max(p=>p.z)),center=(min+max)*.5f;
            for(int i=0;i<points.Length;i++)points[i]=points[i]-center;
            progress?.Invoke(-2);var views=new View[4];using(var session=Session(model))
            {
                progress?.Invoke(-1);
                for(int i=0;i<4;i++)
                {
                    cancel.ThrowIfCancellationRequested();var view=Render(source,points,i*90,cancel);view.body=Infer(session,Tensor(view.rgb,new Box{x=0,y=0,size=Size},cancel),cancel);
                    view.hasHead=HeadCrop(view.body,out view.crop,out view.refine);if(view.hasHead)view.head=Infer(session,Tensor(view.rgb,view.crop,cancel),cancel);views[i]=view;progress?.Invoke(i+1);
                }
            }
            var coverage=new bool[pixelCount];var observed=new bool[pixelCount];var uvFace=Enumerable.Repeat(-1,pixelCount).ToArray();var scores=new float[pixelCount*Regions];var weights=new float[pixelCount];var faceScores=new float[faceCount*Regions];var support=new int[faceCount];var seenByView=new int[4];var bodyProb=new float[Classes];var headProb=new float[Classes];var probabilities=new float[Regions];
            for(int fi=0;fi<faceCount;fi++)
            {
                if((fi&63)==0)cancel.ThrowIfCancellationRequested();int a=source.triangles[fi*3],b=source.triangles[fi*3+1],c=source.triangles[fi*3+2];
                P p=new P(source.uv[a*2]*(width-1),source.uv[a*2+1]*(height-1)),q=new P(source.uv[b*2]*(width-1),source.uv[b*2+1]*(height-1)),r=new P(source.uv[c*2]*(width-1),source.uv[c*2+1]*(height-1));float den=Den(p,q,r);if(Math.Abs(den)<1e-10)continue;Bounds(p,q,r,width,height,out int x0,out int x1,out int y0,out int y1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    Bary(p,q,r,den,x+.5f,y+.5f,out float u,out float v,out float w);if(u<0||v<0||w<0)continue;int pixel=y*width+x;coverage[pixel]=true;uvFace[pixel]=fi;
                    for(int vi=0;vi<4;vi++)
                    {
                        var view=views[vi];float sx=view.screen[a].x*u+view.screen[b].x*v+view.screen[c].x*w,sy=view.screen[a].y*u+view.screen[b].y*v+view.screen[c].y*w;int ix=Clamp((int)sx,0,Size-1),iy=Clamp((int)sy,0,Size-1);if(view.visible[iy*Size+ix]!=fi)continue;
                        Sample(view,sx,sy,bodyProb,headProb,probabilities);float best=0;for(int k=1;k<Regions;k++)best=Math.Max(best,probabilities[k]);if(best<.25f)continue;
                        V n=(view.normal[a]*u+view.normal[b]*v+view.normal[c]*w).Unit();float nz=Clamp(n.z,.08f,1),factor=nz*nz*Math.Max(best,.05f);weights[pixel]+=factor;observed[pixel]=true;seenByView[vi]++;support[fi]++;
                        for(int k=0;k<Regions;k++){float value=probabilities[k]*factor;scores[pixel*Regions+k]+=value;faceScores[fi*Regions+k]+=value;}
                    }
                }
            }
            progress?.Invoke(5);var graph=Graph(source,points,out var unusedColors);var stable=new bool[faceCount];var faceProb=new float[faceCount*6];
            for(int fi=0;fi<faceCount;fi++){float sum=0,best=0;for(int k=1;k<Regions;k++){float v=faceScores[fi*Regions+k];sum+=v;best=Math.Max(best,v);}stable[fi]=support[fi]>=2&&sum>0&&best/sum>.55f;for(int k=0;k<6;k++)faceProb[fi*6+k]=sum>0?faceScores[fi*Regions+k+1]/sum:0;}
            var next=new float[6];for(int pass=0;pass<160;pass++)
            {
                cancel.ThrowIfCancellationRequested();float maximum=0;
                for(int fi=0;fi<faceCount;fi++)
                {
                    if(stable[fi])continue;Array.Clear(next,0,6);float sum=0;foreach(var edge in graph[fi]){sum+=edge.weight;for(int k=0;k<6;k++)next[k]+=faceProb[edge.face*6+k]*edge.weight;}if(sum<=0)continue;
                    for(int k=0;k<6;k++){float value=next[k]/sum;maximum=Math.Max(maximum,Math.Abs(value-faceProb[fi*6+k]));faceProb[fi*6+k]=value;}
                }
                if(maximum<.0001f)break;
            }
            var labels=new byte[pixelCount];int covered=0,seen=0;
            for(int pixel=0;pixel<pixelCount;pixel++)
            {
                if(!coverage[pixel])continue;covered++;int best=0;float value=0;
                if(observed[pixel]){seen++;for(int k=0;k<Regions;k++)if(scores[pixel*Regions+k]>value){value=scores[pixel*Regions+k];best=k;}}
                else{int fi=uvFace[pixel];for(int k=0;k<6;k++)if(faceProb[fi*6+k]>value){value=faceProb[fi*6+k];best=k+1;}}
                labels[pixel]=(byte)best;
            }
            // Two-texel UV padding only; it is not a semantic observation.
            var padded=(byte[])labels.Clone();for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int pixel=y*width+x;if(coverage[pixel])continue;int distance=10;byte value=0;
                for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){int d=dx*dx+dy*dy;if(d>4||d>=distance)continue;int px=x+dx,py=y+dy;if(px<0||py<0||px>=width||py>=height||!coverage[py*width+px])continue;distance=d;value=labels[py*width+px];}padded[pixel]=value;
            }
            var rgbMask=new byte[pixelCount*3];for(int i=0;i<pixelCount;i++)rgbMask[i*3]=rgbMask[i*3+1]=rgbMask[i*3+2]=padded[i];cancel.ThrowIfCancellationRequested();
            return new PhotoSurfaceRegions{rgb=rgbMask,width=width,height=height,covered=covered,observed=seen,inferred=covered-seen,observedByView=seenByView,seconds=(float)clock.Elapsed.TotalSeconds};
        }
    }
}
