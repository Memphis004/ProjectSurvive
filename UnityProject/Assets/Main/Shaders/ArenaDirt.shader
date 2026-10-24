// Stage 08e: the large dirt patch on the arena floor.
//
// A ground patch is a *darkener*, not a light: the floor is the brightest thing the fight is read
// against, and an alpha-blended polygon laid over it can only ever lift it. So this pass multiplies
// the framebuffer (`Blend DstColor Zero`) with a colour that fades back to white where the texture's
// alpha is zero — `lerp(1, dirt, alpha)` — which is what makes the edge of the patch disappear
// instead of ending in a visible quad.
//
// The texture and the tint are authored in daylight colours; the lighting profile's global
// _ArenaTint multiplies the dirt's own colour so a night patch is the same asset, darker.
Shader "ProjectSurvive/ArenaDirt"
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
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ArenaDirtMultiply"
            Tags { "LightMode" = "UniversalForward" }

            Blend DstColor Zero
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
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _ArenaTint;

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
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // The texture is the patch's *shape*; the colour is the tint. Multiplying the
                // texture's own brown by the floor would crush it to black, which is not what a dirt
                // patch is — this one takes a few percent of the floor's light away and no more.
                half3 dirt = _BaseColor.rgb * _ArenaTint.rgb;
                half3 multiplier = lerp(half3(1.0h, 1.0h, 1.0h), dirt, texel.a * _BaseColor.a);
                return half4(multiplier, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
