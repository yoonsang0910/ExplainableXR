Shader "Custom/DepthShader" {
    SubShader 
    {
	Tags { "RenderType"="Opaque" }

    Pass
    {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // vertex shader inputs
            struct appdata
            {
                float4 vertex : POSITION; // vertex position
                float2 uv : TEXCOORD0; // texture coordinate
            };

            // vertex shader outputs ("vertex to fragment")
            struct v2f
            {
                float4 uv : TEXCOORD0; // texture coordinate
                // float4 uv1 : TEXCOORD1;
                float4 vertex : SV_POSITION; // clip space position
            };

            //Vertex Shader
            v2f vert (appdata v){
               v2f o;
               o.vertex = UnityObjectToClipPos(v.vertex);
               // just pass the texture coordinate
               //o.uv = v.uv;
               o.uv = ComputeScreenPos(o.vertex);
               return o;
            }
            // sampler2D _CameraDepthTexture;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            //Fragment Shader
            fixed4 frag (v2f i) : COLOR
            {               
                float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv.xy / i.uv.w));
                return fixed4(depth, depth, depth, 1.0);
            }
        ENDCG
        }
    }
	FallBack "Diffuse"
}