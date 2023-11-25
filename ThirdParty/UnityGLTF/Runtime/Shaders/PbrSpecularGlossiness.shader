
Shader "GLTF/PbrSpecularGlossiness"
{
	Properties
	{
		_Color("Base Color Factor", Color) = (1,1,1,1)
		_MainTex("Base Color Texture", 2D) = "white" {}

		[Toggle(_VERTEX_COLORS)] _VertexColors("Vertex Colors", Float) = 1.0

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		
		_Glossiness("Glossiness Factor", Range(0.0, 1.0)) = 0.5
		[HideInInspector]  _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

		_SpecColor("Specular Factor", Color) = (0.2,0.2,0.2)
		_SpecGlossMap("Spec Gloss Texture", 2D) = "white" {}
		[HideInInspector]  _SpecularHighlights("Specular Highlights", Float) = 1.0
		[HideInInspector]  _GlossyReflections("Glossy Reflections", Float) = 1.0

		_BumpScale("Normal Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		[HideInInspector] _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
		[HideInInspector] _ParallaxMap ("Height Map", 2D) = "black" {}

		_OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
		_OcclusionMap("Occlusion Texture", 2D) = "white" {}

		_EmissionColor("Emissive Factor", Color) = (0,0,0)
		_EmissionMap("Emissive Texture", 2D) = "white" {}

		_Cull("Cull", Int) = 0

		[HideInInspector] _DetailMask("Detail Mask", 2D) = "white" {}

		[HideInInspector] _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
		[HideInInspector] _DetailNormalMapScale("Scale", Float) = 1.0
		[HideInInspector] _DetailNormalMap("Normal Map", 2D) = "bump" {}

		[HideInInspector]  _UVSec ("UV Set for secondary textures", Float) = 0


		[HideInInspector] _Mode ("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend ("__src", Float) = 1.0
		[HideInInspector] _DstBlend ("__dst", Float) = 0.0
		[HideInInspector] _ZWrite ("__zw", Float) = 1.0
	}

	CGINCLUDE
		#define UNITY_SETUP_BRDF_INPUT SpecularSetup
	ENDCG
	
	SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
		LOD 300
		

		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			Cull [_Cull]

			CGPROGRAM
			#pragma target 3.0


			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _EMISSION
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#pragma vertex vert_vcol
			#pragma fragment frag_vcol
			#pragma shader_feature _VERTEX_COLORS
			#include "VertexColor.cginc"

			ENDCG
		}
		Pass
		{
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend [_SrcBlend] One
			Fog { Color (0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			ZTest LEqual
			Cull[_Cull]

			CGPROGRAM
			#pragma target 3.0


			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog

			#pragma vertex vertAdd
			#pragma fragment fragAdd
			#include "UnityStandardInput.cginc"
			#include "UnityStandardCoreForward.cginc"

			ENDCG
		}
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On ZTest LEqual
			Cull[_Cull]

			CGPROGRAM
			#pragma target 3.0



			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _PARALLAXMAP
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityStandardShadow.cginc"

			ENDCG
		}
		Pass
		{
			Name "DEFERRED"
			Tags { "LightMode" = "Deferred" }
			Cull[_Cull]

			CGPROGRAM
			#pragma target 3.0
			#pragma exclude_renderers nomrt



			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _EMISSION
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile_prepassfinal
			#pragma multi_compile_instancing

			#pragma vertex vertDeferred
			#pragma fragment fragDeferred

			#include "UnityStandardCore.cginc"

			ENDCG
		}

		Pass
		{
			Name "META" 
			Tags { "LightMode"="Meta" }

			Cull[_Cull]

			CGPROGRAM
			#pragma vertex vert_meta
			#pragma fragment frag_meta

			#pragma shader_feature _EMISSION
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature EDITOR_VISUALIZATION

			#include "UnityStandardMeta.cginc"
			ENDCG
		}
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
		LOD 150

		Pass
		{
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			Cull[_Cull]

			CGPROGRAM
			#pragma target 2.0
			
			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _EMISSION 
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
			#pragma shader_feature ___ _DETAIL_MULX2

			#pragma skip_variants SHADOWS_SOFT DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED
			
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog

			#pragma vertex vert_vcol
			#pragma fragment frag_vcol
			#pragma shader_feature _VERTEX_COLORS
			#include "VertexColor.cginc"

			ENDCG
		}

		Pass
		{
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend [_SrcBlend] One
			Fog { Color (0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			ZTest LEqual
			Cull[_Cull]

			CGPROGRAM
			#pragma target 2.0

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma skip_variants SHADOWS_SOFT
			
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			
			#pragma vertex vertAdd
			#pragma fragment fragAdd
			#include "UnityStandardInput.cginc"
			#include "UnityStandardCoreForward.cginc"

			ENDCG
		}
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			ZWrite On ZTest LEqual
			Cull[_Cull]

			CGPROGRAM
			#pragma target 2.0

			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _SPECGLOSSMAP
			#pragma skip_variants SHADOWS_SOFT
			#pragma multi_compile_shadowcaster

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityStandardShadow.cginc"

			ENDCG
		}
		Pass
		{
			Name "META" 
			Tags { "LightMode"="Meta" }

			Cull[_Cull]

			CGPROGRAM
			#pragma vertex vert_meta
			#pragma fragment frag_meta

			#pragma shader_feature _EMISSION
			#pragma shader_feature _SPECGLOSSMAP
			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature EDITOR_VISUALIZATION

			#include "UnityStandardMeta.cginc"
			ENDCG
		}
	}

	FallBack "VertexLit"
	CustomEditor "PbrShaderGUI"
}
