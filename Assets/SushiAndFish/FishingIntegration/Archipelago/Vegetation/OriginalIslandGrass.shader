Shader "SushiAndFish/Original Island Grass"
{
    Properties
    {
        [MainTexture] _BaseMap("Original GrassPixel16", 2D) = "white" {}
        [MainColor] _BaseColor("Original root color", Color) = (0.2553,0.3333,0.1843,1)
        _TopColor("Original tip color", Color) = (0.6077,0.645,0.3092,1)
        _Cutoff("Grass leaf cutout", Range(0,1)) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 1
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "UniversalMaterialType"="Lit" }
        Cull Off
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TopColor;
            half _Cutoff;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=p.positionCS; output.positionWS=p.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=input.uv; output.fog=ComputeFogFactor(p.positionCS.z);
            return output;
        }
        half4 MaskFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a-_Cutoff);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 leaf=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                clip(leaf.a-_Cutoff);
                half3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(frontFace,1,-1);
                half3 albedo=leaf.rgb*lerp(_BaseColor.rgb,_TopColor.rgb,saturate(input.uv.y));
                Light sunlight=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=0.35h+0.65h*abs(dot(normal,sunlight.direction));
                half3 lighting=SampleSH(normal)+sunlight.color*diffuse*sunlight.distanceAttenuation*lerp(0.35h,1.0h,sunlight.shadowAttenuation);
                return half4(MixFog(albedo*lighting,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            half4 NormalFragment(Varyings input, FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a-_Cutoff);
                return half4(normalize(input.normalWS)*IS_FRONT_VFACE(frontFace,1,-1),0);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
