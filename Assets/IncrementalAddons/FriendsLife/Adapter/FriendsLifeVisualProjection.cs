using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using SushiAndFish.ResidentialIsland;
using TomodachiLife;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    /// <summary>Committed source wardrobe/room data projected onto the actual host actors and their own rooms.</summary>
    public sealed class FriendsLifeVisualProjection : MonoBehaviour
    {
        readonly Dictionary<string, string> applied = new Dictionary<string, string>();
        readonly Dictionary<string, FriendCharacterData> actorData = new Dictionary<string, FriendCharacterData>();
        readonly Dictionary<int, string> roomStyles = new Dictionary<int, string>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<string, Texture2D> designTextures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, int> designRevisions = new Dictionary<string, int>();
        float next;

        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance;
            if (runtime == null || !runtime.Ready || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .5f;
            foreach (var person in runtime.GetPeople().Where(p => p.controllerKind == PersonControllerKind.Npc))
            {
                var actor = runtime.ActorOf(person.personId);
                var creative = runtime.Core.State.creative?.residents.FirstOrDefault(p => p.residentId == person.nativeResidentId);
                if (actor != null && actor.ResidentData != null && !actor.IsCustomizationApplying)
                {
                    var body = runtime.TransformOf(person.personId);
                    var child = runtime.Core.State.family?.children.FirstOrDefault(c => c.residentId == person.nativeResidentId);
                    string signature = actor.GetInstanceID() + "/" + person.appearanceRevision + "/" + creative?.revision + "/" + child?.appearanceRevision + "/" +
                        body?.GetInstanceID() + "/" +
                        string.Join(";", creative?.garments.Select(g => g.clothId + ":" + g.color + ":" + g.designId) ?? Enumerable.Empty<string>());
                    if (!applied.TryGetValue(person.personId, out var previous) || previous != signature)
                    {
                        try
                        {
                            var data = ProjectCharacter(runtime, person, creative);
                            if (data != null)
                            {
                                if (!actorData.TryGetValue(person.personId, out var authority) || authority == null)
                                { authority = ScriptableObject.CreateInstance<FriendCharacterData>(); actorData[person.personId] = authority; }
                                authority.data = data; FriendResidentBindings.ApplyAppearance(actor, authority);
                                var worker = body == null ? null : body.GetComponentInChildren<OutfitSystem>(true);
                                if (body != actor.transform && worker != null && !worker.isloading) _ = BMAC_SaveSystem.LoadCharacter(worker, data);
                                applied[person.personId] = signature;
                            }
                        }
                        catch (Exception error) { Debug.LogWarning("[FriendsLife] Visual projection pending for " + person.personId + ": " + error.Message); }
                    }
                }
                if (creative != null) ProjectRoom(runtime, person, creative);
            }
        }
        public static CharacterData ProjectCharacter(FriendsLifeRuntime runtime, PersonBinding person, LifeCreativeResident creative)
        {
            var actor = runtime.ActorOf(person.personId);
            CharacterData data = !string.IsNullOrEmpty(person.appearanceJson) ? JsonUtility.FromJson<CharacterData>(person.appearanceJson) : actor?.ResidentData?.CharacterData?.GetCharacterData();
            if (data == null) return null;
            data = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(data));
            data.outfitDatas = data.outfitDatas ?? new List<OutfitData>();
            if (creative?.garments != null)
                foreach (var garment in creative.garments)
                {
                    var definition = runtime.Core.CreativeCatalog.Cloth(garment.clothId);
                    string path = FriendsHostVisualBindings.OutfitResource(definition);
                    if (path == null) throw new InvalidOperationException("Missing source wardrobe category binding: " + garment.clothId);
                    var template = Resources.Load<Outfit>(path);
                    if (template == null || template.Type == null) throw new InvalidOperationException("Missing host outfit template: " + path);
                    string type = template.Type.name;
                    data.outfitDatas.RemoveAll(o => OutfitType(o.outfit) == type || definition.sourceType == "All" && (OutfitType(o.outfit) == "Top" || OutfitType(o.outfit) == "Bottom"));
                    if (definition.sourceType == "Headwear") data.outfitDatas.RemoveAll(o => OutfitType(o.outfit) == "Hat" || OutfitType(o.outfit) == "UpperFace");
                    if (definition.sourceType == "Tops" || definition.sourceType == "Topslong" || definition.sourceType == "BottomsA") data.outfitDatas.RemoveAll(o => OutfitType(o.outfit) == "Overall");
                    data.outfitDatas.Add(FriendsHostVisualBindings.OutfitData(template, garment.color));
                }
            var child = runtime.Core.State.family?.children.FirstOrDefault(c => c.residentId == person.nativeResidentId);
            if (child != null)
            {
                var sourceResident = runtime.Core.FindResident(person.nativeResidentId);
                if (!string.IsNullOrEmpty(sourceResident?.avatarJson))
                {
                    var document = JsonUtility.FromJson<ResidentEditDocument>(sourceResident.avatarJson);
                    if (document?.appearance != null)
                    {
                        // Existing host morphs receive the source's actual raw
                        // height/build; this does not change family growth rules.
                        int height = data.bodyModsKeys?.IndexOf("root") ?? -1;
                        if (height >= 0 && data.bodyMods != null && height < data.bodyMods.Count)
                            data.bodyMods[height].scaleValue = Mathf.Clamp01(document.appearance.height / 127f);
                        int build = data.bodyIDs?.IndexOf("Weight") ?? -1;
                        if (build >= 0 && data.bodyShapes != null && build < data.bodyShapes.Count) data.bodyShapes[build] = document.appearance.build * 100f / 127f;
                    }
                }
            }
            return data;
        }
        static string OutfitType(string resource) => string.IsNullOrEmpty(resource) ? "" : resource.Split('/')[0];
        void ProjectRoom(FriendsLifeRuntime runtime, PersonBinding person, LifeCreativeResident creative)
        {
            if (string.IsNullOrEmpty(creative.roomStyleId) && string.IsNullOrEmpty(creative.roomDesignId)) return;
            var economy = FriendsEconomyService.Instance;
            var room = FriendsHomeInteriors.Instance?.RoomFor(person.personId)?.house;
            if (room == null && economy != null && economy.MembersOf(person.currentHomeId).Count() <= 1) room = economy.Plot(person.currentHomeId)?.house;
            if (room == null) return;
            var design = runtime.Core.State.creative?.designs.FirstOrDefault(d => d.id == creative.roomDesignId);
            string signature = creative.roomStyleId + "/" + creative.roomDesignId + "/" + design?.revision;
            int id = room.GetInstanceID();
            if (roomStyles.TryGetValue(id, out var existing) && existing == signature) return;
            Resources.Load<GameObject>("Housing/AR_PlayerHome"); Resources.Load<GameObject>("Housing/AR_GuestHome");
            var names = FriendsHostVisualBindings.RoomMaterials(creative.roomStyleId);
            Material floor = FindHostMaterial(names[0]), wall = FindHostMaterial(names[1]);
            if (floor == null || wall == null) return;
            Texture2D texture = design == null ? null : TextureFor(design);
            foreach (var renderer in room.GetComponentsInChildren<Renderer>(true))
            {
                bool floorSurface = renderer.name.IndexOf("_Floorboard_", StringComparison.Ordinal) >= 0 || renderer.name.EndsWith("BathTileFloor", StringComparison.Ordinal);
                bool wallSurface = renderer.name.EndsWith("InteriorWallsConnected", StringComparison.Ordinal);
                if (!floorSurface && !wallSurface) continue;
                if (!originalMaterials.TryGetValue(renderer, out var original)) { original = renderer.sharedMaterials.ToArray(); originalMaterials[renderer] = original; }
                var materials = original.ToArray();
                for (int i = 0; i < materials.Length; i++) materials[i] = floorSurface ? floor : wall;
                renderer.sharedMaterials = materials;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetTexture("_BaseMap", texture != null ? texture : (floorSurface ? floor.mainTexture : wall.mainTexture));
                block.SetTexture("_MainTex", texture != null ? texture : (floorSurface ? floor.mainTexture : wall.mainTexture));
                renderer.SetPropertyBlock(block);
            }
            roomStyles[id] = signature;
        }
        static Material FindHostMaterial(string suffix)
        {
            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && (m.name == "AR_GuestHome_" + suffix || m.name == "AR_PlayerHome_" + suffix));
        }
        Texture2D TextureFor(LifeCreativeDesign design)
        {
            if (design.pixels == null || design.pixels.Length != design.width * design.height) return null;
            if (designTextures.TryGetValue(design.id, out var texture) && designRevisions.TryGetValue(design.id, out var revision) && revision == design.revision) return texture;
            if (texture == null) { texture = new Texture2D(design.width, design.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; designTextures[design.id] = texture; }
            var pixels = design.pixels.Select(p => new Color32((byte)(p >> 24), (byte)(p >> 16), (byte)(p >> 8), (byte)p)).ToArray();
            texture.SetPixels32(pixels); texture.Apply(); designRevisions[design.id] = design.revision; return texture;
        }
        void OnDestroy()
        {
            foreach (var data in actorData.Values) if (data != null) Destroy(data);
            foreach (var texture in designTextures.Values) if (texture != null) Destroy(texture);
        }
    }
}
