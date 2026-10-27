using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Offline asset authoring and static geometry inspection. No Play, gameplay
// lifecycle, player input replay or physics stepping is used by this command.
public static class ExpandRockIsland {
    const string Root = "Assets/SushiAndFish/FishingIntegration/Archipelago/";
    const string Out = "D:/UnityProjects/_CodexWork/RockIslandExpansion-20261027/";
    const string IslandPath = Root + "Prefabs/HowToFishIsland1.prefab";
    const string AccessPath = Root + "Prefabs/HowToFishAccessDocks.prefab";
    const string MeshPath = Root + "Meshes/RockIslandExpanded.asset";
    const int Segments = 64;
    static readonly List<string> checks = new List<string>();
    static readonly float[] Rings = { .18f, .36f, .54f, .72f, .80f, .91f, 1f, 1.12f };
    static readonly float[] Heights = { 3.1f, 3.1f, 3.1f, 3.1f, 2.5f, .7f, -.55f, -6f };

    static void Check(bool value, string title) {
        checks.Add((value ? "PASS " : "FAIL ") + title);
        Directory.CreateDirectory(Out); File.WriteAllLines(Out + "Checks.txt", checks);
        if (!value) throw new Exception(title);
    }
    static Transform Named(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    static Bounds Bounds(GameObject root) {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Use the original mainland's outline, excluding its two disconnected
    // offshore rocklets. Its source mesh and source JSON stay untouched.
    static Mesh OriginalMesh(int index) => AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/" + (index == 1 ? "sharedassets7_13" : "sharedassets8_8") + ".asset");
    static Vector2 Palette(int index) => OriginalMesh(index).uv.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
    static Vector2[] OriginalOutline(int index) {
        var source = OriginalMesh(index);
        var points = source.vertices.Select(v => new Vector2(v.x, -v.y))
            .Where(v => index == 2 || v.x < 13 && v.y > -12 && v.y < 13)
            .Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToArray();
        if (index == 2) { var centre = new Vector2((points.Min(v => v.x) + points.Max(v => v.x)) / 2, (points.Min(v => v.y) + points.Max(v => v.y)) / 2); points = points.Select(v => (v - centre) * .45f).ToArray(); }
        var hull = new List<Vector2>();
        foreach (var p in points) { while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        int lower = hull.Count;
        for (int i = points.Length - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        hull.RemoveAt(hull.Count - 1); return hull.ToArray();
    }
    static float OutlineRadius(Vector2 direction, Vector2[] hull) {
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < hull.Length; i++) {
            var a = hull[i]; var edge = hull[(i + 1) % hull.Length] - a;
            float divisor = Cross(direction, edge); if (Mathf.Abs(divisor) < .00001f) continue;
            float distance = Cross(a, edge) / divisor, segment = Cross(a, direction) / divisor;
            if (distance > 0 && segment >= 0 && segment <= 1) nearest = Mathf.Min(nearest, distance);
        }
        if (!float.IsFinite(nearest)) throw new Exception("Original island outline is not closed");
        return nearest;
    }
    static Mesh BuildMesh(int index = 1) {
        var outline = OriginalOutline(index); var vertices = new List<Vector3> { new Vector3(0, 3.1f, 0) };
        foreach (int ring in Enumerable.Range(0, Rings.Length)) for (int i = 0; i < Segments; i++) {
            float angle = i * 2 * Mathf.PI / Segments; var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float shape = Mathf.Clamp(OutlineRadius(direction, outline) / 11.2f, .92f, 1.09f);
            // Compress outline variations in the interior so a broad continuous
            // plateau replaces the original steep, tiny summit.
            float coast = Mathf.InverseLerp(.72f, 1, Rings[ring]);
            shape = Mathf.Lerp(1, shape, Mathf.InverseLerp(.54f, 1, Rings[ring]));
            shape *= 1 + coast * (.085f * Mathf.Sin(angle * 3 + .7f) + .04f * Mathf.Sin(angle * 7));
            float stagger = ring > 3 ? .025f * Mathf.Sin(angle * 5 + ring * 1.9f) : 0;
            var p = new Vector3(Mathf.Cos(angle + stagger) * (index == 1 ? 50 : 56), Heights[ring], Mathf.Sin(angle + stagger) * (index == 1 ? 46 : 48)) * (Rings[ring] * shape);
            p.y = Heights[ring] + (ring <= 3 ? .045f * Mathf.Sin(p.x * .17f) * Mathf.Sin(p.z * .13f) : ring < 6 ? .35f * Mathf.Sin(angle * 4 + ring) : .12f * Mathf.Sin(angle * 5));
            vertices.Add(p);
        }
        var triangles = new List<int>();
        for (int i = 0; i < Segments; i++) { triangles.Add(0); triangles.Add(1 + (i + 1) % Segments); triangles.Add(1 + i); }
        for (int ring = 1; ring < Rings.Length; ring++) for (int i = 0; i < Segments; i++) {
            int a = 1 + (ring - 1) * Segments + i, b = 1 + (ring - 1) * Segments + (i + 1) % Segments;
            int c = 1 + ring * Segments + i, d = 1 + ring * Segments + (i + 1) % Segments;
            triangles.AddRange(new[] { a, b, c, b, d, c });
        }
        // Reuse the actual original rocky mesh along the coast to retain its
        // irregular fractured silhouette, beyond the clear rectangle/route.
        var uvs = Enumerable.Repeat(Palette(index), vertices.Count).ToList(); var source = OriginalMesh(index);
        var sourceCentre = index == 1 ? Vector3.zero : new Vector3(source.bounds.center.x, 0, -source.bounds.center.y);
        foreach (var offset in new[] { new Vector3(-44, .8f, 16), new Vector3(28, .4f, 35), new Vector3(31, .4f, -34) }) {
            int first = vertices.Count;
            foreach (var v in source.vertices) vertices.Add(Vector3.Scale(new Vector3(v.x, v.z, -v.y) - sourceCentre, index == 1 ? new Vector3(.45f, .55f, .45f) : new Vector3(.28f, .45f, .28f)) + offset);
            uvs.AddRange(source.uv);
            triangles.AddRange(source.triangles.Select(i => i + first));
        }
        // Flat face normals and the original rock palette preserve the existing
        // low-poly material language; collision uses exactly the visible mesh.
        var mesh = new Mesh { name = "Expanded original rock island with open plateau" };
        mesh.vertices = triangles.Select(i => vertices[i]).ToArray();
        mesh.uv = triangles.Select(i => uvs[i]).ToArray();
        mesh.triangles = Enumerable.Range(0, triangles.Count).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = index == 1 ? MeshPath : Root + "Meshes/BoneIslandExpanded.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    public static void BuildAndVerify() { BuildBase(); Verify(); }
    public static void BuildBase() {
        Check(!EditorApplication.isPlaying, "Offline assets only; no Play or physics stepping");
        var settings = ArchipelagoSettings.Load(); var sourceIds = settings.regions.SelectMany(r => r.fish).Select(f => f.originalId).ToArray();
        var sourceBoat = settings.boatSpawn; var mesh = BuildMesh();
        var root = PrefabUtility.LoadPrefabContents(IslandPath);
        try {
            var terrain = Named(root, "Island1");
            terrain.localPosition = Vector3.zero; terrain.localRotation = Quaternion.identity; terrain.localScale = Vector3.one;
            terrain.GetComponent<MeshFilter>().sharedMesh = mesh; terrain.GetComponent<MeshCollider>().sharedMesh = mesh;
            // Retain inactive source content for provenance/rollback, while
            // removing all archery props and obsolete source-world blockers.
            var holder = Named(root, "MiniIslandHolder");
            foreach (Transform child in holder) child.gameObject.SetActive(child.name == "Meshes");
            var group = Named(root, "IslandMini1");
            foreach (Transform child in group) child.gameObject.SetActive(child == terrain || child.name == "Tower");
            var tower = Named(root, "Tower"); tower.localPosition = Vector3.zero;
            var towerBounds = Bounds(tower.gameObject);
            tower.localPosition = new Vector3(12, 3.1f - towerBounds.min.y - .04f, 27);
            PrefabUtility.SaveAsPrefabAsset(root, IslandPath);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        // Centre and fish pools stay fixed. Dock moves seaward beyond the new
        // shoreline; only this island's access assembly is rebuilt below.
        var direction = new Vector3(-1, 0, -1).normalized;
        settings.regions[1].dock = settings.regions[1].center + direction * 76;
        settings.regions[1].fishingRadius = 145;
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var island = (GameObject)PrefabUtility.InstantiatePrefab(settings.regions[1].prefab); island.transform.position = settings.regions[1].center;
        Physics.SyncTransforms();
        RebuildIslandAccess(settings, island);
        Object.DestroyImmediate(island); AssetDatabase.SaveAssets();
        Check(settings.boatSpawn == sourceBoat && sourceIds.SequenceEqual(settings.regions.SelectMany(r => r.fish).Select(f => f.originalId)), "Main boat spawn and all 60 regional species remain unchanged");
    }
    public static void BuildSecond() {
        var settings = ArchipelagoSettings.Load(); var mesh = BuildMesh(2); var path = Root + "Prefabs/HowToFishIsland2.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            var terrain = Named(root, "Island1"); terrain.localPosition = Vector3.zero; terrain.localRotation = Quaternion.identity; terrain.localScale = Vector3.one;
            terrain.GetComponent<MeshFilter>().sharedMesh = mesh; terrain.GetComponent<MeshCollider>().sharedMesh = mesh;
            foreach (Transform child in Named(root, "MiniIslandHolder")) child.gameObject.SetActive(child.name == "Meshes");
            foreach (Transform child in terrain.parent) child.gameObject.SetActive(child == terrain);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        settings.regions[2].dock = settings.regions[2].center + new Vector3(-1, 0, -1).normalized * 80;
        settings.regions[2].fishingRadius = 155; EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        var island = (GameObject)PrefabUtility.InstantiatePrefab(settings.regions[2].prefab); island.transform.position = settings.regions[2].center;
        Physics.SyncTransforms(); RebuildIslandAccess(settings, island, 2); Object.DestroyImmediate(island); AssetDatabase.SaveAssets();
    }
    static void RebuildIslandAccess(ArchipelagoSettings settings, GameObject island, int index = 1) {
        var region = settings.regions[index]; var root = PrefabUtility.LoadPrefabContents(AccessPath);
        try {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) if (child.name.StartsWith(region.title)) Object.DestroyImmediate(child.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/HowToFishDock.prefab"); var b = Bounds(prefab);
            var outward = new Vector3(-1, 0, -1).normalized; var rotation = Quaternion.LookRotation(outward);
            var dock = Object.Instantiate(prefab, root.transform); dock.name = region.title + " · 原版码头";
            dock.transform.SetPositionAndRotation(region.dock, rotation); dock.transform.GetChild(0).localPosition -= new Vector3(b.center.x, 0, b.center.z);
            // End well inside the flat plateau. A five-metre clear surface
            // includes the whole shoreline transition, without a ledge seam.
            var terrain = Named(island, "Island1").GetComponent<MeshCollider>();
            var land = region.center + outward * 32; var ray = new Ray(land + Vector3.up * 50, Vector3.down);
            if (!terrain.Raycast(ray, out var hit, 100)) throw new Exception("No plateau under new access route");
            // Bury the land end slightly, so its extended collider cannot leave
            // a raised lip where the player steps off onto the plateau.
            var start = hit.point - Vector3.up * .06f; var end = region.dock + Vector3.up * b.max.y;
            var line = end - start; var frame = new GameObject(region.title + " · 靠岸栈桥"); frame.transform.SetParent(root.transform, false);
            frame.transform.SetPositionAndRotation((start + end) / 2, Quaternion.LookRotation(line, Vector3.up));
            var collider = frame.AddComponent<BoxCollider>(); collider.center = Vector3.down * .07f; collider.size = new Vector3(5, .14f, line.magnitude + .8f);
            var slat = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/BridgeSlat.asset"); var material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            int count = Mathf.CeilToInt(line.magnitude / .5f);
            for (int i = 0; i <= count; i++) Part(frame.transform, "原木桥板", new Vector3(0, -.07f, (i / (float)count - .5f) * line.magnitude), new Vector3(5, .14f, .51f), slat, material);
            for (float z = -line.magnitude / 2; z <= line.magnitude / 2; z += 4) foreach (float x in new[] { -2.35f, 2.35f }) Part(frame.transform, "原木支撑柱", new Vector3(x, -1.4f, z), new Vector3(.16f, 2.8f, .16f), slat, material);
            PrefabUtility.SaveAsPrefabAsset(root, AccessPath);
            File.WriteAllText(Out + "Access" + index + ".txt", "dock=" + region.dock.ToString("F3") + "\nplateau=" + start.ToString("F3") + "\nwidth=5m length=" + line.magnitude.ToString("F2") + " slope=" + Vector3.Angle(line, Vector3.ProjectOnPlane(line, Vector3.up)).ToString("F2"));
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Mesh mesh, Material material) {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    public static void Verify() { VerifyIsland(1); }
    public static void VerifyIsland(int index, bool audit = true) {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var settings = ArchipelagoSettings.Load(); var region = settings.regions[index];
        var island = (GameObject)PrefabUtility.InstantiatePrefab(region.prefab); island.transform.position = region.center;
        var access = (GameObject)PrefabUtility.InstantiatePrefab(settings.accessPrefab);
        var terrain = Named(island, "Island1").GetComponent<MeshCollider>(); var mesh = terrain.sharedMesh;
        Physics.SyncTransforms();
        Check(mesh == Named(island, "Island1").GetComponent<MeshFilter>().sharedMesh, "Visible terrain and non-convex walking collider share the expanded mesh");
        Check(mesh.bounds.size.x > 100 && mesh.bounds.size.z > 90, "Expanded island has more than 100 x 90 metres of full terrain including underwater skirt");
        if (index == 1) {
            Check(island.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Target")).Count() == 5 && !island.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("Target")), "All five source targets are inactive, including the three stacked at the old landing");
            Check(!Named(island, "ExtraColliders").gameObject.activeInHierarchy && !Named(island, "NPCs").gameObject.activeInHierarchy, "Unused source blockers and source-world NPC colliders cannot obstruct the island");
            Check(Named(island, "Tower").gameObject.activeInHierarchy, "Original tower landmark remains at the edge of the open area");
        }
        int samples = 0; float low = float.PositiveInfinity, high = float.NegativeInfinity, maximumSlope = 0; bool clear = true;
        for (float x = -25; x <= 25; x += 1) for (float z = -20; z <= 20; z += 1) {
            var ray = new Ray(region.center + new Vector3(x, 40, z), Vector3.down);
            if (!terrain.Raycast(ray, out var hit, 80)) { clear = false; continue; }
            low = Mathf.Min(low, hit.point.y); high = Mathf.Max(high, hit.point.y); maximumSlope = Mathf.Max(maximumSlope, Vector3.Angle(hit.normal, Vector3.up));
            clear &= !Physics.CheckCapsule(hit.point + Vector3.up * .45f, hit.point + Vector3.up * 1.45f, .39f, ~0, QueryTriggerInteraction.Ignore); samples++;
        }
        Check(samples == 2091 && clear, "Every point of a 50 x 40 metre rectangle has terrain support and full player capsule clearance (2,091 probes)");
        Check(high - low < .12f && maximumSlope < 1.5f, "Open 2,000-square-metre core varies less than 12cm in height and slopes below 1.5 degrees");
        var frame = Named(access, region.title + " · 靠岸栈桥"); var bridge = frame.GetComponent<BoxCollider>();
        Check(bridge.size.x == 5 && Vector3.Angle(frame.forward, Vector3.ProjectOnPlane(frame.forward, Vector3.up)) < 8, "New five-metre-wide continuous gangplank has a gentle slope below eight degrees");
        bool supported = true, unobstructed = true; float previous = float.NaN, worstStep = 0; var routeIssues = new List<string>();
        // Static route inspection across a four-metre usable width from dock
        // to plateau, then continuing to the centre of the open ground.
        var inward = new Vector3(1, 0, 1).normalized; var right = Vector3.Cross(Vector3.up, inward);
        var dockStart = region.dock + inward * 3;
        float routeLength = Vector3.ProjectOnPlane(region.center - dockStart, Vector3.up).magnitude;
        for (float distance = 0; distance <= routeLength; distance += .5f) {
            float midHeight = 0;
            foreach (float side in new[] { -1.8f, 0, 1.8f }) {
                var p = dockStart + inward * distance + right * side;
                if (!Physics.Raycast(p + Vector3.up * 25, Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < .8f || hit.point.y < -3.8f) { supported = false; routeIssues.Add("support " + distance + " side=" + side + " hit=" + hit.point + " normal=" + hit.normal); continue; }
                var obstacles = Physics.OverlapCapsule(hit.point + Vector3.up * .45f, hit.point + Vector3.up * 1.45f, .39f, ~0, QueryTriggerInteraction.Ignore);
                if (obstacles.Length > 0) { unobstructed = false; routeIssues.Add("obstacle " + distance + " side=" + side + " hit=" + hit.point + " " + string.Join(",", obstacles.Select(o => o.name))); }
                if (side == 0) midHeight = hit.point.y;
            }
            if (float.IsFinite(previous)) { float step = Mathf.Abs(midHeight - previous); worstStep = Mathf.Max(worstStep, step); if (step >= .2f) routeIssues.Add("step " + distance + " delta=" + step); } previous = midHeight;
        }
        File.WriteAllLines(Out + "RouteIssues" + index + ".txt", routeIssues);
        Check(supported && unobstructed && worstStep < .2f, "Dock-to-interior route has continuous support, four-metre clear width, body/head clearance and no high step");
        bool water = true; var rotation = Quaternion.LookRotation(-inward);
        foreach (float side in new[] { -6.5f, 6.5f }) water &= ArchipelagoBoatSafety.ClearWater(region.dock + rotation * Vector3.right * side, rotation, -4);
        Check(water, "Both sides of the relocated dock offer clear water under the full boat footprint");
        Check(Vector3.Distance(settings.regions[1].center, settings.regions[2].center) > settings.regions[1].fishingRadius + settings.regions[2].fishingRadius && settings.RegionAt(region.dock) == index, "Expanded island and relocated dock use their original fish pool without overlapping the other island");
        Check(settings.regions.SelectMany(r => r.fish).Select(f => f.originalId).Distinct().Count() == 60, "All sixty fish species remain available in their regional pools");
        var material = terrain.GetComponent<MeshRenderer>().sharedMaterial;
        Check(material && material.shader && material.shader.name == "Universal Render Pipeline/Lit" && material.GetTexture("_BaseMap") && (mesh.uv[0] - Palette(index)).sqrMagnitude < .000001f, "Original rock palette and working URP material are preserved");
        File.WriteAllText(Out + "Geometry" + index + ".txt", "full terrain=" + mesh.bounds.size.ToString("F2") + "\nclear core=50 x 40m (2000m2)\nheight span=" + (high - low).ToString("F4") + "\nmaximum core slope=" + maximumSlope.ToString("F3") + "\nroute maximum step per 0.5m=" + worstStep.ToString("F3") + "\ntriangles=" + mesh.triangles.Length / 3);
        Render(island, access, region.center, index);
        Object.DestroyImmediate(island); Object.DestroyImmediate(access);
        if (audit) InstallFishingMigrationAudit.Verify(); File.WriteAllLines(Out + "Checks.txt", checks);
        Debug.Log("ROCK_ISLAND_EXPANSION_VERIFIED checks=" + checks.Count + " play=false simulation=false savesWritten=false");
    }
    static void Render(GameObject island, GameObject access, Vector3 centre, int index) {
        // Keep only this island's dock in the isolated static preview.
        foreach (Transform t in access.transform) if (!t.name.StartsWith(ArchipelagoSettings.Load().regions[index].title)) t.gameObject.SetActive(false);
        var cam = new GameObject("Static island preview camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.65f, .8f, .83f); cam.nearClipPlane = .1f; cam.farClipPlane = 500;
        // Preview-only water surface shows the actual exposed shoreline. This
        // primitive/material is destroyed and never saved into the game assets.
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.position = new Vector3(centre.x, -4, centre.z); water.transform.localScale = Vector3.one * 40;
        var waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); waterMaterial.SetColor("_BaseColor", new Color(.13f,.48f,.57f)); water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
        var light = new GameObject("Static island preview light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(48, -35, 0); RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
        cam.transform.position = centre + new Vector3(-87, 70, -98); cam.transform.LookAt(centre + Vector3.up); Capture(cam, "island" + index + "-oblique.png");
        cam.orthographic = true; cam.orthographicSize = 70; cam.transform.SetPositionAndRotation(centre + Vector3.up * 150, Quaternion.Euler(90, 0, 0)); Capture(cam, "island" + index + "-top.png");
        cam.orthographic = false; cam.fieldOfView = 75; cam.transform.position = ArchipelagoSettings.Load().regions[index].dock + new Vector3(0, 2.5f, 0); cam.transform.LookAt(centre + Vector3.up * 4.6f); Capture(cam, "island" + index + "-landing-eye.png");
        Object.DestroyImmediate(light.gameObject); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(water); Object.DestroyImmediate(waterMaterial);
    }
    static void Capture(Camera cam, string name) {
        var target = new RenderTexture(1600, 1000, 24); cam.targetTexture = target; cam.Render(); var old = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1600, 1000, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply(); File.WriteAllBytes(Out + name, image.EncodeToPNG());
        RenderTexture.active = old; cam.targetTexture = null; Object.DestroyImmediate(image); Object.DestroyImmediate(target);
    }
}
