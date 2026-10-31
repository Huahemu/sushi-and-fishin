// C009MapFix.cs - staged editor fix for two Deep Blue Sushi map bugs (C009).
//  A) North-east red promenade (RestoredCoastConnections_20261021/SM_sidewalk_long) has an open terrain trench
//     on its town side (world x 61.16..87.64, z 25.0..27.03, bottom ~ -1.7) plus a pit west of it
//     (x 61.16..64.03, z 27.03..30.56, bottom ~ -3.5). The original car-park asphalt SM_street (43) and
//     SM_sidewalk_long (1) that covered it were trimmed. Fix: add a static, collidable filler mesh
//     "C009_NorthPromenadeTrenchFill" (red-brick top, foundation-stone sides, 2 BoxColliders).
//  B) Green Market forecourt mesh MarinaMarket_Forecourt_0 (on Root/Comps/MarinaMarket/MarketModels/
//     SM_sidewalk_island_large (4)) runs to the MarketAvenue curb (world x = 1.0) and Unified_Sidewalks has a
//     matching hole x -1.4..1.0, z -30.118..-13.969. Fix: clip the forecourt mesh at world x <= -1.4 (new mesh
//     asset, MeshFilter+MeshCollider reassigned) and add "C009_MarketAvenueWestSidewalkPatch" (sidewalk material,
//     UV = world xz * 0.3 like Unified_Sidewalks, MeshCollider) under TownRoadNetwork20261019/ContinuousSurfaces.
//
// Idempotent: re-running detects the already-applied state and only logs. Scenes are marked dirty but are
// NOT saved unless "Apply And Save" is used or batch mode is run with -c009MapFixSave.
// Batch: Unity -batchmode -projectPath <proj> -executeMethod C009MapFix.Batch [-c009MapFixSave] -quit
// Drop into Assets/Editor/ (editor-only).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class C009MapFix
{
    const string GameScene = "Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
    const string CityScene = "Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
    const string AssetDir = "Assets/SushiAndFish/CityExpansion/C009MapFix";
    const string RedBrickMat = "Assets/Material/M_tiles_ground 3.mat";
    const string StoneMat = "Assets/SushiAndFish/CityExpansion/SolidFoundation20261021/FoundationStone.mat";
    const string Tag = "C009_MAPFIX ";

    // ---- A: north-east promenade trench ----
    const string FixRootName = "C009_MapFix_20261028";
    const string FillName = "C009_NorthPromenadeTrenchFill";
    const string PromenadePath = "RestoredCoastConnections_20261021/SM_sidewalk_long";
    const float FillTopY = 3.03f;          // terrain is 3.00, promenade tiles 3.14, coping 3.17
    static readonly Bounds[] FillBoxes =
    {   // world-space, filler root at identity
        MinMax(new Vector3(61.16f, -2.30f, 25.00f), new Vector3(87.64f, FillTopY, 27.03f)), // trench along promenade south face
        MinMax(new Vector3(61.16f, -3.95f, 27.03f), new Vector3(64.03f, FillTopY, 30.56f)), // pit between wall (130), promenade and stairs
    };

    // ---- B: Green Market forecourt over MarketAvenue west sidewalk ----
    const string ForecourtPath = "Root/Comps/MarinaMarket/MarketModels/SM_sidewalk_island_large (4)";
    const string ForecourtMeshName = "MarinaMarket_Forecourt_0";
    const string ClippedSuffix = "_C009CurbClip";
    const string SurfacesPath = "TownRoadNetwork20261019/ContinuousSurfaces";
    const string SidewalkName = "Unified_Sidewalks";
    const string PatchName = "C009_MarketAvenueWestSidewalkPatch";
    const float HoleMinX = -1.4f, HoleMaxX = 1.0f, HoleMinZ = -30.118f, HoleMaxZ = -13.969f;
    const float SidewalkUv = 0.3f, BrickUv = 0.2f;

    static Bounds MinMax(Vector3 a, Vector3 b) { var r = new Bounds(); r.SetMinMax(a, b); return r; }
    static void Log(string m) => Debug.Log(Tag + m);
    static void Warn(string m) => Debug.LogWarning(Tag + m);

    [MenuItem("Tools/C009 Map Fix/Dry Run (log only)")] public static void DryRun() => Run(false, true);
    [MenuItem("Tools/C009 Map Fix/Apply (do not save)")] public static void Apply() => Run(false, false);
    [MenuItem("Tools/C009 Map Fix/Apply And Save Scenes")] public static void ApplyAndSave() => Run(true, false);
    public static void Batch()
    {
        var args = Environment.GetCommandLineArgs();
        Run(args.Contains("-c009MapFixSave"), args.Contains("-c009MapFixDryRun"));
    }

    static void Run(bool save, bool dry)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Warn("Exit Play mode first."); return; }
        Log($"start save={save} dry={dry}");
        var gs = Open(GameScene); var cs = Open(CityScene);
        if (!gs.IsValid() || !cs.IsValid()) { Warn("Required scenes could not be opened; nothing changed."); return; }
        if (!dry) { Directory.CreateDirectory(AssetDir); AssetDatabase.Refresh(); }
        bool a = FixPromenadeTrench(cs, dry);
        bool b1 = ClipForecourt(cs, dry);
        bool b2 = AddSidewalkPatch(gs, dry);
        if (dry) { Log("dry run finished; nothing modified."); return; }
        if (a || b1) EditorSceneManager.MarkSceneDirty(cs);
        if (b2) EditorSceneManager.MarkSceneDirty(gs);
        AssetDatabase.SaveAssets();
        if (save)
        {
            if (a || b1) EditorSceneManager.SaveScene(cs);
            if (b2) EditorSceneManager.SaveScene(gs);
            Log("scenes saved.");
        }
        else Log("changes applied in memory only (scenes dirty, not saved). Use 'Apply And Save Scenes' or -c009MapFixSave to persist.");
        Log($"done trench={a} forecourtClip={b1} sidewalkPatch={b2}");
    }

    static Scene Open(string path)
    {
        var s = SceneManager.GetSceneByPath(path);
        if (s.IsValid() && s.isLoaded) return s;
        if (!File.Exists(path)) { Warn("missing scene " + path); return default; }
        return EditorSceneManager.OpenScene(path, SceneManager.sceneCount == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
    }

    static Transform Find(Scene s, string path)
    {
        var parts = path.Split('/');
        foreach (var root in s.GetRootGameObjects().Where(g => g.name == parts[0]))
        {
            Transform t = root.transform; bool ok = true;
            for (int i = 1; i < parts.Length && ok; i++)
            {
                Transform next = null;
                foreach (Transform c in t) if (c.name == parts[i]) { next = c; break; }
                if (next == null) ok = false; else t = next;
            }
            if (ok) return t;
        }
        return null;
    }

    static void CopyRenderingSetup(GameObject from, GameObject to)
    {
        to.layer = from.layer;
        GameObjectUtility.SetStaticEditorFlags(to, GameObjectUtility.GetStaticEditorFlags(from));
        var fr = from.GetComponent<MeshRenderer>(); var tr = to.GetComponent<MeshRenderer>();
        if (fr && tr) { tr.shadowCastingMode = fr.shadowCastingMode; tr.receiveShadows = fr.receiveShadows; tr.lightProbeUsage = fr.lightProbeUsage; }
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    // ---------------- A ----------------
    static bool FixPromenadeTrench(Scene cs, bool dry)
    {
        var promenade = Find(cs, PromenadePath);
        if (!promenade || !promenade.GetComponent<Renderer>()) { Warn("A: promenade not found at " + PromenadePath + "; skipped."); return false; }
        var pb = promenade.GetComponent<Renderer>().bounds;
        Log($"A: promenade bounds min={pb.min:F2} max={pb.max:F2}");
        if (Mathf.Abs(pb.min.z - 27.03f) > .25f || Mathf.Abs(pb.min.x - 64.03f) > .25f || Mathf.Abs(pb.max.x - 87.64f) > .25f)
        { Warn("A: promenade moved since analysis (expected min x 64.03 z 27.03, max x 87.64); filler NOT placed - recheck coordinates."); return false; }
        foreach (var land in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var o = land.transform.position; var size = land.terrainData.size;
            foreach (var p in new[] { new Vector3(70, 0, 25.5f), new Vector3(70, 0, 26.5f), new Vector3(70, 0, 27f), new Vector3(62.5f, 0, 28.5f) })
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + size.x && p.z <= o.z + size.z)
                    Log($"A: terrain '{land.name}' height at ({p.x},{p.z}) = {land.SampleHeight(p) + o.y:F2}");
        }
        var root = cs.GetRootGameObjects().FirstOrDefault(g => g.name == FixRootName);
        var existing = root ? root.transform.Find(FillName) : null;
        if (existing && existing.GetComponents<BoxCollider>().Length == FillBoxes.Length) { Log("A: filler already present - no change."); return false; }
        if (dry) { foreach (var b in FillBoxes) Log($"A: would add filler box min={b.min:F2} max={b.max:F2}"); return false; }

        if (!root) { root = new GameObject(FixRootName); SceneManager.MoveGameObjectToScene(root, cs); Undo.RegisterCreatedObjectUndo(root, "C009 map fix"); }
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
        var go = existing ? existing.gameObject : new GameObject(FillName);
        if (!existing) { go.transform.SetParent(root.transform, false); Undo.RegisterCreatedObjectUndo(go, "C009 map fix"); }
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;

        var mesh = SaveMesh(BuildBoxes(FillBoxes), AssetDir + "/NorthPromenadeTrenchFill.asset");
        var mf = go.GetComponent<MeshFilter>(); if (!mf) mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); if (!mr) mr = go.AddComponent<MeshRenderer>();
        var brick = AssetDatabase.LoadAssetAtPath<Material>(RedBrickMat); var stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMat);
        if (!brick || !stone) Warn("A: material missing (brick=" + (bool)brick + " stone=" + (bool)stone + ")");
        mr.sharedMaterials = new[] { brick, stone ? stone : brick };
        CopyRenderingSetup(promenade.gameObject, go);
        foreach (var c in go.GetComponents<BoxCollider>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var b in FillBoxes) { var c = go.AddComponent<BoxCollider>(); c.center = b.center; c.size = b.size; Log($"A: added collider/filler box min={b.min:F2} max={b.max:F2}"); }
        return true;
    }

    static Mesh BuildBoxes(IEnumerable<Bounds> boxes)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var top = new List<int>(); var side = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> into, bool horizontal)
        {
            int k = v.Count; v.AddRange(new[] { a, b, c, d });
            var n = Vector3.Cross(b - a, c - a);
            foreach (var p in new[] { a, b, c, d })
                uv.Add(horizontal ? new Vector2(p.x, p.z) * BrickUv : (Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * .5f);
            into.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
        }
        foreach (var bx in boxes)
        {
            Vector3 lo = bx.min, hi = bx.max;
            Vector3 p000 = new Vector3(lo.x, lo.y, lo.z), p100 = new Vector3(hi.x, lo.y, lo.z), p010 = new Vector3(lo.x, hi.y, lo.z), p110 = new Vector3(hi.x, hi.y, lo.z);
            Vector3 p001 = new Vector3(lo.x, lo.y, hi.z), p101 = new Vector3(hi.x, lo.y, hi.z), p011 = new Vector3(lo.x, hi.y, hi.z), p111 = new Vector3(hi.x, hi.y, hi.z);
            Quad(p010, p011, p111, p110, top, true);      // +Y top (clockwise seen from above = front face in Unity)
            Quad(p000, p010, p110, p100, side, false);    // -Z
            Quad(p101, p111, p011, p001, side, false);    // +Z
            Quad(p001, p011, p010, p000, side, false);    // -X
            Quad(p100, p110, p111, p101, side, false);    // +X
        }
        var m = new Mesh { name = "C009_NorthPromenadeTrenchFill", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m;
    }

    // ---------------- B1: clip forecourt ----------------
    static bool ClipForecourt(Scene cs, bool dry)
    {
        var t = Find(cs, ForecourtPath);
        if (!t) { Warn("B: forecourt owner not found at " + ForecourtPath + "; skipped."); return false; }
        var mf = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>();
        if (!mf || !mf.sharedMesh) { Warn("B: forecourt has no mesh; skipped."); return false; }
        var src = mf.sharedMesh;
        if (src.name.EndsWith(ClippedSuffix)) { Log("B: forecourt already clipped (" + src.name + ") - no change."); return false; }
        if (src.name != ForecourtMeshName) Warn("B: unexpected forecourt mesh '" + src.name + "' (expected " + ForecourtMeshName + "); clipping anyway.");
        var wb = t.GetComponent<Renderer>().bounds;
        Log($"B: forecourt '{src.name}' world bounds min={wb.min:F3} max={wb.max:F3} ({AssetDatabase.GetAssetPath(src)})");
        if (wb.max.x <= HoleMinX + .01f) { Log("B: forecourt already stops at x<=" + HoleMinX + " - no change."); return false; }
        if (dry) { Log($"B: would clip forecourt to world x <= {HoleMinX}"); return false; }

        var clipped = ClipMesh(src, t.localToWorldMatrix, t.worldToLocalMatrix, HoleMinX);
        clipped.name = ForecourtMeshName + ClippedSuffix;
        clipped = SaveMesh(clipped, AssetDir + "/" + ForecourtMeshName + ClippedSuffix + ".asset");
        Undo.RecordObject(mf, "C009 map fix"); mf.sharedMesh = clipped; EditorUtility.SetDirty(mf);
        if (mc) { Undo.RecordObject(mc, "C009 map fix"); mc.sharedMesh = null; mc.sharedMesh = clipped; EditorUtility.SetDirty(mc); }
        var nb = t.GetComponent<Renderer>().bounds;
        Log($"B: forecourt clipped: {src.triangles.Length / 3} -> {clipped.triangles.Length / 3} tris, new world max.x={nb.max.x:F3} (was {wb.max.x:F3}); original mesh asset left untouched.");
        return true;
    }

    struct V { public Vector3 p, n; public Vector2 uv; public Vector4 t; }
    static V Lerp(V a, V b, float f) => new V { p = Vector3.Lerp(a.p, b.p, f), n = Vector3.Lerp(a.n, b.n, f), uv = Vector2.Lerp(a.uv, b.uv, f), t = Vector4.Lerp(a.t, b.t, f) };

    // Keeps the part of every triangle whose WORLD x <= maxWorldX (Sutherland-Hodgman against one plane).
    static Mesh ClipMesh(Mesh src, Matrix4x4 l2w, Matrix4x4 w2l, float maxWorldX)
    {
        var pos = src.vertices; var nor = src.normals; var uvs = src.uv; var tan = src.tangents;
        bool hasN = nor.Length == pos.Length, hasUv = uvs.Length == pos.Length, hasT = tan.Length == pos.Length;
        var outV = new List<V>(); var subs = new List<List<int>>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tri = src.GetTriangles(s); var list = new List<int>(); subs.Add(list);
            for (int i = 0; i < tri.Length; i += 3)
            {
                var poly = new List<V>();
                for (int k = 0; k < 3; k++) { int j = tri[i + k]; poly.Add(new V { p = pos[j], n = hasN ? nor[j] : Vector3.up, uv = hasUv ? uvs[j] : Vector2.zero, t = hasT ? tan[j] : Vector4.zero }); }
                var res = new List<V>();
                for (int k = 0; k < poly.Count; k++)
                {
                    var a = poly[k]; var b = poly[(k + 1) % poly.Count];
                    float da = l2w.MultiplyPoint3x4(a.p).x - maxWorldX, db = l2w.MultiplyPoint3x4(b.p).x - maxWorldX;
                    bool ina = da <= 0, inb = db <= 0;
                    if (ina) res.Add(a);
                    if (ina != inb) res.Add(Lerp(a, b, da / (da - db)));
                }
                if (res.Count < 3) continue;
                int baseIdx = outV.Count; outV.AddRange(res);
                for (int k = 1; k + 1 < res.Count; k++) { list.Add(baseIdx); list.Add(baseIdx + k); list.Add(baseIdx + k + 1); }
            }
        }
        var m = new Mesh { indexFormat = IndexFormat.UInt32 };
        m.SetVertices(outV.Select(x => x.p).ToList());
        if (hasN) m.SetNormals(outV.Select(x => x.n.normalized).ToList());
        if (hasUv) m.SetUVs(0, outV.Select(x => x.uv).ToList());
        if (hasT) m.SetTangents(outV.Select(x => x.t).ToList());
        m.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
        if (!hasN) m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    // ---------------- B2: sidewalk patch ----------------
    static bool AddSidewalkPatch(Scene gs, bool dry)
    {
        var surfaces = Find(gs, SurfacesPath);
        var sidewalk = surfaces ? surfaces.Find(SidewalkName) : null;
        if (!sidewalk || !sidewalk.GetComponent<MeshFilter>() || !sidewalk.GetComponent<MeshFilter>().sharedMesh) { Warn("B: " + SurfacesPath + "/" + SidewalkName + " not found; skipped."); return false; }
        if (surfaces.Find(PatchName)) { Log("B: sidewalk patch already present - no change."); return false; }
        var swMesh = sidewalk.GetComponent<MeshFilter>().sharedMesh; var m = sidewalk.localToWorldMatrix;
        float y = sidewalk.GetComponent<Renderer>().bounds.max.y;
        // verify the hole still exists and measure its exact edges from the mesh
        var verts = swMesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray(); var tris = swMesh.triangles;
        bool covered = Covers(verts, tris, (HoleMinX + HoleMaxX) * .5f, (HoleMinZ + HoleMaxZ) * .5f);
        if (covered) { Log("B: Unified_Sidewalks already covers the MarketAvenue strip - patch not needed."); return false; }
        var edge = verts.Where(p => p.x > HoleMinX - .05f && p.x < HoleMaxX + .05f && p.z > HoleMinZ - .05f && p.z < HoleMaxZ + .05f).ToArray();
        float x0 = HoleMinX, x1 = HoleMaxX, z0 = HoleMinZ, z1 = HoleMaxZ;
        if (edge.Length >= 4) { x0 = edge.Min(p => p.x); x1 = edge.Max(p => p.x); z0 = edge.Min(p => p.z); z1 = edge.Max(p => p.z); }
        Log($"B: sidewalk hole x[{x0:F3},{x1:F3}] z[{z0:F3},{z1:F3}] y={y:F3} (mesh edge verts={edge.Length})");
        if (dry) { Log("B: would add sidewalk patch"); return false; }

        var corners = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
        var go = new GameObject(PatchName); Undo.RegisterCreatedObjectUndo(go, "C009 map fix");
        go.transform.SetParent(surfaces, false); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        var mesh = new Mesh { name = PatchName };
        mesh.vertices = corners.Select(go.transform.InverseTransformPoint).ToArray();
        mesh.uv = corners.Select(p => new Vector2(p.x, p.z) * SidewalkUv).ToArray();
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; // clockwise from above -> faces +Y
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, AssetDir + "/" + PatchName + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = sidewalk.GetComponent<MeshRenderer>().sharedMaterials;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        CopyRenderingSetup(sidewalk.gameObject, go);
        Log($"B: added {SurfacesPath}/{PatchName} ({(x1 - x0):F2} x {(z1 - z0):F2} m) with material '{(sidewalk.GetComponent<MeshRenderer>().sharedMaterial ? sidewalk.GetComponent<MeshRenderer>().sharedMaterial.name : "null")}' and MeshCollider");
        return true;
    }

    static bool Covers(Vector3[] v, int[] t, float x, float z)
    {
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            float d1 = (x - b.x) * (a.z - b.z) - (a.x - b.x) * (z - b.z);
            float d2 = (x - c.x) * (b.z - c.z) - (b.x - c.x) * (z - c.z);
            float d3 = (x - a.x) * (c.z - a.z) - (c.x - a.x) * (z - a.z);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(neg && pos)) return true;
        }
        return false;
    }
}
