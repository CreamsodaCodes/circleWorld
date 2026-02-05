using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using CircleWorld.Features.Cell;
using CircleWorld.Features.Input;

namespace CircleWorld.Features.Spawning
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SpawningSystem : SystemBase
    {
        private EntityArchetype _cellArchetype;
        private EntityArchetype _foodArchetype;
        private double _lastSpawnTime;
        private const double SpawnCooldown = 0.2; // Seconds between spawns

        protected override void OnCreate()
        {
            _cellArchetype = EntityManager.CreateArchetype(
                typeof(LocalToWorld),
                typeof(LocalTransform),
                typeof(Position),
                typeof(PreviousPosition),
                typeof(PhysicsProperties),
                typeof(OrganismID),
                typeof(CellType),
                typeof(EffectColor) // Using EffectColor for rendering as established in RenderSystem
            );

            _foodArchetype = EntityManager.CreateArchetype(
                typeof(LocalToWorld),
                typeof(LocalTransform),
                typeof(Position),
                typeof(PreviousPosition),
                typeof(PhysicsProperties), // Food might need physics to bounce
                typeof(CellType),
                typeof(Energy),
                typeof(EffectColor)
            );
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<InputData>()) return;

            var input = SystemAPI.GetSingleton<InputData>();
            
            if (input.IsClicking && (SystemAPI.Time.ElapsedTime - _lastSpawnTime > SpawnCooldown))
            {
                _lastSpawnTime = SystemAPI.Time.ElapsedTime;
                
                var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

                // Simple logic: Randomly decide what to spawn or alternate
                // For now, let's spawn a Structure Cell by default, or Food if shift held (but we only have IsClicking)
                // Let's spawn a Structure Cell.
                
                var entity = ecb.CreateEntity(_cellArchetype);
                
                // Initialize Cell
                ecb.SetComponent(entity, new LocalTransform { Position = new float3(input.MouseWorldPosition, 0), Scale = 0.5f });
                ecb.SetComponent(entity, new Position { Value = input.MouseWorldPosition });
                ecb.SetComponent(entity, new PreviousPosition { Value = input.MouseWorldPosition });
                ecb.SetComponent(entity, new PhysicsProperties { Radius = 0.5f, Friction = 0.5f, Bounciness = 0.5f });
                ecb.SetComponent(entity, new CellType { Value = CellKind.Structure });
                ecb.SetComponent(entity, new OrganismID { Value = Entity.Null }); // New independent organism
                ecb.SetComponent(entity, new EffectColor { Value = new float4(0, 0.8f, 1, 1) }); // Cyan

                // Also initialize dynamic buffers if needed? For now, no constraints.
                
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }
        }
    }
}
