Shader "SushiAndFish/Painted fish variation" {
    Properties {
        [PerRendererData] _MainTex ("Fish thumbnail", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _VariantColor ("Alternate skin palette",Color)=(1,1,1,1)
        _DisplayMode ("0 color, 1 grayscale, 2 silhouette", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _Color,_ClipRect,_VariantColor;
            float _DisplayMode;
            Output vert(Input input){Output output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.local=input.vertex;output.vertex=UnityObjectToClipPos(input.vertex);output.color=input.color*_Color;output.uv=input.uv;return output;}
            half4 frag(Output input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color=tex2D(_MainTex,input.uv)*input.color;
                float lum=dot(color.rgb,half3(.2126,.7152,.0722));
                color.rgb=lerp(color.rgb,_VariantColor.rgb*(.55+lum*.8),smoothstep(.18,.65,lum)*.88);
                if(_DisplayMode>1.5)color.rgb=0;
                else if(_DisplayMode>.5)color.rgb=.2+dot(color.rgb,half3(.2126,.7152,.0722))*.8;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(input.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
