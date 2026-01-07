cbuffer Constants : register(b0)
{
	float3 lightPos; // 光源の位置 または 方向ベクトル
	float intensity;
	float3 lightColor;
	float ambient;
	float depth;
	float lightType; // 0 = Point, 1 = Directional
	float2 padding; // アライメント用
};

Texture2D<float4> InputTexture : register(t0);
Texture2D<float4> NormalTexture : register(t1);

SamplerState InputSampler : register(s0)
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

float4 main(
float4 pos : SV_POSITION,
float4 posScene : SCENE_POSITION,
float4 uv0 : TEXCOORD0,
float4 uv1 : TEXCOORD1
) : SV_Target
{
	float4 baseColor = InputTexture.Sample(InputSampler, uv0.xy);
	if (baseColor.a <= 0.0)
		discard;
    
	float4 normalColor = NormalTexture.Sample(InputSampler, uv1.xy);

	float3 normal;
	normal.xy = (normalColor.rg * 2.0 - 1.0) * depth;
	normal.z = sqrt(max(0.0, 1.0 - dot(normal.xy, normal.xy)));
	normal.y *= -1.0;
	normal = normalize(normal);

	float3 lightDir;

// ★分岐: 平行光源か点光源か
	if (lightType > 0.5)
	{
    // Directional: lightPos自体が「光が来る方向」ベクトル
		lightDir = normalize(lightPos);
	}
	else
	{
    // Point: (光源位置 - ピクセル位置) がベクトル
		float3 pixelPos = float3(posScene.xy, 0);
		lightDir = normalize(lightPos - pixelPos);
	}

	float diffuse = max(dot(normal, lightDir), 0.0);
	float3 lighting = lightColor * diffuse * intensity;
	float3 finalRGB = baseColor.rgb * (ambient + lighting);

	return float4(finalRGB, baseColor.a);

}