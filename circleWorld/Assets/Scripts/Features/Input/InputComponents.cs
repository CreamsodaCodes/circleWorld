using Unity.Entities;
using Unity.Mathematics;

namespace CircleWorld.Features.Input
{
    public struct InputData : IComponentData
    {
        public float2 MouseWorldPosition;
        public bool IsClicking;
    }
}
