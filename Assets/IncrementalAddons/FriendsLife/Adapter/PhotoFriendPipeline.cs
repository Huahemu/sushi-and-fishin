using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bozo.ModularCharacters;
using SFB;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Threading;

namespace SushiAndFish.FriendsLife
{
    public enum PhotoFriendStage{Image,Mask,Ready,Generating,Package,Conform,Editor,Profile,AwaitingHome,Failed,Cancelled}
    [Serializable] public sealed class PhotoFriendDraft
    {
        public string draftId,personId,createCommandId,displayName="",catchphrase="",hobby="",nativeAvatarJson="",sourcePath,maskPath,cutoutPath,jobId,sessionId,eventId,uploadedPath,remoteResultUrl,remoteButton,modelPath,packageId,failure="",provider="";
        public string jobProvider,modelProvider,regionMaskPath,regionJob,regionPackageId,regionVersion,lastModelSource,posePath,poseJob,poseVersion;
        public string appearanceCommandId,appearanceCommandJson,nativeBaseJson,pipelineMode,fitVersion,nativeFitJson,nativeFeaturesPath,legacyPackageId,legacyDraftId,legacyAppearanceJson;public int nativeFeatureRevision=-1;public bool nativeMaskPending,editingExisting;
        public int appearanceRevision,remotePhase,inputRevision,jobInputRevision;public bool maskConfirmed;public PhotoFriendStage stage;public CharacterData characterData;
    }
    public sealed class PhotoFriendPipeline:MonoBehaviour
    {
        static PhotoFriendPipeline instance;readonly List<PhotoFriendDraft> drafts=new List<PhotoFriendDraft>();
        readonly Dictionary<string,Texture2D> sources=new Dictionary<string,Texture2D>(),cutouts=new Dictionary<string,Texture2D>();
        readonly Dictionary<string,Stack<byte[]>> undo=new Dictionary<string,Stack<byte[]>>(),redo=new Dictionary<string,Stack<byte[]>>();
        readonly HashSet<string> active=new HashSet<string>();
        readonly Dictionary<string,CancellationTokenSource> httpRequests=new Dictionary<string,CancellationTokenSource>();
        readonly Dictionary<string,CancellationTokenSource> generationOwners=new Dictionary<string,CancellationTokenSource>();
        bool tearingDown;
        bool IsLive(PhotoFriendDraft d)=>!tearingDown&&d!=null&&drafts.Contains(d);
        bool OwnsRequest(PhotoFriendDraft d,CancellationTokenSource owner,int revision)
            =>IsLive(d)&&d.inputRevision==revision&&httpRequests.TryGetValue(d.draftId,out var current)&&ReferenceEquals(current,owner);
        bool CanApply(PhotoFriendDraft d,CancellationTokenSource owner,int revision)
            =>OwnsRequest(d,owner,revision)&&!owner.IsCancellationRequested;
        static void RequestCancellation(CancellationTokenSource owner)
        {if(owner==null)return;try{owner.Cancel();}catch(ObjectDisposedException){}catch(Exception error){Debug.LogWarning("[FriendsLife] Photo cancellation cleanup: "+error.Message);}}
        void RegisterRequest(string id,CancellationTokenSource owner)
        {httpRequests.TryGetValue(id,out var previous);httpRequests[id]=owner;if(!ReferenceEquals(previous,owner))RequestCancellation(previous);}
        void ForgetRequest(string id,CancellationTokenSource owner)
        {if(httpRequests.TryGetValue(id,out var current)&&ReferenceEquals(current,owner))httpRequests.Remove(id);}
        void FinishGeneration(string id,CancellationTokenSource owner)
        {if(generationOwners.TryGetValue(id,out var current)&&ReferenceEquals(current,owner)){generationOwners.Remove(id);active.Remove(id);}}
        void CancelRemovedDraft(string id)
        {
            httpRequests.TryGetValue(id,out var request);generationOwners.TryGetValue(id,out var generation);
            httpRequests.Remove(id);generationOwners.Remove(id);active.Remove(id);
            RequestCancellation(request);if(!ReferenceEquals(request,generation))RequestCancellation(generation);
        }
        void ReleaseDraftResources(string id)
        {
            // Remove only this draft's owned references. Other live entries,
            // including an aliased texture, retain their images and histories.
            if(!tearingDown&&drafts.Any(d=>d.draftId==id))return;
            sources.TryGetValue(id,out var source);cutouts.TryGetValue(id,out var cutout);
            sources.Remove(id);cutouts.Remove(id);undo.Remove(id);redo.Remove(id);
            bool Retained(Texture2D image)=>sources.Values.Any(t=>ReferenceEquals(t,image))||cutouts.Values.Any(t=>ReferenceEquals(t,image));
            if(source&&!Retained(source))Destroy(source);
            if(cutout&&!ReferenceEquals(source,cutout)&&!Retained(cutout))Destroy(cutout);
        }
        public const string NativeMode="native-parameter-fit-v1";public static bool IsNative(PhotoFriendDraft d)=>d!=null&&d.pipelineMode==NativeMode;
        public const string NoFaceMessage="没找到人脸，请换一张正面照或手动制作";
        // Working copy of the photo. Display is 360x440 and analysis samples at 384, so larger
        // photos only make PNG saves, mask edits and undo snapshots slow on the main thread.
        const int MaxWorkingSide=1280,MinWorkingSide=64;
        // Player-facing text: keep our own short Chinese messages, hide raw exception text.
        static readonly string[] Jargon={"原","源","投影","租约","宿主","回读","交接"};
        public static string Friendly(Exception error,string fallback)
        {
            Debug.LogWarning("[FriendsLife] Photo friend: "+error);string m=error?.Message;
            bool own=(error is InvalidDataException||error is InvalidOperationException||error is IOException)&&!string.IsNullOrEmpty(m)&&m.Length<=40&&m.Any(c=>c>=0x4e00&&c<=0x9fff)&&!m.Any(c=>c<128&&char.IsLetter(c))&&!Jargon.Any(w=>m.Contains(w));
            return own?m:fallback;
        }
        public IReadOnlyList<PhotoFriendDraft> Drafts=>drafts;public string Root=>PhotoFriendPaths.Folder("PhotoDrafts");
        public static PhotoFriendPipeline GetOrCreate(){if(!instance){var go=new GameObject("Photo Friend Pipeline");DontDestroyOnLoad(go);instance=go.AddComponent<PhotoFriendPipeline>();}return instance;}
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;Directory.CreateDirectory(Root);
            foreach(string path in Directory.GetFiles(Root,"draft.json",SearchOption.AllDirectories))try{var d=JsonUtility.FromJson<PhotoFriendDraft>(File.ReadAllText(path));if(d!=null&&!string.IsNullOrEmpty(d.draftId)&&!NativePhotoSourceStore.LegacyArchived(d.draftId))drafts.Add(d);}catch(Exception ex){Debug.LogWarning("Photo draft: "+ex.Message);}
            foreach(var d in drafts)if(d.stage==PhotoFriendStage.Failed&&!string.IsNullOrEmpty(d.failure)&&d.failure.Any(c=>c<128&&char.IsLetter(c)))d.failure="请重新匹配或手动制作";
            // Pending is instance-local ownership, not a surviving CTS. Resume
            // only the already requested local mask analysis after a reload;
            // other native stages must not retain a stale pending UI lock.
            foreach(var d in drafts.Where(IsNative).ToArray())
            {
                bool pending=d.nativeMaskPending;d.nativeMaskPending=false;
                if(pending&&d.stage==PhotoFriendStage.Mask)StartMaskAnalysis(d);
                else if(pending)Save(d);
            }
            foreach(var d in drafts.Where(d=>IsNative(d)&&d.stage==PhotoFriendStage.Generating).ToArray())Generate(d);
        }
        void OnDestroy()
        {
            tearingDown=true;
            var owners=httpRequests.Values.Concat(generationOwners.Values).Distinct().ToArray();
            httpRequests.Clear();generationOwners.Clear();active.Clear();if(instance==this)instance=null;
            try{foreach(var owner in owners)RequestCancellation(owner);}
            finally
            {
                foreach(string id in sources.Keys.Concat(cutouts.Keys).Concat(undo.Keys).Concat(redo.Keys).Distinct().ToArray())ReleaseDraftResources(id);
            }
            // Each async operation disposes its own CTS in finally; do not
            // dispose tokens that a pending await/native cancellation still uses.
        }
        public PhotoFriendDraft Create(string personId=null,int revision=0)
        {if(tearingDown)throw new ObjectDisposedException(nameof(PhotoFriendPipeline));var d=new PhotoFriendDraft{draftId=Guid.NewGuid().ToString("N"),personId=personId,appearanceRevision=revision,editingExisting=!string.IsNullOrEmpty(personId),pipelineMode=NativeMode,stage=PhotoFriendStage.Image};drafts.Add(d);Save(d);return d;}
        public PhotoFriendDraft Find(string id)=>tearingDown?null:drafts.FirstOrDefault(d=>d.draftId==id);
        public void ImportRegions(PhotoFriendDraft draft,string pngPath)
        {
            if(!IsLive(draft)||active.Contains(draft.draftId)||draft.characterData==null||string.IsNullOrEmpty(draft.packageId))throw new InvalidOperationException("请先完成照片模型");
            var file=new FileInfo(pngPath);if(!file.Exists||file.Length>8*1024*1024)throw new InvalidDataException("颜色分区图片无效");
            var package=FriendPhotoRegions.CreateVariant(draft.packageId,File.ReadAllBytes(pngPath),draft.characterData);
            draft.packageId=package.packageId;draft.characterData=package.characterData;draft.appearanceRevision++;draft.failure="";Save(draft);
        }
        public void Save(PhotoFriendDraft d)
        {if(!IsLive(d)||!IsNative(d))return;string dir=Path.Combine(Root,d.draftId);Directory.CreateDirectory(dir);string file=Path.Combine(dir,"draft.json"),temp=file+".writing";File.WriteAllText(temp,JsonUtility.ToJson(d,true));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        public void RemoveDraft(string id)
        {
            if(tearingDown||string.IsNullOrEmpty(id))return;var d=Find(id);
            if(d==null){CancelRemovedDraft(id);ReleaseDraftResources(id);return;}
            bool native=IsNative(d);if(!native)NativePhotoSourceStore.ArchiveLegacy(id);
            // Revoke membership before cancellation can complete an old await.
            drafts.Remove(d);CancelRemovedDraft(id);ReleaseDraftResources(id);FriendPortraitCache.ForgetDraft(id);
            if(!native)return;
            string dir=Path.GetFullPath(Path.Combine(Root,id));if(dir.StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(dir))Directory.Delete(dir,true);
        }
        public void Select(PhotoFriendDraft d)
        {
            if(!IsLive(d))return;
            string[] paths;try{paths=StandaloneFileBrowser.OpenFilePanel("选择照片","",new[]{new ExtensionFilter("图片","png","jpg","jpeg")},false);}catch(Exception error){if(IsLive(d)){d.failure=Friendly(error,"选择图片窗口没能打开");Save(d);}return;}
            if(paths==null||paths.Length==0||string.IsNullOrEmpty(paths[0]))return;
            ImportImage(d,paths[0]);
        }
        public void ImportImage(PhotoFriendDraft d,string path)
        {
            if(!IsLive(d))return;
            try
            {
                if(!IsNative(d))throw new InvalidOperationException("这份草稿已过期，请重新添加照片。");
                var info=new FileInfo(path);if(info.Length>32*1024*1024)throw new InvalidOperationException("图片不能超过32MB");
                var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!t.LoadImage(File.ReadAllBytes(path))){Destroy(t);throw new InvalidOperationException("无法读取图片");}
                if(t.width>8192||t.height>8192){Destroy(t);throw new InvalidOperationException("图片尺寸不能超过8192");}
                if(t.width<MinWorkingSide||t.height<MinWorkingSide){Destroy(t);throw new InvalidOperationException("图片太小，请换一张");}
                t=Downscale(t);
                if(d.characterData!=null)d.nativeBaseJson=JsonUtility.ToJson(NativePhotoParameterFit.NativeBase(d.characterData));Cancel(d);string dir=Path.Combine(Root,d.draftId);d.sourcePath=Path.Combine(dir,"source.png");File.WriteAllBytes(d.sourcePath,t.EncodeToPNG());
                if(sources.TryGetValue(d.draftId,out var old))Destroy(old);sources[d.draftId]=t;d.maskConfirmed=false;d.jobId=d.sessionId=d.eventId=d.uploadedPath=d.remoteResultUrl=d.remoteButton=d.modelPath=d.packageId=null;d.jobProvider=d.modelProvider=d.regionMaskPath=d.regionJob=d.regionPackageId=d.regionVersion=d.lastModelSource=null;d.remotePhase=0;d.inputRevision++;d.jobInputRevision=d.inputRevision;d.characterData=null;d.failure="";
                d.maskPath=Path.Combine(dir,"mask.png");d.cutoutPath=Path.Combine(dir,"cutout.png");AutoMask(d);d.stage=PhotoFriendStage.Mask;Save(d);StartMaskAnalysis(d);
            }catch(Exception ex){if(IsLive(d)){d.failure=Friendly(ex,"图片读取失败，请换一张。");Save(d);}}
        }
        // Box-filter downscale; keeps the short side at least MinWorkingSide.
        static Texture2D Downscale(Texture2D t)
        {
            int w=t.width,h=t.height;float scale=Math.Min(1f,Math.Max((float)MaxWorkingSide/Math.Max(w,h),(float)MinWorkingSide/Math.Min(w,h)));if(scale>=1)return t;
            int nw=Mathf.Clamp(Mathf.RoundToInt(w*scale),1,w),nh=Mathf.Clamp(Mathf.RoundToInt(h*scale),1,h);if(nw==w&&nh==h)return t;
            var src=t.GetPixels32();var dst=new Color32[nw*nh];
            for(int y=0;y<nh;y++)
            {
                int sy0=(int)((long)y*h/nh),sy1=Math.Max(sy0+1,(int)((long)(y+1)*h/nh));
                for(int x=0;x<nw;x++)
                {
                    int sx0=(int)((long)x*w/nw),sx1=Math.Max(sx0+1,(int)((long)(x+1)*w/nw)),r=0,g=0,b=0,a=0,n=0;
                    for(int sy=sy0;sy<sy1;sy++){int row=sy*w;for(int sx=sx0;sx<sx1;sx++){var c=src[row+sx];r+=c.r;g+=c.g;b+=c.b;a+=c.a;n++;}}
                    dst[y*nw+x]=new Color32((byte)(r/n),(byte)(g/n),(byte)(b/n),(byte)(a/n));
                }
            }
            var result=new Texture2D(nw,nh,TextureFormat.RGBA32,false);result.SetPixels32(dst);result.Apply();Destroy(t);return result;
        }
        public Texture2D Source(PhotoFriendDraft d)
        {if(!IsLive(d))return null;if(sources.TryGetValue(d.draftId,out var t)&&t)return t;if(string.IsNullOrEmpty(d.sourcePath)||!File.Exists(d.sourcePath))return null;var image=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!image.LoadImage(File.ReadAllBytes(d.sourcePath))){Destroy(image);return null;}sources[d.draftId]=image;return image;}
        public Texture2D Cutout(PhotoFriendDraft d)
        {if(!IsLive(d))return null;if(cutouts.TryGetValue(d.draftId,out var t)&&t)return t;if(string.IsNullOrEmpty(d.cutoutPath)||!File.Exists(d.cutoutPath))return Source(d);var image=new Texture2D(2,2,TextureFormat.RGBA32,false);image.LoadImage(File.ReadAllBytes(d.cutoutPath));cutouts[d.draftId]=image;return image;}
        // Connected border removal is an initial editable mask. Existing alpha is
        // preserved; it does not infer identity, age, personality or preferences.
        void AutoMask(PhotoFriendDraft d)
        {
            var source=Source(d);var pixels=source.GetPixels32();int width=source.width,height=source.height;bool alpha=pixels.Any(p=>p.a<240);
            if(!alpha)
            {
                var seen=new bool[pixels.Length];var queue=new Queue<int>();
                Action<int> seed=i=>{if(!seen[i]){seen[i]=true;queue.Enqueue(i);}};
                for(int x=0;x<width;x++){seed(x);seed((height-1)*width+x);}for(int y=0;y<height;y++){seed(y*width);seed(y*width+width-1);}
                var corners=new[]{pixels[0],pixels[width-1],pixels[(height-1)*width],pixels[pixels.Length-1]};
                while(queue.Count>0)
                {
                    int i=queue.Dequeue();var color=pixels[i];bool bg=false;
                    for(int cornerIndex=0;cornerIndex<corners.Length;cornerIndex++)
                    {
                        var c=corners[cornerIndex];
                        if(Math.Abs(c.r-color.r)+Math.Abs(c.g-color.g)+Math.Abs(c.b-color.b)<66){bg=true;break;}
                    }
                    if(!bg)continue;
                    pixels[i].a=0;int x=i%width,y=i/width;if(x>0)seed(i-1);if(x<width-1)seed(i+1);if(y>0)seed(i-width);if(y<height-1)seed(i+width);
                }
            }
            var cutout=new Texture2D(width,height,TextureFormat.RGBA32,false);cutout.SetPixels32(pixels);cutout.Apply();if(cutouts.TryGetValue(d.draftId,out var old))Destroy(old);cutouts[d.draftId]=cutout;SaveMask(d);undo[d.draftId]=new Stack<byte[]>();redo[d.draftId]=new Stack<byte[]>();
        }
        public void BeginStroke(PhotoFriendDraft d)
        {if(!IsLive(d))return;CancelAnalysis(d);d.nativeMaskPending=false;if(!undo.ContainsKey(d.draftId))undo[d.draftId]=new Stack<byte[]>();if(!redo.ContainsKey(d.draftId))redo[d.draftId]=new Stack<byte[]>();undo[d.draftId].Push(Cutout(d).EncodeToPNG());redo[d.draftId].Clear();d.inputRevision++;}
        public void Paint(PhotoFriendDraft d,Vector2 uv,float radius,bool restore)
        {
            if(!IsLive(d))return;
            var target=Cutout(d);var original=Source(d);int x=Mathf.RoundToInt(uv.x*target.width),y=Mathf.RoundToInt(uv.y*target.height),r=Mathf.Max(2,Mathf.RoundToInt(radius*target.width));
            for(int py=Mathf.Max(0,y-r);py<Mathf.Min(target.height,y+r);py++)for(int px=Mathf.Max(0,x-r);px<Mathf.Min(target.width,x+r);px++)if((px-x)*(px-x)+(py-y)*(py-y)<=r*r){var c=original.GetPixel(px,py);c.a=restore?c.a:0;target.SetPixel(px,py,c);}target.Apply();d.maskConfirmed=false;
        }
        public void EndStroke(PhotoFriendDraft d){if(!IsLive(d))return;SaveMask(d);Save(d);}
        void SaveMask(PhotoFriendDraft d)
        {if(!IsLive(d))return;var target=Cutout(d);File.WriteAllBytes(d.cutoutPath,target.EncodeToPNG());var mask=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);var colors=target.GetPixels32();for(int i=0;i<colors.Length;i++)colors[i]=new Color32(colors[i].a,colors[i].a,colors[i].a,255);mask.SetPixels32(colors);mask.Apply();File.WriteAllBytes(d.maskPath,mask.EncodeToPNG());Destroy(mask);}
        public void UndoMask(PhotoFriendDraft d)=>History(d,undo,redo);
        public void RedoMask(PhotoFriendDraft d)=>History(d,redo,undo);
        void History(PhotoFriendDraft d,Dictionary<string,Stack<byte[]>> from,Dictionary<string,Stack<byte[]>> to)
        {if(!IsLive(d))return;if(!from.TryGetValue(d.draftId,out var stack)||stack.Count==0)return;if(!to.ContainsKey(d.draftId))to[d.draftId]=new Stack<byte[]>();to[d.draftId].Push(Cutout(d).EncodeToPNG());Cutout(d).LoadImage(stack.Pop());SaveMask(d);d.inputRevision++;d.maskConfirmed=false;Save(d);}
        public void ConfirmMask(PhotoFriendDraft d){if(!IsLive(d))return;if(d.nativeMaskPending){d.failure="人像轮廓正在处理";return;}d.maskConfirmed=true;d.stage=PhotoFriendStage.Ready;SaveMask(d);Save(d);}
        public void EditMask(PhotoFriendDraft d){if(!IsLive(d))return;if(active.Contains(d.draftId))Cancel(d);d.stage=PhotoFriendStage.Mask;d.maskConfirmed=false;Save(d);}
        void CancelAnalysis(PhotoFriendDraft d){if(IsLive(d)&&httpRequests.TryGetValue(d.draftId,out var token))RequestCancellation(token);}
        async void StartMaskAnalysis(PhotoFriendDraft d)
        {
            if(!IsLive(d))return;int revision=d.inputRevision;var cancellation=new CancellationTokenSource();bool autoMatch=false,generateAfter=false;
            try
            {
                RegisterRequest(d.draftId,cancellation);d.nativeMaskPending=true;Save(d);
                var features=await NativePhotoParameterFit.Analyze(AutoAnalysisPixels(d,out int width,out int height),width,height,cancellation.Token);
                if(!CanApply(d,cancellation,revision)||d.stage!=PhotoFriendStage.Mask)return;
                NativePhotoParameterFit.ApplyMask(Source(d),Cutout(d),features);SaveMask(d);SaveFeatures(d,features);d.failure="";
                // The player can still use 修改轮廓 afterwards; Failed shows 重新匹配/修改轮廓/手动制作.
                d.maskConfirmed=true;if(features.face==null){d.failure=NoFaceMessage;d.stage=PhotoFriendStage.Failed;}else{d.stage=PhotoFriendStage.Ready;autoMatch=true;}
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(CanApply(d,cancellation,revision))d.failure=Friendly(error,"自动抠图失败，请手动修改轮廓。");}
            finally
            {
                try
                {
                    // Successful results have already changed Mask to Ready or
                    // Failed. Finally owns pending by token, not by stage.
                    if(OwnsRequest(d,cancellation,revision)){d.nativeMaskPending=false;Save(d);generateAfter=autoMatch&&d.stage==PhotoFriendStage.Ready;}
                }
                finally{ForgetRequest(d.draftId,cancellation);cancellation.Dispose();}
            }
            // Capture owner qualification before removing the mask map. There is
            // no await between this final live check and the normal Generate.
            if(generateAfter&&IsLive(d)&&d.inputRevision==revision&&d.stage==PhotoFriendStage.Ready)Generate(d);
        }
        // First analysis uses the original pixels inside the border-removal bounds. The
        // border flood fill can eat dark clothes or hair in cluttered rooms; feeding those
        // holes to the segmenter made the cut-out blotchy.
        Color32[] AutoAnalysisPixels(PhotoFriendDraft d,out int width,out int height)
        {
            var source=Source(d);var cutout=Cutout(d);if(!source)throw new InvalidDataException("照片不存在");width=source.width;height=source.height;var pixels=source.GetPixels32();
            if(!cutout||cutout.width!=width||cutout.height!=height)return pixels;
            var mask=cutout.GetPixels32();int x0=width,y0=height,x1=-1,y1=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(mask[y*width+x].a>24){if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y;}
            if(x1<x0||y1<y0)return pixels;int pad=Math.Max(2,Math.Max(width,height)/50);x0=Math.Max(0,x0-pad);y0=Math.Max(0,y0-pad);x1=Math.Min(width-1,x1+pad);y1=Math.Min(height-1,y1+pad);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(x<x0||x>x1||y<y0||y>y1)pixels[y*width+x].a=0;
            return pixels;
        }
        void SaveFeatures(PhotoFriendDraft d,SushiAndFish.FriendsLife.PhotoProcessing.NativePhotoFeatures features)
        {if(!IsLive(d))return;d.nativeFeaturesPath=Path.Combine(Root,d.draftId,"native-features-"+d.inputRevision+".json");File.WriteAllText(d.nativeFeaturesPath,JsonUtility.ToJson(features));d.nativeFeatureRevision=d.inputRevision;}
        public PhotoFriendDraft CreateNativeFromLegacy(PhotoFriendDraft old)
        {
            if(!IsLive(old))throw new InvalidOperationException("草稿已移除");
            var d=Create(old.personId,old.appearanceRevision);d.legacyDraftId=old.draftId;d.legacyPackageId=old.packageId;d.legacyAppearanceJson=old.characterData==null?null:JsonUtility.ToJson(old.characterData);d.nativeBaseJson=JsonUtility.ToJson(NativePhotoParameterFit.NativeBase(old.characterData));d.characterData=NativePhotoParameterFit.HasGenerated(old.characterData)?null:NativePhotoParameterFit.Copy(old.characterData);d.editingExisting=!string.IsNullOrEmpty(old.personId)&&!string.IsNullOrEmpty(FriendsLifeRuntime.Instance?.GetPerson(old.personId)?.ownedHomeId);d.nativeAvatarJson=old.nativeAvatarJson;d.displayName=old.displayName;d.catchphrase=old.catchphrase;d.hobby=old.hobby;
            if(!string.IsNullOrEmpty(old.sourcePath)&&File.Exists(old.sourcePath))LoadRetainedSource(d,File.ReadAllBytes(old.sourcePath),!string.IsNullOrEmpty(old.maskPath)&&File.Exists(old.maskPath)?File.ReadAllBytes(old.maskPath):null);else d.failure="请重新选择照片";Save(d);return d;
        }
        public bool AttachRetainedSource(PhotoFriendDraft d,CharacterData old)
        {
            if(!IsLive(d))return false;
            if(!NativePhotoSourceStore.TryRead(d.personId,old,out var image,out var mask,out var package))return false;
            d.legacyPackageId=package;d.nativeBaseJson=JsonUtility.ToJson(NativePhotoParameterFit.NativeBase(old));d.legacyAppearanceJson=old==null?null:JsonUtility.ToJson(old);LoadRetainedSource(d,image,mask);return true;
        }
        void LoadRetainedSource(PhotoFriendDraft d,byte[] image,byte[] mask)
        {
            if(!IsLive(d))return;
            string dir=Path.Combine(Root,d.draftId);Directory.CreateDirectory(dir);d.sourcePath=Path.Combine(dir,"source.png");File.WriteAllBytes(d.sourcePath,image);d.maskPath=Path.Combine(dir,"mask.png");d.cutoutPath=Path.Combine(dir,"cutout.png");
            var source=Source(d);var target=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);target.SetPixels32(source.GetPixels32());target.Apply();cutouts[d.draftId]=target;
            if(mask!=null)
            {
                var alpha=new Texture2D(2,2,TextureFormat.RGBA32,false);try{if(!alpha.LoadImage(mask)||alpha.width!=source.width||alpha.height!=source.height)throw new InvalidDataException("旧轮廓尺寸不匹配");var colors=target.GetPixels32();var values=alpha.GetPixels32();for(int i=0;i<colors.Length;i++)colors[i].a=(byte)(colors[i].a*values[i].r/255);target.SetPixels32(colors);target.Apply();}finally{Destroy(alpha);}d.maskConfirmed=true;d.stage=PhotoFriendStage.Ready;
            }
            else{AutoMask(d);d.maskConfirmed=false;d.stage=PhotoFriendStage.Mask;}
            d.inputRevision++;SaveMask(d);Save(d);
        }
        public async void Generate(PhotoFriendDraft d)
        {
            if(!IsLive(d))return;
            if(!IsNative(d)){d.failure="请点“按照片匹配外貌”。";return;}
            if(active.Contains(d.draftId))return;if(d.nativeMaskPending){d.failure="请等待人像轮廓处理";return;}if(!d.maskConfirmed){d.failure="请先确定轮廓";Save(d);return;}
            int revision=d.inputRevision;string job=Guid.NewGuid().ToString("N");var cancellation=new CancellationTokenSource();
            try
            {
                generationOwners[d.draftId]=cancellation;active.Add(d.draftId);RegisterRequest(d.draftId,cancellation);
                d.jobId=job;d.jobInputRevision=revision;d.stage=PhotoFriendStage.Generating;d.failure="";Save(d);
                SushiAndFish.FriendsLife.PhotoProcessing.NativePhotoFeatures features=null;
                // Cached features went through JsonUtility: empty arrays are normalized back to null.
                if(d.nativeFeatureRevision==revision&&!string.IsNullOrEmpty(d.nativeFeaturesPath)&&File.Exists(d.nativeFeaturesPath))
                    try{features=NativePhotoParameterFit.Normalize(JsonUtility.FromJson<SushiAndFish.FriendsLife.PhotoProcessing.NativePhotoFeatures>(File.ReadAllText(d.nativeFeaturesPath)));}catch(Exception error){Debug.LogWarning("[FriendsLife] Photo features cache: "+error.Message);features=null;}
                bool analyzed=false;if(!NativePhotoParameterFit.Usable(features)){features=await NativePhotoParameterFit.Analyze(Cutout(d),cancellation.Token);analyzed=true;}
                cancellation.Token.ThrowIfCancellationRequested();if(!CanApply(d,cancellation,revision)||d.jobId!=job||d.stage!=PhotoFriendStage.Generating)return;
                if(analyzed)d.nativeFeatureRevision=-1;
                if(d.nativeFeatureRevision!=revision)SaveFeatures(d,features);
                if(features.face==null){d.failure=NoFaceMessage;d.stage=PhotoFriendStage.Failed;Save(d);return;}
                var baseline=string.IsNullOrEmpty(d.nativeBaseJson)?d.characterData:JsonUtility.FromJson<CharacterData>(d.nativeBaseJson);var fit=NativePhotoParameterFit.Fit(features,baseline,d.sourcePath,d.cutoutPath,revision);fit.character.characterName=d.displayName??"";NativePhotoParameterFit.Validate(fit.character);
                d.characterData=fit.character;d.fitVersion=NativePhotoParameterFit.Version;d.nativeFitJson=JsonUtility.ToJson(fit);d.appearanceRevision++;d.stage=PhotoFriendStage.Editor;Save(d);
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(CanApply(d,cancellation,revision)&&d.jobId==job){d.failure=Friendly(error,"外貌匹配失败，请换一张照片再试。");d.stage=PhotoFriendStage.Failed;Save(d);}}
            finally
            {
                // Generation ownership is independent of the latest mask/http
                // mapping: old finally clears its own marker, never a newer one.
                FinishGeneration(d.draftId,cancellation);ForgetRequest(d.draftId,cancellation);cancellation.Dispose();
            }
        }
        static void Observe(System.Threading.Tasks.Task task)
        {task.ContinueWith(completed=>{var observed=completed.Exception;},System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);}
        public void Cancel(PhotoFriendDraft d)
        {if(!IsLive(d)||!IsNative(d))return;if(httpRequests.TryGetValue(d.draftId,out var cancellation))RequestCancellation(cancellation);d.nativeMaskPending=false;if(d.stage==PhotoFriendStage.Generating||d.stage==PhotoFriendStage.Package||d.stage==PhotoFriendStage.Conform){d.stage=PhotoFriendStage.Cancelled;d.failure="已取消";Save(d);}}
        public static string Label(PhotoFriendDraft d)=>!IsNative(d)?"旧版草稿，请重新添加照片":d.nativeMaskPending?"正在抠出人像":StatusLabel(d.stage);
        public static string StatusLabel(PhotoFriendStage stage)
        {switch(stage){case PhotoFriendStage.Image:return "选择图片";case PhotoFriendStage.Mask:return "确认轮廓";case PhotoFriendStage.Ready:return "准备匹配外貌";case PhotoFriendStage.Generating:return "正在匹配外貌";case PhotoFriendStage.Package:return "旧版草稿";case PhotoFriendStage.Conform:return "旧版草稿";case PhotoFriendStage.Editor:return "外貌已匹配，可继续修改";case PhotoFriendStage.Profile:return "填写资料";case PhotoFriendStage.AwaitingHome:return "安排住所";case PhotoFriendStage.Cancelled:return "已取消";default:return "外貌匹配未完成";}}
    }
    public sealed class PhotoMaskBrush:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public bool restore;PhotoFriendPipeline pipeline;PhotoFriendDraft draft;RawImage image;bool painting;
        public void Bind(PhotoFriendPipeline p,PhotoFriendDraft d,RawImage target){pipeline=p;draft=d;image=target;}
        public void OnPointerDown(PointerEventData e){if(draft.stage!=PhotoFriendStage.Mask)return;painting=true;pipeline.BeginStroke(draft);OnDrag(e);}
        public void OnDrag(PointerEventData e){if(!painting)return;if(RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,e.position,e.pressEventCamera,out var p)){var rect=image.rectTransform.rect;pipeline.Paint(draft,new Vector2((p.x-rect.x)/rect.width,(p.y-rect.y)/rect.height),.025f,restore);}}
        public void OnPointerUp(PointerEventData e){if(!painting)return;painting=false;pipeline.EndStroke(draft);}
    }
}
