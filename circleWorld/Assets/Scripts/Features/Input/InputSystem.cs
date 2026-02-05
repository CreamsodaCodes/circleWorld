using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CircleWorld.Features.Input
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CircleWorld.Features.Spawning.SpawningSystem))] // Ensure input is ready before spawning (forward reference, safe in C#)
    public partial class InputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Create the singleton entity if it doesn't exist
            if (!SystemAPI.HasSingleton<InputData>())
            {
                EntityManager.CreateSingleton<InputData>();
            }
        }

        protected override void OnUpdate()
        {
            float2 mousePos = float2.zero;
            bool isClicking = false;

            // Use legacy Input for simplicity as per plan
            // Project screen point to world point at Z=0
            if (Camera.main != null)
            {
                var screenPos = UnityEngine.Input.mousePosition;
                screenPos.z = -Camera.main.transform.position.z; // Distance to Z=0 plane
                var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
                mousePos = new float2(worldPos.x, worldPos.y);
            }

            isClicking = UnityEngine.Input.GetMouseButtonDown(0);

            // Update Singleton
            if (SystemAPI.HasSingleton<InputData>())
            {
                SystemAPI.SetSingleton(new InputData
                {
                    MouseWorldPosition = mousePos,
                    IsClicking = isClicking
                });
            }
        }
    }
}
