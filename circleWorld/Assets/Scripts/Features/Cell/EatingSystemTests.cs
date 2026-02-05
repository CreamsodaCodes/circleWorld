using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using CircleWorld.Features.Cell;
using Unity.Collections;
using Unity.Jobs;

namespace CircleWorld.Features.Cell.Tests
{
    public class EatingSystemTests
    {
        private World _world;
        private EntityManager _entityManager;
        private EatingSystem _eatingSystem;

        [SetUp]
        public void SetUp()
        {
            _world = new World("TestWorld");
            _entityManager = _world.EntityManager;
            
            // Create the system
            _eatingSystem = _world.CreateSystem<EatingSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void Mouth_Eats_Food_Gains_Energy()
        {
            // 1. Create Mouth + Organism
            var organismEntity = _entityManager.CreateEntity(typeof(Energy));
            _entityManager.SetComponentData(organismEntity, new Energy { Value = 10f });

            var mouthEntity = _entityManager.CreateEntity(
                typeof(CellType), 
                typeof(Position), 
                typeof(PhysicsProperties), 
                typeof(OrganismID)
            );
            _entityManager.SetComponentData(mouthEntity, new CellType { Value = CellKind.Mouth });
            _entityManager.SetComponentData(mouthEntity, new Position { Value = new float2(0, 0) });
            _entityManager.SetComponentData(mouthEntity, new PhysicsProperties { Radius = 1f });
            _entityManager.SetComponentData(mouthEntity, new OrganismID { Value = organismEntity }); 
            // Usually Entity.Index is not safe if version changes, but for test it's fine. 
            // Real implementation might use the Entity itself as the Key, or a stable ID.
            // Component `struct OrganismID { int Value; }` suggests an ID. 
            // Let's assume for now OrganismID.Value corresponds to the Entity Index or some mapped ID.
            
            // Let's re-read the Plan regarding OrganismID. 
            // "Points to the Organism Entity". If it's simply an int, it's likely the Entity.Index.
            
            // 2. Create Food
            var foodEntity = _entityManager.CreateEntity(
                typeof(CellType),
                typeof(Position),
                typeof(PhysicsProperties),
                typeof(Energy)
            );
            _entityManager.SetComponentData(foodEntity, new CellType { Value = CellKind.Food });
            _entityManager.SetComponentData(foodEntity, new Position { Value = new float2(0.5f, 0) }); // Overlapping (Dist 0.5 < Radii 1+1)
            _entityManager.SetComponentData(foodEntity, new PhysicsProperties { Radius = 1f });
            _entityManager.SetComponentData(foodEntity, new Energy { Value = 5f });

            // 3. Update System
            _eatingSystem.Update();
            _world.Update(); // Process Structural Changes (ECB)

            // 4. Assert
            // Food should be destroyed
            Assert.IsFalse(_entityManager.Exists(foodEntity), "Food entity should be destroyed.");

            // Organism should have gained energy
            // NOTE: EatingSystem needs to look up the Organism Entity by ID. 
            // If OrganismID.Value is just 'int', how do we find the Entity?
            // Ideally OrganismID should store `Entity Target`. The current struct has `int Value`. 
            // I might need to Refactor OrganismID to store Entity, OR EatingSystem assumes ID == Entity.Index.
            // Let's assume for this test I will patch OrganismID to support Entity, or the System handles it.
            // Reviewing `CellComponents.cs`: `struct OrganismID : IComponentData { public int Value; }`
            
            // IF the design strictly uses `int`, then there must be a map or we cast Entity to Int.
            // I will implement the System to treat OrganismID.Value as Entity.Index + Version? No, just Entity? 
            // `Entity` struct fits in IComponentData. 
            // I will PROPOSE to change OrganismID to store `Entity Value` instead of `int`. 
            // For now, I'll stick to `int` and assume it matches `Entity.Index`.
            
            var energy = _entityManager.GetComponentData<Energy>(organismEntity);
            Assert.AreEqual(15f, energy.Value, "Organism Energy should increase by Food Value.");
        }
    }
}
