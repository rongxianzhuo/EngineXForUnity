using System.Collections.Generic;
using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Jobs;
using EngineXMath = EngineX.Baseline.Math;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineX.Demo
{
    /// <summary>
    /// 渲染系统：查询 TransformData + RenderData，
    /// 按 RenderData 中的 Resources 路径加载网格/材质（带缓存），
    /// 同一 (网格, 材质) 的实例合批，GPU Instancing 绘制。
    /// </summary>
    public sealed class DemoRenderSystem : ISystem
    {
        private const int MaxInstancesPerDraw = 1023;

        private EntityQuery _query;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        // 资源缓存：按 Resources 路径加载一次，之后复用
        private readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();
        private readonly HashSet<string> _missingWarned = new HashSet<string>();
        private readonly IResourceLoader _resourceLoader;

        private readonly Matrix4x4[] _drawBuffer = new Matrix4x4[MaxInstancesPerDraw];

        public DemoRenderSystem(IResourceLoader resourceLoader)
        {
            _resourceLoader = resourceLoader;
        }

        /// <summary>合批键：同一 (网格, 材质) 的实例放一批绘制。</summary>
        private readonly struct BatchKey
        {
            public readonly Mesh Mesh;
            public readonly Material Material;

            public BatchKey(Mesh mesh, Material material)
            {
                Mesh = mesh;
                Material = material;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Mesh == other.Mesh && Material == other.Material;
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (Mesh != null ? Mesh.GetHashCode() : 0);
                hash = hash * 31 + (Material != null ? Material.GetHashCode() : 0);
                return hash;
            }
        }

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<TransformData, RenderData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);

            // 按 (网格, 材质) 分组收集实例矩阵（EngineX 定点数 → Unity float）
            var batches = new Dictionary<BatchKey, List<Matrix4x4>>();
            for (int i = 0; i < _chunks.Length; i++)
            {
                var chunk = _chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var t = ref chunk.GetComponentRef<TransformData>(e);
                    ref var render = ref chunk.GetComponentRef<RenderData>(e);

                    var mesh = LoadMesh(render.MeshPath);
                    var material = LoadMaterial(render.MaterialPath);
                    if (mesh == null || material == null)
                    {
                        continue; // 资源缺失已告警，跳过该实例
                    }

                    var key = new BatchKey(mesh, material);
                    if (!batches.TryGetValue(key, out var matrices))
                    {
                        matrices = new List<Matrix4x4>();
                        batches.Add(key, matrices);
                    }
                    matrices.Add(Matrix4x4.TRS(
                        ToUnityVector3(t.Position),
                        ToUnityQuaternion(t.Rotation),
                        ToUnityVector3(t.Scale)));
                }
            }

            // 每组按 1023 上限分批实例化绘制
            foreach (var kv in batches)
            {
                var matrices = kv.Value;
                for (int b = 0; b < matrices.Count; b += _drawBuffer.Length)
                {
                    int batchCount = Mathf.Min(_drawBuffer.Length, matrices.Count - b);
                    matrices.CopyTo(b, _drawBuffer, 0, batchCount);
                    DrawInstanced(kv.Key.Mesh, kv.Key.Material, batchCount);
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
        }

        // ==================== Resources 加载（带缓存） ====================

        private Mesh LoadMesh(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (!_meshCache.TryGetValue(path, out var mesh))
            {
                mesh = _resourceLoader.LoadMesh(path);
                _meshCache[path] = mesh;
                if (mesh == null && _missingWarned.Add("Mesh:" + path))
                {
                    Debug.LogWarning($"[DemoRenderSystem] 找不到 Mesh 资源: {path}（请确认 Assets/Resources/{path} 下存在）");
                }
            }
            return mesh;
        }

        private Material LoadMaterial(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (!_materialCache.TryGetValue(path, out var material))
            {
                material = _resourceLoader.LoadMaterial(path);
                _materialCache[path] = material;
                if (material == null)
                {
                    if (_missingWarned.Add("Material:" + path))
                    {
                        Debug.LogWarning($"[DemoRenderSystem] 找不到 Material 资源: {path}（请确认 Assets/Resources/{path} 下存在）");
                    }
                    return null;
                }
                // 实例化渲染要求材质开启 GPU Instancing（运行时修改不落盘）
                material.enableInstancing = true;
            }
            return material;
        }

        // ==================== 定点数 → Unity 转换 ====================

        private static Vector3 ToUnityVector3(EngineXMath.Vector3 v)
        {
            return new Vector3(v.X.Single(), v.Y.Single(), v.Z.Single());
        }

        private static Quaternion ToUnityQuaternion(EngineXMath.Quaternion q)
        {
            // 防御 1：默认构造的 TransformData 旋转是零四元数 (0,0,0,0)，不是单位四元数
            if (q.X == FP.Zero && q.Y == FP.Zero && q.Z == FP.Zero && q.W == FP.Zero)
            {
                return Quaternion.identity;
            }
            // 防御 2：增量旋转 + 定点数截断会产生范数漂移，
            // Matrix4x4.TRS 要求单位四元数，否则报错，所以边界处归一化
            q = q.Normalized;
            return new Quaternion(q.X.Single(), q.Y.Single(), q.Z.Single(), q.W.Single());
        }

        private void DrawInstanced(Mesh mesh, Material material, int count)
        {
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
            Graphics.RenderMeshInstanced(rp, mesh, 0, _drawBuffer, count);
#else
            Graphics.DrawMeshInstanced(mesh, 0, material, _drawBuffer, count, null,
                ShadowCastingMode.On, true);
#endif
        }
    }
}
