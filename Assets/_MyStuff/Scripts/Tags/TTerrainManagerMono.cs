using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    public struct TTerrainManager : IComponentData
    {
        
    }
    
    public class TTerrainManagerMono : MonoBehaviour
    {
        
    }
    
    public class TTerrainManagerBaker : Baker<TTerrainManagerMono>
    {
        public override void Bake(TTerrainManagerMono authoring)
        {
            AddComponent<TTerrainManager>();
        }
    }
}