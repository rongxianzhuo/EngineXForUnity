using EngineX.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EngineX.Demo
{
    /// <summary>
    /// Unity 实现的 IUiImage：委托给 UnityEngine.UI.Image，sprite 通过 Resources 加载。
    /// 资源路径约定为 Resources/ 下相对路径，后续可替换为 IResourceLoader.LoadSprite。
    /// </summary>
    public class UnityUiImage : UnityUiElementBase, IUiImage
    {
        private readonly Image _image;

        public UnityUiImage(Image image) : base(image.gameObject)
        {
            _image = image;
        }

        public void SetSprite(string resourcePath)
        {
            if (_image == null)
            {
                return;
            }
            _image.sprite = string.IsNullOrEmpty(resourcePath)
                ? null
                : Resources.Load<Sprite>(resourcePath);
        }
    }
}