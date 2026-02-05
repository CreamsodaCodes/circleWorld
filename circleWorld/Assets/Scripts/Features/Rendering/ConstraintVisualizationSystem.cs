using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Transforms;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ConstraintVisualizationSystem : ISystem
    {
        private NativeHashMap<EdgeKey, Entity> _paramVisuals;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderConfig>();
            _paramVisuals = new NativeHashMap<EdgeKey, Entity>(1000, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_paramVisuals.IsCreated) _paramVisuals.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<RenderConfig>()) return;
            
            var config = SystemAPI.GetSingleton<RenderConfig>();
            // Use local EM reference for structural changes
            var entityManager = state.EntityManager;

            // 1. Collect all active edges this frame to a set
            // We use a temporary map or set. 
            var activeEdges = new NativeHashSet<EdgeKey>(1000, Allocator.Temp);
            
            // We need component lookup to read targets
            var positionLookup = SystemAPI.GetComponentLookup<Position>(true);
            
            // We iterate with structural changes enabled so we can Create/Destroy immediately 
            // and update our persistent map freely.
            // Note: This prevents Burst, but this system is Main Thread Logic glue anyway.
            
            Entities
                .WithoutBurst() 
                .WithStructuralChanges()
                .ForEach((Entity entity, in DynamicBuffer<Constraint> constraints, in Position posA) =>
            {
                for (int i = 0; i < constraints.Length; i++)
                {
                    Entity target = constraints[i].Target;
                    if (target == Entity.Null) continue;
                    
                    // Create Key
                    EdgeKey key = new EdgeKey(entity, target);
                    
                    // Avoid processing same edge twice (A-B and B-A produce same key).
                    // We can check if we already added it to activeEdges this frame.
                    if (activeEdges.Contains(key)) continue;
                    
                    activeEdges.Add(key);

                    // Check if exists
                    if (_paramVisuals.TryGetValue(key, out Entity visualEntity))
                    {
                        // Update Transform of visual
                        if (positionLookup.HasComponent(target))
                        {
                           float2 pA = posA.Value;
                           float2 pB = positionLookup[target].Value;
                           
                           float2 mid = (pA + pB) * 0.5f;
                           float2 dir = pB - pA;
                           float len = math.length(dir);
                           float angle = math.atan2(dir.y, dir.x);
                           
                           // Update LocalTransform
                           var t = LocalTransform.FromPositionRotationScale(
                               new float3(mid.x, mid.y, 0),
                               quaternion.RotateZ(angle),
                               1f 
                           );
                           
                           // Calculate Matrix for scaling
                           // Length X, Thickness 0.2f (Hardcoded thin line for now)
                           float4x4 matrix = float4x4.TRS(
                               float3.zero, 
                               quaternion.identity,
                               new float3(len, 0.2f, 1f) 
                           );
                           
                           entityManager.SetComponentData(visualEntity, t);
                           // Ensure it has PostTransformMatrix (we add it on creation, but good to set value)
                           // Check if component exists or just set it? Start with Set, assumes added.
                           if (entityManager.HasComponent<PostTransformMatrix>(visualEntity))
                           {
                               entityManager.SetComponentData(visualEntity, new PostTransformMatrix { Value = matrix });
                           }
                        }
                    }
                    else
                    {
                        // Create New
                        Entity newVisual = entityManager.Instantiate(config.LinePrefab);
                        
                        // Add PostTransformMatrix if missing
                        if (!entityManager.HasComponent<PostTransformMatrix>(newVisual))
                            entityManager.AddComponent<PostTransformMatrix>(newVisual);
                            
                        // Add LocalTransform if missing (it should be there)
                        if (!entityManager.HasComponent<LocalTransform>(newVisual))
                            entityManager.AddComponent<LocalTransform>(newVisual);

                        _paramVisuals.Add(key, newVisual);
                    }
                }
            }).Run();

            // Cleanup removed edges
            // Identify keys in _paramVisuals that are NOT in activeEdges
            var keysToRemove = new NativeList<EdgeKey>(Allocator.Temp);
            foreach (var kvp in _paramVisuals)
            {
                if (!activeEdges.Contains(kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                Entity e = _paramVisuals[key];
                if (entityManager.Exists(e))
                {
                    entityManager.DestroyEntity(e);
                }
                _paramVisuals.Remove(key);
            }
            
            activeEdges.Dispose();
            keysToRemove.Dispose();
        }
    }
}
