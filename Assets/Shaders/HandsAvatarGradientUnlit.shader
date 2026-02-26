Shader "Unlit/HandsAvatarGradientUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _EdgeColor ("Edge Color", Color) = (0.92,0.97,1,1)
        _EdgeIntensity ("Edge Intensity", Range(0,10)) = 0
        _EdgePower ("Edge Power", Range(1,12)) = 6
        _EdgeWidth ("Edge Width", Range(0.01,1)) = 0.22
        _FistGlowStrength ("Fist Glow Strength", Range(0,10)) = 0
        _FistGlowTint ("Fist Glow Tint", Color) = (0.92,0.97,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            fixed4 _EdgeColor;
            half _EdgeIntensity;
            half _EdgePower;
            half _EdgeWidth;
            half _FistGlowStrength;
            fixed4 _FistGlowTint;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float fistMask : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.fistMask = saturate(v.uv2.x);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed vertexAlpha = saturate(i.color.a);
                fixed4 col = _Color;
                col.rgb *= i.color.rgb;
                col.a *= vertexAlpha;
                col.rgb += _EmissionColor.rgb * vertexAlpha;
                return col;
            }
            ENDCG
        }

        Pass
        {
            ZWrite Off
            Blend One One
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAccent
            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            half _EdgeIntensity;
            half _EdgePower;
            half _EdgeWidth;
            half _FistGlowStrength;
            fixed4 _FistGlowTint;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float fistMask : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.fistMask = saturate(v.uv2.x);
                return o;
            }

            fixed4 fragAccent(v2f i) : SV_Target
            {
                float alphaMask = saturate(i.color.a * 1.5);
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float fresnel = pow(saturate(1.0 - dot(n, v)), max(1.0, _EdgePower));
                float edgeMask = smoothstep(1.0 - saturate(_EdgeWidth), 1.0, fresnel);
                float fistMask = saturate(i.fistMask);

                float3 edge = _EdgeColor.rgb * _EdgeIntensity * edgeMask * alphaMask;
                float3 fist = _FistGlowTint.rgb * _FistGlowStrength * fistMask * alphaMask;
                return fixed4(edge + fist, 0);
            }
            ENDCG
        }
    }
}
