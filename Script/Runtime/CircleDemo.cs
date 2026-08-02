using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Framework;
using EngineX.Jobs;
using EngineXMath = EngineX.Baseline.Math;

namespace EngineX.Demo
{
    // ==================== 轨道 Job / System ====================
    // 直接驱动通用的 TransformData 组件（Position + Rotation + Scale）

    public struct OrbitJob : IJobParallelForBatch
    {
        public NativeArray<ChunkHandle> Chunks;
        public FP AngularSpeed; // 弧度/秒
        public FP DeltaTime;

        public void Execute(int startIndex, int count)
        {
            // 每步绕 Y 轴的旋转增量（定点数四元数）
            // 注意：FP 乘法是截断式且 Sin/Cos 走查表插值，直接累乘四元数会导致
            // 范数持续漂移（<1），所以 step 归一化，且每实体组合后再次归一化
            var step = EngineXMath.Quaternion.AngleAxis(AngularSpeed * DeltaTime * FP.Rad2Deg, EngineXMath.Vector3.Up).Normalized;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                var chunk = Chunks[i].Chunk;
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var t = ref chunk.GetComponentRef<TransformData>(e);
                    t.Position = step * t.Position;
                    t.Rotation = (step * t.Rotation).Normalized;
                }
            }
        }
    }

    public sealed class OrbitSystem : ISystem
    {
        public static readonly FP Radius = FP.FromInt(5);

        /// <summary>角速度 ≈ -2π * 0.1 弧度/秒</summary>
        private static readonly FP AngularSpeed = FP.FromFloat(-0.62831853f);

        private EntityQuery _query;
        public JobHandle Handle;
        public FP DeltaTime = FP.One / 50;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<TransformData>();
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
                    AngularSpeed = AngularSpeed,
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

    public class CircleDemo : IGame
    {

        private readonly World _world = new World();
        private readonly SystemsGroup _group = new SystemsGroup();
        private readonly OrbitSystem _orbitSystem = new OrbitSystem();

        public World Create()
        {
            const int entityCount = 10;
            for (int i = 0; i < entityCount; i++)
            {
                var e = _world.CreateEntity();
                FP angle = FP.PI * 2 * i / entityCount;
                var position = new EngineXMath.Vector3(
                    OrbitSystem.Radius * FpMath.Cos(angle),
                    FP.Zero,
                    OrbitSystem.Radius * FpMath.Sin(angle));
                FP scale = FP.FromFloat(0.35f + 0.15f * (i % 3));
                _world.AddComponent(e, TransformData.FromEuler(
                    position,
                    new EngineXMath.Vector3(FP.Zero, angle * FP.Rad2Deg, FP.Zero),
                    new EngineXMath.Vector3(scale, scale, scale)));
            }

            _group.Add(_orbitSystem);
            _group.Create(_world);
            return _world;
        }

        public void Update()
        {
            _group.Update(_world);
            _orbitSystem.Handle.Complete();
        }

        public void Destroy()
        {
            _group.Destroy();
            _world.Dispose();
        }
    }
}
