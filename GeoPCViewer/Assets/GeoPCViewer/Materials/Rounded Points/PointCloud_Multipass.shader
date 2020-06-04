Shader "Unlit/PointCloud_Multipass"
{
    Properties
    {
        _MinDistance ("MinDistance", Float) = 100.0
        _MaxDistance ("MaxDistance", Float) = 300.0
        _Transparency ("Transparency", Float) = 1.0
        _Size ("Size", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "DotPass"

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

        Pass
		{
            Name "BillboardPass"

			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float _Size;

			struct GS_INPUT
			{
				float4 vertex : POSITION;
				float3 normal	: NORMAL;
				float4 color	: COLOR;
				float2 texcoord : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
			};

			struct FS_INPUT {
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			GS_INPUT vert (appdata_full v)
			{
				GS_INPUT o = (GS_INPUT)0;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.color = v.color;
				return o;
			}


			[maxvertexcount(3)]
			void geom (point GS_INPUT tri[1], inout TriangleStream<FS_INPUT> triStream)
			{
                float3 viewDir = normalize(UNITY_MATRIX_IT_MV[2].xyz);
                //float3 camUp = normalize(UNITY_MATRIX_IT_MV[1].xyz);
                //float3 camLeft = normalize(UNITY_MATRIX_IT_MV[0].xyz);
            
				FS_INPUT pIn = (FS_INPUT)0;
                pIn.normal = mul(unity_ObjectToWorld, viewDir);
				pIn.color = tri[0].color;

				float4 vertex = mul(unity_ObjectToWorld, tri[0].vertex);
				float3 tangent = normalize(cross(float3(0,1,0), pIn.normal));
				float3 up = normalize(cross(tangent, pIn.normal));
                
                //up = (up * _Size/2.0);
              
                //float3 upY = up * _Size / 0.5; //Sin(30)
                //float3 downY = up * _Size / 0.5; //Sin(30)
                tangent = tangent * 1.73205080757;// (tangent* _Size/2.0);
                
                tangent *= _Size;
                up *= _Size;
                
                float3 p = vertex.xyz;
                
                //V1
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p + up*2.0, 1.0));
                pIn.texcoord = float2(0, 2.0);
                triStream.Append(pIn);
                
                //V2
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p - up + tangent, 1.0));
                pIn.texcoord = float2(1.73205080757, -1.0);
                triStream.Append(pIn);
                
                //V3
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p - up - tangent, 1.0));
                pIn.texcoord = float2(-1.73205080757, -1.0);
                triStream.Append(pIn);
			}

			float4 frag (FS_INPUT i) : COLOR
			{
                float sqrDist = dot((i.texcoord), (i.texcoord));
                if (sqrDist > 1.0){
                    discard; //return float4(1.0,0.0,0.0,1.0);
                } 
                //else{
                //    float dist = sqrt(sqrDist);
                //    i.color = float4(i.color.r,i.color.g,i.color.b,a);
                //}
                return i.color;
			}
			ENDCG
		}
    }
}