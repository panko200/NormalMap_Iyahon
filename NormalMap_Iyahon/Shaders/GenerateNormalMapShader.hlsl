cbuffer Constants : register(b0)
{
    float strength;
    float radius; // ★追加: 広さ
    float2 size; // (1/w, 1/h)
};

Texture2D<float4> InputTexture : register(t0);
SamplerState InputSampler : register(s0)
{
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

float GetLuma(float4 c)
{
    return dot(c.rgb, float3(0.299, 0.587, 0.114)) * c.a;
}

float4 main(
float4 pos : SV_POSITION,
float4 posScene : SCENE_POSITION,
float4 uv0 : TEXCOORD0
) : SV_Target
{
// 広さを適用
    float2 d = size * radius;
    
    float tl = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(-d.x, -d.y)));
    float t = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(0.0, -d.y)));
    float tr = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(d.x, -d.y)));

    float l = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(-d.x, 0.0)));
    float r = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(d.x, 0.0)));

    float bl = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(-d.x, d.y)));
    float b = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(0.0, d.y)));
    float br = GetLuma(InputTexture.Sample(InputSampler, uv0.xy + float2(d.x, d.y)));

    float dX = (tr + 2.0 * r + br) - (tl + 2.0 * l + bl);
    float dY = (bl + 2.0 * b + br) - (tl + 2.0 * t + tr);

// ★修正: Yを反転 (-dY) していたのを (+dY) にするか、normal生成時に調整
// ここでは normal生成時に y を反転させないことで対応してみる
// (以前は -dY * strength だった)

// YMM4/DirectXの座標系と、一般的なノーマルマップ(OpenGL系が多い)の違いで混乱しやすい部分です
// 「上が明るい＝上を向いている」ように見えるのが正解
// dYがプラス＝下が明るい（値が大きい）
// 法線Yがプラス＝上を向いている
// つまり dY がプラスなら 法線Y はマイナス（下向き）になるべき

    float3 normal = normalize(float3(-dX * strength, -dY * strength, 1.0));

// ★反転オプション: もし逆ならここを normal.y *= -1.0; してください
    normal.y *= -1.0;

    return float4(normal * 0.5 + 0.5, 1.0);

  

}