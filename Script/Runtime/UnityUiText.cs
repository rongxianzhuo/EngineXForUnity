using EngineX.UI;
using TMPro;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 IUiText：委托给 TextMeshProUGUI。
    /// null/空字符串统一视为空字符串，避免 TMP 内部异常。
    /// </summary>
    public class UnityUiText : UnityUiElementBase, IUiText
    {
        private readonly TextMeshProUGUI _text;

        public UnityUiText(TextMeshProUGUI text) : base(text.gameObject)
        {
            _text = text;
        }

        public void SetText(string text)
        {
            if (_text != null)
            {
                _text.text = text ?? string.Empty;
            }
        }
    }
}