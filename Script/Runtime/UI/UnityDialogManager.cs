using System.Collections.Generic;
using EngineX.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EngineXForUnity.UI
{
    /// <summary>
    /// Unity 实现的 DialogManager：按名称从 Resources/UI/ 加载 prefab，
    /// 实例化到 Canvas 下，返回包装后的 UnityDialog。
    /// </summary>
    public class UnityDialogManager : IDialogManager
    {
        private const string ResourceRoot = "Game/";
        private const string CanvasPath = "Game/UI/DialogCanvas";

        private readonly Canvas _canvas = Object.Instantiate(Resources.Load<Canvas>(CanvasPath));
        private readonly Dictionary<string, IDialog> _dialogs = new Dictionary<string, IDialog>();

        public IDialog Show(string name)
        {
            if (_dialogs.TryGetValue(name, out var dialog))
            {
                return dialog;
            }
            var prefab = Resources.Load<GameObject>(ResourceRoot + name);
            if (prefab == null)
            {
                Debug.LogError("Can't find UI Prefab: " + name);
                return null;
            }
            var instance = Object.Instantiate(prefab, _canvas.transform);
            instance.name = name;
            dialog = new UnityDialog(instance);
            _dialogs.Add(name, dialog);
            return dialog;
        }
    }
}