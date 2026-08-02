using EngineX.ECS;
using UnityEngine;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 入口适配器：持有 IGame（纯逻辑）与渲染 SystemsGroup。
    /// 模拟在 FixedUpdate 驱动，渲染在 Update 每帧驱动。
    /// 网格/材质不再由这里注入，统一由实体的 RenderData 组件（Resources 路径）决定。
    /// </summary>
    public class UnityAdapter : MonoBehaviour
    {
        private readonly CircleDemo _circleDemo = new CircleDemo();
        private readonly SystemsGroup _renderGroup = new SystemsGroup();

        private World _world;

        private void Awake()
        {
            _world = _circleDemo.Create();
            _renderGroup.Add(new DemoRenderSystem());
            _renderGroup.Create(_world);
        }

        private void FixedUpdate()
        {
            _circleDemo.Update();
        }

        private void Update()
        {
            _renderGroup.Update(_world);
        }

        private void OnDestroy()
        {
            _circleDemo.Destroy();
            _renderGroup.Destroy();
        }
    }
}
