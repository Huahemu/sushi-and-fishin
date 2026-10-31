using System;

namespace TomodachiLife
{
    // These are raw five-byte CharInfo parameter values, NOT AvatarSelection's
    // RSDB DefaultTrans/DefaultScale slider defaults. Axis and OffsetRotate are
    // explicit per-part accessor inputs; no resident preset is synthesized.
    [Serializable] public sealed class NativeFaceRawParameters
    {
        public string kind, rawSource;
        public int x, y, scale, aspect, rotation, offsetRotate;
        public float[] axis;
        public bool right;
    }
    [Serializable] public sealed class NativeFaceLayoutConstants
    {
        public float spacingStep, eyeBaseY, positionYStep, scaleStep, aspectBase, aspectStep;
        public float eyebrowSpacingBase, eyebrowBaseY, mouthBaseY, pivotXDivisor, pivotYDivisor;
        public float matrixBasisX, matrixBasisY, faceDomain, centerX, angleStepDegrees;
        public float baseHeight, eyeBaseWidth, eyebrowBaseWidth, mouthBaseWidth;
    }
    [Serializable] public sealed class NativeFaceTrigSample
    { public float cosine, sine, deltaCosine, deltaSine; }
    [Serializable] public sealed class NativeFaceSdkArithmetic
    {
        public uint angleIndexHalfRound;
        public float degree180, pi, twoPi, halfPi, inverseTwoPi;
        public float[] sineCoefficients, cosineCoefficients;
        public NativeFaceTrigSample[] sampleTable;
    }
    [Serializable] public sealed class NativeFaceProjection
    { public string key; public float[] matrixColumnMajor; }
    [Serializable] public sealed class NativeFaceAnchoredQuad
    {
        public int index;
        public string name, sourceFunction;
        public bool uvFlipU;
        public float[] positions, uv;
        public int[] triangles;
    }
    [Serializable] public sealed class NativeFaceSourceValue
    {
        public string name, symbol, source, addressHex;
        public long address, sourceOffset;
        public float[] values;
        public uint[] rawWords;
    }
    [Serializable] public sealed class NativeFaceLayoutFixture
    {
        public NativeFaceRawParameters parameters;
        public string projectionKey;
        public float[] native9, matrixColumnMajor;
    }
    [Serializable] public sealed class NativeFaceLayoutData
    {
        public int schema;
        public string name, sourceNumerics, sourceMatrixAudit;
        public string[] sourceFunctions, supported, remaining;
        public bool runtimeParity, expressionsVerified, all22LayersVerified, initialResidentParametersVerified;
        public bool shaderCompositionVerified, fixtureInputsAreResidentDefaults;
        public NativeFaceLayoutConstants constants;
        public NativeFaceSdkArithmetic sdk;
        public NativeFaceProjection[] projections;
        public NativeFaceAnchoredQuad[] quads;
        public NativeFaceSourceValue[] sourceValues;
        public NativeFaceLayoutFixture[] fixtures;
    }
    [Serializable] public sealed class NativeFaceLayoutResult
    {
        public string kind, rawSource, projectionKey;
        public int quadIndex;
        public int[] resolvedRaw;
        // native9 = cx,cy,width,height,degrees,1,0,pivotX,pivotY.
        public float[] native9, matrixColumnMajor, localMatrixColumnMajor;
        public float[] quadLocalPositions, quadFacePositions, quadClipPositions, uv;
        public int[] triangles;
        public bool uvFlipU;
        public bool runtimeParity = false, expressionsVerified = false, all22LayersVerified = false;
        public bool initialResidentParametersVerified = false, shaderCompositionVerified = false;
    }

    public sealed class NativeFaceLayout
    {
        public NativeFaceLayoutData Data { get; }
        private readonly NativeFaceLayoutConstants c;
        private readonly NativeFaceSdkArithmetic sdk;

        public NativeFaceLayout(NativeFaceLayoutData evidence)
        {
            if (evidence == null || evidence.schema != 1 || evidence.constants == null || evidence.sdk == null ||
                evidence.sdk.sampleTable == null || evidence.sdk.sampleTable.Length != 256 ||
                evidence.sdk.sineCoefficients == null || evidence.sdk.sineCoefficients.Length != 5 ||
                evidence.sdk.cosineCoefficients == null || evidence.sdk.cosineCoefficients.Length != 5 ||
                evidence.quads == null || evidence.quads.Length != 3 || evidence.projections == null || evidence.projections.Length != 2)
                throw new ArgumentException("Expected recovered neutral face-layout schema 1, 256 SDK table rows, 5+5 coefficients, 3 quads and 2 projections.");
            Data = evidence; c = evidence.constants; sdk = evidence.sdk;
        }

#if UNITY_5_3_OR_NEWER
        public static NativeFaceLayout Load()
        {
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Avatar/face-layout");
            if (text == null) throw new InvalidOperationException("Recovered native face-layout data has not been staged.");
            return new NativeFaceLayout(UnityEngine.JsonUtility.FromJson<NativeFaceLayoutData>(text.text));
        }
#endif

        public bool TryEvaluateNeutral(NativeFaceRawParameters raw, string projectionKey, out NativeFaceLayoutResult result, out string error)
        {
            result = null; error = null;
            var projection = Array.Find(Data.projections, p => p.key == projectionKey);
            if (projection == null) { error = "Unknown authored face projection: " + projectionKey; return false; }
            if (!TryEvaluateNeutral(raw, projection.matrixColumnMajor, out result, out error)) return false;
            result.projectionKey = projectionKey; return true;
        }

        public bool TryEvaluateNeutral(NativeFaceRawParameters raw, float[] projection, out NativeFaceLayoutResult result, out string error)
        {
            result = null; error = null;
            if (raw == null || (raw.kind != "Eye" && raw.kind != "Eyebrow" && raw.kind != "Mouth")) { error = "Only neutral Eye/Eyebrow/Mouth raw parameters are supported."; return false; }
            if (string.IsNullOrWhiteSpace(raw.rawSource)) { error = "Explicit raw CharInfo/fixture provenance is required; RSDB defaults are not resident inputs."; return false; }
            if (raw.kind == "Mouth" && raw.right) { error = "The neutral mouth uses the centered quad, not a right-eye/brow layer."; return false; }
            if (!ByteValue(raw.x) || !ByteValue(raw.y) || !ByteValue(raw.rotation) || !ByteValue(raw.scale) || !ByteValue(raw.aspect)) { error = "Raw CharInfo parameters must be bytes in 0..255 before native clamping."; return false; }
            if (!Values(raw.axis, 2) || !Values(projection, 16)) { error = "Supply a finite 2f authored axis and a finite 16f column-major projection."; return false; }
            int x = raw.x, y = raw.y, rotation = raw.rotation, scale = raw.scale, aspect = Math.Min(raw.aspect, 6);
            float widthBase, baseY;
            if (raw.kind == "Eye")
            { x = Math.Min(x, 12); y = Math.Min(y, 18); rotation = Math.Min(rotation, 7); scale = Math.Min(scale, 7); widthBase = c.eyeBaseWidth; baseY = c.eyeBaseY; }
            else if (raw.kind == "Eyebrow")
            { x = Math.Min(x, 14); y = Math.Max(3, Math.Min(y, 18)); rotation = Math.Min(rotation, 11); scale = Math.Min(scale, 8); widthBase = c.eyebrowBaseWidth; baseY = c.eyebrowBaseY; }
            else
            { x = 0; y = Math.Min(y, 18); rotation = Math.Min(rotation, 31); scale = Math.Min(scale, 8); widthBase = c.mouthBaseWidth; baseY = c.mouthBaseY; }
            int rotateOffset = raw.offsetRotate < 0 ? 0 : unchecked(32 - raw.offsetRotate);
            float oldAngle = Mul(unchecked(raw.rotation + rotateOffset) & 31, c.angleStepDegrees);
            float finalAngle = Mul(unchecked(rotation + rotateOffset) & 31, c.angleStepDegrees);
            if (raw.right) { oldAngle = -oldAngle; finalAngle = -finalAngle; }
            float scaled = Add(1, Mul(c.scaleStep, scale));
            float width = Mul(scaled, widthBase);
            float height = Mul(Mul(scaled, c.baseHeight), Add(c.aspectBase, Mul(c.aspectStep, aspect)));
            float spacing = Mul(c.spacingStep, x);
            if (raw.kind == "Eyebrow") spacing = Add(c.eyebrowSpacingBase, spacing);
            float cx = raw.kind == "Mouth" ? c.centerX : raw.right ? Add(c.centerX, spacing) : Sub(c.centerX, spacing);
            float cy = Add(baseY, Mul(c.positionYStep, y));
            float ax = Mul(width, raw.axis[0]);
            if (!raw.right && raw.kind != "Mouth") ax = -ax;
            float ay = Mul(height, raw.axis[1]), px = cx, py = cy;
            if (ax != 0 || ay != 0)
            {
                // Table order is cos,sin,deltaCos,deltaSin. The native pivot's
                // terms are copied in that order; conventional sin/cos naming
                // must not silently swap these two source table columns.
                SampleTable(oldAngle, out float oldCos, out float oldSin);
                px = Add(cx, Div(Add(Mul(ay, oldSin), Mul(oldCos, ax)), c.pivotXDivisor));
                py = Sub(cy, Div(Sub(Mul(ay, oldCos), Mul(oldSin, ax)), c.pivotYDivisor));
                SampleTable(finalAngle, out float finalCos, out float finalSin);
                cx = Sub(px, Div(Add(Mul(ay, finalSin), Mul(finalCos, ax)), c.pivotXDivisor));
                cy = Add(py, Div(Sub(Mul(ay, finalCos), Mul(finalSin, ax)), c.pivotYDivisor));
            }
            var native9 = new[] { cx, cy, width, height, finalAngle, 1f, 0f, px, py };
            if (!TryBuildNeutralMatrix(native9, projection, out var matrix, out var local, out error)) return false;
            int quadIndex = raw.kind == "Mouth" ? 0 : raw.right ? 2 : 1;
            var quad = Array.Find(Data.quads, q => q.index == quadIndex);
            if (quad == null || !Values(quad.positions, 12) || !Values(quad.uv, 8) || quad.triangles == null || quad.triangles.Length != 6)
            { error = "Authored quad source contract is missing."; return false; }
            result = new NativeFaceLayoutResult { kind = raw.kind, rawSource = raw.rawSource, projectionKey = "CallerProjection", quadIndex = quadIndex,
                resolvedRaw = new[] { x, y, rotation, scale, aspect }, native9 = native9, matrixColumnMajor = matrix, localMatrixColumnMajor = local,
                quadLocalPositions = (float[])quad.positions.Clone(), uv = (float[])quad.uv.Clone(), triangles = (int[])quad.triangles.Clone(), uvFlipU = quad.uvFlipU,
                quadFacePositions = TransformQuad(local, quad.positions), quadClipPositions = TransformQuad(matrix, quad.positions) };
            return true;
        }

        public bool TryBuildNeutralMatrix(float[] native9, float[] projection, out float[] matrix, out float[] local, out string error)
            => TryBuildDescriptorMatrix(native9, projection, out matrix, out local, out error);

        //00171910 also carries the parent eye's aspect and angle in slots5/6.
        // Order: P*T*Basis*R(parent)*ScaleY(parentAspect)*R(local)*Scale(size).
        public bool TryBuildDescriptorMatrix(float[] native9, float[] projection, out float[] matrix, out float[] local, out string error)
        {
            matrix = local = null; error = null;
            if (!Values(native9, 9) || !Values(projection, 16))
            { error = "Finite native 9f descriptor and 16f projection required."; return false; }
            Polynomial(native9[4], out float sine, out float cosine);
            float width = native9[2], height = native9[3];
            local = new[] { Mul(c.matrixBasisX, Mul(cosine, width)), Mul(c.matrixBasisY, Mul(sine, width)), 0, 0,
                            Mul(c.matrixBasisX, Mul(-sine, height)), Mul(c.matrixBasisY, Mul(cosine, height)), 0, 0,
                            0f, 0f, 1f, 0f, native9[0], native9[1], 0f, 1f };
            if(native9[5]!=1 || native9[6]!=0)
            {
                Polynomial(native9[6],out float parentSin,out float parentCos);
                float x=Mul(cosine,width),y=Mul(native9[5],Mul(sine,width));
                float hx=Mul(-sine,height),hy=Mul(native9[5],Mul(cosine,height));
                local[0]=Mul(c.matrixBasisX,Fma(-parentSin,y,Mul(parentCos,x)));
                local[1]=Mul(c.matrixBasisY,Fma(parentCos,y,Mul(parentSin,x)));
                local[4]=Mul(c.matrixBasisX,Fma(-parentSin,hy,Mul(parentCos,hx)));
                local[5]=Mul(c.matrixBasisY,Fma(parentCos,hy,Mul(parentSin,hx)));
            }
            matrix = new float[16];
            for (int column = 0; column < 4; column++) for (int row = 0; row < 4; row++)
            {
                float value = Mul(projection[row], local[column * 4]);
                value = Fma(projection[row + 4], local[column * 4 + 1], value);
                value = Fma(projection[row + 8], local[column * 4 + 2], value);
                matrix[column * 4 + row] = column == 3 ? Add(projection[row + 12], value) : Fma(projection[row + 12], local[column * 4 + 3], value);
            }
            if (!Values(matrix, 16)) { error = "Native neutral matrix produced nonfinite values."; matrix = local = null; return false; }
            return true;
        }

        public void SampleTable(float degrees, out float cosine, out float sine)
        {
            long angleIndex = (long)Mul(degrees, Div(sdk.angleIndexHalfRound, sdk.degree180));
            uint wrapped = unchecked((uint)angleIndex);
            var row = sdk.sampleTable[(wrapped >> 24) & 255];
            float fraction = Mul(wrapped & 0xffffff, 5.9604644775390625e-8f);
            cosine = Add(row.cosine, Mul(fraction, row.deltaCosine));
            sine = Add(row.sine, Mul(fraction, row.deltaSine));
        }

        public void Polynomial(float degrees, out float sine, out float cosine)
        {
            float radians = Mul(degrees, Div(sdk.pi, sdk.degree180));
            float cycles = Mul(radians, sdk.inverseTwoPi);
            float rounded = (float)Math.Round((double)cycles, MidpointRounding.ToEven);
            if (rounded == 0 && Negative(cycles)) rounded = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
            float angle = Fma(-rounded, sdk.twoPi, radians);
            bool flip = angle > sdk.halfPi;
            if (flip) angle = Sub(sdk.pi, angle);
            bool flip2 = angle < -sdk.halfPi;
            if (flip2) angle = Sub(-sdk.pi, angle);
            float square = Mul(angle, angle);
            var s = sdk.sineCoefficients; var co = sdk.cosineCoefficients;
            float p = Fma(-square, s[0], s[1]); p = Fma(square, p, -s[2]); p = Fma(square, p, s[3]); p = Fma(square, p, -s[4]);
            sine = Mul(angle, Fma(square, p, 1));
            p = Fma(-square, co[0], co[1]); p = Fma(square, p, -co[2]); p = Fma(square, p, co[3]); p = Fma(square, p, -co[4]);
            cosine = Fma(square, p, 1); if (flip || flip2) cosine = -cosine;
        }

        private static float[] TransformQuad(float[] matrix, float[] positions)
        {
            var result = new float[16];
            for (int vertex = 0; vertex < 4; vertex++) for (int row = 0; row < 4; row++)
            {
                float value = Mul(matrix[row], positions[vertex * 3]);
                value = Fma(matrix[row + 4], positions[vertex * 3 + 1], value);
                value = Fma(matrix[row + 8], positions[vertex * 3 + 2], value);
                result[vertex * 4 + row] = Add(value, matrix[row + 12]);
            }
            return result;
        }
        // Double product+sum rounded once matches native FMA on the bounded
        // neutral fixture/angle/projection probes. It is not a full-domain fmaf proof.
        private static float Fma(float a, float b, float addend) => (float)((double)a * b + addend);
        private static float Mul(float a, float b) => (float)((double)a * b);
        private static float Div(float a, float b) => (float)((double)a / b);
        private static float Add(float a, float b) => (float)((double)a + b);
        private static float Sub(float a, float b) => (float)((double)a - b);
        private static bool Negative(float value) => BitConverter.SingleToInt32Bits(value) < 0;
        private static bool ByteValue(int value) => value >= 0 && value <= 255;
        private static bool Values(float[] values, int count)
        {
            if (values == null || values.Length != count) return false;
            foreach (float value in values) if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return true;
        }
    }
}
