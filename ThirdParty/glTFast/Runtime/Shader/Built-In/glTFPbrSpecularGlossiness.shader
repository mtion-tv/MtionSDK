

Shader "glTF/PbrSpecularGlossiness"
{
    Properties
    {
        [MainColor] baseColorFactor("Diffuse", Color) = (1,1,1,1)
        [MainTexture] baseColorTexture("Diffuse Tex", 2D) = "white" {}
        baseColorTexture_Rotation ("Diffuse Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] baseColorTexture_texCoord ("Diffuse Tex UV", Float) = 0

        alphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        glossinessFactor("Glossiness", Range(0.0, 1.0)) = 1

        specularFactor("Specular", Color) = (1,1,1)
        specularGlossinessTexture("Specular-Glossiness Tex", 2D) = "white" {}
        specularGlossinessTexture_Rotation ("Specular-Glossiness Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] specularGlossinessTexture_texCoord ("Specular-Glossiness Tex UV", Float) = 0

        normalTexture_scale("Normal Scale", Float) = 1.0
        [Normal] normalTexture("Normal Tex", 2D) = "bump" {}
        normalTexture_Rotation ("Normal Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] normalTexture_texCoord ("Normal Tex UV Set", Float) = 0


        occlusionTexture_strength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        occlusionTexture("Occlusion Tex", 2D) = "white" {}
        occlusionTexture_Rotation ("Occlusion Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] occlusionTexture_texCoord ("Occlusion Tex UV Set", Float) = 0

        [HDR] emissiveFactor("Emissive", Color) = (0,0,0)
        emissiveTexture("Emissive Tex", 2D) = "white" {}
        emissiveTexture_Rotation ("Emissive Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] emissiveTexture_texCoord ("Emissive Tex UV", Float) = 0
        




        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2.0
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
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0


            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma vertex vertBase
            #pragma fragment fragBaseFacing
            #include "glTFIncludes/glTFUnityStandardCoreForward.cginc"

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
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0


            #pragma shader_feature _NORMALMAP

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #pragma vertex vertAdd
            #pragma fragment fragAddFacing
            #include "glTFIncludes/glTFUnityStandardCoreForward.cginc"

            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0


            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM
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
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers nomrt



            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM

            #pragma multi_compile_prepassfinal
            #pragma multi_compile_instancing

            #pragma vertex vertDeferred
            #pragma fragment fragDeferredFacing

            #include "glTFIncludes/glTF.cginc"
            #include "glTFIncludes/glTFUnityStandardCore.cginc"

            ENDCG
        }

        Pass
        {
            Name "META"
            Tags { "LightMode"="Meta" }

            Cull Off

            CGPROGRAM
            #pragma vertex vert_meta
            #pragma fragment frag_meta

            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "glTFIncludes/glTFUnityStandardMeta.cginc"
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
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM

            #pragma skip_variants SHADOWS_SOFT DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #pragma vertex vertBase
            #pragma fragment fragBaseFacing
            #include "glTFIncludes/glTFUnityStandardCoreForward.cginc"

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
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM
            #pragma skip_variants SHADOWS_SOFT

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #pragma vertex vertAdd
            #pragma fragment fragAddFacing
            #include "glTFIncludes/glTFUnityStandardCoreForward.cginc"

            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM
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

            Cull Off

            CGPROGRAM
            #pragma vertex vert_meta
            #pragma fragment frag_meta

            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSION
            #pragma shader_feature_local _TEXTURE_TRANSFORM
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "glTFIncludes/glTFUnityStandardMeta.cginc"
            ENDCG
        }
    }

    FallBack "VertexLit"
    CustomEditor "GLTFast.Editor.BuiltInShaderGUI"
}
