using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics
{
    [BurstCompile]
    public partial struct IntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsProperties>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Hardcoded World Bounds for now
            float2 worldBounds = new float2(50, 50);

            // Execute the Job
            new IntegrationJob
            {
                DeltaTime = deltaTime,
                WorldBounds = worldBounds
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct IntegrationJob : IJobEntity
    {
        public float DeltaTime;
        public float2 WorldBounds;

        public void Execute(ref Position position, ref PreviousPosition prevPosition, in PhysicsProperties properties)
        {
            // 1. Calculate Velocity (Verlet)
            float2 velocity = position.Value - prevPosition.Value;

            // 2. Save current position as previous
            prevPosition.Value = position.Value;

            // 3. Apply Friction (Damping)
            velocity *= properties.Friction; // Simple linear damping 0..1

            // 4. Integrate (NewPos = Pos + Vel + Acc * dt * dt)
            // Assuming Forces = 0 for now (or add Gravity here)
            float2 newPosition = position.Value + velocity; 

            // 5. World Bounds Check (Simple Box Containment)
            // If out of bounds, clamp and invert velocity (bounce)?
            // For Verlet, "Bounce" means adjusting PreviousPosition to reflect the reflection.
            
            bool hitBoundary = false;

            if (math.abs(newPosition.x) > WorldBounds.x)
            {
                newPosition.x = math.clamp(newPosition.x, -WorldBounds.x, WorldBounds.x);
                hitBoundary = true;
                // Reflect velocity for X:
                // Current Vel is (New - Prev). We want New to be such that Vel is inverted * bounciness.
                // Simplified: just project `prevPosition` to be on the "other side" of `newPosition`.
                // Implicitly handled if we just Clamp `newPosition`? No, that kills energy.
                // To bounce: PrevPos = NewPos - (ReflectedVel)
            }
            if (math.abs(newPosition.y) > WorldBounds.y)
            {
                newPosition.y = math.clamp(newPosition.y, -WorldBounds.y, WorldBounds.y);
                hitBoundary = true;
            }

            // Simple bounce implementation for Verlet:
            // If we hit a wall, we modify `prevPosition` to "fake" a bounce velocity.
            if (hitBoundary)
            {
                // Re-calculate effective velocity after clamp
                float2 clampedVel = newPosition - prevPosition.Value;
                
                // This is tricky in pure position-based. 
                // A common trick: 
                // If X hit wall:
                // PrevPos.x = NewPos.x + (OldVel.x * Bounciness)
                // This makes the new implicit velocity point inwards with magnitude * Bounciness.
                
                // Let's re-eval velocity based on the pre-clamped path
                float2 originalVel = velocity;
                
                if (math.abs(position.Value.x + originalVel.x) > WorldBounds.x) // Check based on Step
                {
                     // Invert X component of the "implicit previous velocity"
                     // The distance we *would* have traveled is originalVel.x
                     // The new distance we want to "have traveled" is -originalVel.x * bounciness
                     // So we set PrevPos.x = NewPos.x + (originalVel.x * properties.Bounciness);
                     // Careful with signs. 
                     // Velocity = Pos - Prev.
                     // We want New_Vel = -Old_Vel * Bounce.
                     // New_Pos - New_Prev = -(Pos - Old_Prev) * Bounce
                     // New_Prev = New_Pos + (Pos - Old_Prev) * Bounce
                     
                     prevPosition.Value.x = newPosition.x + (originalVel.x * properties.Bounciness);
                }
                
                 if (math.abs(position.Value.y + originalVel.y) > WorldBounds.y)
                {
                     prevPosition.Value.y = newPosition.y + (originalVel.y * properties.Bounciness);
                }
            }

            // Update Position
            position.Value = newPosition;
        }
    }
}
