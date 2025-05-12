Shader "Debug/DistanceFieldVis"
{
    Properties
    {
        _DistanceTex ("DistanceTex", 3D) = "" {}
        _WorldOrigin ("WorldOrigin", Vector) = (0,0,0,0)
        _WorldSize   ("WorldSize",   Vector) = (1,1,1,0)
        _MaxDistance ("MaxDistance", Float)  = 20
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_DistanceTex); SAMPLER(sampler_DistanceTex);
            float4 _WorldOrigin;
            float4 _WorldSize;
            float _MaxDistance;

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 ws = mul(unity_ObjectToWorld, float4(IN.positionOS,1)).xyz;
                o.world = ws;
                o.posCS = TransformWorldToHClip(ws);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 rel = (IN.world - _WorldOrigin.xyz) / _WorldSize.xyz;
                float d_norm = SAMPLE_TEXTURE3D(_DistanceTex, sampler_DistanceTex, rel).r;
                float d = d_norm * _MaxDistance;
                // グレースケール：距離が近いほど白
                return half4(d/_MaxDistance, d/_MaxDistance, d/_MaxDistance, 0.5);
            }
            ENDHLSL
        }
    }
}
