Shader "Wave/WaveRingSurface"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _WorldOrigin("World Origin", Vector) = (0,0,0,0)
        _WorldSize ("World Size", Vector) = (64,32,64,0)
        _TintColor ("Tint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode"="UniversalForward"
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE3D(_DistanceTex);
            SAMPLER(sampler_DistanceTex);
            TEXTURE3D(_OccupancyTex);
            SAMPLER(sampler_OccupancyTex);
            float4 _WorldOrigin;
            float4 _WorldSize;
            float _Radius, _Thickness;
            float4 _TintColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                float3 rel = (IN.worldPos - _WorldOrigin.xyz) / _WorldSize.xyz;
                float Dnorm = SAMPLE_TEXTURE3D(_DistanceTex, sampler_DistanceTex, rel).r;
                float D = Dnorm * max(_WorldSize.x, max(_WorldSize.y, _WorldSize.z));

                // Occupancy で表面のみ描画
                float occ = SAMPLE_TEXTURE3D(_OccupancyTex, sampler_OccupancyTex, rel).r;
                if (occ < 0.5) discard;

                // リングマスク
                float inner = _Radius - _Thickness * 0.5;
                float outer = _Radius + _Thickness * 0.5;
                float m = step(inner, D) * step(D, outer);

                half4 col = half4(_TintColor.rgb * m, m * _TintColor.a);
                return col;
            }
            ENDHLSL
        }
    }
}