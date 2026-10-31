using System;
using SushiAndFish.ResidentialIsland;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public sealed class FriendHouseMetadata : MonoBehaviour
    {
        public bool legacyPlayerOwned = true;
        public string privatePersonId = "";
        public bool friendsOwnedClone;
        public string instanceId = "";
        void Awake() => FriendInstances<FriendHouseMetadata>.Add(this);
        void OnDestroy() => FriendInstances<FriendHouseMetadata>.Remove(this);
        public static FriendHouseMetadata For(HousingHouse house)
        {
            if (!house) return null;
            var metadata = house.GetComponent<FriendHouseMetadata>();
            return metadata ? metadata : house.gameObject.AddComponent<FriendHouseMetadata>();
        }
        public static void MarkNew(HousingHouse house, bool legacy = false, string person = "")
        {
            var metadata = For(house); metadata.friendsOwnedClone = true;
            metadata.legacyPlayerOwned = legacy; metadata.privatePersonId = person ?? "";
            if(string.IsNullOrEmpty(metadata.instanceId))metadata.instanceId=Guid.NewGuid().ToString("N");
            foreach (var door in house.GetComponentsInChildren<HousingDoor>(true)) FriendHousingDoor.Bind(door, house);
        }
    }
    public sealed class FriendRestOccupancy : MonoBehaviour
    {
        public string friendOccupantPersonId = "";
        public static FriendRestOccupancy For(HousingRestInteractable rest)
        {
            if (!rest) return null;
            var state = rest.GetComponent<FriendRestOccupancy>();
            return state ? state : rest.gameObject.AddComponent<FriendRestOccupancy>();
        }
    }
    [Serializable] public sealed class FriendHousingDoorState { public string path; public bool open; }
    [Serializable]
    public sealed class FriendHousingPlacement
    {
        public string plot, type, instanceId;
        public float yaw;
        public bool hasTransform;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool legacyPlayerOwned;
        public bool friendsOwnedClone;
        public FriendHousingDoorState[] doors;
    }
    public static class FriendHousingDoor
    {
        static HousingHouse Owner(HousingDoor door)
        {
            if (!door) return null;
            var actual = door.GetComponentInParent<HousingHouse>(true);
            var bound = door.GetComponent<FriendHousingDoorPose>()?.House;
            return bound && bound == actual ? bound : actual;
        }
        public static string PathFromHouse(HousingDoor door)
        {
            if (!door) return null;
            var house = Owner(door); if (!house) return null;
            var root = house.transform;
            string path = door.name;
            for (var parent = door.transform.parent; parent && parent != root; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
        public static void Bind(HousingDoor door, HousingHouse owner = null)
        {
            BindOwned(door, owner);
        }
        static FriendHousingDoorPose BindOwned(HousingDoor door, HousingHouse owner = null)
        {
            if (!door) return null;
            var house = owner ? owner : Owner(door);
            if (!house || door.GetComponentInParent<HousingHouse>(true) != house) return null;
            var metadata = house.GetComponent<FriendHouseMetadata>();
            if (!metadata || !metadata.friendsOwnedClone) return null;
            var pose = door.GetComponent<FriendHousingDoorPose>();
            if (!pose) pose = door.gameObject.AddComponent<FriendHousingDoorPose>();
            pose.Bind(door, house); return pose;
        }
        public static void SetOpenInstant(HousingDoor door, bool open)
        {
            if (!door) return;
            var pose = BindOwned(door); if (pose) pose.Set(open);
        }
        public static void OpenForResident(HousingDoor door)
        {
            if (!door || door.IsOpen) return;
            var metadata = Owner(door)?.GetComponent<FriendHouseMetadata>();
            if (metadata && metadata.friendsOwnedClone) door.Toggle();
        }
    }
    [DefaultExecutionOrder(1000)]
    public sealed class FriendHousingDoorPose : MonoBehaviour
    {
        HousingDoor door; Quaternion closed; bool bound, force, open;
        public HousingHouse House { get; private set; }
        public void Bind(HousingDoor value, HousingHouse owner)
        { House = owner; if (bound) return; door = value; closed = (door.pivot ? door.pivot : door.transform).localRotation; bound = true; }
        public void Set(bool value)
        { if (!bound) return; if (door.IsOpen != value) door.Toggle(); open = value; force = true; Apply(); }
        void LateUpdate()
        { if (!force || !door) return; if (door.IsOpen != open) { force = false; return; } Apply(); }
        void Apply()
        {
            var pivot = door.pivot ? door.pivot : door.transform;
            var axis = pivot.parent ? pivot.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            pivot.localRotation = Quaternion.AngleAxis(open ? door.openAngle : 0, axis) * closed;
        }
    }
}
