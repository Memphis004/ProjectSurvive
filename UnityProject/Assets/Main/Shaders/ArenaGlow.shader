// Stage 08e/08f: the arena's fake light — additive ground pools, the glow around the player's
// lantern and the small floating motes. Real point lights are expensive and capped by the URP
// Forward per-object and per-camera limits, so the arena uses one lantern light plus a handful of
// these.
//
// Additive, no depth write, depth-offset like every other floor decal. Two profile intensities drive
// it and each material listens to one of them: _ArenaLightPool for the ground pools and _ArenaGlow
// for the lantern glow and the motes (which are authored brighter than 1.0 so they cross the
// profile's Bloom threshold and read as small lights rather than as paint). Switching Dusk/Night
// therefore changes no material.
Shader "ProjectSurvive/ArenaGlow"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour (daylight)", Color) = (1, 1, 1, 1)
        _PoolChannel ("Driven by the light-pool intensity", Range(0, 1)) = 1
        _GlowChannel ("Driven by the glow intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ArenaGlowAdditive"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _PoolChannel;
                float _GlowChannel;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _ArenaLightPool;
            float _ArenaGlow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half intensity = (_ArenaLightPool * _PoolChannel) + (_ArenaGlow * _GlowChannel);
                return half4(colour.rgb * colour.a * intensity, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
