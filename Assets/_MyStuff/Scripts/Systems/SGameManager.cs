using Unity.Entities;

namespace Terrain
{
    public partial class SGameManager : SystemBase
    {
        public Entity playerEntity;

        
        protected override void OnStartRunning()
        {
            playerEntity = SystemAPI.GetSingletonEntity<TPlayer>();
        }

        
        protected override void OnUpdate() { }
    }
}