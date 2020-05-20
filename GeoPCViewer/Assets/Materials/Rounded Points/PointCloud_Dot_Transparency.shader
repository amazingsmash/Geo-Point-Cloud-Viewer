Shader "Unlit/PointCloud_Dot_Transparency"
{
    Properties
    {
        _MinDistance ("MinDistance", Float) = 100.0
        _MaxDistance ("MaxDistance", Float) = 300.0
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
            
            float _MinDistance;
            float _MaxDistance;
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

                _MinDistance = 0.0;
                float camDist = distance(vertex.xyz, _WorldSpaceCameraPos);
                float alpha = 1.0 - ((camDist - _MinDistance) / (_MaxDistance - _MinDistance));
                o.color.a  = clamp(/*alpha *  */_Transparency, 0.0, 1.0);
                
                //o.color = float4(alpha, 0,0,1);
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}