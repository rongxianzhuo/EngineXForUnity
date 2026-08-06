using UnityEngine;

namespace EngineXForUnity.AssetLoader
{
    public static class ResourceLoader
    {
        public static Mesh LoadMesh(string resourcePath)
        {
            var go = Load<GameObject>(resourcePath);
            if (!go) return null;
            var filter = go.GetComponent<MeshFilter>();
            return !filter ? null : filter.sharedMesh;
        }

        public static T Load<T>(string resourcePath) where T : Object
        {
            return Resources.Load<T>(resourcePath);
        }
    }
}