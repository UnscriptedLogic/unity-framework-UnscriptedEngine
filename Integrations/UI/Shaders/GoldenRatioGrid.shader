Shader "UnscriptedEngine/UI/GoldenRatioGrid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GridColor ("Framing Guide Color", Color) = (1,0.9,0,0.7)
        _GoldenColor ("Golden Section Color", Color) = (1,0.15,0.1,0.7)
        _LineWidth ("Line Width (Screen Pixels)", Range(0.5,8)) = 1.5
        _GridInset ("Framing Grid Inset", Range(0.01,0.49)) = 0.2763932
        [Toggle] _ShowGrid ("Show Framing Grid", Float) = 1
        [Toggle] _ShowDiagonals ("Show Diagonals", Float) = 1
        [Toggle] _ShowGolden ("Show Golden Sections", Float) = 1
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "GoldenRatioGrid"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _Color, _GridColor, _GoldenColor;
            float _LineWidth, _GridInset, _ShowGrid, _ShowDiagonals, _ShowGolden;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            // Signed distance to an infinite guide, converted to screen pixels.
            float Guide(float2 uv, float2 start, float2 end)
            {
                float2 direction = end - start;
                float distance = (uv.x - start.x) * direction.y - (uv.y - start.y) * direction.x;
                float2 gradient = float2(ddx(distance), ddy(distance));
                float pixels = abs(distance) / max(length(gradient), 0.000001);
                return saturate(_LineWidth * 0.5 + 0.5 - pixels);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float inset = _GridInset;
                float golden = 0.38196601125;
                float grid = 0;
                if (_ShowGrid > 0.5)
                {
                    grid = max(Guide(uv, float2(inset,0), float2(inset,1)),
                               Guide(uv, float2(1-inset,0), float2(1-inset,1)));
                    grid = max(grid, max(Guide(uv, float2(0,inset), float2(1,inset)),
                                         Guide(uv, float2(0,1-inset), float2(1,1-inset))));
                }
                if (_ShowDiagonals > 0.5)
                {
                    grid = max(grid, max(Guide(uv, float2(0,0), float2(1,1)),
                                         Guide(uv, float2(0,1), float2(1,0))));
                    grid = max(grid, max(Guide(uv, float2(0,0), float2(golden,1)),
                                         Guide(uv, float2(0,1), float2(golden,0))));
                    grid = max(grid, max(Guide(uv, float2(1,0), float2(1-golden,1)),
                                         Guide(uv, float2(1,1), float2(1-golden,0))));
                }
                float sections = 0;
                if (_ShowGolden > 0.5)
                {
                    sections = max(Guide(uv, float2(golden,0), float2(golden,1)),
                                   Guide(uv, float2(1-golden,0), float2(1-golden,1)));
                    sections = max(sections, max(Guide(uv, float2(0,golden), float2(1,golden)),
                                                 Guide(uv, float2(0,1-golden), float2(1,1-golden))));
                }
                float yellowAlpha = grid * _GridColor.a;
                float redAlpha = sections * _GoldenColor.a;
                float alpha = redAlpha + yellowAlpha * (1-redAlpha);
                float3 rgb = (_GoldenColor.rgb * redAlpha + _GridColor.rgb * yellowAlpha * (1-redAlpha))
                           / max(alpha, 0.00001);
                fixed4 color = fixed4(rgb * input.color.rgb, alpha * input.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color.a *= mask.x * mask.y;
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
