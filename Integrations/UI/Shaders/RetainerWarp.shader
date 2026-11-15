Shader "UnscriptedEngine/UI/Retainer Warp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured UI", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Curvature ("Curvature", Range(0,0.5)) = 0.08
        _Perspective ("Perspective", Vector) = (0,0,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // Render textures contain already composited RGB. Do not multiply source alpha twice.
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _Color;
            float4 _ClipRect;
            float _Curvature;
            float4 _Perspective;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.local = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float2 warpedPosition = input.uv * 2 - 1;
                warpedPosition /= max(0.1, 1 + dot(_Perspective.xy, warpedPosition));
                warpedPosition *= 1 + _Curvature * dot(warpedPosition, warpedPosition);
                float2 uv = (warpedPosition + 1) * 0.5;
                clip(min(uv.x, uv.y));
                clip(1 - max(uv.x, uv.y));
                float4 color = tex2D(_MainTex, uv);
                color.rgb *= input.color.rgb * input.color.a;
                color.a *= input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(input.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
