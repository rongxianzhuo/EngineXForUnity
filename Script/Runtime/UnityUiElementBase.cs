using EngineX.UI;
using UnityEngine;

namespace EngineX.Demo
{
    /// <summary>
    /// UI 元素适配层基类：实现 IUiElement 的 GetChild/SetVisible/Dispose 默认行为。
    /// 子类只需关注自身的 UI 元素特性接口（SetText/SetSprite/IsPressed 等）。
    /// GameObject 生命周期由 UnityDialog 统一管理，本类不主动销毁。
    /// </summary>
    internal abstract class UnityUiElementBase : IUiElement
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