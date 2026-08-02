using System;
using EngineX.ECS;
using EngineX.Jobs;
using UnityEngine;

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

    /// <summary>
    /// 渲染组件：声明该实体需要被渲染为一个圆球。
    /// Scale 控制渲染尺寸，ColorIndex 用于在渲染层的调色板中选色。
    /// </summary>
    public struct CircleRender : IComponentData
    {
        public float Scale;
        public int ColorIndex;
    }

    /// <summary>
    /// 渲染实例（blittable，可放入 NativeArray）。
    /// 由 ECS 侧的 RenderCollectSystem 收集生成，Unity 渲染层读取后绘制。
    /// Angle 为弧度，绕 Y 轴。
    /// </summary>
    public struct CircleRenderInstance
    {
        public float PosX;
        public float PosY;
        public float PosZ;
        public float Angle;
        public float Scale;
        public int ColorIndex;
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

    // ==================== 渲染收集 System ====================
    // 职责：把 ECS 中的渲染组件（CircleRender + 位置/旋转）收集成
    // 渲染实例数组，作为 ECS 世界输出给 Unity 渲染层的数据缓冲。

    public sealed class RenderCollectSystem : ISystem
    {
        private readonly OrbitSystem _orbitSystem;
        private EntityQuery _query;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        /// <summary>渲染实例缓冲，Unity 渲染层每帧读取。</summary>
        public NativeArray<CircleRenderInstance> Instances = new NativeArray<CircleRenderInstance>(0, Allocator.Persistent);

        public RenderCollectSystem(OrbitSystem orbitSystem)
        {
            _orbitSystem = orbitSystem;
        }

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CirclePosition, CircleRotation, CircleRender>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 先等待轨道 Job 写完位置数据，再收集渲染实例，保证读到的是最新一帧结果
            _orbitSystem.Handle.Complete();

            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);

            int total = 0;
            for (int i = 0; i < _chunks.Length; i++)
            {
                total += _chunks[i].Chunk.Count;
            }

            if (Instances.Length != total)
            {
                Instances.Dispose();
                Instances = new NativeArray<CircleRenderInstance>(total, Allocator.Persistent);
            }

            int output = 0;
            for (int i = 0; i < _chunks.Length; i++)
            {
                var chunk = _chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var pos = ref chunk.GetComponentRef<CirclePosition>(e);
                    ref var rot = ref chunk.GetComponentRef<CircleRotation>(e);
                    ref var ren = ref chunk.GetComponentRef<CircleRender>(e);
                    Instances[output++] = new CircleRenderInstance
                    {
                        PosX = pos.X,
                        PosY = pos.Y,
                        PosZ = pos.Z,
                        Angle = rot.Angle,
                        Scale = ren.Scale,
                        ColorIndex = ren.ColorIndex,
                    };
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
            Instances.Dispose();
        }
    }

    // ==================== 入口：驱动 ECS 世界 ====================

    public class CircleDemo : MonoBehaviour
    {
        private readonly World _world = new World();
        private readonly SystemsGroup _group = new SystemsGroup();
        private readonly OrbitSystem _orbitSystem = new OrbitSystem() { DeltaTime = 0.02f };
        private RenderCollectSystem _renderSystem;

        /// <summary>渲染收集系统，CircleRenderer 从这里读取渲染实例。</summary>
        public RenderCollectSystem RenderSystem
        {
            get
            {
                if (_renderSystem == null)
                {
                    _renderSystem = new RenderCollectSystem(_orbitSystem);
                }
                return _renderSystem;
            }
        }

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
                // 渲染组件：声明该实体需要被渲染，并携带渲染参数
                _world.AddComponent(e, new CircleRender
                {
                    Scale = 0.35f + 0.15f * (i % 3),
                    ColorIndex = i % 4,
                });
            }

            _group.Add(_orbitSystem);
            _group.Add(RenderSystem);
            _group.Create(_world);
        }

        public void FixedUpdate()
        {
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
