using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics
{
    [BurstCompile]
    public partial struct ConstraintRelaxationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsProperties>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Execute the Job
            new ConstraintRelaxationJob
            {
                PositionLookup = SystemAPI.GetComponentLookup<Position>(true) // ReadOnly
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct ConstraintRelaxationJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<Position> PositionLookup;

        public void Execute(ref Position position, in DynamicBuffer<Constraint> constraints)
        {
            if (constraints.Length == 0) return;

            float2 currentPos = position.Value;
            float2 totalDisplacement = float2.zero;
            // Strategy: Accumulate displacements from all constraints, then apply? Or apply sequential?
            // "Gauss-Seidel" style inside the loop means `currentPos` updates instantly.
            // "Jacobi" means we sum `totalDisplacement` and apply at end.
            // Let's use Sequential (Gauss-Seidel) for faster convergence, using temporary variable `currentPos`.
            
            for (int i = 0; i < constraints.Length; i++)
            {
                var constraint = constraints[i];
                if (!PositionLookup.HasComponent(constraint.Target)) continue;

                float2 targetPos = PositionLookup[constraint.Target].Value; // This is Read-Only from START of frame if ReadOnly lookup?
                // Actually, if we use [ReadOnly] Lookup, we get the position at job schedule time.
                // This means effectively Jacobi for "neighbors' positions", but we update "my position" instantly.
                // This is fine.

                float2 delta = targetPos - currentPos;
                float distance = math.length(delta);
                
                if (distance < 0.0001f) continue; // Avoid div by zero

                // Displacement Factor calculated for strict PBD (Position Based Dynamics)
                // We want distance to match RestLength.
                // difference = distance - RestLength
                // correction = difference * Stiffness
                // But we share this with the neighbor. So we only take half? 
                // Standard PBD: w1 / (w1 + w2). If masses equal: 0.5.
                
                float difference = distance - constraint.RestLength;
                float correctionRate = constraint.Stiffness * 0.5f; // Assuming equal mass
                
                float2 correctionVector = math.normalize(delta) * (difference * correctionRate);
                
                // Move TOWARDS target if difference > 0 (stretched)
                // Move AWAY if difference < 0 (compressed)
                // Formula: Target - Current = Delta.
                // We want to move along Delta. 
                // If Dist > Rest, we want to move towards Target. (+Delta direction).
                // CorrectionVector points from Me -> Target because `delta` is Target - Me.
                
                currentPos += correctionVector;
            }

            position.Value = currentPos;
        }
    }
}
