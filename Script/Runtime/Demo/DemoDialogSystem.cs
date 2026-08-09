using EngineX.ECS;
using EngineX.UI;
using EngineXForUnity.UI;

namespace EngineXForUnity.Demo
{
    public class DemoDialogSystem : ISystem
    {
        private EntityQuery _query;
        private EntityCommandBuffer _ecb;

        public void OnCreate(ref SystemState state)
        {
            _query = state.World.Query<DemoDialogData>();
            _ecb = new EntityCommandBuffer();
        }

        public void OnUpdate(ref SystemState state)
        {
            var visitor = new DialogVisitor { Ecb = _ecb };
            _query.ForEachChunk<DialogVisitor>(ref visitor);
            _ecb.Playback(state.World);
            _ecb.Clear();
        }

        public void OnDestroy(ref SystemState state)
        {
            _ecb = null;
        }

        internal struct DialogVisitor : IForEachChunk
        {
            public EntityCommandBuffer Ecb;

            public void Execute(Chunk chunk)
            {
                for (int e = 0; e < chunk.Count; e++)
                {
                    ref var data = ref chunk.GetComponentRef<DemoDialogData>(e);
                    var entity = chunk.GetEntityRef(e);

                    var dialog = DialogManager.Show("DemoDialog");
                    if (dialog == null) return;
                    if (dialog.GetChild<UnityUiButton>("Add").IsPressed())
                    {
                        data.Number++;
                        dialog.GetChild<UnityUiText>("Number").SetText(data.Number.ToString());
                    }
                    if (dialog.GetChild<UnityUiButton>("Close").IsPressed())
                    {
                        DialogManager.Close("DemoDialog");
                        Ecb.DestroyEntity(entity);
                    }
                }
            }
        }
    }
}
