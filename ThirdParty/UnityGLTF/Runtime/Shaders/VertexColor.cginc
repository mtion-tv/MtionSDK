#ifndef VERTEX_COLOR_INCLUDED
#define VERTEX_COLOR_INCLUDED

#include "UnityCG.cginc"
#include "UnityStandardInput.cginc"
#include "UnityStandardCoreForward.cginc"

struct WrappedVertexOutput {
#if UNITY_STANDARD_SIMPLE
    VertexOutputBaseSimple innerValue;
#else
    VertexOutputForwardBase innerValue;
#endif
    fixed4 color : COLOR;
};

WrappedVertexOutput vert_vcol(appdata_full v)
{
    WrappedVertexOutput o;
    o.color = v.color;

    VertexInput vi;
    vi.vertex = v.vertex;
    vi.normal = v.normal;
    vi.uv0 = v.texcoord;
    vi.uv1 = v.texcoord1;
#if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
    vi.uv2 = v.tetcoord2;
#endif
#ifdef _TANGENT_TO_WORLD
    vi.tangent = v.tangent;
#endif
    o.innerValue = vertBase(vi);

    return o;
}

#if UNITY_STANDARD_SIMPLE
half4 frag_vcol(WrappedVertexOutput wvo) : SV_Target
{
    VertexOutputBaseSimple i = wvo.innerValue;

    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    FragmentCommonData s = FragmentSetupSimple(i);

    UnityLight mainLight = MainLightSimple(i, s);

#if !defined(LIGHTMAP_ON) && defined(_NORMALMAP)
    half ndotl = saturate(dot(s.tangentSpaceNormal, i.tangentSpaceLightDir));
#else
    half ndotl = saturate(dot(s.normalWorld, mainLight.dir));
#endif

    half shadowMaskAttenuation = UnitySampleBakedOcclusion(i.ambientOrLightmapUV, 0);
    half realtimeShadowAttenuation = SHADOW_ATTENUATION(i);
    half atten = UnityMixRealtimeAndBakedShadows(realtimeShadowAttenuation, shadowMaskAttenuation, 0);

    s.diffColor.x *= wvo.color.x;
    s.diffColor.y *= wvo.color.y;
    s.diffColor.z *= wvo.color.z;
	s.alpha *= wvo.color.a;

    half occlusion = Occlusion(i.tex.xy);
    half rl = dot(REFLECTVEC_FOR_SPECULAR(i, s), LightDirForSpecular(i, mainLight));

    UnityGI gi = FragmentGI(s, occlusion, i.ambientOrLightmapUV, atten, mainLight);
    half3 attenuatedLightColor = gi.light.color * ndotl;

    half3 c = BRDF3_Indirect(s.diffColor, s.specColor, gi.indirect, PerVertexGrazingTerm(i, s), PerVertexFresnelTerm(i));
    c += BRDF3DirectSimple(s.diffColor, s.specColor, s.smoothness, rl) * attenuatedLightColor;
    c += Emission(i.tex.xy);

    UNITY_APPLY_FOG(i.fogCoord, c);

    return OutputForward(half4(c, 1), s.alpha);
}
#else
half4 frag_vcol(WrappedVertexOutput wvo) : SV_Target
{
    VertexOutputForwardBase i = wvo.innerValue;

    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    FRAGMENT_SETUP(s)

    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    UnityLight mainLight = MainLight();
    UNITY_LIGHT_ATTENUATION(atten, i, s.posWorld);

    s.diffColor.x *= wvo.color.x;
    s.diffColor.y *= wvo.color.y;
    s.diffColor.z *= wvo.color.z;
	s.alpha *= wvo.color.a;

    half occlusion = Occlusion(i.tex.xy);
    UnityGI gi = FragmentGI(s, occlusion, i.ambientOrLightmapUV, atten, mainLight);

    half4 c = UNITY_BRDF_PBS(s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, -s.eyeVec, gi.light, gi.indirect);
    c.rgb += Emission(i.tex.xy);

    UNITY_EXTRACT_FOG_FROM_EYE_VEC(i);
    UNITY_APPLY_FOG(_unity_fogCoord, c.rgb);
    return OutputForward(c, s.alpha);
}
#endif
#endif
