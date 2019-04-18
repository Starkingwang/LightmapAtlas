Shader "Lightmap/LightmapViewer"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Lerping("Lerping", range(0, 3)) = 0
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
		Tags{"RenderType" = "ForwardBase"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile LIGHTMAP_ON LIGHTMAP_OFF
			#pragma multi_compile _LMATLAS

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
			#ifdef LIGHTMAP_ON
				float2 uv2 : TEXCOORD1;
			#endif
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Lerping;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			#ifdef LIGHTMAP_ON
				o.uv2 = v.uv2 * unity_LightmapST.xy + unity_LightmapST.zw;
			#endif
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);

			#ifdef LIGHTMAP_ON

				#ifdef _LMATLAS
					int id = floor(_Lerping);
					int idt = min(3, id + 1);
					half ler = _Lerping - id;

					half2 uv[4] =
					{
						i.uv2 * half2(0.5, 0.5),
						i.uv2 * half2(0.5, 0.5) + half2(0.5,   0),
						i.uv2 * half2(0.5, 0.5) + half2(0,   0.5),
						i.uv2 * half2(0.5, 0.5) + half2(0.5, 0.5)
					};

					half3 lm0 = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, uv[id])).rgb;
					half3 lm1 = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, uv[idt])).rgb;

					half3 lm = lerp(lm0, lm1, ler);
				#else
					half3 lm = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv2)).rgb;
				#endif

					col.rgb *= lm;
			#endif

				return col;
			}
			ENDCG
		}
	}
}
