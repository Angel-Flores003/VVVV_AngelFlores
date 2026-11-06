// Draws the Template View overlay (editor only; only editor code uses it, so it isn't included in builds).
// The Scene view draws its GUI without converting colours for display, so in a Linear colour space project
// the overlay converts its own output back to gamma (_ToGamma = 1) or it comes out too dark. The Tile Palette
// draws during its gizmo pass, where Unity converts already (_ToGamma = 0).
// The same pass twice: tagged for URP (what the Asset Store validator looks for), and untagged for other pipelines.
Shader "Hidden/AtomicRealm/TemplateOverlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ToGamma ("Convert to gamma", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _ToGamma;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_ToGamma > 0.5) c.rgb = LinearToGammaSpace(c.rgb);
                return c;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _ToGamma;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_ToGamma > 0.5) c.rgb = LinearToGammaSpace(c.rgb);
                return c;
            }
            ENDCG
        }
    }
}
