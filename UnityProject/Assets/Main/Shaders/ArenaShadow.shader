// Stage 08f: the arena's fake shadows — a soft multiply, never a shadow map.
//
// Cards do not cast real shadows: URP's shadow pass is off for every card (`ShadowCastingMode.Off`
// and `receiveShadows = false` on the prefab), and each card instead owns one of these quads lying
// on the floor. The quad multiplies the framebuffer (`Blend DstColor Zero`) with a colour that
// fades back to white where the texture's alpha is zero, so the shadow has no visible edge and can
// never lift the floor underneath it.
//
// The tint is the lighting profile's cool end, written as the global `_ArenaShadowColor`, and it is
// deliberately never black: a black ellipse on a green floor reads as a hole, and the floor is the
// surface ghosts are read against. `_ArenaShadowStrength` is how dark a fully covered pixel gets;
// switching Dusk/Night changes both globals and nothing on this material.
//
// The same shader draws the one-call instanced blob shadows under the actors
// (`ArenaBlobShadows`), which is why it carries the instancing macros: a shadow decal is a normal
// renderer, the actor shadows are DrawMeshInstanced, and both must use one material and one look.
Shader "ProjectSurvive/ArenaShadow"
{
    Properties
    {
        _BaseMap ("Shadow shape", 2D) = "white" {}
        _BaseColor ("Colour (alpha = coverage)", Color) = (1, 1, 1, 1)
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
            Name "ArenaShadowMultiply"
            Tags { "LightMode" = "UniversalForward" }

            // Multiply: result = source × destination, so a floor pixel can only be darkened by the
            // amount this pass asks for and never replaced.
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Written by ArenaLightingProfile; the only way a shadow changes between Dusk and Night.
            float4 _ArenaShadowColor;
            float _ArenaShadowStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half coverage = saturate(texel.a * _BaseColor.a);

                // The darkest a shadowed pixel can become is the profile's own tint — a deep teal at
                // full strength, never black — and the edges of the shape fade back to white, which
                // is "no change" for a multiply.
                half3 shadow = saturate(_ArenaShadowColor.rgb);
                half3 multiplier = lerp(half3(1.0h, 1.0h, 1.0h), shadow, coverage * saturate(_ArenaShadowStrength));
                return half4(multiplier, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
