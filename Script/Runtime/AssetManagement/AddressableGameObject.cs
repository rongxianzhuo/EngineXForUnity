using UnityEngine;

namespace EngineXForUnity.AssetManagement
{

    public class AddressableGameObject : MonoBehaviour
    {

        internal AssetHandler<GameObject> AddressableHandle;

        private void OnDestroy()
        {
            AddressableHandle.Release();
        }

    }

}