using System;
using UnityEngine;
using TomodachiPortData;

namespace TomodachiNative
{
    public static class NativeMath
    {
        public static Vector3 Vector(float[] a) => new Vector3(a[0], a[1], a[2]);
        public static Vector3 Reflect(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        public static Quaternion Rotation(Vector4 r, bool euler)
        {
            Quaternion q = euler
                ? Quaternion.AngleAxis(r.z * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(r.y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(r.x * Mathf.Rad2Deg, Vector3.right)
                : new Quaternion(r.x, r.y, r.z, r.w);
            q = new Quaternion(q.x, -q.y, -q.z, q.w);
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-8f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }
        public static Matrix4x4 Reflect(Matrix4x4 m)
        {
            var c = Matrix4x4.Scale(new Vector3(-1, 1, 1)); return c * m * c;
        }
    }

}
