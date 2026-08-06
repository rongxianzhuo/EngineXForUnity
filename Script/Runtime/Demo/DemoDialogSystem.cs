using EngineX.ECS;
using EngineX.UI;
using EngineXForUnity.UI;

namespace EngineXForUnity.Demo
{
    public class DemoDialogSystem : ISystem
    {
        private struct DialogVisitor : IForEach<DemoDialogData>
        {
            public void Execute(ref DemoDialogData data)
            {
                var dialog = DialogManager.Show("DemoDialog");
                if (dialog.GetChild<UnityUiButton>("Add").IsPressed())
                {
                    data.Number++;
                    var text = dialog.GetChild<UnityUiText>("Number");
                    text.SetText(data.Number.ToString());
                }
                if (dialog.GetChild<UnityUiButton>("Close").IsPressed())
                {
                    UnityEngine.Debug.Log("Click close");
                }
            }
        }
        
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var visitor = new DialogVisitor();
            state.World.Query<DemoDialogData>().ForEach<DialogVisitor, DemoDialogData>(ref visitor);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}