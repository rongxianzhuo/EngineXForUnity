using UnityEngine;
using Object = System.Object;

namespace EngineX.Demo
{
    public interface IResourceLoader
    {
        Mesh LoadMesh(string resourcePath);
        Material LoadMaterial(string resourcePath);
    }
}