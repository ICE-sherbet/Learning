Shader "Custom/RippleGridURP"
{
    Properties
    {
        _BaseMap ("Albedo (RGB)", 2D) = "white" {}
        _GridOrigin ("Grid Origin (X,Z)", Vector) = (0,0,0,0)
        _GridSize ("Grid Size (X,Z)", Vector) = (10,0,10,0)
        _HeightScale ("Height Scale", Float) = 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_HeightField);
            SAMPLER(sampler_HeightField);
            float4 _GridOrigin;
            float4 _GridSize;
            float _HeightScale;

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                // Transform to world space
                float3 wpos = TransformObjectToWorld(IN.position);
                float3 wsNormal = TransformObjectToWorldNormal(IN.normal);

                // Compute grid UV
                float2 frac = (wpos.xz - _GridOrigin.xz) / _GridSize.xz;
                frac = saturate(frac);

                // Sample height field in vertex shader
                float h = SAMPLE_TEXTURE2D_LOD(_HeightField, sampler_HeightField, frac, 0).r;
                float heightOffset = h * _HeightScale;

                // Offset vertex along normal
                wpos.xyz += wsNormal * heightOffset;

                OUT.worldPos = wpos.xyz;
                OUT.normalWS = wsNormal;
                OUT.uv = IN.uv;

                // Compute displaced clip position
                OUT.pos = TransformWorldToHClip(wpos.xyz);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Sample albedo texture
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return half4(IN.uv.x, IN.uv.y, IN.worldPos.x / 8.0, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}