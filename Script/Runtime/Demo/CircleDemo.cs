using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Framework;
using EngineX.Jobs;
using EngineXForUnity.Systems;
using EngineXMath = EngineX.Baseline.Math;

namespace EngineXForUnity.Demo
{

    public struct CircleData : IComponentData
    {
        
    }

    public struct OrbitJob : IJobParallelForBatch
    {
        public NativeArray<ChunkHandle> Chunks;
        public FP AngularSpeed;
        public FP DeltaTime;

        public void Execute(int startIndex, int count)
        {
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

        private static readonly FP AngularSpeed = FP.FromFloat(-0.62831853f);

        private EntityQuery _query;
        public JobHandle Handle;
        public FP DeltaTime = FP.One / 50;
        private NativeArray<ChunkHandle> _chunks = new NativeArray<ChunkHandle>(0, Allocator.Persistent);

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<TransformData, CircleData>();
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

    public class CircleDemo : IGame
    {
        private const string SphereResourcePath = "Demo/Sphere";
        private const string CubeResourcePath = "Demo/Cube";
        private const string MaterialResourcePath = "Demo/BaseMaterial";

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
                _world.AddComponent(e, new CircleData());
                _world.AddComponent(e, TransformData.FromEuler(
                    position,
                    new EngineXMath.Vector3(FP.Zero, angle * FP.Rad2Deg, FP.Zero),
                    new EngineXMath.Vector3(scale, scale, scale)));
                // 球体与正方体间隔排列，验证 RenderData 的 per-entity 资源声明
                string meshPath = (i % 2) == 0 ? SphereResourcePath : CubeResourcePath;
                _world.AddComponent(e, new RenderData(meshPath, MaterialResourcePath));
            }

            // 输入实体：声明"我需要输入"，由适配层 InputBridgeSystem 每帧填充
            var inputEntity = _world.CreateEntity();
            _world.AddComponent(inputEntity, new InputData());
            
            // UI实体，一个非常简单的UI，上面有一个数字文本不断累加
            var dialogEntity = _world.CreateEntity();
            _world.AddComponent(dialogEntity, new DemoDialogData());

            _group.Add(_orbitSystem);
            _group.Add(new CameraControlSystem());
            _group.Add(new DemoDialogSystem());
            _group.Create(_world);

            // 相机实体：变换来自 TransformData（俯视原点），参数来自 CameraData
            var cameraEntity = _world.CreateEntity();
            _world.AddComponent(cameraEntity, TransformData.FromEuler(
                new EngineXMath.Vector3(FP.Zero, FP.FromInt(6), FP.FromInt(-10)),
                new EngineXMath.Vector3(FP.FromInt(25), FP.Zero, FP.Zero)));
            _world.AddComponent(cameraEntity, new CameraData(FP.FromInt(60)));

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
