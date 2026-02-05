using Unity.Entities;

namespace CircleWorld.Features.Cell
{
    public enum CellKind
    {
        Structure,
        Connector,
        Mouth,
        Storage,
        Reproducer,
        Food
    }

    public struct CellType : IComponentData
    {
        public CellKind Value;
    }
}
