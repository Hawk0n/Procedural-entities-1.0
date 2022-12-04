using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    public class MDPlanetTerrainMono : MonoBehaviour
    {
        public Material baseMaterial;
        public Mesh testMesh;
        public GameObject baseChunkPrefab;
        public ComputeShader densityShader, marchShader;
        public DPlanetSettings dPlanetSettings;
        public int[] lodRangeArray;
        public DTerrainNoiseLayer[] dTerrainNoiseLayers;
    }
    
    
    public class MDPlanetTerrain : IComponentData
    {
        public Material baseMaterial;
        public Mesh testMesh;
        public Entity baseChunkPrefab;
        public ComputeShader densityShader, marchShader;
        public DPlanetSettings dPlanetSettings;
        public int[] lodRangeArray;
        public DTerrainNoiseLayer[] dTerrainNoiseLayers;
    }

    public class TerrainManagerDataBaker : Baker<MDPlanetTerrainMono>
    {
        public override void Bake(MDPlanetTerrainMono authoring)
        {
            AddComponentObject(new MDPlanetTerrain {
                baseMaterial = authoring.baseMaterial,
                testMesh = authoring.testMesh,
                baseChunkPrefab = GetEntity(authoring.baseChunkPrefab),
                densityShader = authoring.densityShader,
                marchShader = authoring.marchShader,
                dPlanetSettings = authoring.dPlanetSettings,
                lodRangeArray = authoring.lodRangeArray,
                dTerrainNoiseLayers = authoring.dTerrainNoiseLayers
            } );
        }
    }
}