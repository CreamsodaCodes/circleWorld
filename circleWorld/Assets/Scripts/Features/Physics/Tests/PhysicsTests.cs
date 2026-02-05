using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics
{
    public class PhysicsTests
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
        public void IntegrationSystem_UpdatesPosition_BasedOnVelocity()
        {
            // 1. Setup
            var entity = _entityManager.CreateEntity(
                typeof(Position), 
                typeof(PreviousPosition), 
                typeof(PhysicsProperties)
            );

            // Initial State:
            // Pos = (10, 10)
            // PrevPos = (9, 10)
            // Implied Velocity = (1, 0)
            _entityManager.SetComponentData(entity, new Position { Value = new float2(10, 10) });
            _entityManager.SetComponentData(entity, new PreviousPosition { Value = new float2(9, 10) });
            _entityManager.SetComponentData(entity, new PhysicsProperties { Friction = 1.0f, Radius = 1f, Bounciness = 0.5f });

            // 2. Execute
            var system = _world.GetOrCreateSystem<IntegrationSystem>();
            var systemGroup = _world.GetOrCreateSystem<FixedStepSimulationSystemGroup>();
            
            // Manually update the system
            system.Update(_world.Unmanaged);

            // 3. Assert
            // Expected:
            // Velocity = (10,10) - (9,10) = (1,0)
            // NewPos = CurrentPos + Velocity = (10,10) + (1,0) = (11,10)
            // (Ignoring Time.DeltaTime scaling for pure Verlet if we assume 1 step, 
            // but standard Verlet usually does: NextPos = Pos + (Pos - PrevPos) + Accel * dt * dt)
            // Wait, the plan says: `Velocity = Position - PrevPosition; Position += Velocity`
            // This is "Velocity Verlet" or "Position Based Dynamics" simplified.
            
            var pos = _entityManager.GetComponentData<Position>(entity);
            
            // We expect strictly (11, 10) if no drag/forces and simple addition
            Assert.AreEqual(11f, pos.Value.x, 0.001f);
            Assert.AreEqual(10f, pos.Value.y, 0.001f);
        }
    }
}
