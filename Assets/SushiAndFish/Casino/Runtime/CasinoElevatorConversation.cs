using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
using Choice=PaperPixelGames.Core.Managers.DialoguePanelController.ConversationChoice;

namespace SushiAndFish.Casino
{
    public sealed class CasinoElevatorConversation : MonoBehaviour
    {
        public ResidentBillboardText billboardTemplate;
        ResidentBillboardText billboard;
        DialoguePanelController panel;
        ParkCasinoIntegration casino;
        bool open;
        static readonly string[] FloorNames={"LOBBY","第一层","第二层","第三层","第四层","BOSS"};
        public bool IsOpen => open && panel && panel.HasCustomConversation;

        public bool Open(ElevatorGuy npc, ParkCasinoIntegration host)
        {
            var elevator=NetworkSingleton<ElevatorManager>.Instance;
            if(!billboardTemplate || !Singleton<GameUIManager>.Instance || elevator.isTeleporting || elevator.IsLocked) return false;
            casino=host;
            panel=Singleton<GameUIManager>.Instance.DialoguePanelController;
            if(!panel || panel.IsVisible)return false;
            if(!billboard)billboard=Instantiate(billboardTemplate,npc.transform);
            // Match the shop billboard's world size at both the outdoor lobby
            // scale and the full size indoor floors.
            var size=billboardTemplate.transform.lossyScale*host.CurrentScale;
            var parentScale=npc.transform.lossyScale;
            billboard.transform.localScale=new Vector3(size.x/parentScale.x,size.y/parentScale.y,size.z/parentScale.z);
            // The native shop caption is centred at the actor's chest. Keep
            // that placement at both the scaled lobby and full size floors.
            var bodyCollider=npc.GetComponentsInChildren<Collider>().FirstOrDefault(c=>c.enabled&&!c.isTrigger);
            Vector3 chest=bodyCollider?bodyCollider.bounds.center:npc.transform.position;
            var camera=CasinoRuntimeContext.PlayerCamera;
            Vector3 view=camera.WorldToViewportPoint(chest);
            float originalDepth=view.z,frontDepth=view.z;
            foreach(var renderer in npc.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled)continue;
                var bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    frontDepth=Mathf.Min(frontDepth,camera.WorldToViewportPoint(point).z);
                }
            }
            // Keep the original projected type size, while preventing the
            // attendant's head from occluding the native shop caption.
            view.z=Mathf.Max(camera.nearClipPlane+.08f,frontDepth-.05f*host.CurrentScale);
            if(originalDepth>0)billboard.transform.localScale*=view.z/originalDepth;
            view.y=.62f;
            billboard.transform.position=camera.ViewportToWorldPoint(view);
            open=true;
            Greeting();
            return true;
        }
        void Greeting()
        {
            string message=casino.CurrentFloor==0 ? "老实赚钱，很枯燥吧！快来让你的积蓄翻倍吧！" :
                casino.CurrentFloor==5 ? "怎么样，很壮观吧？接下来我们继续玩吧！" : "还要继续玩吗？";
            panel.ShowCustomConversation(billboard,message,Closed,
                new Choice("好的",FloorMenu),new Choice("不了",Decline),new Choice("结束",Quit));
        }
        void FloorMenu()
        {
            var choices=new List<Choice>();
            for(int i=1;i<=5;i++)
            {
                if(casino.CurrentFloor==i)continue;
                int floor=i;
                choices.Add(new Choice(FloorNames[i],()=>Travel(floor)));
            }
            choices.Add(new Choice("返回",Greeting));
            panel.ShowCustomConversation(billboard,casino.CurrentFloor==0 ?
                "请问要去第几层，我来帮你！还是说你想见见我们老板？" : "还要去哪些层呢？",Closed,choices.ToArray());
        }
        void Decline() { if(casino.CurrentFloor==0)Quit();else Travel(0); }
        void Travel(int floor)
        {
            Quit();
            // The exact path used by the original elevator button. It retains
            // its player check, door tweens, floor activation and opening SFX.
            NetworkSingleton<ElevatorManager>.Instance.ServerTryTeleportPlayers(floor);
        }
        void Quit() { if(panel&&panel.HasCustomConversation)panel.Hide();else Closed(); }
        void Closed() {open=false;if(billboard)billboard.Hide();}
        void OnDisable(){if(open)Quit();}
    }
}
