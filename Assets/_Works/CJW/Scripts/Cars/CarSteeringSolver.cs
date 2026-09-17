using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>
    /// 자전거 모델과 Pure Pursuit의 수식만 모아둔 곳. 상태를 들고 있지 않은 순수 함수들이다.
    ///
    /// 기호는 이렇게 읽는다.
    ///   L  = 축거(앞축과 뒷축 사이 거리)
    ///   δ  = 조향각
    ///   R  = 회전 반경        R = L / tan δ
    ///   κ  = 곡률(반경의 역수) κ = tan δ / L
    ///   Ld = Lookahead(몇 m 앞의 점을 겨누는가)
    ///   α  = 차 정면과 목표점 사이의 각
    /// </summary>
    public static class CarSteeringSolver
    {
        /// <summary>최소 회전 반경. R = L / tan δ_max</summary>
        public static float MinTurnRadius(float wheelBase, float maxSteerAngleDeg)
        {
            return wheelBase / Mathf.Tan(maxSteerAngleDeg * Mathf.Deg2Rad);
        }

        /// <summary>최대 곡률. 최소 회전 반경의 역수와 같다.</summary>
        public static float MaxCurvature(float wheelBase, float maxSteerAngleDeg)
        {
            return Mathf.Tan(maxSteerAngleDeg * Mathf.Deg2Rad) / wheelBase;
        }

        /// <summary>월드 지점을 차 기준 평면 좌표(x = 오른쪽, z = 정면)로 바꾼다. transform.InverseTransformPoint는 스케일로 나눠 프리팹 스케일이 1이 아니면 거리가 틀어지므로 쓰지 않는다.</summary>
        public static Vector3 ToLocalPlanar(Transform origin, Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - origin.position;
            delta.y = 0f;

            Vector3 right = origin.right;
            Vector3 forward = origin.forward;
            right.y = 0f;
            forward.y = 0f;

            return new Vector3(
                Vector3.Dot(delta, right.normalized),
                0f,
                Vector3.Dot(delta, forward.normalized));
        }

        /// <summary>이 목표점을 따라가려면 Ld가 최소 얼마여야 하는가. Ld ≥ 2·R_min·sin α — 이보다 작으면 최소 회전 반경보다 작은 원이 필요해 차가 따라갈 수 없다.</summary>
        public static float RequiredLookahead(Vector3 local, float minTurnRadius)
        {
            float sinAlpha = Mathf.Abs(local.x) / Mathf.Max(local.magnitude, 1e-4f);
            return 2f * minTurnRadius * sinAlpha;
        }

        /// <summary>전진해서 이 목표에 닿을 때 쓸 곡률(κ = 2x / Ld²). 목표가 최소 회전원 안에 들어와 닿을 수 없으면 0(직진)을 돌려준다.</summary>
        public static float TargetCurvature(Vector3 local, float maxCurvature, float minTurnRadius)
        {
            float curvature = DesiredCurvature(local, maxCurvature);

            if (curvature != 0f && IsInsideTurningCircle(local, curvature, minTurnRadius))
            {
                return 0f;
            }

            return curvature;
        }

        /// <summary>이 목표 쪽으로 돌고 싶은 방향의 곡률. 닿을 수 있는지는 따지지 않는다. 후진 중에는 속도 부호 때문에 이 값을 뒤집어 쓴다.</summary>
        public static float DesiredCurvature(Vector3 local, float maxCurvature)
        {
            // 목표가 옆이나 뒤에 있으면 원호 공식이 무너진다. 가까운 쪽으로 최대로 꾫는다.
            if (local.z <= 0.01f)
            {
                return local.x >= 0f ? maxCurvature : -maxCurvature;
            }

            float curvature = 2f * local.x / Mathf.Max(local.sqrMagnitude, 1e-4f);
            return Mathf.Clamp(curvature, -maxCurvature, maxCurvature);
        }

        /// <summary>지금 자세에서 전진만으로 이 목표에 닿을 수 있는가. false면 물러나서 각을 벌어야 한다.</summary>
        public static bool CanReachForward(Vector3 local, float minTurnRadius)
        {
            // 옆이거나 뒤에 있다. 전진으로는 안 된다.
            if (local.z <= 0.01f)
            {
                return false;
            }

            float curvature = 2f * local.x / Mathf.Max(local.sqrMagnitude, 1e-4f);

            // 정면 직진이라 회전원을 따질 일이 없다.
            if (curvature == 0f)
            {
                return true;
            }

            return !IsInsideTurningCircle(local, curvature, minTurnRadius);
        }

        /// <summary>목표점이 최소 회전원 안에 들어와 있는가. 회전원 중심은 차 기준 (±R, 0)이다.</summary>
        private static bool IsInsideTurningCircle(Vector3 local, float curvature, float minTurnRadius)
        {
            Vector3 center = new Vector3(Mathf.Sign(curvature) * minTurnRadius, 0f, 0f);
            return (local - center).magnitude < minTurnRadius;
        }

        /// <summary>곡률 κ로 돌 때 허용 횡가속도를 넘지 않는 최대 속도. v = √(a_max / κ).</summary>
        public static float CurveSpeedLimit(float curvature, float maxLateralAccel)
        {
            return Mathf.Sqrt(maxLateralAccel / Mathf.Max(Mathf.Abs(curvature), 1e-4f));
        }

        /// <summary>남은 거리 안에 멈추려면 지금 낼 수 있는 최대 속도. v = √(2ad).</summary>
        public static float StopSpeedLimit(float distance, float brakeAccel)
        {
            return Mathf.Sqrt(2f * brakeAccel * Mathf.Max(distance, 0f));
        }

        /// <summary>곡률을 조향각으로. δ = atan(L·κ)</summary>
        public static float CurvatureToSteer(float curvature, float wheelBase)
        {
            return Mathf.Atan(wheelBase * curvature);
        }

        /// <summary>조향각을 곡률로. κ = tan δ / L</summary>
        public static float SteerToCurvature(float steerRad, float wheelBase)
        {
            return Mathf.Tan(steerRad) / wheelBase;
        }
    }
}
