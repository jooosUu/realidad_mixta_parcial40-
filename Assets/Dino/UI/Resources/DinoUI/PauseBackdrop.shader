// Fondo de los menús: el último cuadro del juego congelado, en sepia,
// oscurecido, con un poco de desenfoque y viñeta. Se usa en un RawImage.
Shader "DinoWest/PauseBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captura", 2D) = "black" {}
        _Bright ("Brillo", Range(0, 1.5)) = 0.62
        _Blur ("Desenfoque (px)", Range(0, 8)) = 2
        _Mix ("Mezcla sepia", Range(0, 1)) = 0.95
        _Dark ("Tono oscuro", Color) = (0.027, 0.016, 0.016, 1)
        _Light ("Tono claro", Color) = (0.84, 0.776, 0.682, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Bright, _Blur, _Mix;
            fixed4 _Dark, _Light;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _Blur;
                fixed3 c = tex2D(_MainTex, i.uv).rgb * 0.2;
                c += tex2D(_MainTex, i.uv + float2( d.x, 0)).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + float2(-d.x, 0)).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + float2(0,  d.y)).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + float2(0, -d.y)).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + d).rgb * 0.1;
                c += tex2D(_MainTex, i.uv - d).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb * 0.1;
                c += tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb * 0.1;

                float lum = dot(c, float3(0.299, 0.587, 0.114));
                fixed3 s = lerp(_Dark.rgb, _Light.rgb, lum);
                s = saturate((s - 0.5) * 1.35 + 0.5);
                fixed3 col = lerp(c, s, _Mix) * _Bright;

                float r = length(i.uv - 0.5) * 2.0;
                float v = r < 0.5 ? lerp(0.0, 0.18, r / 0.5) : lerp(0.18, 0.85, saturate((r - 0.5) / 0.5));
                col *= (1.0 - v);
                return fixed4(col, i.color.a);
            }
            ENDCG
        }
    }
}
