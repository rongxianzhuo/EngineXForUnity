using EngineX.Baseline.FixedPoint;
using EngineX.ECS;
using EngineX.ECS.Components;
using EngineX.Jobs;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace EngineXForUnity.Systems
{
    /// <summary>
    /// 输入桥接系统（适配层）：读取 Unity 输入，写入游戏世界的 InputData 组件。
    /// 语义：游戏侧创建挂 InputData 的实体声明"我需要输入"，本系统负责填充。
    /// 必须在游戏模拟之前执行（UnityAdapter.FixedUpdate 中先于 IGame.Update）。
    /// </summary>
    public sealed class InputBridgeSystem : ISystem
    {
        private EntityQuery _query;
        private Chunk[] _chunks = new Chunk[0];

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<InputData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var needed = _query.CalculateChunkCount();
            if (_chunks.Length != needed)
            {
                _chunks = new Chunk[needed];
            }
            _query.ToChunkArray(_chunks);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // 新 Input System：直接读键盘状态（值域 -1/0/1）
            var keyboard = Keyboard.current;
            float h = 0f;
            float v = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
            }
            FP horizontal = FP.FromFloat(h);
            FP vertical = FP.FromFloat(v);
#else
            // 旧输入管理器：Unity 输入轴（键盘 A/D、←/→ 与 W/S、↑/↓，值域 -1..1）
            FP horizontal = FP.FromFloat(Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f));
            FP vertical = FP.FromFloat(Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f));
#endif

            for (int i = 0; i < _chunks.Length; i++)
            {
                var chunk = _chunks[i];
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var input = ref chunk.GetComponentRef<InputData>(e);
                    input.Horizontal = horizontal;
                    input.Vertical = vertical;
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
