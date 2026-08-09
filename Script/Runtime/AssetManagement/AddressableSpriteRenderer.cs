using UnityEngine;
using UnityEngine.U2D;

namespace EngineXForUnity.AssetManagement
{
    
    [RequireComponent(typeof(SpriteRenderer))]
    public class AddressableSpriteRenderer : MonoBehaviour
    {

        public Color imageColor = Color.white;

        private string _key;
        private SpriteRenderer _spriteRenderer;
        private AssetHandler<Sprite> _handleSprite;
        private AssetHandler<SpriteAtlas> _handleSpriteAtlas;
        private bool _isDestroyed;

        public SpriteRenderer SpriteRendererComponent
        {
            get
            {
                if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
                return _spriteRenderer;
            }
        }

        private void Awake()
        {
            if (SpriteRendererComponent.sprite == null && string.IsNullOrEmpty(_key))
            {
                SpriteRendererComponent.color = Color.clear;
            }
        }

        public void SetSprite(Sprite sprite)
        {
            SpriteRendererComponent.sprite = sprite;
            SpriteRendererComponent.color = imageColor;
        }

        public async void SetSpriteAtlas(string atlasKey, string spriteName)
        {
            var key = $"{atlasKey}@{spriteName}";
            if (key == _key) return;
            Release();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(spriteName))
            {
                SpriteRendererComponent.sprite = null;
                SpriteRendererComponent.color = imageColor;
                return;
            }
            _key = key;
            var handle = await AssetLoader.LoadAsset<SpriteAtlas>(atlasKey);
            if (_isDestroyed || _key != key)
            {
                SpriteRendererComponent.color = Color.clear;
                return;
            }

            _handleSpriteAtlas = handle;
            SpriteRendererComponent.sprite = handle.Asset.GetSprite(spriteName);
            SpriteRendererComponent.color = imageColor;
        }

        public async void SetSpriteKey(string key)
        {
            if (key == _key) return;
            Release();
            if (string.IsNullOrEmpty(key))
            {
                SpriteRendererComponent.sprite = null;
                SpriteRendererComponent.color = imageColor;
                return;
            }
            _key = key;
            var handle = await AssetLoader.LoadAsset<Sprite>(key);
            if (_isDestroyed || _key != key)
            {
                SpriteRendererComponent.color = Color.clear;
                return;
            }

            _handleSprite = handle;
            SpriteRendererComponent.sprite = handle.Asset;
            SpriteRendererComponent.color = imageColor;
        }

        private void Release()
        {
            _handleSprite.Release();
            _handleSpriteAtlas.Release();
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            Release();
        }
    }
}
