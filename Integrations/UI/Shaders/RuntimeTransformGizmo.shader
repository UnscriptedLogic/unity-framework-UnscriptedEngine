Shader "UnscriptedEngine/Runtime Transform Gizmo"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct Attributes { float4 vertex : POSITION; };
            struct Varyings { float4 vertex : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings output; output.vertex = UnityObjectToClipPos(input.vertex); return output; }
            float4 frag(Varyings input) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
