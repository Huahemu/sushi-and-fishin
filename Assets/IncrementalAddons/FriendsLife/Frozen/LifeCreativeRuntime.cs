using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    public sealed partial class LifeSimulationRuntime
    {
        private LifeCreativeCatalog creativeCatalog;
        public LifeCreativeCatalog CreativeCatalog=>creativeCatalog??(creativeCatalog=LifeCreativeCatalog.Load());
        // An activity controller may open its travel UI and return true. Without
        // that controller a real timed excursion/return loop remains playable.
        public Func<string,string,bool> CreativeActivityRequest;
        public void BindCreativeCatalog(LifeCreativeCatalog catalog){creativeCatalog=catalog??throw new ArgumentNullException(nameof(catalog));}
        public LifeCreativeState NormalizeCreativeState()
        {
            if(State.creative==null)State.creative=new LifeCreativeState();var state=State.creative;
            if(state.residents==null)state.residents=new List<LifeCreativeResident>();if(state.designs==null)state.designs=new List<LifeCreativeDesign>();
            if(state.buildings==null)state.buildings=new List<LifeCreativeBuilding>();if(state.trips==null)state.trips=new List<LifeCreativeTrip>();
            state.residents.RemoveAll(r=>r==null);state.designs.RemoveAll(d=>d==null);state.buildings.RemoveAll(b=>b==null);state.trips.RemoveAll(t=>t==null);
            foreach(var r in state.residents)
            {
                r.ownerState=state;
                if(r.wardrobe==null)r.wardrobe=new List<LifeCreativeOwnedCloth>();if(r.garments==null)r.garments=new List<LifeCreativeGarment>();
                if(r.ownedRoomStyles==null)r.ownedRoomStyles=new List<string>();if(r.learnedHabits==null)r.learnedHabits=new List<string>();if(r.equippedHabits==null)r.equippedHabits=new List<string>();
                if(r.knownPhrases==null)r.knownPhrases=new List<string>();if(r.topics==null)r.topics=new List<string>();if(r.tastes==null)r.tastes=new List<LifeCreativeTaste>();if(r.decorations==null)r.decorations=new List<LifeCreativeDecoration>();
            }
            return state;
        }
        public LifeCreativeResident CreativeResident(string residentId)
        {
            var state=NormalizeCreativeState();var resident=FindResident(residentId);if(resident==null)return null;
            var value=state.residents.FirstOrDefault(r=>r.residentId==residentId);
            if(value==null){value=new LifeCreativeResident{residentId=residentId,lastLevel=resident.level,ownerState=state};state.residents.Add(value);if(!string.IsNullOrWhiteSpace(resident.catchphrase))value.knownPhrases.Add(resident.catchphrase);}
            return value;
        }
        public LifeCreativeDesign CreativeDesign(string id)=>NormalizeCreativeState().designs.FirstOrDefault(d=>d.id==id);
        private LifeSimulationResolution CreativeFinish(LifeSimulationResident resident,LifeSimulationResolution result,string message,string dailyCategory=null)
        {
            result.success=true;result.message=message;if(string.IsNullOrEmpty(result.provenance))result.provenance="Playable creative/wardrobe policy over the existing source catalogs";
            var state=NormalizeCreativeState();state.revision++;if(resident!=null){var personal=CreativeResident(resident.id);personal.revision++;if(!string.IsNullOrEmpty(dailyCategory))RecordDailyAction(resident.id,dailyCategory,message);}
            result.walletAfter=State.walletRaw;result.happinessAfter=resident?.happinessMeter??0;result.levelAfter=resident?.level??0;
            Journal("creative",message,result.provenance,resident?.id);Changed();return result;
        }
        private bool CreativePay(int price,LifeSimulationResolution result)
        {if(price<0||State.walletRaw<price)return false;if(price>0)ChangeWallet(-price,"creative-purchase",result);return true;}
        public bool OwnsCreativeCloth(string residentId,string clothId,int color=0)
            =>CreativeResident(residentId)?.wardrobe.Any(i=>i.clothId==clothId&&i.color==color)==true;
        public LifeSimulationResolution BuyCreativeCloth(string residentId,string clothId,int color=0,bool equip=true)
        {
            var resident=FindResident(residentId);var item=CreativeCatalog.Cloth(clothId);if(resident==null||item==null)return Fail("没有找到居民或衣物。");
            color=Math.Max(0,Math.Min(item.colors-1,color));var personal=CreativeResident(residentId);var result=StartResult(resident,"wardrobe");
            if(!OwnsCreativeCloth(residentId,clothId,color))
            {
                if(!item.forSale)return Fail("这件衣物需要任务或创作解锁。");if(!CreativePay(item.price,result))return Fail("零钱不够购买这件衣物。");
                personal.wardrobe.Add(new LifeCreativeOwnedCloth{clothId=clothId,color=color});
            }
            if(equip){var equipped=EquipCreativeCloth(residentId,clothId,color);equipped.walletBefore=result.walletBefore;equipped.walletAfter=State.walletRaw;return equipped;}
            return CreativeFinish(resident,result,"买下了 "+item.name+"，已放进 "+resident.displayName+" 的衣柜。","Cloth");
        }
        public LifeSimulationResolution EquipCreativeCloth(string residentId,string clothId,int color=0,string designId=null)
        {
            var resident=FindResident(residentId);var item=CreativeCatalog.Cloth(clothId);var personal=CreativeResident(residentId);
            if(resident==null||item==null||personal==null||!OwnsCreativeCloth(residentId,clothId,color))return Fail("请先购买这个颜色的衣物。");
            if(!string.IsNullOrEmpty(designId)&&CreativeDesign(designId)?.kind!="Clothing")return Fail("请选一份衣物绘制作品。");
            if(item.sourceType=="All"){personal.garments.RemoveAll(g=>g.slot=="Tops"||g.slot=="Bottoms");}
            else if(item.slot=="Bottoms")personal.garments.RemoveAll(g=>CreativeCatalog.Cloth(g.clothId)?.sourceType=="All");
            personal.garments.RemoveAll(g=>g.slot==item.slot);personal.garments.Add(new LifeCreativeGarment{slot=item.slot,clothId=clothId,color=color,designId=designId});
            resident.activity="换上新衣服";resident.social=Need(resident.social+5);
            return CreativeFinish(resident,StartResult(resident,"wardrobe"),resident.displayName+" 换上了 "+item.name+"（颜色 "+(color+1)+"）。","Cloth");
        }
        public LifeSimulationResolution RemoveCreativeGarment(string residentId,string slot)
        {
            var resident=FindResident(residentId);var personal=CreativeResident(residentId);if(resident==null||personal==null)return Fail("请先选择居民。");
            personal.garments.RemoveAll(g=>g.slot==slot);return CreativeFinish(resident,StartResult(resident,"wardrobe"),"取下了 "+slot+"，衣物仍保留在衣柜里。","Cloth");
        }
        public LifeSimulationResolution BuyCreativeRoom(string residentId,string styleId)
        {
            var resident=FindResident(residentId);var style=CreativeCatalog.Room(styleId);var personal=CreativeResident(residentId);
            if(resident==null||personal==null||style==null)return Fail("请先选择居民和房间风格。");if(string.IsNullOrEmpty(resident.homeId))return Fail("先安排一栋住宅，才能布置房间。");
            var result=StartResult(resident,"room-style");
            if(!personal.ownedRoomStyles.Contains(styleId)){if(!CreativePay(style.price,result))return Fail("零钱不够购买这个房间风格。");personal.ownedRoomStyles.Add(styleId);}
            personal.roomStyleId=styleId;personal.roomDesignId=null;resident.activity="布置房间";
            return CreativeFinish(resident,result,resident.displayName+" 的房间换成了 "+style.name+"。","RoomStyle");
        }
        public LifeSimulationResolution UseCreativeGoods(string residentId,string goodsId)
        {
            var resident=FindResident(residentId);var item=CreativeCatalog.Goods(goodsId);var personal=CreativeResident(residentId);
            if(resident==null||personal==null||item==null)return Fail("请先选择居民和物品。");
            var result=StartResult(resident,"creative-goods");
            if(!AcquireAndConsume("Goods",goodsId,item.price,result))return Fail("没有这个物品，零钱也不够购买。");
            personal.heldGoodsId=goodsId;personal.heldGoodsAction=item.action;personal.heldMinutesRemaining=item.durationMinutes;
            switch(item.action)
            {
                case "Travel":
                    if(CreativeActivityRequest==null||!CreativeActivityRequest(residentId,"Travel"))State.creative.trips.Add(new LifeCreativeTrip{id=Guid.NewGuid().ToString("N"),residentId=residentId,destination="海边小镇",minutesRemaining=60,reward=200});
                    resident.activity="去旅行";break;
                case "Pet":personal.petDesignId="goods:"+goodsId;personal.petHunger=80;resident.activity="陪宠物玩";resident.social=Need(resident.social+20);break;
                case "Read":resident.activity="阅读 "+item.name;personal.curiosity=Math.Min(5,personal.curiosity+1);resident.energy=Need(resident.energy+15);AddCreativeTopic(personal,"书中的故事");break;
                case "Music":resident.activity="演奏和听音乐";resident.social=Need(resident.social+20);AddCreativeTopic(personal,"音乐");break;
                case "Sport":resident.activity="运动 "+item.name;resident.energy=Need(resident.energy+20);resident.hunger=Need(resident.hunger-8);AddCreativeTopic(personal,"运动");break;
                case "Game":resident.activity="玩 "+item.name;resident.social=Need(resident.social+15);resident.energy=Need(resident.energy+10);AddCreativeTopic(personal,"游戏");CreativeActivityRequest?.Invoke(residentId,"Game");break;
                case "Photo":resident.activity="拍照留念";AddCreativeTopic(personal,"岛上的风景");CreativeActivityRequest?.Invoke(residentId,"Photo");break;
                case "Style":resident.activity="整理仪容";personal.expression=Math.Min(5,personal.expression+1);break;
                default:personal.decorations.Add(new LifeCreativeDecoration{id=Guid.NewGuid().ToString("N"),goodsId=goodsId,x=personal.decorations.Count%5-2,z=personal.decorations.Count/5%5-2});resident.activity="摆放 "+item.name;break;
            }
            int satisfy=Rules.TryGiftSatisfy(Configuration.defaultGiftTasteHash,out var sourceAmount)?sourceAmount:8;
            AddHappiness(resident,satisfy,result,true);resident.social=Need(resident.social+Configuration.socialNeedRecovery);
            result.provenance="Source GoodsSatisfy/native satisfaction and emotion balls; playable distinct item actions";
            return CreativeFinish(resident,result,resident.displayName+" 正在使用 "+item.name+"："+resident.activity+"。","Goods");
        }
        public LifeSimulationResolution GiveCreativeHabit(string residentId,string habitId)
        {
            var resident=FindResident(residentId);var habit=CreativeCatalog.Habit(habitId);var personal=CreativeResident(residentId);
            if(resident==null||personal==null||habit==null)return Fail("请先选择居民和小习惯。");
            var result=StartResult(resident,"habit");
            if(!personal.learnedHabits.Contains(habitId))
            {
                if(resident.level+1<habit.minimumLevel&&personal.habitTokens<1)return Fail("等级或习惯券不足；升级后可解锁更多习惯。");
                if(!CreativePay(habit.price,result))return Fail("零钱不够学习这个习惯。");if(resident.level+1<habit.minimumLevel)personal.habitTokens--;personal.learnedHabits.Add(habitId);
            }
            personal.equippedHabits.RemoveAll(id=>CreativeCatalog.Habit(id)?.group==habit.group);personal.equippedHabits.Add(habitId);
            while(personal.equippedHabits.Count>5)personal.equippedHabits.RemoveAt(0);
            ApplyCreativeStomachHabit(resident,personal);resident.activity="练习 "+habit.name;
            return CreativeFinish(resident,result,resident.displayName+" 学会并用上了 "+habit.name+"。","Habit");
        }
        private void ApplyCreativeStomachHabit(LifeSimulationResident resident,LifeCreativeResident personal)
        {
            if(personal.equippedHabits.Any(h=>h.IndexOf("StomachBig",StringComparison.OrdinalIgnoreCase)>=0)){resident.stomachBig=true;resident.stomachSmall=false;}
            else if(personal.equippedHabits.Any(h=>h.IndexOf("StomachSmall",StringComparison.OrdinalIgnoreCase)>=0)){resident.stomachSmall=true;resident.stomachBig=false;}
        }
        public LifeSimulationResolution TuneCreativePersonality(string residentId,string axis,int value)
        {
            var resident=FindResident(residentId);var p=CreativeResident(residentId);if(resident==null||p==null)return Fail("请先选择居民。");value=Math.Max(1,Math.Min(5,value));
            switch(axis){case "Energy":p.energy=value;break;case "Social":p.sociability=value;break;case "Patience":p.patience=value;break;case "Expression":p.expression=value;break;case "Curiosity":p.curiosity=value;break;default:return Fail("未知性格维度。");}
            return CreativeFinish(resident,StartResult(resident,"personality"),"调整了 "+resident.displayName+" 的微性格。");
        }
        public LifeSimulationResolution TeachCreativePhrase(string residentId,string phrase,bool topic=false)
        {
            var resident=FindResident(residentId);var personal=CreativeResident(residentId);if(resident==null||personal==null)return Fail("请先选择居民。");
            phrase=(phrase??"").Trim();if(phrase.Length==0)return Fail("请输入想教的话。");if(phrase.Length>40)phrase=phrase.Substring(0,40);
            if(topic)AddCreativeTopic(personal,phrase);else{resident.catchphrase=phrase;if(!personal.knownPhrases.Contains(phrase))personal.knownPhrases.Add(phrase);}
            return CreativeFinish(resident,StartResult(resident,"culture"),resident.displayName+" 学会了"+(topic?"话题":"口头禅")+"“"+phrase+"”。","Phrase");
        }
        private static void AddCreativeTopic(LifeCreativeResident resident,string topic){if(!resident.topics.Contains(topic))resident.topics.Add(topic);while(resident.topics.Count>30)resident.topics.RemoveAt(0);}
        public LifeSimulationResolution ShareCreativeCulture(string fromId,string toId)
        {
            var source=FindResident(fromId);var target=FindResident(toId);var from=CreativeResident(fromId);var to=CreativeResident(toId);
            if(source==null||target==null||source==target)return Fail("请选择两位不同的朋友。");
            if(!string.IsNullOrWhiteSpace(source.catchphrase)){target.catchphrase=source.catchphrase;if(!to.knownPhrases.Contains(source.catchphrase))to.knownPhrases.Add(source.catchphrase);}
            foreach(string topic in from.topics.Take(3))AddCreativeTopic(to,topic);
            var relation=State.relationships.FirstOrDefault(r=>r.fromId==fromId&&r.toId==toId);
            if(relation!=null&&Rules.Data.relation!=null){relation.meter=NativeLifeRulesCalculations.ApplyRelationReaction(relation.meter,4,false,Rules.Data.relation);relation.nativeRank=NativeLifeRulesCalculations.RelationRank(relation.meter,Rules.Data.relation);}
            target.social=Need(target.social+10);return CreativeFinish(target,StartResult(target,"culture"),source.displayName+" 把口头禅和话题分享给了 "+target.displayName+"。","Phrase");
        }
        public LifeSimulationResolution DiscoverCreativeFood(string residentId,string foodName)
        {
            var resident=FindResident(residentId);var personal=CreativeResident(residentId);var food=Rules.FindFood(foodName);if(resident==null||personal==null||food==null)return Fail("居民或食物不存在。");
            var known=personal.tastes.FirstOrDefault(t=>t.foodName==foodName);int seed=personal.curiosity+personal.expression+personal.patience;foreach(char ch in residentId+foodName)seed=unchecked(seed*31+ch);
            string column=known?.column??new[]{"TasteA","TasteB","TasteC","TasteD"}[(seed&int.MaxValue)%4];var result=GiveFood(residentId,foodName,column);
            if(!result.success)return result;
            var batch=State.emotionBalls.FirstOrDefault(b=>b.id==result.emotionBallBatchId);
            int amount=result.nativeMoodApplied?0:batch?.satisfactionAmount??unchecked((result.levelAfter-result.levelBefore)*100+result.happinessAfter-result.happinessBefore);
            RecordCreativeTaste(residentId,foodName,column,amount);
            result.message+=" 发现口味："+personal.tastes.First(t=>t.foodName==foodName).label;return result;
        }
        public void RecordCreativeTaste(string residentId,string foodName,string column,int satisfaction)
        {
            var personal=CreativeResident(residentId);if(personal==null)return;var taste=personal.tastes.FirstOrDefault(t=>t.foodName==foodName);
            if(taste==null){taste=new LifeCreativeTaste{foodName=foodName};personal.tastes.Add(taste);}taste.column=column??"TasteA";taste.satisfaction=satisfaction;taste.label=satisfaction>10?"很喜欢":satisfaction>0?"喜欢":satisfaction==0?"普通":"不喜欢";taste.times++;State.creative.revision++;Changed();
        }

        public LifeCreativeDesign CreateCreativeDesign(string title,string kind="Decor",int size=32)
        {
            var state=NormalizeCreativeState();if(state.designs.Count>=100)throw new InvalidOperationException("作品库已满100份，请先删除不用的作品。");
            if(!new[]{"Food","Clothing","Pet","Decor","Building"}.Contains(kind))kind="Decor";size=Math.Max(8,Math.Min(64,size));
            var design=new LifeCreativeDesign{id=Guid.NewGuid().ToString("N"),title=string.IsNullOrWhiteSpace(title)?"我的新作品":title.Trim().Substring(0,Math.Min(32,title.Trim().Length)),kind=kind,width=size,height=size,pixels=Enumerable.Repeat(unchecked((int)0xfff4ceff),size*size).ToArray(),createdMinute=State.gameMinutes};
            state.designs.Add(design);state.revision++;Changed();return design;
        }
        public void PaintCreativeDesign(string designId,int x0,int y0,int x1,int y1,int rgba,int radius=0)
        {
            var d=CreativeDesign(designId);if(d==null)return;EnsureCreativePixels(d);radius=Math.Max(0,Math.Min(3,radius));x0=Math.Max(0,Math.Min(d.width-1,x0));x1=Math.Max(0,Math.Min(d.width-1,x1));y0=Math.Max(0,Math.Min(d.height-1,y0));y1=Math.Max(0,Math.Min(d.height-1,y1));
            int dx=Math.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.Abs(y1-y0),sy=y0<y1?1:-1,error=dx+dy;
            while(true){for(int y=y0-radius;y<=y0+radius;y++)for(int x=x0-radius;x<=x0+radius;x++)if(x>=0&&y>=0&&x<d.width&&y<d.height)d.pixels[y*d.width+x]=rgba;if(x0==x1&&y0==y1)break;int twice=2*error;if(twice>=dy){error+=dy;x0+=sx;}if(twice<=dx){error+=dx;y0+=sy;}}
            d.revision++;State.creative.revision++;Changed();
        }
        public void StampCreativeDesign(string designId,string stamp,int centerX,int centerY,int rgba)
        {
            var d=CreativeDesign(designId);if(d==null)return;EnsureCreativePixels(d);
            string[] rows=stamp=="Heart"?new[]{"01100110","11111111","11111111","01111110","00111100","00011000"}:stamp=="Star"?new[]{"0001000","0011100","1111111","0111110","0011100","0110110","1100011"}:stamp=="Face"?new[]{"0111110","1100011","1010101","1000001","1011101","1100011","0111110"}:new[]{"0011100","0111110","1111111","1111111","1111111","0111110","0011100"};
            for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='1'){int px=centerX+x-rows[y].Length/2,py=centerY+y-rows.Length/2;if(px>=0&&py>=0&&px<d.width&&py<d.height)d.pixels[py*d.width+px]=rgba;}
            d.revision++;State.creative.revision++;Changed();
        }
        public void SetCreativeDesignText(string designId,string title,string text)
        {var d=CreativeDesign(designId);if(d==null)return;d.title=(title??d.title).Substring(0,Math.Min(32,(title??d.title).Length));d.text=(text??"").Substring(0,Math.Min(40,(text??"").Length));d.revision++;State.creative.revision++;Changed();}
        public void ClearCreativeDesign(string designId,int rgba)
        {var d=CreativeDesign(designId);if(d==null)return;EnsureCreativePixels(d);for(int i=0;i<d.pixels.Length;i++)d.pixels[i]=rgba;d.revision++;State.creative.revision++;Changed();}
        public static void EnsureCreativePixels(LifeCreativeDesign design)
        {design.width=Math.Max(8,Math.Min(64,design.width));design.height=Math.Max(8,Math.Min(64,design.height));if(design.pixels==null||design.pixels.Length!=design.width*design.height)design.pixels=Enumerable.Repeat(unchecked((int)0xfff4ceff),design.width*design.height).ToArray();}
        public LifeSimulationResolution CraftCreativeDesign(string residentId,string designId)
        {
            var resident=FindResident(residentId);var d=CreativeDesign(designId);if(resident==null||d==null)return Fail("请先选择居民和作品。");var result=StartResult(resident,"craft");int price=d.kind=="Building"?250:d.kind=="Food"?25:80;
            if(!CreativePay(price,result))return Fail("制作材料需要 "+price+" 零钱。");d.stock++;return CreativeFinish(resident,result,"制作了一份“"+d.title+"”，库存 "+d.stock+"。");
        }
        public LifeSimulationResolution UseCreativeDesign(string residentId,string designId)
        {
            var resident=FindResident(residentId);var p=CreativeResident(residentId);var d=CreativeDesign(designId);if(resident==null||p==null||d==null)return Fail("请先选择居民和作品。");if(d.stock<1)return Fail("作品库存不足，请先制作一份。");var result=StartResult(resident,"ugc");
            switch(d.kind)
            {
                case "Food":resident.hunger=Need(resident.hunger+40);AddHappiness(resident,12,result);resident.activity="品尝自绘食物 "+d.title;break;
                case "Clothing":
                    var shirt=(CreativeCatalog.Data.clothes??Array.Empty<LifeCreativeClothDefinition>()).FirstOrDefault(c=>c.slot=="Tops"&&c.id.IndexOf("UgcTshirt",StringComparison.OrdinalIgnoreCase)>=0)??CreativeCatalog.Data.clothes.FirstOrDefault(c=>c.slot=="Tops");
                    if(shirt==null)return Fail("衣物模板尚未准备好。");if(!OwnsCreativeCloth(residentId,shirt.id))p.wardrobe.Add(new LifeCreativeOwnedCloth{clothId=shirt.id});
                    p.garments.RemoveAll(g=>g.slot=="Tops");p.garments.Add(new LifeCreativeGarment{slot="Tops",clothId=shirt.id,designId=d.id});break;
                case "Pet":p.petDesignId=d.id;p.petHunger=80;resident.activity="领养了 "+d.title;break;
                case "Decor":p.decorations.Add(new LifeCreativeDecoration{id=Guid.NewGuid().ToString("N"),designId=d.id,x=p.decorations.Count%5-2,z=p.decorations.Count/5%5-2});break;
                case "Building":return Fail("建筑作品请使用“在岛上建造”按钮，建成后可以入住。");
                default:return Fail("未知作品类型。");
            }
            d.stock--;d.uses++;return CreativeFinish(resident,result,resident.displayName+" 用上了自绘作品“"+d.title+"”。",d.kind=="Clothing"?"Cloth":d.kind=="Food"?"Food":d.kind=="Decor"?"RoomStyle":"Other");
        }
        public LifeSimulationResolution UseCreativeRoomDesign(string residentId,string designId)
        {
            var resident=FindResident(residentId);var p=CreativeResident(residentId);var d=CreativeDesign(designId);if(resident==null||p==null||d==null||d.kind!="Decor")return Fail("请选择一份装饰作品。");if(string.IsNullOrEmpty(resident.homeId))return Fail("请先安排住宅。");p.roomDesignId=d.id;
            return CreativeFinish(resident,StartResult(resident,"room-ugc"),"把“"+d.title+"”铺成了房间壁纸和地板。","RoomStyle");
        }
        public LifeSimulationResolution MoveCreativeDecoration(string residentId,string id,int dx,int dz,bool rotate=false)
        {
            var resident=FindResident(residentId);var decoration=CreativeResident(residentId)?.decorations.FirstOrDefault(d=>d.id==id);if(resident==null||decoration==null)return Fail("没有找到这件摆设。");decoration.x=Math.Max(-2,Math.Min(2,decoration.x+dx));decoration.z=Math.Max(-2,Math.Min(2,decoration.z+dz));if(rotate)decoration.quarterTurns=(decoration.quarterTurns+1)%4;
            return CreativeFinish(resident,StartResult(resident,"room-layout"),"移动了房间摆设。","RoomStyle");
        }
        public LifeSimulationResolution RemoveCreativeDecoration(string residentId,string id)
        {
            var resident=FindResident(residentId);var p=CreativeResident(residentId);var decoration=p?.decorations.FirstOrDefault(d=>d.id==id);if(resident==null||decoration==null)return Fail("没有找到这件摆设。");p.decorations.Remove(decoration);var d=CreativeDesign(decoration.designId);if(d!=null)d.stock++;
            if(!string.IsNullOrEmpty(decoration.goodsId))
            {
                var item=State.inventory.FirstOrDefault(v=>v.category=="Goods"&&v.itemName==decoration.goodsId);
                if(item==null){item=new LifeSimulationInventoryStack{category="Goods",itemName=decoration.goodsId};State.inventory.Add(item);}item.quantity++;
            }
            return CreativeFinish(resident,StartResult(resident,"room-layout"),"收起了摆设，物品已回到库存。","RoomStyle");
        }
        public LifeSimulationResolution RegisterCreativeBuilding(string residentId,string designId,string houseId)
        {
            var resident=FindResident(residentId);var d=CreativeDesign(designId);if(resident==null||d==null||d.kind!="Building"||d.stock<1)return Fail("建筑作品不可用。");if(string.IsNullOrEmpty(houseId))return Fail("住宅尚未建成。");
            State.creative.buildings.Add(new LifeCreativeBuilding{houseId=houseId,designId=designId});d.stock--;d.uses++;return CreativeFinish(resident,StartResult(resident,"building-ugc"),"自绘建筑“"+d.title+"”建成了，可以安排朋友入住。","IslandEdit");
        }
        public LifeSimulationResolution CareCreativePet(string residentId,bool feed)
        {
            var resident=FindResident(residentId);var p=CreativeResident(residentId);if(resident==null||p==null||string.IsNullOrEmpty(p.petDesignId))return Fail("还没有领养宠物。");var result=StartResult(resident,"pet");if(feed&&!CreativePay(20,result))return Fail("宠物食物需要20零钱。");p.petHunger=Math.Min(100,p.petHunger+(feed?35:0));p.petAffection=Math.Min(100,p.petAffection+(feed?8:12));resident.social=Need(resident.social+10);
            return CreativeFinish(resident,result,feed?"给宠物喂了食物。":"和宠物开心地玩了一会儿。","Goods");
        }
        public void AdvanceCreative(double elapsedGameMinutes)
        {
            if(double.IsNaN(elapsedGameMinutes)||double.IsInfinity(elapsedGameMinutes)||elapsedGameMinutes<0)return;var state=NormalizeCreativeState();double before=state.elapsedMinutes;state.elapsedMinutes+=elapsedGameMinutes;
            foreach(var resident in State.residents)
            {
                var p=CreativeResident(resident.id);if(resident.level>p.lastLevel){p.habitTokens+=resident.level-p.lastLevel;p.lastLevel=resident.level;}
                if(p.heldMinutesRemaining>0){p.heldMinutesRemaining=Math.Max(0,p.heldMinutesRemaining-elapsedGameMinutes);if(p.heldMinutesRemaining==0){p.heldGoodsId=p.heldGoodsAction=null;p.revision++;state.revision++;}}
                int hours=(int)Math.Min(24,Math.Floor(state.elapsedMinutes/60)-Math.Floor(before/60));
                if(hours>0){if(!string.IsNullOrEmpty(p.petDesignId))p.petHunger=Math.Max(0,p.petHunger-hours*6);resident.social=Need(resident.social+hours*p.equippedHabits.Count*.5f);resident.energy=Need(resident.energy+hours*(p.energy-3)*.3f);}
            }
            foreach(var trip in state.trips.ToArray())
            {
                trip.minutesRemaining-=elapsedGameMinutes;if(trip.minutesRemaining>0)continue;var resident=FindResident(trip.residentId);state.trips.Remove(trip);if(resident==null)continue;
                var result=StartResult(resident,"travel-return");ChangeWallet(trip.reward,"travel-souvenir",result);resident.energy=Need(resident.energy+35);resident.social=Need(resident.social+25);resident.activity="旅行归来";AddCreativeTopic(CreativeResident(resident.id),trip.destination+"的故事");CreativeFinish(resident,result,resident.displayName+" 从 "+trip.destination+" 回来了，带回了旅行纪念。","Travel");
            }
            if(state.elapsedMinutes>=state.nextSpreadMinute)
            {
                state.nextSpreadMinute=state.elapsedMinutes+120;var people=State.residents.Where(r=>!string.IsNullOrWhiteSpace(r.catchphrase)||CreativeResident(r.id).topics.Count>0).ToArray();
                if(people.Length>0){var source=people[(state.spreadCursor++&int.MaxValue)%people.Length];var relation=State.relationships.Where(r=>r.fromId==source.id&&r.meter>=80&&!r.fighting).OrderByDescending(r=>r.meter).FirstOrDefault();if(relation!=null)ShareCreativeCulture(source.id,relation.toId);}
            }
        }
    }
}
