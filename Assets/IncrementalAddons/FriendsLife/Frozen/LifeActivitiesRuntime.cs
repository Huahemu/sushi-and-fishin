using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        // Host gate (C009 IslandExtras). Off: no new accidents; saved ones still expire normally.
        public static bool IslandAccidentsEnabled=true;
        public const string ActivitiesProvenance="Playable activity reconstruction; source catalog IDs/labels/reward references retained; timing, interaction and unlock policy simplified";
        public Action<string,string> ActivityFacilityVisit;
        public int ActivitiesRevision=>EnsureActivities().revision;
        public IReadOnlyList<LifeTravelDefinition> TravelDestinations=>LifeActivitiesCatalog.Travels;
        public IReadOnlyList<LifeDreamDefinition> DreamCatalog=>LifeActivitiesCatalog.Dreams;
        public IReadOnlyList<LifePhotoDefinition> PhotoPresets=>LifeActivitiesCatalog.Photos;
        public LifeActivitiesState Activities { get{var value=EnsureActivities();RefreshActivityMarket();return value;} }
        private LifeActivitiesState EnsureActivities()
        {
            if(State.activities==null)State.activities=new LifeActivitiesState();var a=State.activities;
            // Unity JSON can materialize a serialized null class field as an
            // empty inline record. An absent session must remain absent.
            if(a.minigame!=null&&(string.IsNullOrEmpty(a.minigame.id)||string.IsNullOrEmpty(a.minigame.definitionId)))a.minigame=null;
            if(a.randomState==0)a.randomState=0x6d2b79f5;
            a.travels=a.travels??new List<LifeTravelSession>();a.dreams=a.dreams??new List<LifeDreamSession>();
            a.photos=a.photos??new List<LifePhotoRecord>();a.history=a.history??new List<LifeActivityRecord>();a.news=a.news??new List<LifeNewsBulletin>();
            a.accidents=a.accidents??new List<LifeAccident>();a.facilities=a.facilities??new List<LifeFacilityState>();a.market=a.market??new List<LifeMarketOffer>();
            a.unlockedPhotos=a.unlockedPhotos??new List<string>();a.dreamsSeen=a.dreamsSeen??new List<string>();a.wishes=a.wishes??new List<string>();
            foreach(var definition in LifeActivitiesCatalog.Facilities)if(!a.facilities.Any(f=>f.id==definition.id))a.facilities.Add(new LifeFacilityState{id=definition.id,unlocked=definition.id=="Square"});
            return a;
        }
        private int ActivityRandom(int maximum)
        {
            if(maximum<1)return 0;var a=EnsureActivities();uint n=a.randomState;n^=n<<13;n^=n>>17;n^=n<<5;a.randomState=n;return (int)((ulong)n*(uint)maximum>>32);
        }
        private LifeActivitiesResult ActivityResult(bool success,string message,string sessionId="",bool won=false,int reward=0)
        {LastError=success?null:message;return new LifeActivitiesResult{success=success,message=message,sessionId=sessionId,won=won,rewardRaw=reward};}
        private void ActivityChanged(){EnsureActivities().revision++;Changed();}
        private void ActivityMoney(int delta,string kind)
        {ChangeWallet(delta,kind,new LifeSimulationResolution());}
        private void ActivityInventory(string category,string item,int quantity=1)
        {
            if(string.IsNullOrEmpty(item)||quantity<=0)return;var stack=State.inventory.FirstOrDefault(v=>v.category==category&&v.itemName==item);
            if(stack==null){stack=new LifeSimulationInventoryStack{category=category,itemName=item};State.inventory.Add(stack);}stack.quantity+=quantity;
        }
        private bool ConsumeActivityItem(string category,string item)
        {var stack=State.inventory.FirstOrDefault(v=>v.category==category&&v.itemName==item&&v.quantity>0);if(stack==null)return false;stack.quantity--;return true;}
        private void ActivityRecord(string category,string definition,string residentId,string title,string result,bool won=false,int reward=0)
        {
            var a=EnsureActivities();a.history.Add(new LifeActivityRecord{id=Guid.NewGuid().ToString("N"),category=category,definitionId=definition,residentId=residentId,title=title,result=result,won=won,rewardRaw=reward,gameMinutes=State.gameMinutes});
            while(a.history.Count>300)a.history.RemoveAt(0);Journal("activity",result,ActivitiesProvenance,residentId,sourceEvent:definition);
            RecordDailyAction(residentId,"Other",title);
        }
        private void ActivityNews(string title,string body,string residentId="")
        {
            var a=EnsureActivities();a.news.Add(new LifeNewsBulletin{id=Guid.NewGuid().ToString("N"),title=title,body=body,residentId=residentId,gameMinutes=State.gameMinutes});
            while(a.news.Count>100)a.news.RemoveAt(0);
        }
        public bool IsResidentInActivity(string residentId)
        {
            var a=EnsureActivities();return a.minigame!=null&&!a.minigame.completed&&a.minigame.participantIds.Contains(residentId)||
                a.travels.Any(t=>!t.returned&&(t.residentId==residentId||t.partnerId==residentId))||a.dreams.Any(d=>d.residentId==residentId&&!d.woken);
        }
        public bool IsResidentAway(string residentId)
        {var a=EnsureActivities();return a.travels.Any(t=>!t.returned&&(t.residentId==residentId||t.partnerId==residentId))||a.dreams.Any(d=>d.residentId==residentId&&!d.woken);}
        public void AdvanceActivities(double elapsedGameMinutes)
        {
            if(double.IsNaN(elapsedGameMinutes)||double.IsInfinity(elapsedGameMinutes)||elapsedGameMinutes<0)return;
            var a=EnsureActivities();bool changed=false;int day=(int)(State.gameMinutes/Math.Max(60,Configuration.minutesPerDay));
            if(a.lastDay!=day)
            {
                a.lastDay=day;ActivityNews("第"+(day+1)+"天的海岛新闻","今天岛上有 "+State.residents.Count+" 位居民，已有 "+a.travelsCompleted+" 次旅行与 "+a.minigamesWon+" 场小游戏胜利。");changed=true;
            }
            if(RefreshActivityMarket())changed=true;
            foreach(var trip in a.travels.Where(t=>!t.returned))
            {
                foreach(string id in new[]{trip.residentId,trip.partnerId}){var r=FindResident(id);if(r!=null)r.activity="旅行中";}
                if(!trip.completed&&!trip.awaitingChoice&&State.gameMinutes>=trip.readyAt){trip.awaitingChoice=true;trip.message=TravelScene(trip);changed=true;}
            }
            foreach(var dream in a.dreams.Where(d=>!d.woken))
            {
                var resident=FindResident(dream.residentId);if(resident!=null)resident.activity="梦中";
                if(!dream.completed&&!dream.awaitingChoice&&State.gameMinutes>=dream.readyAt){dream.awaitingChoice=true;dream.message=DreamScene(dream);changed=true;}
            }
            if(IslandAccidentsEnabled&&State.residents.Count>0&&State.gameMinutes>=a.nextAccidentAt&&!a.accidents.Any(x=>!x.resolved))
            {
                a.nextAccidentAt=State.gameMinutes+180;var resident=State.residents[ActivityRandom(State.residents.Count)];
                if(!IsResidentInActivity(resident.id))
                {
                    string[] kinds={"LostItem","Stumble","WrongDelivery","Rain"};string kind=kinds[ActivityRandom(kinds.Length)];
                    string text=kind=="LostItem"?"在路边发现了遗失的东西。":kind=="Stumble"?"散步时不小心绊了一下。":kind=="WrongDelivery"?"收到了送错的包裹。":"出门时遇到了突然的阵雨。";
                    a.accidents.Add(new LifeAccident{id=Guid.NewGuid().ToString("N"),residentId=resident.id,kind=kind,message=resident.displayName+text,expiresAt=State.gameMinutes+90});changed=true;
                }
            }
            foreach(var accident in a.accidents.Where(x=>!x.resolved&&State.gameMinutes>=x.expiresAt).ToArray())
            {accident.resolved=true;accident.resolution="居民自行处理了这件小插曲。";var r=FindResident(accident.residentId);if(r!=null)r.energy=Need(r.energy-5);ActivityRecord("Accident",accident.kind,accident.residentId,"日常小插曲",accident.message+accident.resolution);changed=true;}
            if(changed)ActivityChanged();
        }
        public LifeActivitiesResult BuyTravelTicket()
        {
            if(State.walletRaw<1500)return ActivityResult(false,"购买旅行券需要1500。");ActivityMoney(-1500,"travel-ticket");ActivityInventory("TravelTicket","TravelTicket");ActivityChanged();return ActivityResult(true,"购买了一张旅行券。");
        }
        public LifeActivitiesResult StartTravel(string residentId,string destinationId,bool useTicket=true,string partnerId=null)
        {
            var definition=LifeActivitiesCatalog.Travels.FirstOrDefault(v=>v.id==destinationId);var resident=FindResident(residentId);var partner=FindResident(partnerId);
            if(definition==null||resident==null)return ActivityResult(false,"请选择居民与旅行目的地。");
            if(IsResidentInActivity(residentId)||partner!=null&&IsResidentInActivity(partner.id))return ActivityResult(false,"居民正在参加其他活动。");
            if(partner!=null&&(partner.id==residentId||!definition.canWedding))return ActivityResult(false,"这趟旅行暂时不适合双人出发。");
            if(useTicket){if(!ConsumeActivityItem("TravelTicket","TravelTicket"))return ActivityResult(false,"没有旅行券，请先在活动页购买。");}
            else{if(State.walletRaw<1500)return ActivityResult(false,"这次旅行需要1500或一张旅行券。");ActivityMoney(-1500,"travel-booking");}
            var trip=new LifeTravelSession{id=Guid.NewGuid().ToString("N"),residentId=residentId,partnerId=partner?.id??"",destinationId=destinationId,readyAt=State.gameMinutes+8,message="正在前往"+definition.title+"。"};
            EnsureActivities().travels.Add(trip);resident.activity="旅行中";if(partner!=null)partner.activity="旅行中";ActivityRecord("Travel",destinationId,residentId,definition.title,resident.displayName+" 出发去"+definition.title+"。");ActivityChanged();return ActivityResult(true,trip.message,trip.id);
        }
        private string TravelScene(LifeTravelSession trip)
        {var title=LifeActivitiesCatalog.Travels.First(t=>t.id==trip.destinationId).title;return title+" · "+new[]{"第1站：要做什么？","第2站：要做什么？","最后一站：要做什么？"}[Math.Min(2,trip.stage)];}
        public LifeActivitiesResult ChooseTravelActivity(string tripId,int choice)
        {
            var trip=EnsureActivities().travels.FirstOrDefault(t=>t.id==tripId);if(trip==null||trip.returned||trip.completed||!trip.awaitingChoice||choice<0||choice>2)return ActivityResult(false,"这段旅程还没有到作出选择的时候。");
            if(choice==1&&State.walletRaw<100)return ActivityResult(false,"品尝当地美食需要100。");if(choice==2&&State.walletRaw<150)return ActivityResult(false,"购买纪念品需要150。");
            if(choice==1)ActivityMoney(-100,"travel-food");if(choice==2)ActivityMoney(-150,"travel-souvenir");trip.exploration+=choice==0?2:1;trip.choices.Add(new[]{"游览风景","品尝当地美食","挑选纪念品"}[choice]);trip.stage++;trip.awaitingChoice=false;
            if(trip.stage>=3)return FinishTravel(trip,false);trip.readyAt=State.gameMinutes+8;trip.message="在旅行中："+trip.choices.Last()+"。";ActivityChanged();return ActivityResult(true,trip.message,trip.id);
        }
        public LifeActivitiesResult ReturnFromTravel(string tripId)
        {var trip=EnsureActivities().travels.FirstOrDefault(t=>t.id==tripId);return trip==null||trip.returned?ActivityResult(false,"这位居民已经在岛上了。"):FinishTravel(trip,true);}
        private LifeActivitiesResult FinishTravel(LifeTravelSession trip,bool early)
        {
            if(trip.returned)return ActivityResult(false,"旅行已经结算。");var a=EnsureActivities();var definition=LifeActivitiesCatalog.Travels.First(t=>t.id==trip.destinationId);trip.returned=trip.completed=true;
            foreach(string id in new[]{trip.residentId,trip.partnerId}){var r=FindResident(id);if(r!=null){r.activity="散步";r.energy=Need(r.energy+20);r.social=Need(r.social+20);}}
            string souvenirs="";
            if(!early)
            {
                foreach(string item in definition.foodRewards){ActivityInventory("Food",item);souvenirs+=" "+TomodachiLife.NativeLifeLocalization.Food(item);}
                foreach(string item in definition.goodsRewards){ActivityInventory("Goods",item);souvenirs+=" "+TomodachiLife.NativeLifeLocalization.Goods(item);}
                foreach(string item in definition.coordinateRewards){ActivityInventory("Coordinate",item);souvenirs+=" "+item;}
                if(souvenirs.Length==0){ActivityInventory("TravelTicket","TravelTicket");souvenirs=" 一张旅行券";}
                a.travelsCompleted++;UnlockProgressPhoto();
            }
            trip.message=early?"居民提前结束旅程，平安返回岛上。":"旅行结束，带回了"+souvenirs+"。";
            ActivityRecord("Travel",trip.destinationId,trip.residentId,definition.title,trip.message,!early);ActivityNews("旅行归来",FindResident(trip.residentId)?.displayName+" 完成了"+definition.title+"。",trip.residentId);ActivityChanged();return ActivityResult(true,trip.message,trip.id,!early);
        }
        public LifeActivitiesResult StartDream(string residentId,string dreamId)
        {
            var resident=FindResident(residentId);var definition=LifeActivitiesCatalog.Dreams.FirstOrDefault(d=>d.id==dreamId);
            if(resident==null||definition==null)return ActivityResult(false,"请选择一位居民和梦境。");if(IsResidentInActivity(residentId))return ActivityResult(false,"居民正在参加其他活动。");
            var dream=new LifeDreamSession{id=Guid.NewGuid().ToString("N"),residentId=residentId,dreamId=dreamId,readyAt=State.gameMinutes+5,message=resident.displayName+"睡着了。"};EnsureActivities().dreams.Add(dream);resident.activity="梦中";ActivityChanged();return ActivityResult(true,dream.message,dream.id);
        }
        private string DreamScene(LifeDreamSession dream)
        {var title=LifeActivitiesCatalog.Dreams.First(d=>d.id==dream.dreamId).title;return title+" · 梦里：要怎么做？";}
        public LifeActivitiesResult ChooseDreamAction(string dreamId,int choice)
        {
            var dream=EnsureActivities().dreams.FirstOrDefault(d=>d.id==dreamId);if(dream==null||dream.woken||dream.completed||!dream.awaitingChoice||choice<0||choice>2)return ActivityResult(false,"梦中的下一幕尚未出现。");
            dream.choices.Add(new[]{"靠近","观察","绕开"}[choice]);dream.lucidity+=choice==dream.stage%3?2:1;dream.stage++;dream.awaitingChoice=false;
            if(dream.stage>=3)return WakeFromDream(dreamId,false);dream.readyAt=State.gameMinutes+5;dream.message="梦还在继续。";ActivityChanged();return ActivityResult(true,dream.message,dream.id);
        }
        public LifeActivitiesResult WakeFromDream(string dreamId,bool early=true)
        {
            var a=EnsureActivities();var dream=a.dreams.FirstOrDefault(d=>d.id==dreamId);if(dream==null||dream.woken)return ActivityResult(false,"居民已经醒来了。");
            var definition=LifeActivitiesCatalog.Dreams.First(d=>d.id==dream.dreamId);dream.woken=dream.completed=true;var resident=FindResident(dream.residentId);if(resident!=null){resident.activity="散步";resident.energy=Need(resident.energy+30);}
            string present="";
            if(!early)
            {
                if(!string.IsNullOrEmpty(definition.rewardItem)){ActivityInventory(definition.rewardCategory,definition.rewardItem);present=definition.rewardCategory=="Food"?TomodachiLife.NativeLifeLocalization.Food(definition.rewardItem):TomodachiLife.NativeLifeLocalization.Goods(definition.rewardItem);}
                else{var item=Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)].name;ActivityInventory("Goods",item);present=TomodachiLife.NativeLifeLocalization.Goods(item);}
                a.dreamsCompleted++;if(!a.dreamsSeen.Contains(dream.dreamId))a.dreamsSeen.Add(dream.dreamId);UnlockProgressPhoto();
            }
            string sleeper=FindResident(dream.residentId)?.displayName??"居民";dream.message=early?sleeper+"醒了。":sleeper+"醒了，得到了 "+present+"。";ActivityRecord("Dream",dream.dreamId,dream.residentId,definition.title,dream.message,!early);ActivityChanged();return ActivityResult(true,dream.message,dream.id,!early);
        }
        public bool IsPhotoUnlocked(string presetId)
        {var definition=LifeActivitiesCatalog.Photos.FirstOrDefault(p=>p.id==presetId);return definition!=null&&(definition.sourceUnlockId==0||EnsureActivities().unlockedPhotos.Contains(presetId));}
        public LifeActivitiesResult RecordActivityPhoto(string residentId,string presetId,string path,string partnerId="",int pose=0,int angle=0)
        {
            var resident=FindResident(residentId);if(resident==null||!IsPhotoUnlocked(presetId))return ActivityResult(false,"请选择居民与已解锁的摄影场景。");
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return ActivityResult(false,"照片没有成功保存，请重试。");
            var photo=new LifePhotoRecord{id=Guid.NewGuid().ToString("N"),residentId=residentId,partnerId=partnerId,presetId=presetId,path=Path.GetFullPath(path),label=resident.displayName+"的照片",pose=pose,angle=angle,gameMinutes=State.gameMinutes};EnsureActivities().photos.Add(photo);
            ActivityRecord("Photo",presetId,residentId,"摄影",photo.label+"已保存。");ActivityChanged();return ActivityResult(true,"照片已加入相册。",photo.id);
        }
        private void UnlockProgressPhoto()
        {var a=EnsureActivities();var locked=LifeActivitiesCatalog.Photos.Where(p=>!IsPhotoUnlocked(p.id)).ToArray();if(locked.Length>0)a.unlockedPhotos.Add(locked[ActivityRandom(locked.Length)].id);}
        private bool RefreshActivityMarket()
        {
            var a=EnsureActivities();int day=(int)(State.gameMinutes/Math.Max(60,Configuration.minutesPerDay));int hour=(int)(State.gameMinutes%Configuration.minutesPerDay)/60;int period=hour<12?0:hour<18?1:2;
            if(a.marketDay==day&&a.marketPeriod==period&&a.market.Count>0)return false;a.marketDay=day;a.marketPeriod=period;a.market.Clear();
            for(int i=0;i<6;i++)
            {
                bool food=period!=1||i%2==0;string name;int price;
                if(food){var r=Rules.Data.foods[ActivityRandom(Rules.Data.foods.Length)];name=r.name;price=Math.Max(0,r.priceRaw);}else{var r=Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)];name=r.name;price=Math.Max(0,r.buyingPriceRaw);}
                int discounted=(int)(price*(period==2?.5f:period==1?.9f:.85f));a.market.Add(new LifeMarketOffer{id=day+"-"+period+"-"+i,category=food?"Food":"Goods",itemName=name,originalPrice=price,unitPrice=discounted,remaining=period==2?1:3});
            }
            return true;
        }
        public LifeActivitiesResult PurchaseMarketOffer(string offerId)
        {
            RefreshActivityMarket();var offer=EnsureActivities().market.FirstOrDefault(o=>o.id==offerId);if(offer==null||offer.remaining<=0)return ActivityResult(false,"这件限时商品已经售完或换货了。");if(State.walletRaw<offer.unitPrice)return ActivityResult(false,"钱不够购买这件商品。");
            ActivityMoney(-offer.unitPrice,"activity-market");ActivityInventory(offer.category,offer.itemName);offer.remaining--;ActivityChanged();return ActivityResult(true,"已购入 "+(offer.category=="Food"?TomodachiLife.NativeLifeLocalization.Food(offer.itemName):TomodachiLife.NativeLifeLocalization.Goods(offer.itemName))+"，可在商店页送给居民。");
        }
        public LifeActivitiesResult CollectDailyDonations()
        {
            var a=EnsureActivities();int day=(int)(State.gameMinutes/Math.Max(60,Configuration.minutesPerDay));if(a.donationClaimDay==day)return ActivityResult(false,"今天的居民捐款已经领取了，明天再来吧。");if(State.residents.Count==0)return ActivityResult(false,"岛上还没有居民。");
            int reward=State.residents.Count*100+Math.Min(1000,a.donationTotal/10);a.donationClaimDay=day;ActivityMoney(reward,"daily-island-donations");ActivityNews("今日捐款","居民捐了 "+reward+" 金币。");ActivityChanged();return ActivityResult(true,"领取居民捐款 "+reward+"。",reward:reward);
        }
        public LifeActivitiesResult DonateToIsland(int amount)
        {if(amount<=0||State.walletRaw<amount)return ActivityResult(false,"请选择能够支付的捐款金额。");ActivityMoney(-amount,"island-development");EnsureActivities().donationTotal+=amount;ActivityChanged();return ActivityResult(true,"为海岛建设捐出了 "+amount+"。");}
        public LifeActivitiesResult UnlockFacility(string facilityId)
        {
            var a=EnsureActivities();var definition=LifeActivitiesCatalog.Facilities.FirstOrDefault(f=>f.id==facilityId);var facility=a.facilities.FirstOrDefault(f=>f.id==facilityId);
            if(definition==null||facility==null)return ActivityResult(false,"找不到这项设施。");if(facility.unlocked)return ActivityResult(false,"这项设施已经开放了。");
            if(State.residents.Count<definition.requiredResidents||a.donationTotal<definition.requiredDonations)return ActivityResult(false,"居民人数或海岛建设捐款还未达到开放条件。");if(State.walletRaw<definition.cost)return ActivityResult(false,"建设资金不足。");
            ActivityMoney(-definition.cost,"facility-development");facility.unlocked=true;UnlockProgressPhoto();ActivityNews("新设施开放",definition.title+"已开放。");ActivityRecord("Facility",facilityId,"",definition.title,definition.title+"已开放。");ActivityChanged();return ActivityResult(true,definition.title+"已开放。");
        }
        public LifeActivitiesResult VisitActivityFacility(string facilityId,string residentId)
        {
            var facility=EnsureActivities().facilities.FirstOrDefault(f=>f.id==facilityId);var resident=FindResident(residentId);if(facility==null||!facility.unlocked||resident==null)return ActivityResult(false,"请先开放设施并选择居民。");
            facility.visits++;resident.social=Need(resident.social+8);resident.energy=Need(resident.energy+5);ActivityFacilityVisit?.Invoke(residentId,facilityId);ActivityRecord("Facility",facilityId,residentId,"设施活动",resident.displayName+"去了"+(LifeActivitiesCatalog.Facilities.FirstOrDefault(f=>f.id==facilityId)?.title??"设施")+"。");ActivityChanged();return ActivityResult(true,"心情变好了。");
        }
        public LifeActivitiesResult ContributeEmotionBalls(string residentId)
        {
            if(!EnsureActivities().facilities.Any(f=>f.id=="Fountain"&&f.unlocked))return ActivityResult(false,"请先建设愿望喷泉。");
            var batches=State.emotionBalls.Where(b=>b.residentId==residentId).ToArray();int value=batches.Sum(b=>Math.Max(0,b.valueRaw));if(value==0)return ActivityResult(false,"这位居民没有待投入的心情球。");foreach(var batch in batches)State.emotionBalls.Remove(batch);EnsureActivities().fountainPoints+=value;ActivityChanged();return ActivityResult(true,"投入心情球，得到 "+value+" 愿望点；这些心情球不会再计入钱包。");
        }
        public LifeActivitiesResult MakeFountainWish(string residentId,string wish)
        {
            var a=EnsureActivities();if(FindResident(residentId)==null)return ActivityResult(false,"请选择一位居民。");if(a.fountainPoints<500)return ActivityResult(false,"许愿需要500愿望点，请先投入心情球。");
            if(!a.facilities.Any(f=>f.id=="Fountain"&&f.unlocked))return ActivityResult(false,"请先建设愿望喷泉。");
            a.fountainPoints-=500;a.wishes.Add(wish);if(wish=="Travel")ActivityInventory("TravelTicket","TravelTicket");else if(wish=="Photo")UnlockProgressPhoto();else{var item=Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)].name;ActivityInventory("Goods",item);}
            ActivityRecord("Wish",wish,residentId,"愿望喷泉","居民许下了一个愿望，收到了一份礼物。",true);ActivityChanged();return ActivityResult(true,"愿望实现了！奖励已加入物品或场景收藏。");
        }
        public LifeActivitiesResult WatchActivityNews(string bulletinId)
        {var news=EnsureActivities().news.FirstOrDefault(n=>n.id==bulletinId);if(news==null)return ActivityResult(false,"这条新闻不存在。");news.watched=true;ActivityChanged();return ActivityResult(true,news.title+"："+news.body);}
        public LifeActivitiesResult ResolveActivityAccident(string accidentId,int choice)
        {
            var accident=EnsureActivities().accidents.FirstOrDefault(a=>a.id==accidentId);if(accident==null||accident.resolved)return ActivityResult(false,"这件小插曲已经结束了。");var resident=FindResident(accident.residentId);if(resident==null)return ActivityResult(false,"居民不存在。");
            if(choice==1&&State.walletRaw<50)return ActivityResult(false,"提供帮助需要50。");if(choice==1)ActivityMoney(-50,"accident-help");
            accident.resolved=true;accident.resolution=choice==0?"居民在玩家陪伴下解决了问题。":choice==1?"玩家提供了实物帮助，居民平安返回。":"居民自己尝试了一番，事情告一段落。";
            resident.social=Need(resident.social+(choice==2?0:15));resident.energy=Need(resident.energy+(choice==1?15:choice==2?-5:5));
            if(accident.kind=="LostItem"&&choice==0){var item=Rules.Data.goods[ActivityRandom(Rules.Data.goods.Length)].name;ActivityInventory("Goods",item);accident.resolution+="找到的物品已放入物品栏。";}
            ActivityRecord("Accident",accident.kind,resident.id,"日常小插曲",accident.message+accident.resolution,true);ActivityNews("岛上小插曲",accident.message+accident.resolution,resident.id);ActivityChanged();return ActivityResult(true,accident.resolution);
        }
    }
}
