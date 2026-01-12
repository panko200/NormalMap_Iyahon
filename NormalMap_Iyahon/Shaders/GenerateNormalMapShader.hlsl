cbuffer Constants : register(b0)
{
    float strength;
    float radius;
    float2 size;
    
    int mode;
    float detailStrength;
    float thresholdAlpha;
    float thresholdBright;

    float detailRadius;
    float thresholdDiff;
    float curve;
    int format;
    
    int outputMode;
    float invert;
    float2 padding;
};

Texture2D<float4> InputTexture : register(t0);
SamplerState InputSampler : register(s0)
{
    Filter = MIN_MAG_MIP_POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

bool IsAbsoluteBackground(float4 c)
{
    float luma = dot(c.rgb, float3(0.299, 0.587, 0.114));
    return c.a < thresholdAlpha || luma < thresholdBright;
}

float CalculateLuma(float4 c)
{
    return dot(c.rgb, float3(0.299, 0.587, 0.114)) * c.a;
}

float GetLuma(float2 uv)
{
    float4 c = InputTexture.SampleLevel(InputSampler, uv, 0);
    if (IsAbsoluteBackground(c))
        return 0.0;
    return CalculateLuma(c);
}

float4 CalculateSDF(float2 uv, float aspect)
{
    if (radius <= 0.5)
        return float4(0, 0, 1, 0);

    float4 centerColor = InputTexture.SampleLevel(InputSampler, uv, 0);
    if (IsAbsoluteBackground(centerColor))
        return float4(0, 0, 1, 0);

    float centerLuma = CalculateLuma(centerColor);

    float minDistanceSq = 999999.0;
    float2 nearestOffset = float2(0, 0);
    bool foundEdge = false;

    int rX = (int) radius;
    int rY = (int) radius;
    
    if (aspect > 1.0)
        rX = (int) ceil(radius * aspect);
    else
        rY = (int) ceil(radius / aspect);

    [loop]
    for (int y = -rY; y <= rY; y++)
    {
        for (int x = -rX; x <= rX; x++)
        {
            if (x == 0 && y == 0)
                continue;

            float X_adj = (float) x;
            float Y_adj = (float) y;
            
            if (aspect > 1.0)
                X_adj = x / aspect;
            else
                Y_adj = y * aspect;

            float distBox = max(abs(X_adj), abs(Y_adj));
            if (distBox > radius)
                continue;

            float distSq = X_adj * X_adj + Y_adj * Y_adj;
            if (distSq >= minDistanceSq)
                continue;

            float2 offsetUV = float2(x, y) * size;
            float4 neighborColor = InputTexture.SampleLevel(InputSampler, uv + offsetUV, 0);

            bool isEdge = false;
            
            if (IsAbsoluteBackground(neighborColor))
            {
                isEdge = true;
            }
            else if (thresholdDiff > 0.001)
            {
                float neighborLuma = CalculateLuma(neighborColor);
                if ((centerLuma - neighborLuma) > thresholdDiff)
                {
                    isEdge = true;
                }
            }

            if (isEdge)
            {
                minDistanceSq = distSq;
                nearestOffset = float2(x, y);
                foundEdge = true;
            }
        }
    }

    if (foundEdge)
    {
        // ★修正ポイント: 高さ計算用の距離にもアスペクト比補正を適用する
        float2 distVec = nearestOffset;
        
        // 探索時と同じロジックで補正して、「正規化された距離」にする
        if (aspect > 1.0)
            distVec.x /= aspect;
        else
            distVec.y *= aspect;

        float distAdj = length(distVec);
        float t = saturate(distAdj / radius);

        // Height計算
        float height = t;
        
        if (curve > 0.0)
            height = t + (t * curve * 0.5);
        else if (curve < 0.0)
            height = t + ((1.0 - t) * abs(curve) * 0.5);

        height = saturate(height);

        // Normal計算
        float2 dir = nearestOffset;
        if (aspect > 1.0)
            dir.x /= aspect;
        else
            dir.y *= aspect;
        dir = normalize(dir);
        
        float baseZ = 1.0 / max(0.01, strength);
        float zFinal = baseZ;
        float curvePower = 5.0 * baseZ;

        if (curve > 0.0)
            zFinal = baseZ + (t * curve * curvePower);
        else if (curve < 0.0)
            zFinal = baseZ + ((1.0 - t) * abs(curve) * curvePower);
        
        float3 norm = normalize(float3(dir.x, dir.y, max(0.01, zFinal)));
        
        return float4(norm, height);
    }
    
    return float4(0, 0, 1, 1.0);
}

float4 CalculateSobel(float2 uv)
{
    float luma = GetLuma(uv);
    if (detailStrength <= 0.01)
        return float4(0, 0, 1, luma);

    float r = max(1.0, detailRadius);
    float2 step = size * r;

    float tl = GetLuma(uv + float2(-step.x, -step.y));
    float t = GetLuma(uv + float2(0, -step.y));
    float tr = GetLuma(uv + float2(step.x, -step.y));
    float l = GetLuma(uv + float2(-step.x, 0));
    float rP = GetLuma(uv + float2(step.x, 0));
    float bl = GetLuma(uv + float2(-step.x, step.y));
    float b = GetLuma(uv + float2(0, step.y));
    float br = GetLuma(uv + float2(step.x, step.y));

    float dX = (tr + 2.0 * rP + br) - (tl + 2.0 * l + bl);
    float dY = (bl + 2.0 * b + br) - (tl + 2.0 * t + tr);

    return float4(-dX * detailStrength, -dY * detailStrength, 1.0, luma);
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float aspect = size.y / size.x;
    float4 result = float4(0, 0, 1, 0);

    if (mode == 0) // Sobel
    {
        result = CalculateSobel(uv0);
        result.xyz = normalize(result.xyz);
    }
    else if (mode == 1) // SDF
    {
        result = CalculateSDF(uv0, aspect);
    }
    else // Blend
    {
        float4 sdf = CalculateSDF(uv0, aspect);
        float4 sobel = CalculateSobel(uv0);
        
        result.xyz = sdf.xyz + sobel.xyz;
        result.z = sdf.z;
        result.xyz = normalize(result.xyz);
        
        result.w = saturate(sdf.w + (sobel.w * 0.1 * detailStrength));
    }
    
    if (invert > 0.5)
    {
        result.x *= -1.0;
        result.y *= -1.0;
        result.w = 1.0 - result.w;
    }

    if (outputMode == 0) // Normal Map
    {
        if (format == 0)
        {
            result.y *= -1.0;
        }
        return float4(result.xyz * 0.5 + 0.5, 1.0);
    }
    else // Height Map
    {
        return float4(result.w, result.w, result.w, 1.0);
    }
}