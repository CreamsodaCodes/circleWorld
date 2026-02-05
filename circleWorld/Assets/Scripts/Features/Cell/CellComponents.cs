using Unity.Entities;
using Unity.Mathematics;

namespace CircleWorld.Features.Cell
{
    public struct Position : IComponentData
    {
        public float2 Value;
    }

    public struct PreviousPosition : IComponentData
    {
        public float2 Value;
    }

    public struct PhysicsProperties : IComponentData
    {
        public float Radius;
        public float Friction;
        public float Bounciness;
    }

    public struct OrganismID : IComponentData
    {
        public Entity Value;
    }

    public struct Constraint : IBufferElementData
    {
        public Entity Target;
        public float RestLength;
        public float Stiffness;
    }
}
