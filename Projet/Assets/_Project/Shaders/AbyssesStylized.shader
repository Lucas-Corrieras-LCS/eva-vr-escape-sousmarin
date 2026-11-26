// Direction artistique « illustration sous-marine » (sombre) pour Quest 2.
//
// Reprend tel quel l'éclairage « Simple Lit » d'URP (lightmaps bakées, sondes, lumière de cabine par couche de rendu,
// brouillard, rendu stéréo instancié du casque) et ne change que la fin du calcul :
//   1. texture adoucie : la tôle n'est plus qu'un léger grain, l'objet se lit par sa couleur (aplats doux) ;
//   2. dégradé vertical : sombre au ras du sol, plus clair vers le plafond (profondeur, comme un fond marin) ;
//   3. contour lumineux : les bords des objets et des plongeurs captent une lueur colorée, ils se détachent du noir
//      (force par matériau dans _SpecColor.a, qui ne sert plus aux reflets ; atténué avec la distance).
// Pas de normal map ni de reflets spéculaires : moins coûteux que le Lit précédent.
//
// Réglages globaux (palette de l'étage), posés par FloorAmbience / SurfaceWorld via AbyssStyle :
//   _AbyssLow / _AbyssHigh (rgb) : teinte au sol / au plafond ; _AbyssHigh.a = 1 active le style (0 = rendu normal) ;
//   _AbyssRim (rgb, a = intensité) ; _AbyssParams = (grain de texture 0..1, hauteur basse, hauteur haute, finesse du contour).
Shader "Abysses/Stylized"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB)", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.2
        _SpecColor("Specular Color", Color) = (0, 0, 0, 0)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SmoothnessSource("Smoothness Source", Float) = 0.0
        _SpecularHighlights("Specular Highlights", Float) = 0.0
        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}

        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Shininess("Smoothness", Float) = 0.0
        [HideInInspector] _GlossinessSource("GlossinessSource", Float) = 0.0
        [HideInInspector] _SpecSource("SpecularHighlights", Float) = 0.0
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex LitPassVertexSimple
            #pragma fragment StylizedFragment

            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define BUMP_SCALE_NOT_SUPPORTED 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"

            // Palette de l'étage (globales : hors du CBUFFER matériau, le SRP Batcher reste actif).
            half4 _AbyssLow;
            half4 _AbyssHigh;
            half4 _AbyssRim;
            half4 _AbyssParams;

            void StylizedFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Style actif seulement si une palette a été posée (sinon : rendu Simple Lit normal, jamais noir).
                half styled = step(0.5h, _AbyssHigh.a);

                SurfaceData surfaceData;
                InitializeSimpleLitSurfaceData(input.uv, surfaceData);
                // 1. Texture adoucie : on revient vers la couleur du matériau, la texture ne garde qu'un grain léger.
                half grain = lerp(1.0h, _AbyssParams.x, styled);
                surfaceData.albedo = lerp(_BaseColor.rgb, surfaceData.albedo, grain);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
                InitializeBakedGIData(input, inputData);

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);

                // 2. Dégradé vertical (sol sombre → plafond plus clair).
                half height = smoothstep(_AbyssParams.y, _AbyssParams.z, inputData.positionWS.y);
                half3 gradient = lerp(_AbyssLow.rgb, _AbyssHigh.rgb, height);
                color.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), gradient, styled);

                // 3. Contour lumineux : plus fort sur les surfaces claires, discret sur les sombres.
                half facing = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half rim = pow(1.0h - facing, max(_AbyssParams.w, 0.5h));
                // Dosage par matériau (_SpecColor.a, inutilisé sans reflets : murs discrets, personnages forts)
                // et atténuation avec la distance (les grands murs vus de biais au loin ne se délavent pas).
                float distance = length(GetCameraPositionWS() - inputData.positionWS);
                rim *= _SpecColor.a * saturate(3.0 / max(distance, 0.001));
                color.rgb += rim * _AbyssRim.rgb * (_AbyssRim.a * styled) * (0.35h + 0.65h * Luminance(surfaceData.albedo));

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Simple Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Simple Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Simple Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Simple Lit/META"
        UsePass "Universal Render Pipeline/Simple Lit/MOTIONVECTORS"
        UsePass "Universal Render Pipeline/Simple Lit/XRMOTIONVECTORS"
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
