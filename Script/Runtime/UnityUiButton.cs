using EngineX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 IUiButton：委托给 UnityEngine.UI.Button。
    /// IsPressed 基于 IPointerDownHandler/IPointerUpHandler 轮询状态实现，
    /// 符合 ECS 轮询交互约定（不引入事件订阅）。
    /// SetEnabled(false) 同时清空按下状态，防止禁用期间残留。
    /// </summary>
    public class UnityUiButton : UnityUiElementBase, IUiButton, IPointerDownHandler, IPointerUpHandler
    {
        private readonly Button _button;
        private bool _isPressed;

        public UnityUiButton(Button button) : base(button.gameObject)
        {
            _button = button;
        }

        public bool IsPressed()
        {
            return _isPressed;
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
                _isPressed = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
        }
    }
}