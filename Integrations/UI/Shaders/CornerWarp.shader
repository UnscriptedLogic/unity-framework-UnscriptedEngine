Shader "UnscriptedEngine/UI/Corner Warp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BottomLeft ("Bottom Left (UV)", Vector) = (0,0,0,0)
        _TopLeft ("Top Left (UV)", Vector) = (0,1,0,0)
        _TopRight ("Top Right (UV)", Vector) = (1,1,0,0)
        _BottomRight ("Bottom Right (UV)", Vector) = (1,0,0,0)
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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
            Name "CornerWarp"

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
                float4 localPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _BottomLeft;
            float4 _TopLeft;
            float4 _TopRight;
            float4 _BottomRight;
            float4 _ClipRect;
            float4 _TextureSampleAdd;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex;
                output.uv = input.uv;
                output.color = input.color * _Color;

                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float Cross2D(float2 first, float2 second)
            {
                return first.x * second.y - first.y * second.x;
            }

            float3 GetBarycentricCoordinates(float2 samplePosition, float2 first, float2 second, float2 third)
            {
                float2 firstToSecond = second - first;
                float2 firstToThird = third - first;
                float denominator = Cross2D(firstToSecond, firstToThird);
                float safeDenominator = abs(denominator) < 0.000001
                    ? (denominator < 0 ? -0.000001 : 0.000001)
                    : denominator;
                float2 firstToPoint = samplePosition - first;
                float secondWeight = Cross2D(firstToPoint, firstToThird) / safeDenominator;
                float thirdWeight = Cross2D(firstToSecond, firstToPoint) / safeDenominator;
                return float3(1.0 - secondWeight - thirdWeight, secondWeight, thirdWeight);
            }

            float GetPolygonCoverage(float2 samplePosition)
            {
                float2 bottomLeft = _BottomLeft.xy;
                float2 topLeft = _TopLeft.xy;
                float2 topRight = _TopRight.xy;
                float2 bottomRight = _BottomRight.xy;
                float signedArea = Cross2D(bottomRight - bottomLeft, topRight - bottomLeft)
                    + Cross2D(topRight - bottomLeft, topLeft - bottomLeft);
                float winding = signedArea < 0 ? -1.0 : 1.0;

                float bottomEdge = Cross2D(bottomRight - bottomLeft, samplePosition - bottomLeft)
                    * winding / max(length(bottomRight - bottomLeft), 0.000001);
                float rightEdge = Cross2D(topRight - bottomRight, samplePosition - bottomRight)
                    * winding / max(length(topRight - bottomRight), 0.000001);
                float topEdge = Cross2D(topLeft - topRight, samplePosition - topRight)
                    * winding / max(length(topLeft - topRight), 0.000001);
                float leftEdge = Cross2D(bottomLeft - topLeft, samplePosition - topLeft)
                    * winding / max(length(bottomLeft - topLeft), 0.000001);

                float edgeDistance = min(min(bottomEdge, rightEdge), min(topEdge, leftEdge));
                float antialiasWidth = max(fwidth(edgeDistance), 0.00001);
                return smoothstep(-antialiasWidth, antialiasWidth, edgeDistance);
            }

            float2 GetTextureCoordinates(float2 samplePosition)
            {
                float3 firstTriangle = GetBarycentricCoordinates(
                    samplePosition, _BottomLeft.xy, _BottomRight.xy, _TopRight.xy);
                if (min(firstTriangle.x, min(firstTriangle.y, firstTriangle.z)) >= 0)
                {
                    return firstTriangle.y * float2(1, 0) + firstTriangle.z * float2(1, 1);
                }

                float3 secondTriangle = GetBarycentricCoordinates(
                    samplePosition, _BottomLeft.xy, _TopRight.xy, _TopLeft.xy);
                return secondTriangle.y * float2(1, 1) + secondTriangle.z * float2(0, 1);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 textureCoordinates = GetTextureCoordinates(input.uv);
                fixed4 color = (tex2D(_MainTex, textureCoordinates) + _TextureSampleAdd) * input.color;
                color.a *= GetPolygonCoverage(input.uv);

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
