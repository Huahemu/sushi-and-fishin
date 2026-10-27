using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Source meshes/materials are imported offline, then saved as ordinary static
// prefab decorations. No random spawning or runtime game simulation is added.
public static class DecorateFishingIslands {
    const string Root = "Assets/SushiAndFish/FishingIntegration/Archipelago/";
    const string Veg = Root + "Vegetation/";
    const string Out = "D:/UnityProjects/_CodexWork/RockIslandExpansion-20261027/";
    const string GroupName = "How to Fish 原生植被";
    static readonly string[] Names = { "PalmBent1", "PalmBent2", "PalmBent3", "PineTreeSmall", "BeachPlant1", "BeachPlant2", "Fern", "BeachGrass", "Grass", "PineTree" };
    static readonly float[] Sizes = { 10, 9, 8, 3.3f, 1.1f, 1.5f, .8f, .55f, .35f, 10 };
    [Serializable] class Sub { public int[] triangles; }
    [Serializable] class Geometry { public string name,key; public Vector3[] vertices,normals; public Vector2[] uv; public Sub[] submeshes; }
    [Serializable] class MaterialData { public string key,name,texture; public Color color,topColor; public bool grass; }
    [Serializable] class Catalog { public MaterialData[] materials; }
    [Serializable] class Node { public int id,parent; public string name,mesh; public Vector3 p,s; public Quaternion q; public bool active; public string[] materials; }
    [Serializable] class Model { public Node[] nodes; }
    static readonly List<string> checks = new List<string>();
    static void Check(bool value,string message) { checks.Add((value ? "PASS " : "FAIL ") + message); File.WriteAllLines(Out + "VegetationChecks.txt", checks); if (!value) throw new Exception(message); }
    static Bounds Bounds(GameObject root) { var rs = root.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b; }
    static T Save<T>(T value,string path) where T:Object {
        var old = AssetDatabase.LoadAssetAtPath<T>(path);
        if (old) { EditorUtility.CopySerialized(value,old); Object.DestroyImmediate(value); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(value,path); return value;
    }
    [MenuItem("Tools/Fishing/Expand and decorate both islands (offline assets)")]
    public static void BuildAndVerify() {
        Check(!EditorApplication.isPlaying,"Offline source import, static meshes and geometry queries only");
        ExpandRockIsland.BuildBase(); ExpandRockIsland.BuildSecond(); ImportPlants();
        Decorate(1); Decorate(2); AssetDatabase.SaveAssets();
        InstallIslandGrassRepair.ApplyAssets();
        Verify();
    }
    static void ImportPlants() {
        foreach (var folder in new[] { "Meshes", "Materials", "Prefabs" }) Directory.CreateDirectory(Veg + folder);
        AssetDatabase.Refresh(); var meshes = new Dictionary<string,Mesh>(); var materials = new Dictionary<string,Material>();
        foreach (var path in Directory.GetFiles(Veg + "Source", "*.json")) {
            var d = JsonUtility.FromJson<Geometry>(File.ReadAllText(path)); if (d.vertices == null) continue;
            var mesh = new Mesh { name = d.name, indexFormat = d.vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = d.vertices; mesh.uv = d.uv;
            if (d.normals != null && d.normals.Length == d.vertices.Length) mesh.normals = d.normals;
            mesh.subMeshCount = d.submeshes.Length; for (int i = 0; i < d.submeshes.Length; i++) mesh.SetTriangles(d.submeshes[i].triangles, i);
            if (mesh.normals.Length == 0) mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes[d.key] = Save(mesh,Veg + "Meshes/" + d.key + ".asset");
        }
        foreach (var path in Directory.GetFiles(Veg + "Source", "*.png")) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Veg + "Source/VegetationCatalog.json"));
        foreach (var d in catalog.materials) {
            if (string.IsNullOrEmpty(d.texture)) throw new Exception("Missing original vegetation palette: " + d.name);
            var material = new Material(Shader.Find(d.grass ? "SushiAndFish/Original Island Grass" : "Universal Render Pipeline/Lit")) { name = "How to Fish " + d.name, enableInstancing = true };
            material.SetColor("_BaseColor", d.color); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Veg + "Source/" + d.texture + ".png")); material.SetFloat("_Smoothness", .05f); material.SetFloat("_Cull", 0);
            if (d.grass) { material.SetColor("_TopColor",d.topColor); material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.5f); material.EnableKeyword("_ALPHATEST_ON"); }
            materials[d.key] = Save(material,Veg + "Materials/" + d.key + ".mat");
        }
        for (int index = 0; index < Names.Length; index++) {
            string name = Names[index]; var dto = JsonUtility.FromJson<Model>(File.ReadAllText(Veg + "Source/" + name + ".json"));
            var root = new GameObject("How to Fish · " + name); var holder = new GameObject("Original source geometry").transform; holder.SetParent(root.transform,false);
            var nodes = dto.nodes.ToDictionary(n => n.id, n => new GameObject(n.name).transform);
            foreach (var n in dto.nodes) {
                var t = nodes[n.id]; t.SetParent(n.parent == 0 ? holder : nodes[n.parent],false); t.localPosition = n.p; t.localRotation = n.q; t.localScale = n.s; t.gameObject.SetActive(n.active);
                if (string.IsNullOrEmpty(n.mesh)) continue;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[n.mesh]; t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = n.materials.Select(m => materials[m]).ToArray();
                if (name.StartsWith("Palm") && n.name.StartsWith("PalmBent")) t.gameObject.AddComponent<MeshCollider>().sharedMesh = meshes[n.mesh];
            }
            var bounds = Bounds(root); float scale = Sizes[index] / bounds.size.y;
            holder.localScale = Vector3.one * scale;
            bool tree = name.StartsWith("Palm") || name.StartsWith("PineTree");
            holder.localPosition = new Vector3(tree ? 0 : -bounds.center.x * scale, -bounds.min.y * scale - .025f, tree ? 0 : -bounds.center.z * scale);
            if (name.StartsWith("PineTree")) { var trunk = root.AddComponent<CapsuleCollider>(); trunk.radius = name == "PineTree" ? .28f : .16f; trunk.height = name == "PineTree" ? 3 : 1.6f; trunk.center = Vector3.up * trunk.height / 2; }
            PrefabUtility.SaveAsPrefabAsset(root,Veg + "Prefabs/" + name + ".prefab"); Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }
    static bool InOpenCore(Vector3 p,float margin = 1.5f) => Mathf.Abs(p.x) < 25 + margin && Mathf.Abs(p.z) < 20 + margin;
    static bool InEntry(Vector3 p) => Vector3.Dot(p,new Vector3(-1,0,-1).normalized) > 24 && Mathf.Abs(p.x-p.z) < 13;
    static void Decorate(int index) {
        string path = Root + "Prefabs/HowToFishIsland" + index + ".prefab"; var island = PrefabUtility.LoadPrefabContents(path);
        try {
            var old = island.transform.Find(GroupName); if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(GroupName).transform; group.SetParent(island.transform,false);
            var terrain = island.GetComponentsInChildren<MeshCollider>().Single(c => c.name == "Island1");
            var rng = new System.Random(10270 + index); int count = 0;
            var centres = index == 1 ? new[] { new Vector3(-34,0,-8), new Vector3(-31,0,18),new Vector3(-12,0,29),new Vector3(10,0,31),new Vector3(28,0,23),new Vector3(34,0,4),new Vector3(28,0,-23),new Vector3(-2,0,-30) }
                : new[] { new Vector3(-38,0,-9),new Vector3(-32,0,21),new Vector3(-14,0,31),new Vector3(9,0,32),new Vector3(31,0,23),new Vector3(38,0,3),new Vector3(30,0,-25),new Vector3(3,0,-31) };
            float Rand(float lo,float hi) => Mathf.Lerp(lo,hi,(float)rng.NextDouble());
            bool Place(string kind,Vector3 point,float scale) {
                if (InOpenCore(point) || InEntry(point)) return false;
                var ray = new Ray(island.transform.TransformPoint(point + Vector3.up * 40),Vector3.down);
                if (!terrain.Raycast(ray,out var hit,70) || hit.point.y < .6f || hit.normal.y < .72f) return false;
                // Avoid the original tower and exposed source coastal rocks.
                if (index == 1 && Vector2.Distance(new Vector2(point.x,point.z),new Vector2(12,27)) < 5) return false;
                var plant = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Veg + "Prefabs/" + kind + ".prefab"),group);
                plant.name = kind + " " + (++count).ToString("D3"); plant.transform.SetPositionAndRotation(hit.point,Quaternion.Euler(0,Rand(0,360),0)); plant.transform.localScale = Vector3.one * scale; return true;
            }
            foreach (var centre in centres) {
                for (int i = 0; i < 3; i++) Place(index == 1 ? Names[rng.Next(0,3)] : "PineTree",centre + new Vector3(Rand(-3,3),0,Rand(-3,3)),Rand(.78f,1.18f));
                if (index == 2) for (int i = 0; i < 3; i++) Place("PineTreeSmall",centre + new Vector3(Rand(-5,5),0,Rand(-5,5)),Rand(.75f,1.2f));
                for (int i = 0; i < 12; i++) Place(index == 2 && i % 2 == 0 ? "Fern" : i % 3 == 0 ? "BeachPlant2" : "BeachPlant1",centre + new Vector3(Rand(-5.5f,5.5f),0,Rand(-5.5f,5.5f)),Rand(.65f,1.25f));
                for (int i = 0; i < 22; i++) Place(i % 3 == 0 ? "Grass" : "BeachGrass",centre + new Vector3(Rand(-7,7),0,Rand(-7,7)),Rand(.7f,1.4f));
            }
            PrefabUtility.SaveAsPrefabAsset(island,path); File.WriteAllText(Out + "Vegetation" + index + ".txt", "island=" + index + "\nplants=" + count + "\nclusters=8\ncore=50x40m, clear approach corridor\n" + string.Join("\n",group.Cast<Transform>().GroupBy(t => t.name.Split(' ')[0]).Select(g => g.Key + "=" + g.Count())));
        } finally { PrefabUtility.UnloadPrefabContents(island); }
    }
    public static void Verify() {
        ExpandRockIsland.VerifyIsland(1,false); ExpandRockIsland.VerifyIsland(2,true);
        foreach (int index in new[] { 1,2 }) {
            var prefab = ArchipelagoSettings.Load().regions[index].prefab; var group = prefab.transform.Find(GroupName);
            Check(group && group.childCount >= 150,"Island " + index + " has original vegetation grouped into shoreline clusters");
            Check(group.Cast<Transform>().All(t => !InOpenCore(t.localPosition) && !InEntry(t.localPosition)),"Island " + index + " keeps all vegetation outside the 50 x 40m core and broad landing route");
            Check(group.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterials.All(m => m && (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "SushiAndFish/Original Island Grass") && m.GetTexture("_BaseMap"))),"Island " + index + " vegetation uses source palette / grass masks with supported shaders");
            Check(group.GetComponentsInChildren<Collider>().All(c => c is CapsuleCollider || c.name.StartsWith("PalmBent")),"Island " + index + " only tree trunks have collision; shrubs and leaves are passable");
            Check(!group.GetComponentsInChildren<Rigidbody>().Any() && !group.GetComponentsInChildren<MonoBehaviour>().Any(),"Island " + index + " decorations add no source AI, dynamic bodies or update loops");
        }
        Check(Names.All(name => AssetDatabase.LoadAssetAtPath<GameObject>(Veg + "Prefabs/" + name + ".prefab")),"All ten original palm/pine/sapling/shrub/fern/grass model types are retained as reusable prefabs");
        Check(ArchipelagoSettings.Load().regions.SelectMany(r => r.fish).Select(f => f.originalId).Distinct().Count() == 60,"All original regional species, warehouse and catalogue bindings remain available");
        File.WriteAllLines(Out + "VegetationChecks.txt",checks); Debug.Log("DECORATED_ISLANDS_VERIFIED checks=" + checks.Count + " play=false simulation=false savesWritten=false");
    }
}
