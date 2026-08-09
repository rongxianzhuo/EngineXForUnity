using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace EngineXForUnity.AssetManagement
{

    public static class AssetLoader
    {

        private static readonly Dictionary<string, int> _tempLoadedAssets = new Dictionary<string, int>();

        internal static IEnumerator Initialize()
        {
            yield break;
        }

        public static async Task<GameObject> InstantiatePrefab(string key
            , Transform parent
            , Vector3 position
            , Quaternion rotation
            , Func<bool> isInterrupted)
        {
            var handler = await LoadAsset<GameObject>(key);
            if (isInterrupted())
            {
                handler.Release();
                return null;
            }
            if (handler.Asset == null) return null;
            var ret = Object.Instantiate(handler.Asset, position, rotation, parent);
            ret.name = handler.Asset.name;
            ret.AddComponent<AddressableGameObject>().AddressableHandle = handler;
            return ret;
        }

        public static async Task<GameObject> InstantiatePrefab(string key
            , Transform parent
            , Func<bool> isInterrupted)
        {
            var handler = await LoadAsset<GameObject>(key);
            if (isInterrupted())
            {
                handler.Release();
                return null;
            }
            if (handler.Asset == null) return null;
            var ret = Object.Instantiate(handler.Asset, parent);
            ret.name = handler.Asset.name;
            ret.AddComponent<AddressableGameObject>().AddressableHandle = handler;
            return ret;
        }

        public static async Task<GameObject> InstantiatePrefabAtPosition(string key
            , Transform position
            , Func<bool> isInterrupted)
        {
            var handler = await LoadAsset<GameObject>(key);
            if (isInterrupted())
            {
                handler.Release();
                return null;
            }
            if (handler.Asset == null) return null;
            var ret = Object.Instantiate(handler.Asset
                , position.position + handler.Asset.transform.position
                , handler.Asset.transform.rotation);
            ret.name = handler.Asset.name;
            ret.AddComponent<AddressableGameObject>().AddressableHandle = handler;
            return ret;
        }

        public static async Task<string> LoadTextAsset(string key)
        {
            var handler = await LoadAsset<TextAsset>(key);
            if (handler.Asset == null) return string.Empty;
            var ret = handler.Asset.text;
            handler.Release();
            return ret;
        }

        public static async Task<byte[]> LoadTextAssetBytes(string key)
        {
            var handler = await LoadAsset<TextAsset>(key);
            if (handler.Asset == null) return Array.Empty<byte>();
            var ret = handler.Asset.bytes;
            handler.Release();
            return ret;
        }

        public static async Task LoadScene(string key)
        {
            var sceneHandle = Addressables.LoadSceneAsync(key);
            await sceneHandle.Task;
        }

        public static async Task<AssetHandler<T>> LoadAsset<T>(string key) where T : Object
        {
            var handler = Addressables.LoadAssetAsync<T>(key);
            await handler.Task;
            if (!handler.IsValid())
            {
                Debug.LogError($"No asset='{typeof(T)}' in {key}");
                return default;
            }
            return new AssetHandler<T>(handler, key);
        }

        internal static void AddTempAssetReference(string key)
        {
            if (!_tempLoadedAssets.TryGetValue(key, out var count))
            {
                count = 0;
            }
            count++;
            _tempLoadedAssets[key] = count;
        }

        internal static void RemoveTempAssetReference(string key)
        {
            if (!_tempLoadedAssets.TryGetValue(key, out var count))
            {
                count = 0;
            }
            count--;
            if (count < 0)
            {
                Debug.LogError("Unknown error");
                return;
            }
            _tempLoadedAssets[key] = count;
        }

    }

}