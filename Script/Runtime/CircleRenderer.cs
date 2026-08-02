using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineX.Demo
{
    /// <summary>
    /// EngineXForUnity 渲染层示例：
    /// 每帧从 ECS 的 RenderCollectSystem 读取渲染实例，用 GPU Instancing 真正绘制出球体。
    /// 需要与 CircleDemo 挂在同一个 GameObject 上。
    /// </summary>
    [RequireComponent(typeof(CircleDemo))]
    public sealed class CircleRenderer : MonoBehaviour
    {
        [Tooltip("按 CircleRender.ColorIndex 取色的调色板")]
        public Color[] Palette =
        {
            new Color(0.95f, 0.55f, 0.45f),
            new Color(0.45f, 0.80f, 0.95f),
            new Color(0.60f, 0.95f, 0.55f),
            new Color(0.95f, 0.90f, 0.50f),
        };

        [Tooltip("单次实例化绘制的数量上限（平台限制 1023）")]
        public int MaxInstancesPerDraw = 1023;

        private CircleDemo _demo;
        private Mesh _mesh;
        private Material[] _materials;
        private Matrix4x4[] _drawBuffer;

        private void Awake()
        {
            _demo = GetComponent<CircleDemo>();
            if (_demo == null)
            {
                Debug.LogError("CircleRenderer 需要与 CircleDemo 挂在同一个 GameObject 上。");
                enabled = false;
                return;
            }

            // 复用 Unity 内置球体网格
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);

            // 为调色板中的每种颜色准备一个可实例化的材质
            _materials = new Material[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                var mat = new Material(FindLitShader());
                mat.color = Palette[i];
                mat.enableInstancing = true;
                _materials[i] = mat;
            }

            _drawBuffer = new Matrix4x4[Mathf.Max(1, MaxInstancesPerDraw)];
        }

        private void LateUpdate()
        {
            if (_demo == null || !_demo.RenderSystem.Instances.IsCreated)
            {
                return;
            }

            var instances = _demo.RenderSystem.Instances;
            int count = instances.Length;
            if (count == 0)
            {
                return;
            }

            // 按颜色分组，同色实例合并到一批里
            var batches = new List<Matrix4x4>[Palette.Length];
            for (int c = 0; c < batches.Length; c++)
            {
                batches[c] = new List<Matrix4x4>();
            }

            for (int i = 0; i < count; i++)
            {
                var inst = instances[i];
                int colorIndex = Mathf.Abs(inst.ColorIndex) % Palette.Length;
                batches[colorIndex].Add(Matrix4x4.TRS(
                    new Vector3(inst.PosX, inst.PosY, inst.PosZ),
                    Quaternion.Euler(0f, inst.Angle * Mathf.Rad2Deg, 0f),
                    Vector3.one * inst.Scale));
            }

            // 逐颜色分批实例化绘制
            for (int c = 0; c < batches.Length; c++)
            {
                var list = batches[c];
                if (list.Count == 0)
                {
                    continue;
                }
                for (int b = 0; b < list.Count; b += _drawBuffer.Length)
                {
                    int batchCount = Mathf.Min(_drawBuffer.Length, list.Count - b);
                    list.CopyTo(b, _drawBuffer, 0, batchCount);
                    DrawInstanced(c, batchCount);
                }
            }
        }

        private void DrawInstanced(int colorIndex, int count)
        {
            var material = _materials[colorIndex];
#if UNITY_2022_2_OR_NEWER
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                // 渲染前剔除用的包围盒，覆盖轨道范围即可
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
            };
            Graphics.RenderMeshInstanced(rp, _mesh, 0, _drawBuffer, count, null);
#else
            Graphics.DrawMeshInstanced(_mesh, 0, material, _drawBuffer, count, null,
                ShadowCastingMode.On, true);
#endif
        }

        private static Shader FindLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }
            shader = Shader.Find("HDRP/Lit");
            if (shader != null)
            {
                return shader;
            }
            return Shader.Find("Standard");
        }

        private void OnDestroy()
        {
            if (_materials == null)
            {
                return;
            }
            foreach (var mat in _materials)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
        }
    }
}
