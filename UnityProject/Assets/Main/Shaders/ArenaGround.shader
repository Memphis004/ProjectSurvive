// Stage 08e: the arena floor. Stage 08h: it writes no depth.
//
// The floor is the first thing drawn (queue 2000, so before the decals at 2950, the card shadows at
// 2960, the glow at 2970, the blob shadows at 2990 and every sprite at 3000) and it is the only
// material in the scene that is *behind* everything else. Writing depth bought nothing and cost the
// actor sprites their lower half: a billboard stands on the ground plane with a centre pivot and the
// camera's 50° tilt, so its lower half hangs below that plane, and a depth-writing floor discarded
// exactly those fragments. Occlusion in this arena is authored — `BattleWorldMap.SortingOrder`
// (docs/adr/0018-battle-camera-projection.md) — and a depth-writing floor is a second,
// contradictory rule (docs/adr/0038-the-floor-writes-no-depth.md).
//
// The texture is a flat colour with faint macro noise; the profile's global _ArenaTint is what turns
// the daylight floor into a night one.
Shader "ProjectSurvive/ArenaGround"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour (daylight)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ArenaGroundForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _ArenaTint;
            float4 _ArenaShadowColor;
            float _ArenaShadowStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadowCoverage = 1.0h - mainLight.shadowAttenuation;
                half3 shadowMultiplier = lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    saturate(_ArenaShadowColor.rgb),
                    shadowCoverage * saturate(_ArenaShadowStrength));
                colour.rgb *= _ArenaTint.rgb * shadowMultiplier;
                colour.rgb = MixFog(colour.rgb, input.fogFactor);
                return colour;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
