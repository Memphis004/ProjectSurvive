// Stage 08e: the arena's card shader.
//
// One quad, pivot at its base, alpha-clipped (`clip`) so the parts of the quad its texture does not
// cover are not drawn at all, and a gentle sway applied only to the upper part of the card (uv.y),
// which is what makes a ring of trees read as foliage rather than as a fence. Who hides whom is
// decided by sorting order alone — see the pass below.
//
// The art is authored in daylight colours. Night is a profile: the profile writes the global
// _ArenaTint (and the fog/ambient/post-processing around it), so the same material and the same
// texture produce the night look. _ArenaSway is the settings switch (0 disables the sway) and is
// also global, so toggling it never swaps a material.
Shader "ProjectSurvive/ArenaCard"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour (daylight)", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.35
        _SwayAmplitude ("Sway (world units)", Range(0, 0.5)) = 0.12
        _SwaySpeed ("Sway speed", Float) = 1.1
        // 1 = solid, 0 = dithered away. Written per card by ArenaCardFade when the card stands
        // between the camera and the player, so a player who walks into the tree wall is still
        // readable. A screen-door dither rather than alpha blending: this shader writes no depth
        // and blends nothing, and staying opaque is what keeps the card from revealing the actors
        // that are already sorted behind it.
        _Fade ("Occlusion fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ArenaCardForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            // Occlusion between a card and an actor is decided by BattleWorldMap.SortingOrder, the
            // same scale the sprites use, not by the camera's depth buffer: a foliage card is drawn
            // before or after an actor purely by the row it stands on. So this pass writes no depth
            // and tests none — if it did, a tree could disappear behind the opaque floor decal it
            // stands on, or win an argument the sorting order already settled.
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // _Fade lives inside the per-material CBUFFER, not beside the globals: it is written by
            // a MaterialPropertyBlock on one renderer at a time, and a property outside the CBUFFER
            // would be read as a shader global that the block cannot override.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _SwayAmplitude;
                float _SwaySpeed;
                float _Fade;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Global, written by ArenaLightingProfile. Kept out of the per-material CBUFFER so
            // every card of every style shares one value and SRP batching stays intact.
            float4 _ArenaTint;
            float _ArenaSway;

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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Only the top of the card moves. The quad's pivot is its base and its uv.y is
                // 0 there, so `upper` is exactly "the foliage, not the trunk".
                float upper = saturate(input.uv.y);
                float phase = (_Time.y * _SwaySpeed) + (positionWS.x * 0.35) + (positionWS.z * 0.27);
                float sway = sin(phase) * _SwayAmplitude * upper * upper * _ArenaSway;
                positionWS.x += sway;
                positionWS.z += sway * 0.25;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // A 4×4 ordered dither, built arithmetically so the shader needs no lookup table.
            // Returns 0 … 0.9375 over the sixteen pixels of one cell.
            float Bayer2(float2 cell)
            {
                cell = floor(cell);
                return frac((cell.x * 0.5) + (cell.y * cell.y * 0.75));
            }

            float Bayer4(float2 pixel)
            {
                return (Bayer2(pixel * 0.5) * 0.25) + Bayer2(pixel);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(colour.a - _Cutoff);

                // The fade is a discard, not a blend: at _Fade < 1 the card leaves a screen-door
                // pattern, so the player behind it is seen *through* the card at half resolution
                // instead of disappearing behind a translucent tint.
                clip(_Fade - Bayer4(input.positionCS.xy) - 0.001h);

                colour.rgb *= _ArenaTint.rgb;
                colour.rgb = MixFog(colour.rgb, input.fogFactor);
                colour.a = 1.0h;
                return colour;
            }
            ENDHLSL
        }

        // The shadow pass uses the same texture alpha and cutoff as the visible card, so the
        // transparent quad corners never enter the moon's shadow map. Keep the foliage sway here
        // too, otherwise the silhouette shadow would lag behind the rendered canopy.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _SwayAmplitude;
                float _SwaySpeed;
                float _Fade;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float _ArenaSway;

            // SetupShadowCasterConstantBuffer writes both, but nothing in Core.hlsl or Shadows.hlsl
            // declares them: URP's own ShadowCasterPass.hlsl declares them for exactly this reason.
            // Without these two lines this pass does not compile, and a pass that does not compile is
            // not skipped — it is drawn with the error shader, which casts the whole quad as an opaque
            // rectangle instead of the alpha-clipped silhouette.
            float3 _LightDirection;
            float3 _LightPosition;

            float3 SwayCard(float3 positionWS, float2 uv)
            {
                float upper = saturate(uv.y);
                float phase = (_Time.y * _SwaySpeed) + (positionWS.x * 0.35) + (positionWS.z * 0.27);
                float sway = sin(phase) * _SwayAmplitude * upper * upper * _ArenaSway;
                positionWS.x += sway;
                positionWS.z += sway * 0.25;
                return positionWS;
            }

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = SwayCard(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_TARGET
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
