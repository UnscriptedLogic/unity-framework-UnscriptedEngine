Shader "UnscriptedEngine/UI/Retainer Warp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured UI", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Curvature ("Curvature", Range(-0.5,0.5)) = 0.08
        _Perspective ("Perspective", Vector) = (0,0,0,0)
        _PerspectiveFOV ("Perspective FOV", Range(1,150)) = 60
        [Toggle] _FitContent ("Fit Content", Float) = 1
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
            #pragma shader_feature_local_fragment _ RETAINER_EDITOR_PREVIEW
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
            float _PerspectiveFOV;
            float _FitContent;
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
                float2 perspective = _Perspective.xy;
                perspective *= min(1, 0.8 / max(0.00001, abs(perspective.x) + abs(perspective.y)));
                perspective *= tan(radians(clamp(_PerspectiveFOV, 1, 150)) * 0.5) / tan(radians(30.0));
                perspective *= min(1, 0.95 / max(0.00001, abs(perspective.x) + abs(perspective.y)));
                if (_FitContent > 0.5)
                {
                    float contraction = max(0, -_Curvature);
                    float bound = sqrt(4 * contraction + sqrt(16 * contraction * contraction + 1));
                    float2 minimum = -bound / (1 + perspective - abs(perspective.yx));
                    float2 maximum = bound / (1 - perspective - abs(perspective.yx));
                    float2 center = (minimum + maximum) * 0.5;
                    float2 extent = (maximum - minimum) * 0.5;
                    float padding = _Curvature == 0 && all(perspective == 0) ? 1 : 1.002;
                    warpedPosition = warpedPosition * max(extent.x, extent.y) * padding + center;
                    perspective /= bound;
                }
                float denominator = 1 + dot(perspective, warpedPosition);
                clip(denominator - 0.000001);
                warpedPosition /= max(0.000001, denominator);
                float radiusSquared = dot(warpedPosition, warpedPosition);
                float radialScale = _Curvature < 0 ? pow(1 - 4 * _Curvature * radiusSquared, -0.25) : 1 + _Curvature * radiusSquared;
                warpedPosition *= radialScale;
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
                #if defined(RETAINER_EDITOR_PREVIEW) && !defined(UNITY_COLORSPACE_GAMMA)
                // IMGUI composites in display space. Runtime UI keeps its normal linear output.
                color.rgb = LinearToGammaSpace(color.rgb / max(color.a, 0.0001)) * color.a;
                #endif
                return color;
            }
            ENDCG
        }
    }
}
