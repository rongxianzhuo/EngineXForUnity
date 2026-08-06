using UnityEngine;
using Object = System.Object;

namespace EngineXForUnity.AssetLoader
{
    public interface IResourceLoader
    {
        Mesh LoadMesh(string resourcePath);
        Material LoadMaterial(string resourcePath);
    }
}