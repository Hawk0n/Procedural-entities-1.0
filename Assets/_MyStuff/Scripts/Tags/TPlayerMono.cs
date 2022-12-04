using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    public struct TPlayer : IComponentData
    {
        
    }
    
    public class TPlayerMono : MonoBehaviour {}

    public class TPlayerBaker : Baker<TPlayerMono>
    {
        public override void Bake(TPlayerMono authoring)
        {
            AddComponent<TPlayer>();
        }
    }
}