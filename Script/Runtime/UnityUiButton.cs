using EngineX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 IUiButton：委托给 UnityEngine.UI.Button。
    /// IsPressed 采用边沿触发（edge-triggered）—— 每次 PointerDown 触发一次
    /// "待消费"事件，逻辑层在下一个游戏逻辑帧调用 IsPressed 时返回 true 并自动
    /// 清零，确保一次点击只存在一个逻辑帧的"按下"信号（避免长按连续触发动作）。
    /// 符合 ECS 轮询交互约定（不引入事件订阅）。
    /// SetEnabled(false) 同时清掉待消费事件，防止禁用时残留。
    /// </summary>
    public class UnityUiButton : UnityUiElementBase, IUiButton, IPointerDownHandler, IPointerUpHandler
    {
        private readonly Button _button;
        private bool _pendingPress;

        public UnityUiButton(Button button) : base(button.gameObject)
        {
            _button = button;
        }

        public bool IsPressed()
        {
            if (_pendingPress)
            {
                _pendingPress = false;
                return true;
            }
            return false;
        }

        public void SetEnabled(bool enabled)
        {
            if (_button == null)
            {
                return;
            }
            _button.interactable = enabled;
            if (!enabled)
            {
                _pendingPress = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pendingPress = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 边沿触发：PointerUp 不再清零，按下事件已在 IsPressed 消费
        }
    }
}