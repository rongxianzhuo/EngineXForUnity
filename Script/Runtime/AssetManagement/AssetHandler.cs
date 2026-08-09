using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace EngineXForUnity.AssetManagement
{
    public readonly struct AssetHandler<T> where T : Object
    {

        private readonly AsyncOperationHandle<T> _handle;

        public readonly bool IsPreload;

        public readonly string Key;

        private readonly T _asset;

        public T Asset => IsValid ? _handle.Result : _asset;

        public bool IsValid => _handle.IsValid();

        internal AssetHandler(AsyncOperationHandle<T> handle, string key)
        {
            _handle = handle;
            Key = key;
            IsPreload = false;
            _asset = null;
#if UNITY_EDITOR
            AssetLoader.AddTempAssetReference(key);
#endif
        }

        internal AssetHandler(T t, string key)
        {
            _handle = default;
            Key = key;
            IsPreload = true;
            _asset = t;
        }

        public void Release()
        {
            if (IsPreload) return;
            if (!IsValid) return;
            _handle.Release();
#if UNITY_EDITOR
            AssetLoader.RemoveTempAssetReference(Key);
#endif
        }

    }
}