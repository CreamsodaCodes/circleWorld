using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using CircleWorld.Features.Cell;
using CircleWorld.Features.Physics;

namespace CircleWorld.Features.Reproduction
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ReproductionSystem : ISystem
    {
        private const float COST = 50.0f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 1. Identification Phase
            // We need to find Reproducer cells that have enough energy.
            // We cannot execute structural changes (Instantiate) while iterating via SystemAPI,
            // so we collect the candidates first.
            
            var candidates = new NativeList<Entity>(Allocator.Temp);

            foreach (var (energy, type, entity) in SystemAPI.Query<RefRW<Energy>, RefRO<CellType>>()
                         .WithEntityAccess())
            {
                if (type.ValueRO.Value == CellKind.Reproducer && energy.ValueRO.Value >= COST)
                {
                    energy.ValueRW.Value -= COST; // Pay the cost immediately
                    candidates.Add(entity);
                }
            }

            if (candidates.Length == 0)
            {
                candidates.Dispose();
                return;
            }

            // 2. Processing Phase
            // For each candidate, we traverse its graph and clone it.
            foreach (var root in candidates)
            {
                ReproduceOrganism(ref state, root);
            }

            candidates.Dispose();
        }

        private void ReproduceOrganism(ref SystemState state, Entity rootSpecimen)
        {
            // A. Traversal (BFS) to gather the whole organism
            var visited = new NativeHashSet<Entity>(128, Allocator.Temp);
            var queue = new NativeQueue<Entity>(Allocator.Temp);
            
            queue.Enqueue(rootSpecimen);
            visited.Add(rootSpecimen);

            while (queue.TryDequeue(out Entity current))
            {
                // Check connections
                if (state.EntityManager.HasBuffer<Constraint>(current))
                {
                    var constraints = state.EntityManager.GetBuffer<Constraint>(current);
                    foreach (var constraint in constraints)
                    {
                        if (!visited.Contains(constraint.Target))
                        {
                            visited.Add(constraint.Target);
                            queue.Enqueue(constraint.Target);
                        }
                    }
                }
            }
            queue.Dispose();

            // Convert to array for batch instantiation
            var originalEntities = visited.ToNativeArray(Allocator.Temp);
            visited.Dispose();

            // B. Cloning
            // Instantiate the whole set.
            var clonedEntities = new NativeArray<Entity>(originalEntities.Length, Allocator.Temp);
            state.EntityManager.Instantiate(originalEntities, clonedEntities);

            // C. Remapping
            // We need a map from Old -> New to fix the constraints.
            var map = new NativeHashMap<Entity, Entity>(originalEntities.Length, Allocator.Temp);
            for (int i = 0; i < originalEntities.Length; i++)
            {
                map.Add(originalEntities[i], clonedEntities[i]);
            }

            var offset = new float2(5, 5); // Simple offset for now

            // Process clones
            for (int i = 0; i < clonedEntities.Length; i++)
            {
                Entity clone = clonedEntities[i];

                // 1. Remap Constraints
                if (state.EntityManager.HasBuffer<Constraint>(clone))
                {
                    var buffer = state.EntityManager.GetBuffer<Constraint>(clone);
                    for (int k = 0; k < buffer.Length; k++)
                    {
                        var c = buffer[k];
                        // If the target is part of the cloned group, remap it.
                        // If it's outside (shouldn't happen in a clean organism), keep it or break it?
                        // For now, assume strict internal connectivity.
                        if (map.TryGetValue(c.Target, out Entity newTarget))
                        {
                            c.Target = newTarget;
                            buffer[k] = c;
                        }
                    }
                }

                // 2. Offset Position
                if (state.EntityManager.HasComponent<Position>(clone))
                {
                    var pos = state.EntityManager.GetComponentData<Position>(clone);
                    pos.Value += offset;
                    state.EntityManager.SetComponentData(clone, pos);
                }
                
                // 3. Reset State (Velocity)
                if (state.EntityManager.HasComponent<PreviousPosition>(clone))
                {
                    var pos = state.EntityManager.GetComponentData<Position>(clone);
                    state.EntityManager.SetComponentData(clone, new PreviousPosition { Value = pos.Value });
                }
            }

            map.Dispose();
            originalEntities.Dispose();
            clonedEntities.Dispose();
        }
    }
}
