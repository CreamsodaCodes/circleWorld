using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Rendering
{
     [MaterialProperty("_BaseColor")]
     public struct EffectColor : IComponentData
     {
         public float4 Value;
     }
}
