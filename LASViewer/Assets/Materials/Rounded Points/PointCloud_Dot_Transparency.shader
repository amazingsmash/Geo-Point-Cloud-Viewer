Shader "Unlit/PointCloud_Dot_Transparency"
{
    Properties
    {
        _Transparency ("Transparency", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            float _Transparency;

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color    : COLOR;
            };

            v2f vert (
                float4 vertex : POSITION, // vertex position input
                float4 color    : COLOR)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.color = color;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                i.color.a = _Transparency;
                return i.color;
            }
            ENDCG
        }
    }
}