using System;
using EngineX.ECS;
using UnityEngine;

namespace EngineX.Demo
{
    public class UnityAdapter : MonoBehaviour
    {
        public Material material;
        
        private readonly CircleDemo _circleDemo = new CircleDemo();
        private readonly SystemsGroup _renderGroup = new SystemsGroup();
        
        private World _world;
        
        private void Awake()
        {
            _world = _circleDemo.Create();
            _renderGroup.Add(new DemoRenderSystem(material));
            _renderGroup.Create(_world);
        }

        private void FixedUpdate()
        {
            _circleDemo.Update();
        }

        private void Update()
        {
            _renderGroup.Update(_world);
        }

        private void OnDestroy()
        {
            _circleDemo.Destroy();
            _renderGroup.Destroy();
        }
    }
}