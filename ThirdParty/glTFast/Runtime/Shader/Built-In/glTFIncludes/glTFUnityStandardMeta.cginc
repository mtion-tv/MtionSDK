


#ifndef UNITY_STANDARD_META_INCLUDED
#define UNITY_STANDARD_META_INCLUDED


#include "UnityCG.cginc"
#include "glTFUnityStandardInput.cginc"
#include "UnityMetaPass.cginc"
#include "glTFUnityStandardCore.cginc"

struct v2f_meta
{
    float4 pos      : SV_POSITION;
    float4 uv       : TEXCOORD0;
#ifdef EDITOR_VISUALIZATION
    float2 vizUV        : TEXCOORD1;
    float4 lightCoord   : TEXCOORD2;
#endif
    half4 color :COLOR;
};

v2f_meta vert_meta (VertexInput v)
{
    v2f_meta o;
#ifdef UNITY_COLORSPACE_GAMMA
    o.color.rgb = LinearToGammaSpace(v.color.rgb);
    o.color.a = v.color.a;
#else
    o.color = v.color;
#endif
    o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);

    o.uv.xy = TexCoordsSingle((baseColorTexture_texCoord==0)?v.uv0:v.uv1,baseColorTexture);
#ifdef _NORMALMAP
    o.uv.zw = TexCoordsSingle((normalTexture_texCoord==0)?v.uv0:v.uv1,normalTexture);
#else
    o.uv.zw = float2(0,0);
#endif
    
#ifdef EDITOR_VISUALIZATION
    o.vizUV = 0;
    o.lightCoord = 0;
    if (unity_VisualizationMode == EDITORVIZ_TEXTURE)
        o.vizUV = UnityMetaVizUV(unity_EditorViz_UVIndex, v.uv0.xy, v.uv1.xy, v.uv2.xy, unity_EditorViz_Texture_ST);
    else if (unity_VisualizationMode == EDITORVIZ_SHOWLIGHTMASK)
    {
        o.vizUV = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
        o.lightCoord = mul(unity_EditorViz_WorldToLight, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));
    }
#endif
    return o;
}

half3 UnityLightmappingAlbedo (half3 diffuse, half3 specular, half smoothness)
{
    half roughness = SmoothnessToRoughness(smoothness);
    half3 res = diffuse;
    res += specular * roughness * 0.5;
    return res;
}

float4 frag_meta (v2f_meta i) : SV_Target
{
    
#ifdef _METALLICGLOSSMAP
    FragmentCommonData data = UNITY_SETUP_BRDF_INPUT (i.uv,i.uv,i.color);
#else
    FragmentCommonData data = UNITY_SETUP_BRDF_INPUT (i.uv,i.color);
#endif

    
    UnityMetaInput o;
    UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);

#ifdef EDITOR_VISUALIZATION
    o.Albedo = data.diffColor;
    o.VizUV = i.vizUV;
    o.LightCoord = i.lightCoord;
#else
    o.Albedo = UnityLightmappingAlbedo (data.diffColor, data.specColor, data.smoothness);
#endif
    o.SpecularColor = data.specColor;
    o.Emission = Emission(i.uv.xy);

    return UnityMetaFragment(o);
}

#endif // UNITY_STANDARD_META_INCLUDED
