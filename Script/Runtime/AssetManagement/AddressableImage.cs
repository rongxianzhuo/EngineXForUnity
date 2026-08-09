using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace EngineXForUnity.AssetManagement
{
    
    [RequireComponent(typeof(Image))]
    public class AddressableImage : MonoBehaviour
    {

        public Color imageColor = Color.white;

        private string _key;
        private Image _image;
        private AssetHandler<Sprite> _spriteAssetHandler;
        private AssetHandler<SpriteAtlas> _spriteAtlasAssetHandler;
        private bool _isDestroyed;

        public Image ImageComponent
        {
            get
            {
                if (_image == null) _image = GetComponent<Image>();
                return _image;
            }
        }

        private void Awake()
        {
            if (ImageComponent.sprite == null && string.IsNullOrEmpty(_key))
            {
                ImageComponent.color = Color.clear;
            }
        }

        public void SetSprite(Sprite sprite)
        {
            ImageComponent.sprite = sprite;
            ImageComponent.color = imageColor;
        }

        public async void SetSpriteAtlas(string atlasKey, string spriteName)
        {
            var key = $"{atlasKey}@{spriteName}";
            if (key == _key) return;
            Release();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(spriteName))
            {
                ImageComponent.sprite = null;
                ImageComponent.color = imageColor;
                return;
            }
            ImageComponent.color = Color.clear;
            _key = key;
            var handle = await AssetLoader.LoadAsset<SpriteAtlas>(atlasKey);
            if (_isDestroyed || _key != key || handle.Asset == null)
            {
                handle.Release();
                return;
            }

            _spriteAtlasAssetHandler = handle;
            ImageComponent.sprite = handle.Asset.GetSprite(spriteName);
            ImageComponent.color = imageColor;
        }

        public async void SetSpriteKey(string key)
        {
            if (key == _key)
            {
                if (string.IsNullOrEmpty(key)) ImageComponent.color = Color.clear;
                else ImageComponent.color = imageColor;
                return;
            }
            Release();
            if (string.IsNullOrEmpty(key))
            {
                ImageComponent.sprite = null;
                ImageComponent.color = imageColor;
                return;
            }
            ImageComponent.color = Color.clear;
            _key = key;
            var handle = await AssetLoader.LoadAsset<Sprite>(key);
            if (_isDestroyed || _key != key || handle.Asset == null)
            {
                handle.Release();
                return;
            }

            _spriteAssetHandler = handle;
            ImageComponent.sprite = handle.Asset;
            ImageComponent.color = imageColor;
        }

        public void Clear()
        {
            if (string.IsNullOrEmpty(_key)) return;
            _key = null;
            Release();
            ImageComponent.sprite = null;
            ImageComponent.color = Color.clear;
        }

        private void Release()
        {
            _spriteAssetHandler.Release();
            _spriteAtlasAssetHandler.Release();
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            Release();
        }
    }
}
