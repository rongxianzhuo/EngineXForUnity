using System.Collections.Generic;
using System.Threading.Tasks;
using EngineX.UI;
using EngineXForUnity.AssetManagement;
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
        private const string CanvasPath = "EngineXForUnity/UI/DialogCanvas";

        private readonly Canvas _canvas = Object.Instantiate(Resources.Load<Canvas>(CanvasPath));

        private readonly Dictionary<string, (AssetHandler<GameObject>, UnityDialog)> _dialogs =
            new Dictionary<string, (AssetHandler<GameObject>, UnityDialog)>();

        public IDialog Show(string name)
        {
            if (_dialogs.TryGetValue(name, out var tuple))
            {
                return tuple.Item2;
            }
            _dialogs[name] = default;
            _ = LoadDialog(name);
            return null;
        }

        private async Task LoadDialog(string name)
        {
            var handler = await AssetLoader.LoadAsset<GameObject>($"Assets/Game/Addressable/UI/{name}.prefab");
            if (!_dialogs.ContainsKey(name))
            {
                handler.Release();
                return;
            }
            var instance = Object.Instantiate(handler.Asset, _canvas.transform);
            _dialogs[name] = (handler, new UnityDialog(instance));
        }

        public void Close(string name)
        {
            if (!_dialogs.TryGetValue(name, out var tuple))
            {
                return;
            }
            Object.Destroy(tuple.Item2.Obj);
            tuple.Item1.Release();
            _dialogs.Remove(name);
        }
    }
}