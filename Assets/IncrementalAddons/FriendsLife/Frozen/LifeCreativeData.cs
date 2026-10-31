using System;
using System.Collections.Generic;
using System.Linq;

namespace TomodachiLife.Simulation
{
    [Serializable] public sealed class LifeCreativeState
    {
        public int schema=1,revision,spreadCursor;
        public double elapsedMinutes,nextSpreadMinute=120;
        public List<LifeCreativeResident> residents=new List<LifeCreativeResident>();
        public List<LifeCreativeDesign> designs=new List<LifeCreativeDesign>();
        public List<LifeCreativeBuilding> buildings=new List<LifeCreativeBuilding>();
        public List<LifeCreativeTrip> trips=new List<LifeCreativeTrip>();
    }
    [Serializable] public sealed class LifeCreativeResident
    {
#if LIFE_SIMULATION_STANDALONE
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        [NonSerialized] public LifeCreativeState ownerState;
        public string residentId,roomStyleId,roomDesignId,petDesignId,heldGoodsId,heldGoodsAction;
        public double heldMinutesRemaining;
        public int revision,energy=3,sociability=3,patience=3,expression=3,curiosity=3,habitTokens=2,petHunger=80,petAffection,lastLevel;
        public List<LifeCreativeOwnedCloth> wardrobe=new List<LifeCreativeOwnedCloth>();
        public List<LifeCreativeGarment> garments=new List<LifeCreativeGarment>();
        public List<string> ownedRoomStyles=new List<string>(),learnedHabits=new List<string>(),equippedHabits=new List<string>(),knownPhrases=new List<string>(),topics=new List<string>();
        public List<LifeCreativeTaste> tastes=new List<LifeCreativeTaste>();
        public List<LifeCreativeDecoration> decorations=new List<LifeCreativeDecoration>();
    }
    [Serializable] public sealed class LifeCreativeOwnedCloth { public string clothId; public int color; }
    [Serializable] public sealed class LifeCreativeGarment { public string slot,clothId,designId; public int color; }
    [Serializable] public sealed class LifeCreativeTaste { public string foodName,column,label; public int satisfaction,times; }
    [Serializable] public sealed class LifeCreativeDecoration { public string id,designId,goodsId; public int x,z,quarterTurns; }
    [Serializable] public sealed class LifeCreativeBuilding { public string houseId,designId; }
    [Serializable] public sealed class LifeCreativeTrip { public string id,residentId,destination; public double minutesRemaining; public int reward; }
    [Serializable] public sealed class LifeCreativeDesign
    {
        public string id,title,kind="Decor",text="";
        public int width=32,height=32,revision,stock=1,uses;
        public int[] pixels;
        public double createdMinute;
    }
    [Serializable] public sealed class LifeCreativeClothDefinition
    {
        public string id,name,slot,sourceType,modelResource,visualFallbackResource,sourceReference;
        public int price,colors=1;
        public bool forSale=true;
    }
    [Serializable] public sealed class LifeCreativeRoomDefinition
    {
        public string id,name,floorTexture,wallTexture,sourceReference;
        public int price;
    }
    [Serializable] public sealed class LifeCreativeHabitDefinition
    {
        public string id,name,group,preview,sourceReference;
        public int minimumLevel=1,price=200;
    }
    [Serializable] public sealed class LifeCreativeGoodsDefinition
    {
        public string id,name,action,modelResource,sourceReference;
        public int price,durationMinutes=30;
    }
    [Serializable] public sealed class LifeCreativeCatalogData
    {
        public int schema=1;
        public string provenance;
        public string[] requiredModels,requiredTextures;
        public LifeCreativeClothDefinition[] clothes;
        public LifeCreativeRoomDefinition[] rooms;
        public LifeCreativeHabitDefinition[] habits;
        public LifeCreativeGoodsDefinition[] goods;
    }
    public sealed class LifeCreativeCatalog
    {
        public LifeCreativeCatalogData Data{get;private set;}
        public LifeCreativeCatalog(LifeCreativeCatalogData data){Data=data??throw new ArgumentNullException(nameof(data));}
        public static LifeCreativeCatalog FromJson(string json)=>new LifeCreativeCatalog(LifeSimulationJson.Read<LifeCreativeCatalogData>(json));
        public static LifeCreativeCatalog Load()
        {
#if !LIFE_SIMULATION_STANDALONE
            var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("LifeCreative/catalog");if(asset!=null)return FromJson(asset.text);
#endif
            return new LifeCreativeCatalog(new LifeCreativeCatalogData{provenance="Playable fallback catalog; complete source catalog is a separate resource",clothes=new[]{new LifeCreativeClothDefinition{id="starter-shirt",name="休闲上衣",slot="Tops",price=200,colors=8,modelResource="Models/Model/ClothTopsTshirtLong.bfres/ClothTopsTshirtLong"}},rooms=new[]{new LifeCreativeRoomDefinition{id="simple-room",name="温暖小屋",price=300}},habits=new[]{new LifeCreativeHabitDefinition{id="Friendly",name="爱打招呼",group="Greeting",minimumLevel=1}},goods=new[]{new LifeCreativeGoodsDefinition{id="Ticket",name="旅行券",action="Travel",price=500}}});
        }
        public LifeCreativeClothDefinition Cloth(string id)=>(Data.clothes??Array.Empty<LifeCreativeClothDefinition>()).FirstOrDefault(v=>v.id==id);
        public LifeCreativeRoomDefinition Room(string id)=>(Data.rooms??Array.Empty<LifeCreativeRoomDefinition>()).FirstOrDefault(v=>v.id==id);
        public LifeCreativeHabitDefinition Habit(string id)=>(Data.habits??Array.Empty<LifeCreativeHabitDefinition>()).FirstOrDefault(v=>v.id==id);
        public LifeCreativeGoodsDefinition Goods(string id)=>(Data.goods??Array.Empty<LifeCreativeGoodsDefinition>()).FirstOrDefault(v=>v.id==id);
    }
}
