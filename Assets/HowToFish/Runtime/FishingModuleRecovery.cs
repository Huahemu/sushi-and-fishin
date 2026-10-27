using UnityEngine;
namespace HowToFish {
    public sealed partial class FishingModule {
        public int WaitingCatchCount {get{int count=0;foreach(var fish in loose)if(fish&&fish.IsAvailable&&!fish.Attached&&!fish.item.owner)count++;return count;}}
        public bool CatchAtRisk {get{foreach(var fish in loose)if(fish&&fish.IsEscaping)return true;return false;}}
        public string WaitingCatchNotice {get{int alive=0,dead=0;foreach(var fish in loose){if(!fish||!fish.IsAvailable||fish.Attached||fish.item.owner)continue;if(fish.IsDead)dead++;else alive++;}return alive>0?"活鱼 "+alive+" 条 · 切换鱼枪击败后拿取":dead>0?"已击败 "+dead+" 条 · E 拾取入库":"上岸后先射击，再拾取";}}
        public void GetLandingPose(FishingCatch fish,out Vector3 position,out Quaternion rotation){position=landingPoint.position+Vector3.up*.15f;rotation=landingPoint.rotation;}
    }
}
