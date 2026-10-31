using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TomodachiLife.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife
{
    // Question pictures and boards copied from IslandLifeHudActivities. Only
    // layout/palette/portrait access adapt to the host tablet; no game rule,
    // answer input, reward, clock or camera is changed here.
    public static class FriendMinigameVisuals
    {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Queue<string> order=new Queue<string>();
        static readonly Color Paper=new Color(.96f,.91f,.79f),Orange=new Color(.83f,.39f,.16f);
        public static string ObjectLabel(string value)=>value=="Apple"?"苹果":value=="Guitar"?"吉他":value=="BaseBall"?"棒球":value=="Umbrella"?"雨伞":value;
        static Texture2D Cached(string key,Func<Texture2D> create)
        {
            if(textures.TryGetValue(key,out var texture)&&texture)return texture;
            texture=create();textures[key]=texture;order.Enqueue(key);
            while(order.Count>48){string old=order.Dequeue();if(textures.TryGetValue(old,out var stale)){textures.Remove(old);if(stale)UnityEngine.Object.Destroy(stale);}}
            return texture;
        }
        static RawImage Picture(Transform parent,float x,float y,float width,float height)
        {var image=FriendUi.Rect("OriginalQuestionPicture",parent,x,y,width,height).gameObject.AddComponent<RawImage>();FriendUi.UseTabletGraphic(image);image.raycastTarget=false;return image;}
        public static void Append(Transform parent,ref float y,LifeMinigameSession game)
        {
            var timer=FriendUi.Text("GameClock",parent,0,y,1120,60,"",22);y+=70;
            var clock=timer.gameObject.AddComponent<FriendMinigameTicker>();clock.sessionId=game.id;clock.kind="Clock";
            if(game.definitionId=="Bowling"||game.definitionId=="FerrisWheelPush"||game.definitionId=="ViewFromFerrisWheel"||game.definitionId=="StatuesGame"||game.definitionId=="CoinTurn"||game.definitionId=="Dummy")
            {
                var board=FriendUi.Text("OriginalGameBoard",parent,0,y,1120,100,"",26);y+=110;
                var ticker=board.gameObject.AddComponent<FriendMinigameTicker>();ticker.sessionId=game.id;ticker.kind="Board";
            }
            if(game.definitionId=="EastAndWest"||game.definitionId=="EastAndWestGroup")
            {FriendUi.Text("UsedFoods",parent,0,y,1120,80,"已经提及："+string.Join("、",game.usedFoods.Skip(Math.Max(0,game.usedFoods.Count-8)).Select(TomodachiLife.NativeLifeLocalization.Food)),21);y+=90;}
            if(game.definitionId=="ThreeShellGame")
            {
                for(int i=0;i<3;i++){var cup=FriendUi.Text("CupBoard_"+i,parent,i*370,y,340,100,"",28);cup.alignment=TextAlignmentOptions.Center;var ticker=cup.gameObject.AddComponent<FriendMinigameTicker>();ticker.sessionId=game.id;ticker.kind="Cup";ticker.index=i;}
                y+=115;
            }
            bool mii=game.definitionId=="CafeLatteGame"||game.definitionId=="SilhouetteGameMii";
            if(!mii&&!game.definitionId.StartsWith("SilhouetteGame",StringComparison.Ordinal))return;
            if(game.definitionId=="SilhouetteGameNoting")
            {
                int visible=0;for(int i=0;i<game.options.Count;i++)if(i!=game.answer)Picture(parent,visible++*365,y,330,160).texture=Shape(game.options[i],false,false);
                y+=180;return;
            }
            if(game.answer<0||game.answer>=game.options.Count)return;
            var image=Picture(parent,425,y,250,225);
            if(mii)
            {
                var person=FriendsLifeRuntime.Instance?.PersonFromNative(game.options[game.answer]);
                var portrait=image.gameObject.AddComponent<FriendMinigamePortrait>();portrait.personId=person?.personId;portrait.coffee=game.definitionId=="CafeLatteGame";portrait.silhouette=true;
            }
            else image.texture=Shape(game.options[game.answer],game.definitionId!="SilhouetteGameMosaic",game.definitionId=="SilhouetteGameMosaic");
            if(game.definitionId=="SilhouetteGameZoom")image.uvRect=new Rect(.3f,.3f,.4f,.4f);
            y+=245;
        }
        public static void OddPortrait(Transform tile,LifeMinigameSession game,int index)
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||game.participantIds.Count==0)return;
            // The host's registered roster also retains unarrived drafts. The
            // actual source session already records its eligible participants.
            string native=game.participantIds[index==game.answer?Math.Min(1,game.participantIds.Count-1):0];
            var person=runtime.PersonFromNative(native);var image=Picture(tile,130,5,70,70);
            var portrait=image.gameObject.AddComponent<FriendMinigamePortrait>();portrait.personId=person?.personId;
            if(index==game.answer)image.rectTransform.localRotation=Quaternion.Euler(0,0,12);
        }
        public static Texture2D Portrait(Texture source,bool coffee)
        {
            if(!source)return null;
            return Cached("portrait/"+source.GetInstanceID()+"/"+coffee,()=>
            {
                var target=RenderTexture.GetTemporary(128,128,0);var old=RenderTexture.active;var image=new Texture2D(128,128,TextureFormat.RGBA32,false);
                try
                {
                    Graphics.Blit(source,target);RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();var pixels=image.GetPixels();Color bg=pixels[0];
                    for(int i=0;i<pixels.Length;i++){var c=pixels[i];float difference=Mathf.Abs(c.r-bg.r)+Mathf.Abs(c.g-bg.g)+Mathf.Abs(c.b-bg.b);bool foreground=bg.a<.1f?c.a>.1f:difference>.18f;pixels[i]=foreground?(coffee?new Color(.3f,.15f,.06f):FriendUi.Ink):(coffee?new Color(.89f,.76f,.52f):Paper);}
                    image.SetPixels(pixels);image.Apply();return image;
                }
                finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);}
            });
        }
        public static Texture2D Shape(string key,bool silhouette,bool mosaic)
        {
            return Cached("shape/"+key+"/"+silhouette+"/"+mosaic,()=>
            {
                int size=mosaic?16:128;var image=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=mosaic?FilterMode.Point:FilterMode.Bilinear};var pixels=new Color[size*size];var pieces=key.Split('+');
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float px=(x+.5f)/size,py=(y+.5f)/size;bool inside=false;
                    for(int n=0;n<pieces.Length;n++)
                    {float xx=px+(n==0?pieces.Length==2?.14f:0:-.14f);string p=pieces[n];float dx=xx-.5f,dy=py-.44f;bool shape=p=="Apple"?dx*dx/.09f+dy*dy/.075f<1||Mathf.Abs(xx-.53f)<.025f&&py>.68f&&py<.85f:p=="Guitar"?dx*dx/.055f+dy*dy/.07f<1||Mathf.Abs(dx)<.045f&&py>.4f&&py<.93f:p=="BaseBall"?dx*dx+dy*dy<.105f:p=="Umbrella"?dx*dx+(py-.55f)*(py-.55f)<.14f&&py>.55f||Mathf.Abs(dx)<.025f&&py>.15f&&py<.58f:false;inside|=shape;}
                    pixels[y*size+x]=inside?(silhouette?FriendUi.Ink:key.Contains("Apple")?new Color(.8f,.15f,.1f):Orange):Paper;
                }
                image.SetPixels(pixels);image.Apply();return image;
            });
        }
    }
    public sealed class FriendMinigamePortrait:MonoBehaviour
    {
        public string personId;public bool coffee,silhouette;RawImage image;TMP_Text waiting;Texture previous;float next;
        void Awake(){image=GetComponent<RawImage>();image.color=Color.clear;waiting=FriendUi.Text("PreparingPortrait",transform,0,0,((RectTransform)transform).rect.width,((RectTransform)transform).rect.height,"准备中…",20);waiting.alignment=TextAlignmentOptions.Center;}
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;var source=FriendPortraitCache.Get(personId);
            if(!source||source==previous)return;previous=source;image.texture=silhouette?FriendMinigameVisuals.Portrait(source,coffee):source;image.color=Color.white;if(waiting)waiting.gameObject.SetActive(false);
        }
    }
    public sealed class FriendMinigameTicker:MonoBehaviour
    {
        public string sessionId,kind;public int index;TMP_Text label;float next;
        void Awake(){label=GetComponent<TMP_Text>();}
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;var game=FriendsLifeRuntime.Instance?.Core?.State?.activities?.minigame;
            if(game==null||game.id!=sessionId||!label)return;
            if(kind=="Clock"){label.text="第 "+(game.round+1)+" 轮   得分 "+game.score+"   "+game.roundElapsed.ToString("0.0")+" 秒\n"+game.message;return;}
            if(kind=="Cup"){label.text="杯子 "+(index+1)+"\n"+(game.phase==0&&game.roundElapsed<.8f&&index<game.board.Count&&game.board[index]==1?"●":"▰");return;}
            switch(game.definitionId)
            {
                case "Bowling":label.text="球瓶："+new string('●',Math.Max(0,game.pins))+"\n本轮第 "+(game.shots+1)+" 投";break;
                case "FerrisWheelPush":label.text="○  当前吊舱 "+(game.board.Count>0?game.board[0]:0)+" → 目标 "+game.target+"\n剩余 "+Math.Max(0,3-game.shots)+" 次";break;
                case "ViewFromFerrisWheel":label.text="当前方向："+game.phase+"\n已记录："+string.Join("、",game.visitedViews);break;
                case "StatuesGame":label.text=(game.phase==0?"绿灯":"红灯")+"\n进度："+Mathf.RoundToInt(game.distance*100)+"%";break;
                case "CoinTurn":label.text=game.phase==1?"硬币旋转中…":game.phase==2?game.message:"预测正面或反面";break;
                case "Dummy":label.text=game.phase==1?"现在点击！":"等待…";break;
            }
        }
    }
}
