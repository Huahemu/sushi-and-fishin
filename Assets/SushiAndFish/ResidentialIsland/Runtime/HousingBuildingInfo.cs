using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;

namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingBuildingInfo
    {
        public string id,title;
        public Rect rect;
        public bool residence;
        public HousingHouse house;
        public readonly List<HousingFloorInfo> floors=new List<HousingFloorInfo>();
        public static List<HousingBuildingInfo> Collect()
        {
            var result=new List<HousingBuildingInfo>();
            var transforms=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Transform Find(string name)=>transforms.FirstOrDefault(t=>t.name==name&&t.parent&&t.parent.name.StartsWith("CityBuildings"));
            void Public(string id,string title,Transform source,string description)
            {
                if(!source)return;var bounds=BoundsOf(source);var item=new HousingBuildingInfo{id=id,title=title,rect=Rect.MinMaxRect(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z)};
                item.floors.Add(new HousingFloorInfo{title="一层",roots=new[]{source},description=()=>description});result.Add(item);
            }
            Public("restaurant","餐厅",Find("AR_Restaurant"),"客人常说“就吃两贯”，最后总要再点一盘。老板已经学会多拿一个碟子，省得来回跑。");
            var cinema=Find("AR_CinemaLobby");
            Public("cinema","电影院",cinema,"售票、爆米花和等朋友都在这一层。有人每次提前半小时来，朋友到了，他的爆米花也吃完了。");
            var auditorium=Find("AR_CinemaAuditorium");
            if(cinema&&auditorium)result.Last().floors.Add(new HousingFloorInfo{title="二层 · 放映厅",roots=new[]{auditorium},description=()=>"放映厅有 48 个座位。总有人挑最后一排，说是视野好；朋友知道，他只是怕又被发现睡着了。"});
            var casino=ParkCasinoIntegration.Instance;
            if(casino&&casino.Ready&&casino.ModuleRoot)
            {
                // Ground-level footprint of the casino where it stands in the park
                // (the elevated roof and signs are left out).
                var park=casino.CurrentFloor==0?casino.ModuleRoot:casino.permanentExterior?casino.permanentExterior.transform:null;
                var bounds=new Bounds(casino.parkOrigin,new Vector3(20,1,20));bool any=false;
                if(park)foreach(var r in park.GetComponentsInChildren<Renderer>())if(r.bounds.min.y<casino.parkOrigin.y+1.2f&&r.bounds.size.x<200){if(any)bounds.Encapsulate(r.bounds);else bounds=r.bounds;any=true;}
                var item=new HousingBuildingInfo{id="casino",title="赌场",rect=Rect.MinMaxRect(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z)};
                var floors=casino.ModuleRoot.GetComponentsInChildren<CasinoFloor>(true);
                for(int i=0;i<=5;i++)
                {
                    int floor=i;var floorRoots=floors.Where(f=>f.floorIndex==floor).Select(f=>f.transform).ToArray();
                    if(floor==5)
                    {
                        var boss=casino.ModuleRoot.GetComponentsInChildren<SushiAndFish.Casino.Original.CasinoLab.CasinoLabBossRoom>(true).FirstOrDefault();
                        if(boss)floorRoots=new[]{boss.transform};
                    }
                    item.floors.Add(new HousingFloorInfo{title=i==0?"LOBBY":i==5?"BOSS":i+" 层",roots=floorRoots,
                        description=i==0?()=>"电梯旁的人负责带路，机台负责收钱。门口没写营业结束时间；老板认为大家总能再玩一把。":
                            i==5?()=>"赌场老板。楼下每层都有机台，他给自己留了一间办公室。账算得很快，客人说要走时，他通常会问最后一把。":()=>Wagers(floor,floorRoots)});
                }
                result.Add(item);
            }
            foreach(var plot in HousingIsland.Instance.Plots.Where(p=>p.house))
            {
                var item=new HousingBuildingInfo{id=plot.id,title=plot.house.definition.title,rect=plot.rect,residence=true,house=plot.house};
                item.floors.Add(new HousingFloorInfo{title="住宅",roots=new[]{plot.house.transform},description=()=>""});result.Add(item);
            }
            return result;
        }
        static string Wagers(int floor,Transform[] roots)
        {
            var games=roots.SelectMany(t=>t.GetComponentsInChildren<GameBase>(true)).Distinct().ToArray();
            var manager=NetworkSingleton<SushiAndFish.Casino.Original.GameManager>.Instance;
            long quota=manager?manager.currentQuota:0;
            return string.Join("\n",games.GroupBy(g=>g.GameName+"|"+g.BaseMinBet+"|"+g.BaseMaxBet).Select(group=>
            {
                var game=group.First();long min=game.BaseMinBet,max=game.BaseMaxBet;
                if(manager)
                {
                    double multiplier=quota*.001*Math.Pow(2,game.casinoLevel-floor-1);
                    min=Math.Max(1L,(long)Math.Round(FathF.RoundByFirstNDigits(game.BaseMinBet*multiplier,2),MidpointRounding.AwayFromZero));
                    max=Math.Max(5L,(long)Math.Round(FathF.RoundByFirstNDigits(game.BaseMaxBet*multiplier*game.MaxBetOverrideMultiplier,2),MidpointRounding.AwayFromZero));
                }
                return "• "+(string.IsNullOrWhiteSpace(game.GameName)?game.GameType.ToString():game.GameName)+"  MIN "+min+" / MAX "+max;
            }).OrderBy(s=>s));
        }
        public static Bounds BoundsOf(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);var b=new Bounds(root.position,Vector3.zero);
            bool any=false;foreach(var r in renderers)if(r is MeshRenderer||r is SkinnedMeshRenderer){if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);}return b;
        }
    }
    public sealed class HousingFloorInfo {public string title;public Transform[] roots;public Func<string> description;}
}
