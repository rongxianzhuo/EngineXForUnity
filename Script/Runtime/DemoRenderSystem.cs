using System.Collections.Generic;
using EngineX.ECS;
using EngineX.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineX.Demo
{
    public sealed class DemoRenderSystem : ISystem
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly OrbitSystem _orbitSystem;
        private readonly Material _baseMaterial;

        private EntityQuery _query;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        // 渲染资源：网格由系统创建并持有，材质由外部注入
        private Mesh _mesh;
        private readonly Matrix4x4[] _drawBuffer = new Matrix4x4[MaxInstancesPerDraw];

        public DemoRenderSystem(OrbitSystem orbitSystem, Material baseMaterial)
        {
            _orbitSystem = orbitSystem;
            _baseMaterial = baseMaterial;
        }

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CirclePosition, CircleRotation, RendererData>();

            // 复用 Unity 内置球体网格
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(primitive);

            // 实例化渲染要求材质开启 GPU Instancing（运行时修改不会落盘到材质资产）
            _baseMaterial.enableInstancing = true;
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

            // 收集所有实例的变换矩阵
            var matrices = new List<Matrix4x4>();
            for (int i = 0; i < _chunks.Length; i++)
            {
                var chunk = _chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var pos = ref chunk.GetComponentRef<CirclePosition>(e);
                    ref var rot = ref chunk.GetComponentRef<CircleRotation>(e);
                    ref var renderer = ref chunk.GetComponentRef<RendererData>(e);
                    matrices.Add(Matrix4x4.TRS(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        Quaternion.Euler(0f, rot.Angle * Mathf.Rad2Deg, 0f),
                        Vector3.one * renderer.Scale));
                }
            }

            // 超出单次上限时自动分批，统一用 baseMaterial 实例化绘制
            for (int b = 0; b < matrices.Count; b += _drawBuffer.Length)
            {
                int batchCount = Mathf.Min(_drawBuffer.Length, matrices.Count - b);
                matrices.CopyTo(b, _drawBuffer, 0, batchCount);
                DrawInstanced(batchCount);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
        }

        private void DrawInstanced(int count)
        {
#if UNITY_2022_2_OR_NEWER
            var rp = new RenderParams(_baseMaterial)
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
            Graphics.DrawMeshInstanced(_mesh, 0, _baseMaterial, _drawBuffer, count, null,
                ShadowCastingMode.On, true);
#endif
        }
    }
}