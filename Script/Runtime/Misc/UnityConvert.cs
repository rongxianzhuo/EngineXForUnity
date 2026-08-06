using EngineX.Baseline.FixedPoint;
using EngineXMath = EngineX.Baseline.Math;
using UnityEngine;

namespace EngineXForUnity.Misc
{
    /// <summary>
    /// EngineX 定点数 → Unity float 的边界转换工具（含防御逻辑）。
    /// 适配层所有"组件数据 → Unity 对象"的转换都走这里。
    /// </summary>
    internal static class UnityConvert
    {
        public static Vector3 ToVector3(EngineXMath.Vector3 v)
        {
            return new Vector3(v.X.Single(), v.Y.Single(), v.Z.Single());
        }

        public static Quaternion ToQuaternion(EngineXMath.Quaternion q)
        {
            // 防御 1：默认构造的组件旋转是零四元数 (0,0,0,0)，不是单位四元数
            if (q.X == FP.Zero && q.Y == FP.Zero && q.Z == FP.Zero && q.W == FP.Zero)
            {
                return Quaternion.identity;
            }
            // 防御 2：定点数截断会产生范数漂移，
            // Matrix4x4.TRS / Transform 要求单位四元数，边界处归一化
            q = q.Normalized;
            return new Quaternion(q.X.Single(), q.Y.Single(), q.Z.Single(), q.W.Single());
        }
    }
}
