// ProjectSurvive - the ghost-doctor's outline, in one URP-friendly pass (Stage 17b follow-up).
//
// Why not spine-unity's own "Spine/Outline/Skeleton": it draws the outline and the body as two
// passes of one SubShader, and URP renders exactly one untagged (SRPDefaultUnlit) pass per
// material. The result was a figure made of thin dark lines with no body at all - measured in
// artifacts/ui/battle-player-1920x1080.png before this file existed.
//
// The body half is a copy of Spine/Skeleton's Normal pass (premultiplied alpha, vertex colour
// tint, One / OneMinusSrcAlpha), so the figure looks exactly as it did. The outline half samples
// eight neighbours in screen-pixel steps taken from the UV derivatives, which makes the ink one
// screen pixel wide at every camera size and texture resolution instead of a number of atlas
// texels that would vanish at 45-pixel-tall figures.
Shader "ProjectSurvive/SpinePlayerOutline"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Atlas Page", 2D) = "black" {}
        _OutlineColor ("Outline Colour", Color) = (0.05, 0.04, 0.035, 1)
        _OutlineWidth ("Outline Width (screen px)", Range(0, 4)) = 1.2
        _Cutoff ("Edge Alpha Cutoff", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Fog { Mode Off }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // The farthest of the eight neighbours' alpha, in UV steps of one screen pixel.
            float EdgeAlpha (float2 uv, float2 stepUV)
            {
                float e = 0;
                e = max(e, tex2D(_MainTex, uv + float2( stepUV.x, 0)).a);
                e = max(e, tex2D(_MainTex, uv + float2(-stepUV.x, 0)).a);
                e = max(e, tex2D(_MainTex, uv + float2(0,  stepUV.y)).a);
                e = max(e, tex2D(_MainTex, uv + float2(0, -stepUV.y)).a);
                e = max(e, tex2D(_MainTex, uv + float2( stepUV.x,  stepUV.y)).a);
                e = max(e, tex2D(_MainTex, uv + float2(-stepUV.x,  stepUV.y)).a);
                e = max(e, tex2D(_MainTex, uv + float2( stepUV.x, -stepUV.y)).a);
                e = max(e, tex2D(_MainTex, uv + float2(-stepUV.x, -stepUV.y)).a);
                return e;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, i.uv);
                float alpha = texColor.a * i.color.a;

                // The body: exactly what Spine/Skeleton's Normal pass returns, premultiplied.
                if (alpha >= _Cutoff)
                {
                    return texColor * i.color;
                }

                // The ink: only where this texel is empty and a neighbour is not.
                float2 pixels = float2(abs(ddx(i.uv.x)) + abs(ddy(i.uv.x)),
                                       abs(ddx(i.uv.y)) + abs(ddy(i.uv.y)));
                float2 stepUV = pixels * max(_OutlineWidth, 0.001);

                float edge = EdgeAlpha(i.uv, stepUV) * i.color.a;
                if (edge < _Cutoff)
                {
                    return float4(0, 0, 0, 0);
                }

                // Premultiplied, so it blends with the same One / OneMinusSrcAlpha as the body; the
                // neighbour's own alpha softens the ink where it fades out.
                float ink = _OutlineColor.a * saturate(edge / max(_Cutoff, 0.001));
                return float4(_OutlineColor.rgb * ink, ink);
            }
            ENDCG
        }
    }

    Fallback Off
}
