using EngineX.UI;
using UnityEngine;

namespace EngineXForUnity.UI
{
    public abstract class UnityUiElementBase : IUiElement
    {
        protected readonly GameObject GameObject;

        protected UnityUiElementBase(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        public virtual T GetChild<T>(string name) where T : IUiElement
        {
            return default;
        }

        public virtual IDialog GetChild(string name)
        {
            return null;
        }

        public virtual void SetVisible(bool visible)
        {
            if (GameObject != null)
            {
                GameObject.SetActive(visible);
            }
        }

        public virtual void Dispose()
        {
            // 由 UnityDialog 统一管理 GameObject 生命周期
        }
    }
}