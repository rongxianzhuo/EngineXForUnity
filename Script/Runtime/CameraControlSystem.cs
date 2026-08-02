using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Jobs;
using EngineXMath = EngineX.Baseline.Math;

namespace EngineX.Demo
{
    /// <summary>
    /// 相机控制系统（游戏侧示例逻辑）：读取 InputData，在相机水平面内移动相机。
    /// WASD/方向键 → 前/后/左/右平移（沿相机朝向的 XZ 平面投影）。
    /// 属于游戏逻辑，与引擎无关；适配层只负责填 InputData 和同步 CameraData。
    /// </summary>
    public sealed class CameraControlSystem : ISystem
    {
        private static readonly FP MoveSpeed = FP.FromFloat(5f);   // 单位/秒
        private static readonly FP DeltaTime = FP.One / 50;        // 与模拟固定步长一致

        private EntityQuery _inputQuery;
        private EntityQuery _cameraQuery;
        private NativeArray<ChunkHandle> _inputChunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);
        private NativeArray<ChunkHandle> _cameraChunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        public void OnCreate(ref SystemState state)
        {
            _inputQuery = state.World.Query<InputData>();
            _cameraQuery = state.World.Query<TransformData, CameraData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ResizeIfNeeded(ref _inputChunks, _inputQuery);
            _inputQuery.ToChunkArray(_inputChunks);
            if (_inputChunks.Length == 0 || _inputChunks[0].Chunk.Count == 0)
            {
                return; // 游戏侧未声明输入实体
            }
            ref var input = ref _inputChunks[0].Chunk.GetComponentRef<InputData>(0);
            if (input.Horizontal == FP.Zero && input.Vertical == FP.Zero)
            {
                return;
            }

            ResizeIfNeeded(ref _cameraChunks, _cameraQuery);
            _cameraQuery.ToChunkArray(_cameraChunks);
            if (_cameraChunks.Length == 0 || _cameraChunks[0].Chunk.Count == 0)
            {
                return; // 游戏侧未声明相机实体
            }
            ref var transform = ref _cameraChunks[0].Chunk.GetComponentRef<TransformData>(0);

            // 相机朝向投影到水平面（保持平移不改变高度）
            var forward = transform.Rotation * EngineXMath.Vector3.Forward;
            var right = transform.Rotation * EngineXMath.Vector3.Right;
            forward.Y = FP.Zero;
            right.Y = FP.Zero;
            if (forward.SqrMagnitude > FP.Zero)
            {
                forward = forward.Normalized;
            }
            if (right.SqrMagnitude > FP.Zero)
            {
                right = right.Normalized;
            }

            // 位置 += (前向*Vertical + 右向*Horizontal) * 速度 * 步长
            var move = (forward * input.Vertical + right * input.Horizontal) * MoveSpeed * DeltaTime;
            transform.Position += move;
        }

        public void OnDestroy(ref SystemState state)
        {
            _inputChunks.Dispose();
            _cameraChunks.Dispose();
        }

        private static void ResizeIfNeeded(ref NativeArray<ChunkHandle> chunks, EntityQuery query)
        {
            var needed = query.CalculateChunkCount();
            if (chunks.Length != needed)
            {
                chunks.Dispose();
                chunks = new NativeArray<ChunkHandle>(needed, Allocator.Persistent);
            }
        }
    }
}
