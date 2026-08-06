using EngineX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EngineXForUnity.UI
{
    public class UnityUiButton : UnityUiElementBase, IUiButton
    {
        private bool _pendingPress;

        public UnityUiButton(Button button) : base(button.gameObject)
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            _pendingPress = true;
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
    }
}