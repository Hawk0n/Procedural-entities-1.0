using System;
using System.Collections.Generic;
using Terrain;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Helpers
{
    public struct HMyRenderMeshUtility
    {
        [Flags]
        internal enum EntitiesGraphicsComponentFlags
        {
            None = 0,
            GameObjectConversion = 1 << 0,
            InMotionPass = 1 << 1,
            LightProbesBlend = 1 << 2,
            LightProbesCustom = 1 << 3,
            DepthSorted = 1 << 4,
            Baking = 1 << 5,
        }
        
        
        internal class EntitiesGraphicsComponentTypes
        {
            private ComponentTypeSet[] m_ComponentTypePermutations;

            public EntitiesGraphicsComponentTypes()
            {
                // Subtract one because of "None"
                int numFlags = Enum.GetValues(typeof(EntitiesGraphicsComponentFlags)).Length - 1;

                var permutations = new List<ComponentTypeSet>();
                for (int flags = 0; flags < (1 << numFlags); ++flags)
                    permutations.Add(GenerateComponentTypes((EntitiesGraphicsComponentFlags)flags));

                m_ComponentTypePermutations = permutations.ToArray();
            }

            public ComponentTypeSet GetComponentTypes(EntitiesGraphicsComponentFlags flags) =>
                m_ComponentTypePermutations[(int) flags];

            public static ComponentTypeSet GenerateComponentTypes(EntitiesGraphicsComponentFlags flags)
            {
                List<ComponentType> components = new List<ComponentType>()
                {
                    // Absolute minimum set of components required by Entities Graphics
                    // to be considered for rendering. Entities without these components will
                    // not match queries and will never be rendered.
                    ComponentType.ReadWrite<WorldRenderBounds>(),
                    ComponentType.ReadWrite<RenderFilterSettings>(),
                    ComponentType.ReadWrite<MaterialMeshInfo>(),
                    ComponentType.ChunkComponent<ChunkWorldRenderBounds>(),
                    ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>(),
                    // Extra transform related components required to render correctly
                    // using many default SRP shaders. Custom shaders could potentially
                    // work without it.
                    ComponentType.ReadWrite<WorldToLocal_Tag>(),
                    // Components required by Entities Graphics package visibility culling.
                    ComponentType.ReadWrite<RenderBounds>(),
                    ComponentType.ReadWrite<PerInstanceCullingTag>(),
                };

                // RenderMesh is no longer used at runtime, it is only used during conversion.
                // At runtime all entities use RenderMeshArray.
                if (flags.HasFlag(EntitiesGraphicsComponentFlags.GameObjectConversion) | flags.HasFlag(EntitiesGraphicsComponentFlags.Baking) )
                    components.Add(ComponentType.ReadWrite<RenderMesh>());

                if (!flags.HasFlag(EntitiesGraphicsComponentFlags.GameObjectConversion) | flags.HasFlag(EntitiesGraphicsComponentFlags.Baking) )
                    components.Add(ComponentType.ReadWrite<RenderMeshArray>());

                // Baking uses TransformUsageFlags, and as such should not be explicitly adding LocalToWorld to anything
                if(!flags.HasFlag(EntitiesGraphicsComponentFlags.Baking))
                    components.Add(ComponentType.ReadWrite<LocalToWorld>());

                // Components required by objects that need to be rendered in per-object motion passes.
    #if USE_HYBRID_MOTION_PASS
                if (flags.HasFlag(EntitiesGraphicsComponentFlags.InMotionPass))
                    components.Add(ComponentType.ReadWrite<BuiltinMaterialPropertyUnity_MatrixPreviousM>());
    #endif

                if (flags.HasFlag(EntitiesGraphicsComponentFlags.LightProbesBlend))
                    components.Add(ComponentType.ReadWrite<BlendProbeTag>());
                else if (flags.HasFlag(EntitiesGraphicsComponentFlags.LightProbesCustom))
                    components.Add(ComponentType.ReadWrite<CustomProbeTag>());

                if (flags.HasFlag(EntitiesGraphicsComponentFlags.DepthSorted))
                    components.Add(ComponentType.ReadWrite<DepthSorted_Tag>());

                return new ComponentTypeSet(components.ToArray());
            }
        }

        internal static EntitiesGraphicsComponentTypes s_EntitiesGraphicsComponentTypes = new EntitiesGraphicsComponentTypes();

        // Use a boolean constant for guarding most of the code so both ifdef branches are
        // always compiled.
        // This leads to the following warning due to the other branch being unreachable, so disable it
        // warning CS0162: Unreachable code detected
#pragma warning disable CS0162

#if USE_HYBRID_MOTION_PASS
        internal const bool kUseHybridMotionPass = true;
#else
        internal const bool kUseHybridMotionPass = false;
#endif
        /// <summary>
        /// Set the Entities Graphics component values to render the given entity using the given description.
        /// Any missing components will be added, which results in structural changes.
        /// </summary>
        /// <param name="entity">The entity to set the component values for.</param>
        /// <param name="entityManager">The <see cref="EntityManager"/> used to set the component values.</param>
        /// <param name="renderMeshDescription">The description that determines how the entity is to be rendered.</param>
        /// <param name="renderMeshArray">The instance of the RenderMeshArray which contains mesh and material.</param>
        /// <param name="materialMeshInfo">The MaterialMeshInfo used to index into renderMeshArray.</param>
        public static void AddComponents(
            Entity entity,
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            in RenderMeshDescription renderMeshDescription,
            RenderMeshArray renderMeshArray,
            MaterialMeshInfo materialMeshInfo = default)
        {
            var material = renderMeshArray.GetMaterial(materialMeshInfo);
            var mesh = renderMeshArray.GetMesh(materialMeshInfo);

            // Entities with Static are never rendered with motion vectors
            bool inMotionPass = kUseHybridMotionPass &&
                                renderMeshDescription.FilterSettings.IsInMotionPass &&
                                !entityManager.HasComponent<Static>(entity);

            EntitiesGraphicsComponentFlags flags = EntitiesGraphicsComponentFlags.None;
            if (inMotionPass) flags |= EntitiesGraphicsComponentFlags.InMotionPass;
            flags |= LightProbeFlags(renderMeshDescription.LightProbeUsage);
            flags |= DepthSortedFlags(material);

            // Add all components up front using as few calls as possible.
            ecb.AddComponent(entity, s_EntitiesGraphicsComponentTypes.GetComponentTypes(flags));

            ecb.SetSharedComponentManaged(entity, renderMeshDescription.FilterSettings);
            ecb.SetSharedComponentManaged(entity, renderMeshArray);
            ecb.SetComponent(entity, materialMeshInfo);

            if (mesh != null)
            {
                var localBounds = mesh.bounds.ToAABB();
                ecb.SetComponent(entity, new RenderBounds { Value = localBounds });
            }
        }

#pragma warning restore CS0162
        internal static EntitiesGraphicsComponentFlags DepthSortedFlags(Material material)
        {
            if (IsMaterialTransparent(material))
                return EntitiesGraphicsComponentFlags.DepthSorted;
            else
                return EntitiesGraphicsComponentFlags.None;
        }


        /// <summary>
        /// Return true if the given <see cref="Material"/> is known to be transparent. Works
        /// for materials that use HDRP or URP conventions for transparent materials.
        /// </summary>
        private const string kSurfaceTypeHDRP = "_SurfaceType";
        private const string kSurfaceTypeURP = "_Surface";
        private static int kSurfaceTypeHDRPNameID = Shader.PropertyToID(kSurfaceTypeHDRP);
        private static int kSurfaceTypeURPNameID = Shader.PropertyToID(kSurfaceTypeURP);
        private static bool IsMaterialTransparent(Material material)
        {
            if (material == null)
                return false;

#if HDRP_10_0_0_OR_NEWER
            // Material.GetSurfaceType() is not public, so we try to do what it does internally.
            const int kSurfaceTypeTransparent = 1; // Corresponds to non-public SurfaceType.Transparent
            if (material.HasProperty(kSurfaceTypeHDRPNameID))
                return (int) material.GetFloat(kSurfaceTypeHDRPNameID) == kSurfaceTypeTransparent;
            else
                return false;
#elif URP_10_0_0_OR_NEWER
            const int kSurfaceTypeTransparent = 1; // Corresponds to SurfaceType.Transparent
            if (material.HasProperty(kSurfaceTypeURPNameID))
                return (int) material.GetFloat(kSurfaceTypeURPNameID) == kSurfaceTypeTransparent;
            else
                return false;
#else
            return false;
#endif
        }

        internal enum StaticLightingMode
        {
            None = 0,
            LightMapped = 1,
            LightProbes = 2,
        }

        internal static StaticLightingMode StaticLightingModeFromRenderer(Renderer renderer)
        {
            var staticLightingMode = StaticLightingMode.None;
            if (renderer.lightmapIndex >= 65534 || renderer.lightmapIndex < 0)
                staticLightingMode = StaticLightingMode.LightProbes;
            else if (renderer.lightmapIndex >= 0)
                staticLightingMode = StaticLightingMode.LightMapped;

            return staticLightingMode;
        }

        internal static EntitiesGraphicsComponentFlags LightProbeFlags(LightProbeUsage lightProbeUsage)
        {
            switch (lightProbeUsage)
            {
                case LightProbeUsage.BlendProbes:
                    return EntitiesGraphicsComponentFlags.LightProbesBlend;
                case LightProbeUsage.CustomProvided:
                    return EntitiesGraphicsComponentFlags.LightProbesCustom;
                default:
                    return EntitiesGraphicsComponentFlags.None;
            }
        }

        internal static string FormatRenderMesh(RenderMesh renderMesh) =>
            $"RenderMesh(material: {renderMesh.material}, mesh: {renderMesh.mesh}, subMesh: {renderMesh.subMesh})";

        internal static bool ValidateMesh(RenderMesh renderMesh)
        {
            if (renderMesh.mesh == null)
            {
                Debug.LogWarning($"RenderMesh must have a valid non-null Mesh. {FormatRenderMesh(renderMesh)}");
                return false;
            }
            else if (renderMesh.subMesh < 0 || renderMesh.subMesh >= renderMesh.mesh.subMeshCount)
            {
                Debug.LogWarning($"RenderMesh subMesh index out of bounds. {FormatRenderMesh(renderMesh)}");
                return false;
            }

            return true;
        }

        internal static bool ValidateMaterial(RenderMesh renderMesh)
        {
            if (renderMesh.material == null)
            {
                Debug.LogWarning($"RenderMesh must have a valid non-null Material. {FormatRenderMesh(renderMesh)}");
                return false;
            }

            return true;
        }

        internal static bool ValidateRenderMesh(RenderMesh renderMesh) =>
            ValidateMaterial(renderMesh) && ValidateMesh(renderMesh);
    }
}