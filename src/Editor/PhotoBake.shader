Shader "Hidden/Codec/PhotoFrames/PhotoBake" {
	Properties {
		_MainTex("Texture", 2D) = "white" {}
	}
	SubShader {
		Lighting Off
		ZTest Always
		//Blend One Zero //replace
		Blend SrcAlpha OneMinusSrcAlpha //transparency blend


		Pass {
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_TexelSize; // 1 / width, 1 / height, width, height
			//float4 _MainTex_ST; // scaleX, scaleY, offsetX, offsetY

			uniform float4x4 _UV;
			uniform float4 _Margin;
			uniform float4 _Crop;

			struct VertexData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(VertexData v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				//o.uv = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
				o.uv = mul(_UV, float4(v.uv, 0, 1)).xy;

				return o;
			}

			float4 frag(v2f i) : SV_Target {
				float2 uv = i.uv;

				bool outsideTexture = uv.x < _Margin.x || _Margin.z < uv.x
					|| uv.y < _Margin.y || _Margin.w < uv.y;

				uv = lerp(_Crop.xy, _Crop.zw, uv);
				uv = clamp(uv, _Crop.xy + _MainTex_TexelSize.xy * 0.5, _Crop.zw - _MainTex_TexelSize.xy * 0.5);

				float4 color = tex2D(_MainTex, uv);

				return outsideTexture ? float4(0, 0, 0, 0) : color;
			}
			ENDHLSL
		}
	}
}