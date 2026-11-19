Shader "Custom/TopDownRiver2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (alpha = water mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Flow)]
        _FlowAngle ("Flow Direction (degrees, 0 = right, 90 = up)", Range(0, 360)) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.6

        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.78, 0.88, 1)
        _DeepColor ("Deep Color", Color) = (0.10, 0.38, 0.62, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _PatchScale ("Color Patch Scale", Float) = 0.5

        [Header(Current Streaks)]
        _StreakColor ("Streak Color", Color) = (0.80, 0.97, 1, 1)
        _StreakScale ("Streak Scale (across the flow)", Float) = 2.0
        _StreakStretch ("Streak Stretch (along the flow)", Range(1, 12)) = 5
        _StreakThreshold ("Streak Threshold", Range(0.3, 0.9)) = 0.58
        _StreakIntensity ("Streak Intensity", Range(0, 1)) = 0.55

        [Header(Foam Flecks)]
        _FleckColor ("Fleck Color", Color) = (1, 1, 1, 1)
        _FleckScale ("Fleck Scale", Float) = 6
        _FleckAmount ("Fleck Amount", Range(0, 0.4)) = 0.1
        _FleckIntensity ("Fleck Intensity", Range(0, 1)) = 0.9

        [Header(Bank Foam)]
        _FoamColor ("Bank Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth ("Bank Foam Width (texels)", Range(0, 8)) = 2
        _FoamIntensity ("Bank Foam Intensity", Range(0, 1)) = 0.8

        [Header(Pixel Art)]
        _PixelsPerUnit ("Snap Pattern To Pixels Per Unit (0 = off)", Float) = 16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "TopDownRiver2D"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _FlowAngle;
                float _FlowSpeed;
                half4 _ShallowColor;
                half4 _DeepColor;
                float _Opacity;
                float _PatchScale;
                half4 _StreakColor;
                float _StreakScale;
                float _StreakStretch;
                float _StreakThreshold;
                float _StreakIntensity;
                half4 _FleckColor;
                float _FleckScale;
                float _FleckAmount;
                float _FleckIntensity;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamIntensity;
                float _PixelsPerUnit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  color      : COLOR;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * valueNoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float mask = sprite.a * IN.color.a;
                if (mask <= 0.001) discard;

                float2 wp = IN.positionWS.xy;
                if (_PixelsPerUnit > 0.5)
                    wp = floor(wp * _PixelsPerUnit) / _PixelsPerUnit;

                // Rotate world space so x runs ALONG the river and y ACROSS it
                float ang = radians(_FlowAngle);
                float2 dir = float2(cos(ang), sin(ang));
                float2 fuv = float2(dot(wp, dir), dot(wp, float2(-dir.y, dir.x)));

                float flow = t * _FlowSpeed;

                // --- Base color: slow, long patches drifting downstream ---
                float2 pBase = float2((fuv.x - flow * 0.35) / (_StreakStretch * 0.6), fuv.y) * _PatchScale;
                float patch = fbm(pBase);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.3, 0.7, patch));

                // --- Current streaks: two stretched layers moving at different speeds ---
                float2 p1 = float2((fuv.x - flow) / _StreakStretch, fuv.y) * _StreakScale;
                float2 p2 = float2((fuv.x - flow * 1.6) / (_StreakStretch * 1.3), fuv.y + 7.3) * _StreakScale * 1.7;
                float n1 = fbm(p1);
                float n2 = fbm(p2);
                float s1 = smoothstep(_StreakThreshold, _StreakThreshold + 0.08, n1);
                float s2 = smoothstep(_StreakThreshold + 0.03, _StreakThreshold + 0.11, n2);
                float streak = saturate(s1 + s2 * 0.7);
                col = lerp(col, _StreakColor.rgb, streak * _StreakIntensity);

                // --- Foam flecks: small bright dashes racing downstream ---
                float2 pf = float2((fuv.x - flow * 2.0) * _FleckScale * 0.35, fuv.y * _FleckScale);
                float fleckN = valueNoise(pf) * valueNoise(pf * 1.9 + 11.0);
                float fleck = step(1.0 - _FleckAmount * 0.6, fleckN * 1.6);
                col = lerp(col, _FleckColor.rgb, fleck * _FleckIntensity * _FleckColor.a);

                // --- Bank foam from sprite alpha edges, streaked along the flow ---
                float2 off = _MainTex_TexelSize.xy * _FoamWidth;
                float a0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( off.x, 0)).a;
                float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-off.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,  off.y)).a;
                float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -off.y)).a;
                float edge = (_FoamWidth > 0.01) ? saturate(1.0 - min(min(a0, a1), min(a2, a3))) : 0.0;
                float2 pe = float2((fuv.x - flow * 1.2) / 2.0, fuv.y) * 5.0;
                float foam = smoothstep(0.35, 0.55, fbm(pe) + edge * 0.5) * edge;
                col = lerp(col, _FoamColor.rgb, foam * _FoamIntensity * _FoamColor.a);

                return half4(col, mask * _Opacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
