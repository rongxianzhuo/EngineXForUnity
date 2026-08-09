using System.Collections.Generic;
using EngineX.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EngineXForUnity.UI
{
    /// <summary>
    /// Unity 实现的 Dialog：包装 UGUI prefab 实例，按名递归索引子树中的
    /// TextMeshProUGUI/Image/Button 元素，提供 GetChild/SetVisible/Dispose。
    /// Dispose 幂等，可重复调用。
    /// </summary>
    public class UnityDialog : IDialog
    {
        internal GameObject Obj { get; private set; }

        private readonly Dictionary<string, IUiElement> _index = new Dictionary<string, IUiElement>();

        public UnityDialog(GameObject gameObject)
        {
            Obj = gameObject;
            BuildIndex(Obj.transform);
        }

        public T GetChild<T>(string name) where T : IUiElement
        {
            if (string.IsNullOrEmpty(name))
            {
                return default;
            }
            return _index.TryGetValue(name, out var element) ? (T)element : default;
        }

        private void BuildIndex(Transform node)
        {
            var rawName = node.name;
            if (rawName.Length > 1 && rawName[0] == '$')
            {
                var key = rawName.Substring(1);
                if (_index.ContainsKey(key))
                {
                    Debug.LogWarning($"[UnityDialog] {Obj.name} 中存在重复的可寻址节点名: ${key}");
                }
                var text = node.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    _index[key] = new UnityUiText(text);
                }
                var image = node.GetComponent<Image>();
                if (image != null)
                {
                    _index[key] = new UnityUiImage(image);
                }
                var button = node.GetComponent<Button>();
                if (button != null)
                {
                    _index[key] = new UnityUiButton(button);
                }
            }
            for (int i = 0; i < node.childCount; i++)
            {
                BuildIndex(node.GetChild(i));
            }
        }
    }
}