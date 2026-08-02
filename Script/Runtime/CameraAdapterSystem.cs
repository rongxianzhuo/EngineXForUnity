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

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<CameraData>();
            _camera = Camera.main;
            if (!_camera) Debug.LogError("Camera is not found.");
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_camera) return;

            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks.Dispose();
                _chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
            _query.ToChunkArray(_chunks);

            if (_chunks.Length == 0 || _chunks[0].Chunk.Count == 0)
            {
                return;
            }

            ref var data = ref _chunks[0].Chunk.GetComponentRef<CameraData>(0);
            Apply(ref data);
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
            if (_camera)
            {
                UnityEngine.Object.Destroy(_camera.gameObject);
            }
        }

        private void Apply(ref CameraData data)
        {
            _camera.transform.position = UnityConvert.ToVector3(data.Position);
            _camera.transform.rotation = UnityConvert.ToQuaternion(data.Rotation);
            _camera.fieldOfView = data.Fov > FP.Zero ? data.Fov.Single() : DefaultFov;
            _camera.nearClipPlane = data.NearClip > FP.Zero ? data.NearClip.Single() : DefaultNear;
            _camera.farClipPlane = data.FarClip > FP.Zero ? data.FarClip.Single() : DefaultFar;
            _camera.orthographic = data.Projection == CameraProjection.Orthographic;
        }
    }
}
