using System;
using System.Collections.Generic;
using EngineX.ECS;
using EngineX.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineX.Demo
{
    // ==================== ECS 组件定义 ====================

    public struct CirclePosition : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
    }

    public struct CircleRotation : IComponentData
    {
        public float Angle;
    }

    // ==================== 轨道 Job / System ====================

    public struct OrbitJob : IJobParallelForBatch
    {
        public NativeArray<ChunkHandle> Chunks;
        public float Radius;
        public float AngularSpeed;
        public float DeltaTime;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                var chunk = Chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var rot = ref chunk.GetComponentRef<CircleRotation>(e);
                    ref var pos = ref chunk.GetComponentRef<CirclePosition>(e);
                    rot.Angle += AngularSpeed * DeltaTime;
                    float c = (float)Math.Cos(rot.Angle);
                    float s = (float)Math.Sin(rot.Angle);
                    pos.X = Radius * c;
                    pos.Y = 0f;
                    pos.Z = Radius * s;
                }
            }
        }
    }

    public sealed class OrbitSystem : ISystem
    {
        public const float Radius = 5;

        private EntityQuery _query;
        public JobHandle Handle;
        public float DeltaTime = 1f / 50f;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CirclePosition, CircleRotation>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);   // 零分配填充
            Handle = JobSystem.ScheduleParallel(
                new OrbitJob
                {
                    Chunks = _chunks,
                    Radius = Radius,
                    AngularSpeed = -2f * (float)Math.PI * 0.1f,
                    DeltaTime = DeltaTime,
                },
                _chunks, 1, state.Dependency);
            state.Dependency = Handle;
        }

        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            _chunks.Dispose();
        }
    }


    // ==================== 入口：驱动 ECS 世界 ====================

    public class CircleDemo : MonoBehaviour
    {
        public Material baseMaterial;
        
        private readonly World _world = new World();
        private readonly SystemsGroup _group = new SystemsGroup();
        private readonly OrbitSystem _orbitSystem = new OrbitSystem() { DeltaTime = 0.02f };
        private DemoRenderSystem _renderSystem;

        public void Awake()
        {
            const int entityCount = 10;
            for (int i = 0; i < entityCount; i++)
            {
                var e = _world.CreateEntity();
                float angle = (float)(2.0 * Math.PI * i / entityCount);
                _world.AddComponent(e, new CircleRotation { Angle = angle });
                _world.AddComponent(e, new CirclePosition
                {
                    X = OrbitSystem.Radius * (float)Math.Cos(angle),
                    Z = OrbitSystem.Radius * (float)Math.Sin(angle),
                });
                // 渲染数据组件：声明该实体需要被渲染，并携带渲染参数
                _world.AddComponent(e, new RendererData
                {
                    Scale = 0.35f + 0.15f * (i % 3),
                });
            }

            _renderSystem = new DemoRenderSystem(_orbitSystem, baseMaterial);
            _group.Add(_orbitSystem);
            _group.Add(_renderSystem);
            _group.Create(_world);
        }

        public void Update()
        {
            _orbitSystem.DeltaTime = Time.deltaTime;
            _group.Update(_world);
            _orbitSystem.Handle.Complete();
        }

        public void OnDestroy()
        {
            _group.Destroy();
            _world.Dispose();
        }
    }
}
