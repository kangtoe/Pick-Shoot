// 피격 플래시용 흰 실루엣 (Enemies §1). 스프라이트 텍스처의 알파만 읽고 색은 _Color로 채운다 — 그림이 무엇이든 실루엣이 통째로 단색이 된다.
// EnemyHealth가 피격 순간 렌더러의 머티리얼을 이 셰이더의 머티리얼로 바꿔 끼웠다가 되돌린다. 렌더러의 색 틴트(SpriteRenderer.color)는
// 곱셈이라 흰색을 넣어도 원본 그대로여서 틴트로는 낼 수 없는 효과다.
// URP 2D 렌더러는 LightMode 태그가 없는 패스(SRPDefaultUnlit)를 그대로 그리므로 조명 패스 없는 Unlit로 둔다 — 씬 조명은 전역 백색 하나라 차이가 없다.
Shader "PickShoot/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Flash Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a * _Color.a;
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
