Shader "Custom/Mountain1Shader"
{
    Properties
    {
        _Color ("Color Tint", Color) = (1,1,1,1)
        
        [Header(Base Texture (Rock))]
        _MainTex ("Base Albedo (RGB)", 2D) = "white" {}
        _BaseNormal ("Base Normal Map", 2D) = "bump" {}
        
        [Header(Top Texture (Snow))]
        _TopTex ("Top Albedo (RGB)", 2D) = "white" {}
        _TopNormal ("Top Normal Map", 2D) = "bump" {}
        
        [Header(Mask Texture)]
        _MaskTex ("Blend Mask (R channel)", 2D) = "black" {}
        
        [Header(Settings)]
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BaseNormal;
        
        sampler2D _TopTex;
        sampler2D _TopNormal;
        
        sampler2D _MaskTex;

        struct Input
        {
            float2 uv_MainTex; // UVs for the base texture
            float2 uv_TopTex;  // UVs for the top texture (can tile independently)
            float2 uv_MaskTex; // UVs for the mask (usually shouldn't tile)
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Sample all textures
            fixed4 cBase = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 cTop = tex2D(_TopTex, IN.uv_TopTex);
            
            fixed3 nBase = UnpackNormal(tex2D(_BaseNormal, IN.uv_MainTex));
            fixed3 nTop = UnpackNormal(tex2D(_TopNormal, IN.uv_TopTex));
            
            // We only need one channel (e.g., Red) for the mask
            fixed maskValue = tex2D(_MaskTex, IN.uv_MaskTex).r; 

            // 2. Lerp (Linear Interpolate) between Base and Top based on the mask
            fixed4 finalAlbedo = lerp(cBase, cTop, maskValue) * _Color;
            fixed3 finalNormal = lerp(nBase, nTop, maskValue);

            // 3. Output to the Standard lighting model
            o.Albedo = finalAlbedo.rgb;
            o.Normal = finalNormal;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = finalAlbedo.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}