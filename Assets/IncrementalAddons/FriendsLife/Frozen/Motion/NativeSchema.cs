using System;

namespace TomodachiPortData
{
    [Serializable] public class NativeBone
    {
        public string name, rotationMode;
        public int parent, smoothIndex, rigidIndex;
        public uint flags;
        public float[] translation, rotation, scale;
    }
    [Serializable] public class NativeValue
    {
        public string name, text, rawJson;
        public float[] numbers;
        public string[] strings;
    }
    [Serializable] public class NativeTextureBinding
    {
        public string sampler, texture, sourceFile, unityFile;
    }
    [Serializable] public class NativeMaterial
    {
        public string name, shader;
        public NativeTextureBinding[] textures;
        public NativeValue[] parameters, renderInfos, options, samplerAssignments;
    }
    [Serializable] public class NativeShape
    {
        public string name, meshFile;
        public int material, bone, skinCount, vertices, indices;
        public string[] attributes;
    }
    [Serializable] public class NativeModel
    {
        public int schema = 1;
        public string source, sourceSha256, name;
        public NativeBone[] bones;
        public int[] matrixToBone;
        public float[] inverseMatrices;
        public NativeMaterial[] materials;
        public NativeShape[] shapes;
    }
    [Serializable] public class NativeCurve
    {
        public long sourceOffset;
        public int targetOffset, flags, elements;
        public string kind;
        public float scale, offset, tangentScale, tangentOffset, encodedScale, encodedOffset;
        public bool compactHermite;
        public float[] frames, keys, hermite;
    }
    [Serializable] public class NativeBoneTrack
    {
        public string name;
        public uint baseFlags, transformFlags;
        public float[] scale, rotation, translation;
        public NativeCurve[] curves;
    }
    [Serializable] public class NativeSkeletalClip
    {
        public string name, rotationMode, scaleMode;
        public int frames;
        public bool loop;
        public NativeBoneTrack[] tracks;
    }
    [Serializable] public class NativeAnimationBank
    {
        public int schema = 1;
        public string source, sourceSha256;
        public NativeSkeletalClip[] clips;
    }
}
