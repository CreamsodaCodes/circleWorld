using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Rendering.Tests
{
    public class RenderTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = World.DefaultGameObjectInjectionWorld = World.Create();
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void CellTransformSystem_SynchronizesPosition_ToLocalTransform()
        {
            // 1. Setup
            var entity = _entityManager.CreateEntity(typeof(Position), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new Position { Value = new float2(100, 50) });
            _entityManager.SetComponentData(entity, LocalTransform.Identity);

            // 2. Execute
            var system = _world.GetOrCreateSystem<CellTransformSystem>();
            system.Update(_world.Unmanaged);

            // 3. Assert
            var transform = _entityManager.GetComponentData<LocalTransform>(entity);
            Assert.AreEqual(100f, transform.Position.x, 0.001f);
            Assert.AreEqual(50f, transform.Position.y, 0.001f);
            // Default scale 1
            Assert.AreEqual(1f, transform.Scale, 0.001f);
        }

        [Test]
        public void ConstraintVisualizationSystem_CreatesVisuals_ForConstraints()
        {
            // 1. Setup Config
            var prefab = _entityManager.CreateEntity(); // Empty prefab
            _entityManager.AddComponent<LocalTransform>(prefab); // Ensure prefab has transform
            var configEntity = _entityManager.CreateEntity(typeof(RenderConfig));
            _entityManager.SetComponentData(configEntity, new RenderConfig { LinePrefab = prefab });

            // 2. Setup Entities with Constraint
            var entityA = _entityManager.CreateEntity(typeof(Position));
            var entityB = _entityManager.CreateEntity(typeof(Position));
            _entityManager.SetComponentData(entityA, new Position { Value = new float2(0, 0) });
            _entityManager.SetComponentData(entityB, new Position { Value = new float2(10, 0) });

            // Add Constraint to A pointing to B
            _entityManager.AddComponent<Constraint>(entityA);
            var buffer = _entityManager.GetBuffer<Constraint>(entityA);
            buffer.Add(new Constraint { Target = entityB, RestLength = 10, Stiffness = 1 });

            // 3. Execute
            var system = _world.GetOrCreateSystem<ConstraintVisualizationSystem>();
            system.Update(_world.Unmanaged);

            // 4. Assert
            // We expect the system to have created 1 NEW entity (the visual).
            // Total Entities = Prefab(1) + Config(1) + A(1) + B(1) + Visual(1) = 5
            var allEntities = _entityManager.GetAllEntities(Allocator.Temp);
            
            // Note: NUnit creates some entities? No, fresh World.
            Assert.AreEqual(5, allEntities.Length);
            
            // Find Entity with PostTransformMatrix (added by system)
            int visualCount = 0;
            Entity visualE = Entity.Null;
            foreach (var e in allEntities)
            {
                if (_entityManager.HasComponent<PostTransformMatrix>(e))
                {
                    visualCount++;
                    visualE = e;
                }
            }
            Assert.AreEqual(1, visualCount, "Should have created exactly 1 visual entity");

            // Check Transform of visual
            var t = _entityManager.GetComponentData<LocalTransform>(visualE);
            // Midpoint of (0,0) and (10,0) is (5,0)
            Assert.AreEqual(5f, t.Position.x, 0.001f); 
            
            allEntities.Dispose();
        }
    }
}
