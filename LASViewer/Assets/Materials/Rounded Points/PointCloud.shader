Shader "Unlit/PointCloud"
{
	Properties
	{
		_Size ("Size", Float) = 0.1
	}
	SubShader
	{
		Tags { "Queue"="AlphaTest" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
		//AlphaToMask On
		Cull Off

		Pass
		{
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
                
                up = (up * _Size/2.0);
                tangent = (tangent* _Size/2.0);
                float3 p = vertex.xyz;
                
                //V1
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p + up, 1.0));
                pIn.texcoord = float2(0, 2.0);;
                triStream.Append(pIn);
                
                //V2
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p - up + tangent, 1.0));
                pIn.texcoord = float2(1.73205080757, -1.0);;
                triStream.Append(pIn);
                
                //V3
                pIn.vertex = mul(UNITY_MATRIX_VP, float4(p - up - tangent, 1.0));
                pIn.texcoord = float2(-1.73205080757, -1.0);;
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
