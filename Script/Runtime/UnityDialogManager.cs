using EngineX.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 DialogManager：按名称从 Resources/UI/ 加载 prefab，
    /// 实例化到 Canvas 下，返回包装后的 UnityDialog。
    /// </summary>
    public class UnityDialogManager : IDialogManager
    {
        private const string ResourceRoot = "UI/";

        private readonly Canvas _canvas;

        public UnityDialogManager()
        {
            _canvas = Object.FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("EngineX Canvas");
                _canvas = go.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.AddComponent<CanvasScaler>();
                go.AddComponent<GraphicRaycaster>();
            }
        }

        public IDialog Load(string name)
        {
            var prefab = Resources.Load<GameObject>(ResourceRoot + name);
            if (prefab == null)
            {
                return null;
            }
            var instance = Object.Instantiate(prefab, _canvas.transform);
            instance.name = name;
            return new UnityDialog(instance);
        }
    }
}