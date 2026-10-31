using PaperPixelGames.Core.Ai.NPCs;
using UnityEngine;

namespace SushiAndFish.FriendsLife.SourceGraphs
{
    [DefaultExecutionOrder(950)]
    public sealed class NativeDateFacialHold:MonoBehaviour
    {
        NativeDateFacialProjection projection;
        ResidentController actor;
        string owner;
        int sourceCode;
        public string LastError=>projection?.LastError;
        public string Owner=>owner;
        public int SourceCode=>sourceCode;
        public bool BlushAttached=>projection?.BlushOutfitAttached==true;
        public bool BlinkShapesBound=>projection?.BlinkShapesBound==true;
        public void Bind(ResidentController body,string executionKey,int expressionCode)
        {
            if(actor!=body||owner!=executionKey||projection==null)
            {Stop(owner);actor=body;owner=executionKey;projection=new NativeDateFacialProjection(body);}
            sourceCode=expressionCode;
        }
        void LateUpdate(){if(projection!=null&&Time.timeScale>0)projection.Apply(sourceCode);}
        public void Stop(string executionKey)
        {
            if(owner!=executionKey)return;
            projection?.Dispose();projection=null;actor=null;owner=null;
        }
        void OnDisable()=>Stop(owner);
        void OnDestroy()=>Stop(owner);
    }
}
