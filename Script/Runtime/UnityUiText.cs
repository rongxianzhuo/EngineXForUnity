using EngineX.UI;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 IUiText：委托给 UnityEngine.UI.Text。
    /// null/空字符串统一视为空字符串，避免 Unity 内部异常。
    /// </summary>
    public class UnityUiText : UnityUiElementBase, IUiText
    {
        private readonly Text _text;

        public UnityUiText(Text text) : base(text.gameObject)
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