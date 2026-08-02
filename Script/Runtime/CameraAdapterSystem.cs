using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Jobs;
using UnityEngine;

namespace EngineX.Demo
{
    /// <summary>
    /// 相机适配系统：读取游戏侧的 TransformData + CameraData，
    /// 同步到 Unity Camera。位置/旋转来自 TransformData，相机参数来自 CameraData。
    /// 语义：任何挂 (TransformData, CameraData) 的实体即视为相机；多个时取第一个。
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
            _query = state.World.Query<TransformData, CameraData>();
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

            ref var transform = ref _chunks[0].Chunk.GetComponentRef<TransformData>(0);
            ref var cameraData = ref _chunks[0].Chunk.GetComponentRef<CameraData>(0);

            _camera.transform.position = UnityConvert.ToVector3(transform.Position);
            _camera.transform.rotation = UnityConvert.ToQuaternion(transform.Rotation);
            // 防御：default(CameraData) 时 Fov/Near/Far 为 0，回退默认值
            _camera.fieldOfView = cameraData.Fov > FP.Zero ? cameraData.Fov.Single() : DefaultFov;
            _camera.nearClipPlane = cameraData.NearClip > FP.Zero ? cameraData.NearClip.Single() : DefaultNear;
            _camera.farClipPlane = cameraData.FarClip > FP.Zero ? cameraData.FarClip.Single() : DefaultFar;
            _camera.orthographic = cameraData.Projection == CameraProjection.Orthographic;
        }

        public void OnDestroy(ref SystemState state)
        {
            _chunks.Dispose();
            // 相机由场景持有，适配层只同步不销毁
        }
    }
}
