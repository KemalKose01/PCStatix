struct VSIN {
    float3 pos : POSITION;
    float2 uv  : TEXCOORD0;
};

struct VSOUT {
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

VSOUT mainVS(VSIN input)
{
    VSOUT o;
    o.pos = float4(input.pos, 1.0);
    o.uv = input.uv;
    return o;
}

float4 mainPS(VSOUT input) : SV_TARGET
{
    float2 uv = input.uv;

    float x = uv.x;
    float y = uv.y;

    // GPU'yu yor
    [loop]
    for(int i = 0; i < 5000; i++)
    {
        x = sin(x * 6.283 + y);
        y = cos(y * 6.283 + x);
    }

    return float4(x, y, abs(x-y), 1);
}
