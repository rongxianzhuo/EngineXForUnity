using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Jobs;
using UnityEngine;

namespace EngineX.Demo
{
    /// <summary>
    /// 相机适配系统：读取游戏侧的 CameraData 组件，同步到 Unity Camera。
    /// 语义：任何挂 CameraData 的实体即视为相机；多个时取第一个（多相机后续再定）。
    /// </summary>
    public sealed class CameraAdapterSystem : ISystem
    {
        private const float DefaultFov = 60f;
        private const float DefaultNear = 0.3f;
        private const float DefaultFar = 1000f;

        private EntityQuery _query;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);
        private Camera _camera;
        private bool _ownsCamera;

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CameraData>();

            // 复用场景里的 Main Camera；没有则自动创建
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("EngineX Camera");
                _camera = go.AddComponent<Camera>();
                _camera.tag = "MainCamera";
                _ownsCamera = true;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_camera == null)
            {
                return;
            }

            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);

            if (_chunks.Length == 0 || _chunks[0].Chunk.Count == 0)
            {
                return; // 游戏侧尚未声明相机，保持现状
            }

            ref var data = ref _chunks[0].Chunk.GetComponentRef<CameraData>(0);
            Apply(data);
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
            if (_ownsCamera && _camera != null)
            {
                UnityEngine.Object.Destroy(_camera.gameObject);
            }
        }

        private void Apply(ref CameraData data)
        {
            _camera.transform.position = UnityConvert.ToVector3(data.Position);
            _camera.transform.rotation = UnityConvert.ToQuaternion(data.Rotation);
            // 防御：default(CameraData) 时 Fov/Near/Far 为 0，回退默认值
            _camera.fieldOfView = data.Fov > FP.Zero ? data.Fov.Single() : DefaultFov;
            _camera.nearClipPlane = data.NearClip > FP.Zero ? data.NearClip.Single() : DefaultNear;
            _camera.farClipPlane = data.FarClip > FP.Zero ? data.FarClip.Single() : DefaultFar;
            _camera.orthographic = data.Projection == CameraProjection.Orthographic;
            // 注：正交模式的 orthographicSize 暂未纳入组件契约，保持 Unity 默认值
        }
    }
}
