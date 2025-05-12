Shader "Wave/RippleMaskedSurface"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D)    = "white" {}
        _MaskTex   ("Ripple Mask", 2D) = "white" {}
        _Threshold ("Mask Threshold", Range(0,1)) = 0.01
    }
    SubShader
    {
        Tags
        {
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
        }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SamplerState sampler_BaseMap;
            TEXTURE2D(_MaskTex);
            SamplerState sampler_MaskTex;
            float4 _BaseMap_ST;
            float4 _MaskTex_ST;
            float _Threshold;

            struct Attributes {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uvBase = TRANSFORM_TEX(IN.uv, _BaseMap);
                float2 uvMask = TRANSFORM_TEX(IN.uv, _MaskTex);
                float mask = _MaskTex.Sample(sampler_MaskTex, uvMask).r;
                if (mask < _Threshold) discard;
                return _BaseMap.Sample(sampler_BaseMap, uvBase);
            }
            ENDHLSL
        }
    }
}
