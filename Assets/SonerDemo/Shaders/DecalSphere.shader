Shader "Wave/DecalSphere"
{
         Properties
    {
        _MaskTex    ("Ripple Mask", 2D)       = "white" {}
        _TintColor  ("Tint Color", Color)     = (1,1,1,1)
        _CenterWS   ("Center World Pos", Vector) = (0,0,0,0)
        _Radius     ("Radius", Float)         = 1.0
        _Falloff    ("Falloff", Range(0.0,1.0)) = 0.2
    }
    SubShader
    {
        Tags
        {
            "Queue"                    = "Transparent"
            "RenderPipeline"           = "UniversalPipeline"
            "UniversalPipelineDecal"   = "True"
        }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"

            TEXTURE2D(_MaskTex);

            SamplerState sampler_MaskTex;
            float4 _TintColor;
            float3 _CenterWS;
            float _Radius;
            float _Falloff;

            struct Attributes {
                float3 positionOS : POSITION;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS = TransformWorldToHClip(OUT.worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 距離ベースでマスクを計算
                float d = distance(IN.worldPos, _CenterWS);
                float m = saturate(1.0 - smoothstep(_Radius * (1.0 - _Falloff), _Radius, d));
                // テクスチャサンプル
                float t = _MaskTex.Sample(sampler_MaskTex, float2(d / _Radius, 0.0)).r;
                half4 col;
                col.rgb = _TintColor.rgb;
                col.a   = m * t * _TintColor.a;
                return col;
            }
            ENDHLSL
        }
    }
}
