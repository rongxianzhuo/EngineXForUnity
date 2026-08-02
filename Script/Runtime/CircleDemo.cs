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

    /// <summary>
    /// 渲染数据组件：声明该实体需要被渲染，并携带渲染参数。
    /// Scale 控制渲染尺寸，ColorIndex 用于在渲染调色板中选色。
    /// </summary>
    public struct RendererData : IComponentData
    {
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

    // ==================== 渲染 System ====================
    // 职责：把 ECS 中的位置/旋转/渲染数据组件转换成真正的
    // Unity 渲染（GPU Instancing 绘制球体）。

    public sealed class DemoRenderSystem : ISystem
    {
        private const int MaxInstancesPerDraw = 1023;

        /// <summary>渲染调色板，按 RendererData.ColorIndex 取色。</summary>
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.55f, 0.45f),
            new Color(0.45f, 0.80f, 0.95f),
            new Color(0.60f, 0.95f, 0.55f),
            new Color(0.95f, 0.90f, 0.50f),
        };

        private readonly OrbitSystem _orbitSystem;
        private EntityQuery _query;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        // 渲染资源：网格与调色板材质，由系统创建并持有
        private Mesh _mesh;
        private Material[] _materials;
        private readonly Matrix4x4[] _drawBuffer = new Matrix4x4[MaxInstancesPerDraw];

        public DemoRenderSystem(OrbitSystem orbitSystem)
        {
            _orbitSystem = orbitSystem;
        }

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CirclePosition, CircleRotation, RendererData>();

            // 复用 Unity 内置球体网格
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(primitive);

            // 为调色板中的每种颜色准备一个可实例化的材质
            _materials = new Material[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                var mat = new Material(FindLitShader());
                mat.color = Palette[i];
                mat.enableInstancing = true;
                _materials[i] = mat;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // 先等轨道 Job 写完位置数据，保证渲染的是最新一帧结果
            _orbitSystem.Handle.Complete();

            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);

            // 按颜色分组，同色实例合并到同一批
            var batches = new List<Matrix4x4>[Palette.Length];
            for (int c = 0; c < batches.Length; c++)
            {
                batches[c] = new List<Matrix4x4>();
            }

            for (int i = 0; i < _chunks.Length; i++)
            {
                var chunk = _chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var pos = ref chunk.GetComponentRef<CirclePosition>(e);
                    ref var rot = ref chunk.GetComponentRef<CircleRotation>(e);
                    ref var renderer = ref chunk.GetComponentRef<RendererData>(e);
                    int colorIndex = Mathf.Abs(renderer.ColorIndex) % Palette.Length;
                    batches[colorIndex].Add(Matrix4x4.TRS(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        Quaternion.Euler(0f, rot.Angle * Mathf.Rad2Deg, 0f),
                        Vector3.one * renderer.Scale));
                }
            }

            // 逐颜色分批实例化绘制
            for (int c = 0; c < Palette.Length; c++)
            {
                var list = batches[c];
                if (list.Count == 0)
                {
                    continue;
                }
                for (int b = 0; b < list.Count; b += _drawBuffer.Length)
                {
                    int batchCount = Mathf.Min(_drawBuffer.Length, list.Count - b);
                    list.CopyTo(b, _drawBuffer, 0, batchCount);
                    DrawInstanced(c, batchCount);
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
            if (_materials == null)
            {
                return;
            }
            foreach (var mat in _materials)
            {
                if (mat != null)
                {
                    UnityEngine.Object.Destroy(mat);
                }
            }
        }

        private void DrawInstanced(int colorIndex, int count)
        {
            var material = _materials[colorIndex];
#if UNITY_2022_2_OR_NEWER
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                // 渲染前剔除用的包围盒，覆盖轨道范围即可
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
            };
            // 注意：RenderMeshInstanced 没有 MaterialPropertyBlock 参数，
            // 需要逐实例属性时通过 RenderParams.matProps 传入
            Graphics.RenderMeshInstanced(rp, _mesh, 0, _drawBuffer, count);
#else
            Graphics.DrawMeshInstanced(_mesh, 0, material, _drawBuffer, count, null,
                ShadowCastingMode.On, true);
#endif
        }

        private static Shader FindLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }
            shader = Shader.Find("HDRP/Lit");
            if (shader != null)
            {
                return shader;
            }
            return Shader.Find("Standard");
        }
    }

    // ==================== 入口：驱动 ECS 世界 ====================

    public class CircleDemo : MonoBehaviour
    {
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
                    ColorIndex = i % 4,
                });
            }

            _renderSystem = new DemoRenderSystem(_orbitSystem);
            _group.Add(_orbitSystem);
            _group.Add(_renderSystem);
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
