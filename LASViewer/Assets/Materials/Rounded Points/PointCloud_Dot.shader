Shader "Unlit/PointCloud_Dot"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "Queue"="AlphaTest" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Blend One OneMinusSrcAlpha
		AlphaToMask On
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
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

			float4 frag (FS_INPUT i) : COLOR
			{
				return i.color;
			}
			ENDCG
		}
	}
}
