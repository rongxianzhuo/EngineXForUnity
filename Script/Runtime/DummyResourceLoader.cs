using UnityEngine;

namespace EngineX.Demo
{
    public class DummyResourceLoader : IResourceLoader
    {
        public Mesh LoadMesh(string resourcePath)
        {
            return Resources.Load<GameObject>(resourcePath).GetComponent<MeshFilter>().sharedMesh;
        }

        public Material LoadMaterial(string resourcePath)
        {
            return Resources.Load<Material>(resourcePath);
        }
    }
}