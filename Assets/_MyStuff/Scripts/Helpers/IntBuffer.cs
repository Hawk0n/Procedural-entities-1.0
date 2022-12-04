using Unity.Entities;
using UnityEngine.Rendering;

namespace Helpers
{
    [InternalBufferCapacity(1)]
    public struct IntBuffer : IBufferElementData
    {
        public static implicit operator int(IntBuffer e) { return e.value; }
        public static implicit operator IntBuffer(int e) { return new IntBuffer { value = e }; }

        public int value;
    }
}