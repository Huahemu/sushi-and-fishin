using System;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
#endif

namespace TomodachiLife
{
    [Serializable] public sealed class NativeMiiMaskParameters
    {
        public int mode;
        public float neighborUvStep, alphaFactor = 1;
        public float[] color0, color1, color2;
        public string source;
        public bool bindingVerified, colorsVerified;
    }
    [Serializable] public sealed class NativeFaceLayerInput
    {
        public string id, textureResource;
        public NativeFaceRawParameters raw;
        public NativeMiiMaskParameters modulate;
        public bool rawParametersVerified, gameInitialPresetVerified;
        // Optional migration preview descriptor. These additional editable
        // details remain inspection-only until their native relative layout is ported.
        public float[] inspectionDescriptor;
        public int inspectionQuadIndex;
    }
    [Serializable] public sealed class NativeFaceCompositionSettings
    {
        public int resolution = 512;
        public string projectionKey = "NativeFlippedY";
        public bool inspectionOnly = true;
        public bool framebufferBlendVerified, surfaceUvVerified;
        public string expressionName;
        public string framebufferBlendSource = "Inspection: premultiplied source-over accumulation; original NVN state not verified";
        public string surfaceUvSource = "Authored mt_Mask mesh UV unchanged; final native render-target orientation not visually verified";
    }
    [Serializable] public sealed class NativeFaceLayerPlan
    {
        public string id, textureResource;
        public NativeFaceRawParameters raw;
        public NativeMiiMaskParameters modulate;
        public NativeFaceLayoutResult layout;
        public bool rawParametersVerified, gameInitialPresetVerified;
    }
    [Serializable] public sealed class NativeFaceCompositionReport
    {
        public string validation = "Recovered neutral layout and MiiMask arithmetic; Unity rendering and original-game parity remain separate";
        public string projectionKey, framebufferBlendSource, surfaceUvSource;
        public int resolution, layers;
        public bool inspectionOnly, composed, attachedToAuthoredSurface;
        public bool rawParametersVerified, gameInitialPresetVerified, partToModeBindingVerified, colorsVerified;
        public bool framebufferBlendVerified, surfaceUvVerified;
        public bool runtimeParity = false, all22LayersVerified = false, expressionsVerified = false;
        public string[] layerSources;
    }
    // Arithmetic shared by offline checks and the Unity compositor. Inputs and
    // output are straight RGBA. Framebuffer accumulation is a separate contract.
    public static class NativeMiiMask
    {
        public static bool Validate(NativeMiiMaskParameters p, out string error)
        {
            error = null;
            if (p == null || !Vector(p.color0) || !Vector(p.color1) || !Vector(p.color2) ||
                !Finite(p.neighborUvStep) || !Finite(p.alphaFactor) || p.neighborUvStep < 0)
            { error = "MiiMask requires three finite RGBA vectors, finite alphaFactor and nonnegative neighborUvStep."; return false; }
            if (string.IsNullOrWhiteSpace(p.source))
            { error = "Explicit MiiMask CPU/color binding provenance is required."; return false; }
            return true;
        }

        public static float[] Evaluate(NativeMiiMaskParameters p, float[] texel, float[] neighborAlphaRLUD, out bool discardPixel)
        {
            if (!Validate(p, out string error)) throw new ArgumentException(error);
            if (!Vector(texel) || !Vector(neighborAlphaRLUD)) throw new ArgumentException("Finite texel and right/left/up/down neighbor alpha vectors required.");
            var value = new float[4]; float r = texel[0], g = texel[1], b = texel[2], a = texel[3];
            switch (p.mode)
            {
                case 0: Array.Copy(p.color0, value, 4); break;
                case 1: Array.Copy(texel, value, 4); break;
                case 2:
                    for (int i = 0; i < 3; i++) value[i] = r * p.color0[i] + g * p.color1[i] + b * p.color2[i];
                    value[3] = a; break;
                case 3:
                    Array.Copy(p.color0, value, 3); value[3] = r; break;
                case 4:
                    for (int i = 0; i < 3; i++) value[i] = r * p.color0[i];
                    value[3] = 1; break;
                case 5:
                    for (int i = 0; i < 3; i++) value[i] = r * p.color0[i];
                    // Preserve exp2(log2(abs(g))*exponent), including NaN at 0^0.
                    value[3] = (float)Math.Pow(2, Math.Log(Math.Abs(g), 2) * p.color1[1]); break;
                case 6:
                    for (int i = 0; i < 4; i++) { float x = r * p.color0[i]; value[i] = (float)((double)(p.color1[i] - x) * g + x); }
                    break;
                case 7:
                    for (int i = 0; i < 3; i++) value[i] = g * p.color1[i] + b * p.color2[i];
                    value[3] = Saturate(a - r); break;
                case 8:
                    for (int i = 0; i < 3; i++) value[i] = b * p.color2[i];
                    value[3] = Saturate(a - r - g); break;
                case 9:
                    Array.Copy(texel, value, 4);
                    bool horizontal = neighborAlphaRLUD[0] > a || neighborAlphaRLUD[1] > a;
                    bool vertical = neighborAlphaRLUD[2] > a || neighborAlphaRLUD[3] > a;
                    if (a > 0.200000003f && !(horizontal && vertical)) value[3] *= p.alphaFactor;
                    break;
                default: for (int i = 0; i < 4; i++) value[i] = 1; break;
            }
            discardPixel = p.mode != 9 && value[3] == 0;
            return value;
        }
        public static float[] SourceOverPremultiplied(float[] straightSource, float[] premultipliedDestination)
        {
            if (!Vector(straightSource) || !Vector(premultipliedDestination)) throw new ArgumentException("Finite RGBA vectors required.");
            float a = straightSource[3], keep = 1 - a;
            return new[] { straightSource[0] * a + premultipliedDestination[0] * keep,
                straightSource[1] * a + premultipliedDestination[1] * keep,
                straightSource[2] * a + premultipliedDestination[2] * keep,
                a + premultipliedDestination[3] * keep };
        }
        private static float Saturate(float value) => Math.Max(0, Math.Min(1, value));
        private static bool Vector(float[] value)
        { if (value == null || value.Length != 4) return false; foreach (float x in value) if (!Finite(x)) return false; return true; }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    public sealed class NativeFaceCompositor : IDisposable
    {
        public NativeFaceCompositionReport LastReport { get; private set; }
        public NativeFaceLayerPlan[] Plan { get; private set; }
        private readonly NativeFaceLayout layout;
        public NativeFaceCompositor(NativeFaceLayout evidence) => layout = evidence ?? throw new ArgumentNullException(nameof(evidence));

        public bool TryPlan(NativeFaceLayerInput[] layers, NativeFaceCompositionSettings settings, out string error)
        {
            error = null;
            if (layers == null || settings == null || settings.resolution < 16 || settings.resolution > 4096)
            { error = "Supply explicit neutral face layers and a 16..4096 square render target."; return false; }
            if (string.IsNullOrWhiteSpace(settings.framebufferBlendSource) || string.IsNullOrWhiteSpace(settings.surfaceUvSource))
            { error = "Composition blend and surface UV provenance must be explicit."; return false; }
            var result = new NativeFaceLayerPlan[layers.Length]; var sources = new string[layers.Length];
            bool rawVerified = true, initialVerified = true, bindingVerified = true, colorsVerified = true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layers.Length; i++)
            {
                var input = layers[i];
                if (input == null || string.IsNullOrWhiteSpace(input.id) || !ids.Add(input.id) || string.IsNullOrWhiteSpace(input.textureResource))
                { error = "Each layer requires a unique ID and an explicit texture resource."; return false; }
                if (!NativeMiiMask.Validate(input.modulate, out error)) return false;
                NativeFaceLayoutResult evaluated;
                if (input.inspectionDescriptor != null)
                {
                    if (!settings.inspectionOnly || input.raw == null || string.IsNullOrWhiteSpace(input.raw.rawSource))
                    { error = "Editable detail descriptors require explicit inspection provenance."; return false; }
                    var projection = Array.Find(layout.Data.projections, p => p.key == settings.projectionKey);
                    var quad = Array.Find(layout.Data.quads, q => q.index == input.inspectionQuadIndex);
                    if (projection == null || quad == null) { error = "Editable detail projection or quad missing."; return false; }
                    if (!layout.TryBuildNeutralMatrix(input.inspectionDescriptor, projection.matrixColumnMajor, out var matrix, out var local, out error)) return false;
                    evaluated = new NativeFaceLayoutResult {kind=input.raw.kind, rawSource=input.raw.rawSource, matrixColumnMajor=matrix,
                        localMatrixColumnMajor=local, native9=input.inspectionDescriptor, quadIndex=quad.index,
                        quadLocalPositions=quad.positions, uv=quad.uv, triangles=quad.triangles, uvFlipU=quad.uvFlipU};
                }
                else if (!layout.TryEvaluateNeutral(input.raw, settings.projectionKey, out evaluated, out error)) return false;
                rawVerified &= input.rawParametersVerified;
                initialVerified &= input.gameInitialPresetVerified;
                bindingVerified &= input.modulate.bindingVerified; colorsVerified &= input.modulate.colorsVerified;
                sources[i] = input.id + ": " + input.raw.rawSource + " | " + input.modulate.source;
                result[i] = new NativeFaceLayerPlan { id = input.id, textureResource = input.textureResource,
                    raw = input.raw, modulate = input.modulate, layout = evaluated, rawParametersVerified = input.rawParametersVerified, gameInitialPresetVerified = input.gameInitialPresetVerified };
            }
            if (!settings.inspectionOnly && !(rawVerified && bindingVerified && colorsVerified && settings.framebufferBlendVerified && settings.surfaceUvVerified))
            { error = "Unverified raw parameters, CPU/color bindings, framebuffer blend or surface UV require inspectionOnly=true."; return false; }
            Plan = result;
            LastReport = new NativeFaceCompositionReport { inspectionOnly = settings.inspectionOnly,
                projectionKey = settings.projectionKey, resolution = settings.resolution, layers = layers.Length,
                rawParametersVerified = rawVerified, gameInitialPresetVerified = initialVerified, partToModeBindingVerified = bindingVerified, colorsVerified = colorsVerified,
                framebufferBlendVerified = settings.framebufferBlendVerified, surfaceUvVerified = settings.surfaceUvVerified,
                framebufferBlendSource = settings.framebufferBlendSource, surfaceUvSource = settings.surfaceUvSource, layerSources = sources };
            return true;
        }

#if UNITY_5_3_OR_NEWER
        public RenderTexture Composite { get; private set; }
        private Material partMaterial;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Attachment> attachments = new List<Attachment>();
        private sealed class Attachment { public Renderer renderer; public int index; public bool originallyEnabled; public Material original, installed; }

        public RenderTexture Compose(NativeFaceLayerInput[] layers, NativeFaceCompositionSettings settings)
        {
            if (!TryPlan(layers, settings, out string error)) throw new ArgumentException(error);
            // Resolve all assets before changing a currently displayed composition.
            var textures = new Texture[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                textures[i] = Resources.Load<Texture>(layers[i].textureResource);
                if (textures[i] == null) throw new InvalidOperationException("Native face texture not imported: " + layers[i].textureResource);
            }
            if (partMaterial == null)
            {
                var shader = Shader.Find("Tomodachi Life/Original face part composite");
                if (shader == null) throw new InvalidOperationException("Recovered MiiMask compositor shader missing.");
                partMaterial = new Material(shader) { name = "Native MiiMask modes (inspection blend)" };
            }
            RenderTexture target = new RenderTexture(settings.resolution, settings.resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { name = "Native neutral face composite" + (settings.inspectionOnly ? " [Inspection]" : ""), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            target.Create();
            var commands = new CommandBuffer { name = "Recovered neutral face layers" };
            var newMeshes = new List<Mesh>();
            try
            {
                commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.clear);
                commands.SetViewport(new Rect(0, 0, settings.resolution, settings.resolution));
                for (int i = 0; i < Plan.Length; i++)
                {
                    var plan = Plan[i]; var data = plan.layout; var p = plan.modulate;
                    var mesh = new Mesh { name = plan.id + " original anchored quad" };
                    var vertices = new Vector3[4]; var uv = new Vector2[4];
                    for (int vertex = 0; vertex < 4; vertex++)
                    {
                        vertices[vertex] = new Vector3(data.quadLocalPositions[vertex * 3], data.quadLocalPositions[vertex * 3 + 1], data.quadLocalPositions[vertex * 3 + 2]);
                        uv[vertex] = new Vector2(data.uv[vertex * 2], data.uv[vertex * 2 + 1]);
                    }
                    mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = data.triangles; newMeshes.Add(mesh);
                    var matrix = new Matrix4x4();
                    for (int column = 0; column < 4; column++) for (int row = 0; row < 4; row++) matrix[row, column] = data.matrixColumnMajor[column * 4 + row];
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_MainTex", textures[i]); properties.SetMatrix("_NativeFaceMatrix", matrix);
                    properties.SetInt("_Mode", p.mode); properties.SetFloat("_NeighborUvStep", p.neighborUvStep); properties.SetFloat("_AlphaFactor", p.alphaFactor);
                    properties.SetVector("_Color0", ToVector(p.color0)); properties.SetVector("_Color1", ToVector(p.color1)); properties.SetVector("_Color2", ToVector(p.color2));
                    commands.DrawMesh(mesh, Matrix4x4.identity, partMaterial, 0, 0, properties);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            catch { target.Release(); DestroyOwned(target); foreach (var mesh in newMeshes) DestroyOwned(mesh); throw; }
            finally { commands.Release(); }
            foreach (var mesh in ownedMeshes) DestroyOwned(mesh); ownedMeshes.Clear(); ownedMeshes.AddRange(newMeshes);
            var old = Composite; Composite = target;
            foreach (var attachment in attachments) if (attachment.installed != null) attachment.installed.SetTexture("_MainTex", target);
            if (old != null) { old.Release(); DestroyOwned(old); }
            LastReport.composed = true; LastReport.attachedToAuthoredSurface = attachments.Count > 0;
            return target;
        }

        // The caller passes the original mt_Mask renderer/material slot. No
        // replacement geometry, projection mesh or empirical offset is added.
        public void AttachToMask(Renderer surface, int materialIndex = 0)
        {
            if (surface == null || Composite == null) throw new InvalidOperationException("Compose first, then pass the authored native mask renderer.");
            var materials = surface.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length) throw new ArgumentOutOfRangeException(nameof(materialIndex));
            foreach (var existing in attachments) if (existing.renderer == surface && existing.index == materialIndex) return;
            var shader = Shader.Find("Tomodachi Life/Original face overlay");
            if (shader == null) throw new InvalidOperationException("Curved face overlay shader missing.");
            var installed = new Material(shader) { name = "Native authored mask composite" + (LastReport.inspectionOnly ? " [Inspection]" : "") };
            installed.SetTexture("_MainTex", Composite);
            attachments.Add(new Attachment { renderer = surface, index = materialIndex, originallyEnabled = surface.enabled, original = materials[materialIndex], installed = installed });
            materials[materialIndex] = installed; surface.sharedMaterials = materials; surface.enabled = true;
            LastReport.attachedToAuthoredSurface = true;
        }
        public void Detach()
        {
            foreach (var attachment in attachments)
            {
                if (attachment.renderer != null)
                {
                    var materials = attachment.renderer.sharedMaterials;
                    if (attachment.index < materials.Length && materials[attachment.index] == attachment.installed)
                    { materials[attachment.index] = attachment.original; attachment.renderer.sharedMaterials = materials; attachment.renderer.enabled = attachment.originallyEnabled; }
                }
                DestroyOwned(attachment.installed);
            }
            attachments.Clear(); if (LastReport != null) LastReport.attachedToAuthoredSurface = false;
        }
        private static Vector4 ToVector(float[] value) => new Vector4(value[0], value[1], value[2], value[3]);
        private static void DestroyOwned(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        // Fixture-derived test parameters are explicit and reproducible. Catalog
        // DefaultTrans/DefaultScale never enter the raw CharInfo input path.
        public static NativeFaceLayerInput[] CreateInspectionNeutral(ResidentAppearance appearance, AvatarCatalog catalog, NativeFaceLayout evidence)
        {
            var result = new List<NativeFaceLayerInput>();
            // Neutral selected layer IDs are 2 (mouth), 4/5 (brows), 16/17
            // (eyes) in the original 22-layer descriptor builder 0016facc.
            foreach (string kind in new[] { "Mouth", "Eyebrow", "Eye" })
            {
                var selection = appearance.Selection(kind); if (selection == null) continue;
                var part = catalog.Part(selection.part);
                if (string.IsNullOrEmpty(part.texture)) continue; // Authored Nothing selection.
                var fixture = Array.Find(evidence.Data.fixtures, x => x.parameters.kind == kind && !x.parameters.right && x.parameters.scale == 4 && x.parameters.aspect == 3)
                    ?? Array.Find(evidence.Data.fixtures, x => x.parameters.kind == kind && !x.parameters.right);
                if (fixture == null) throw new InvalidOperationException("Inspection fixture missing for " + kind);
                int sides = kind == "Mouth" ? 1 : 2;
                for (int side = 0; side < sides; side++)
                {
                    var test = fixture.parameters;
                    var raw = new NativeFaceRawParameters { kind = kind, x = test.x, y = test.y, scale = test.scale, aspect = test.aspect, rotation = test.rotation,
                        axis = (float[])part.expressionAxis.Clone(), offsetRotate = part.NativeRotationOffset, right = side == 1,
                        rawSource = "Inspection fixture raw bytes for " + part.id + "; authored part AxisForExpression/OffsetRotate from " + part.sourceRow + "; not a resident preset" };
                    // The full SDK eye/mouth palette accessor binding is kept
                    // unverified here; explicit test endpoints show mask channels.
                    // Native builder uses GammaType=1 (SDK sRGB endpoints).
                    // RT write/view transfer remains explicitly unverified.
                    Color selected = catalog.Color("common", selection.color, false);
                    Color green = kind == "Mouth" ? catalog.Color("upperLip", selection.color, false) : Color.white;
                    Color red = kind == "Eye" ? catalog.Color("common", selection.subColor, false) : selected;
                    var color = new[] { selected.r, selected.g, selected.b, 1f };
                    var p = new NativeMiiMaskParameters { mode = part.useTextureColor ? 1 : kind == "Eyebrow" ? 3 : 2,
                        color0 = new[] {red.r,red.g,red.b,1f}, color1 = new[] {green.r,green.g,green.b,1f}, color2 = kind == "Eye" ? color : new[] { 1f, 1f, 1f, 1f },
                        source = "Inspection explicit color indices and channel flags; native SDK common/UpperLip/EyeWhite/Teeth accessor mapping, 0016facc.c and 0016f980 upload; original resident flags not captured",
                        bindingVerified = false, colorsVerified = false };
                    result.Add(new NativeFaceLayerInput { id = part.id + (sides == 1 ? "" : side == 0 ? " Left" : " Right"),
                        raw = raw, textureResource = part.texture, modulate = p, rawParametersVerified = false });
                }
            }
            return result.ToArray();
        }

        // This uses actual source template bytes; the caller still chooses the
        // eye/mouth red-channel branches explicitly because the current game's
        // creation preset and runtime feature flags have not been captured.
        public static NativeFaceLayerInput[] CreateSdkTemplateNeutral(NativeSdkMiiPreset preset, AvatarCatalog catalog,
            bool includeEyeRedChannel, bool includeMouthRedChannel, int eyeRedCommonColor, string channelBranchSource)
        {
            if (preset == null || !preset.sourceVerified || string.IsNullOrWhiteSpace(channelBranchSource))
                throw new ArgumentException("Source-verified SDK template and explicit runtime channel-branch provenance required.");
            var result = new List<NativeFaceLayerInput>();
            foreach (string kind in new[] {"Mouth","Eyebrow","Eye"})
            {
                var source = Array.Find(preset.neutralFace, x => x.kind == kind);
                if (source == null || !source.sourceVerified) throw new InvalidOperationException("SDK neutral source missing: " + kind);
                var part = catalog.Part(source.matchingCatalogPart); var raw = source.gameConvertedRaw;
                Color selected = catalog.Color("common", source.sdkColor, false);
                Color red = kind == "Eye" ? catalog.Color("common", eyeRedCommonColor, false) : selected;
                Color green = kind == "Mouth" ? catalog.Color("upperLip", source.sdkColor, false) : Color.white;
                Color blue = kind == "Eye" ? selected : Color.white;
                int mode = part.useTextureColor ? 1 : kind == "Eyebrow" ? 3 : kind == "Eye" ? (includeEyeRedChannel ? 2 : 7) : (includeMouthRedChannel ? 2 : 8);
                int sides = kind == "Mouth" ? 1 : 2;
                for(int side=0;side<sides;side++)
                {
                    var copy = new NativeFaceRawParameters {kind=raw.kind,x=raw.x,y=raw.y,scale=raw.scale,aspect=raw.aspect,rotation=raw.rotation,
                        axis=(float[])raw.axis.Clone(),offsetRotate=part.NativeRotationOffset,right=side==1,rawSource=raw.rawSource+"; category rotation basis from00173d58/029fe308"};
                    result.Add(new NativeFaceLayerInput { id=part.id+(sides==1?"":side==0?" Left":" Right"),textureResource=part.texture,raw=copy,rawParametersVerified=true,
                        modulate=new NativeMiiMaskParameters {mode=mode,color0=new[] {red.r,red.g,red.b,1f},color1=new[] {green.r,green.g,green.b,1f},color2=new[] {blue.r,blue.g,blue.b,1f},
                            bindingVerified=true,colorsVerified=true,source="SDK template "+preset.id+"; native descriptor/upload and SDK GammaType1 palettes; explicit branch selection: "+channelBranchSource} });
                }
            }
            return result.ToArray();
        }

        public static NativeFaceLayerInput[] WithEditableDetails(NativeFaceLayerInput[] neutral, ResidentAppearance appearance, AvatarCatalog catalog,NativeFaceCompositionSettings settings=null)
        {
            if(settings!=null&&!string.IsNullOrEmpty(settings.expressionName))neutral=NativeFaceLayerBuilder.ApplyExpression(neutral,catalog,settings.expressionName);
            return NativeFaceLayerBuilder.AppendOptional(neutral, appearance, catalog);
        }
#endif

        public void Dispose()
        {
#if UNITY_5_3_OR_NEWER
            Detach();
            foreach (var mesh in ownedMeshes) DestroyOwned(mesh); ownedMeshes.Clear();
            DestroyOwned(partMaterial); partMaterial = null;
            if (Composite != null) { Composite.Release(); DestroyOwned(Composite); Composite = null; }
#endif
            Plan = null;
        }
    }
}
