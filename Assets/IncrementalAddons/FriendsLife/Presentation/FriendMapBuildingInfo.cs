using System;using System.Linq;using UnityEngine;using UnityEngine.UI;using TMPro;using SushiAndFish.ResidentialIsland;
namespace SushiAndFish.FriendsLife
{
    public static class FriendMapBuildingInfo
    {
        public static string Identity(HousingHouse house){var metadata=house?house.GetComponent<FriendHouseMetadata>():null;return metadata&&metadata.friendsOwnedClone?metadata.instanceId??"":"";}
        public static string Entity(string id,HousingHouse house)=>"Building/"+id+"/"+Identity(house);
        public static HousingBuildingInfo New(HousingPlot plot)
        {
            var island=FriendHousingIsland.Instance;var house=plot?.house;var metadata=house?house.GetComponent<FriendHouseMetadata>():null;
            if(!island||plot==null||!island.Plots.Contains(plot)||!house||house.definition==null||!metadata||!metadata.friendsOwnedClone)return null;
            // Original residence=true always renders LocalPlayer. NEW homes
            // use the original building preview first, then the actual selected
            // resident portrait adapter below; no false LocalPlayer identity.
            var info=new HousingBuildingInfo{id=plot.id,title=house.definition.title,rect=plot.rect,house=house,residence=false};
            info.floors.Add(new HousingFloorInfo{title="住宅",roots=new[]{house.transform},description=()=>string.Join("、",FriendsEconomyService.Instance?.MembersOf(plot.id).Select(p=>p.displayName)??Enumerable.Empty<string>())});return info;
        }
        public static HousingBuildingInfo ForPlot(HousingPlot plot)
        {var added=New(plot);return added??HousingBuildingInfo.Collect().FirstOrDefault(i=>i.house==plot?.house);}
        public static HousingBuildingInfo Find(string id,string instance)
        {
            var plot=FriendHousingIsland.Instance?.Plots.FirstOrDefault(p=>p.id==id&&p.house&&p.house.GetComponent<FriendHouseMetadata>()?.friendsOwnedClone==true);
            if(plot!=null){if(Identity(plot.house)!=instance)return null;return New(plot);}return HousingBuildingInfo.Collect().FirstOrDefault(i=>i.id==id&&string.IsNullOrEmpty(instance));
        }
        public static void Open(HousingMap map,HousingBuildingInfo info,string personId=null,int floor=0)
        {
            if(!map||info==null)return;map.OpenInfo(info);if(map.Window&&floor!=0)map.Window.SetFloor(floor);var metadata=info.house?info.house.GetComponent<FriendHouseMetadata>():null;
            if(metadata&&metadata.friendsOwnedClone&&map.Window&&map.Window.Panel)map.Window.Panel.gameObject.AddComponent<FriendResidenceCardPortrait>().Bind(map.Window,personId);
        }
    }
    public sealed class FriendResidenceCardPortrait:MonoBehaviour
    {
        HousingInfoWindow window;RawImage image;string personId;
        public void Bind(HousingInfoWindow owner,string preferred)
        {
            window=owner;var runtime=FriendsLifeRuntime.Instance;var service=FriendsEconomyService.Instance;string home=window.Building.id;var people=service?.MembersOf(home).ToArray()??Array.Empty<PersonBinding>();string owned=service?.OwnerOf(home);
            var person=people.FirstOrDefault(p=>p.personId==preferred)??people.FirstOrDefault(p=>p.personId==owned)??people.OrderBy(p=>p.personId,StringComparer.Ordinal).FirstOrDefault();
            var title=transform.Find("BuildingName")?.GetComponent<TMP_Text>();var description=transform.Find("Description")?.GetComponent<TMP_Text>();
            if(person==null){if(title)title.text="住宅";if(description)description.text="空置";return;}
            personId=person.personId;if(title)title.text=personId==owned?"户主":"住户";if(description){description.text=person.displayName+(people.Length>1?"\n"+string.Join("、",people.Select(p=>p.displayName)):"");description.alignment=TextAlignmentOptions.Center;}
            image=transform.Find("FloorPlan")?.GetComponent<RawImage>();if(image){image.maskable=false;FriendUi.UseTabletGraphic(image);FriendPortraitCache.Bind(image,personId);image.color=image.texture?Color.white:Color.clear;}
        }
        void LateUpdate(){if(!window||!window.Panel){Destroy(this);return;}if(image&&!string.IsNullOrEmpty(personId))image.color=image.texture?Color.white:Color.clear;}
    }
}
