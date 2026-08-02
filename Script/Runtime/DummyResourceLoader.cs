using UnityEngine;

namespace EngineX.Demo
{
    public class DummyResourceLoader : IResourceLoader
    {
        public Mesh LoadMesh(string resourcePath)
        {
            var go = Resources.Load<GameObject>(resourcePath);
            if (!go) return null;
            var filter = go.GetComponent<MeshFilter>();
            if (!filter) return null;
            return filter.sharedMesh;
        }

        public Material LoadMaterial(string resourcePath)
        {
            return Resources.Load<Material>(resourcePath);
        }
    }
}