using System.Collections.Generic;
using EngineX.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 Dialog：包装 UGUI prefab 实例，按名递归索引子树中的
    /// TextMeshProUGUI/Image/Button 元素，提供 GetChild/SetVisible/Dispose。
    /// Dispose 幂等，可重复调用。
    /// </summary>
    public class UnityDialog : IDialog
    {
        private readonly GameObject _gameObject;
        private readonly Dictionary<string, IUiElement> _index = new Dictionary<string, IUiElement>();
        private bool _disposed;

        public UnityDialog(GameObject gameObject)
        {
            _gameObject = gameObject;
            BuildIndex(_gameObject.transform);
        }

        public T GetChild<T>(string name) where T : IUiElement
        {
            if (_disposed || string.IsNullOrEmpty(name))
            {
                return default;
            }
            return _index.TryGetValue(name, out var element) ? (T)element : default;
        }

        public IDialog GetChild(string name)
        {
            if (_disposed || string.IsNullOrEmpty(name))
            {
                return null;
            }
            return _index.TryGetValue(name, out var element) ? element : null;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || _gameObject == null)
            {
                return;
            }
            _gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
            }
        }

        private void BuildIndex(Transform node)
        {
            var text = node.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                _index[node.name] = new UnityUiText(text);
            }
            var image = node.GetComponent<Image>();
            if (image != null)
            {
                _index[node.name] = new UnityUiImage(image);
            }
            var button = node.GetComponent<Button>();
            if (button != null)
            {
                _index[node.name] = new UnityUiButton(button);
            }
            for (int i = 0; i < node.childCount; i++)
            {
                BuildIndex(node.GetChild(i));
            }
        }
    }
}