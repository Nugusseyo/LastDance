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

        /// <summary>
        /// 월드 지점을 차 기준 평면 좌표(x = 오른쪽, z = 정면)로 바꾼다. 단위는 미터다.
        ///
        /// transform.InverseTransformPoint를 쓰면 안 된다. 그쪽은 스케일로 나누기 때문에
        /// 프리팹 스케일이 1이 아니면 거리 단위가 통째로 틀어진다. 예를 들어 스케일 0.4면
        /// local이 2.5배로 부풀고, κ = 2x / Ld² 는 0.4배로 줄어들어 차가 필요한 만큼 안 돌게 된다.
        ///
        /// right·forward는 스케일과 무관하게 길이가 1이므로, 내적으로 직접 뽑으면
        /// 어떤 스케일에서도 실제 미터가 나온다.
        /// </summary>
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

        /// <summary>
        /// 이 목표점을 따라가려면 Ld가 최소 얼마여야 하는가.
        ///
        /// Pure Pursuit이 그리는 원의 반경은 R = Ld / (2·sin α)다.
        /// 목표점이 옆으로 붙을수록(sin α가 1에 가까울수록) R이 작아지고,
        /// 최소 회전 반경보다 작아지면 차는 따라갈 수 없어 최대 조향에 물린 채 맴돈다.
        ///
        /// 조건을 뒤집으면 Ld ≥ 2·R_min·sin α 가 나온다.
        /// sin α에 걸어둔 덕분에 직선 구간에서는 이 하한이 사실상 사라져
        /// Ld가 짧게 유지되고 경로에 밀착한다. 주차 정확도가 여기서 나온다.
        /// </summary>
        public static float RequiredLookahead(Vector3 local, float minTurnRadius)
        {
            float sinAlpha = Mathf.Abs(local.x) / Mathf.Max(local.magnitude, 1e-4f);
            return 2f * minTurnRadius * sinAlpha;
        }

        /// <summary>
        /// 목표점을 지나는 원의 곡률. κ = 2x / Ld²
        ///
        /// 차를 원점, 정면을 +z로 두면 원은 z축에 접해야 하므로 중심이 (R, 0)에 온다.
        /// (x − R)² + z² = R² 을 풀면 Ld² = 2Rx, 곧 κ = 2x / Ld² 가 나온다.
        ///
        /// 두 가지 예외를 함께 처리한다.
        ///  · 목표점이 옆이나 뒤에 있으면 원호 공식이 무너지므로 최대 조향으로 크게 돌아 나간다.
        ///  · 목표점이 최소 회전원 안에 들어오면 어떤 조향으로도 닿을 수 없다.
        ///    계속 꺾으면 그 자리를 영원히 맴돌므로, 조향을 풀고 직진해 원 밖으로 빠져나온 뒤 다시 붙는다.
        /// </summary>
        public static float TargetCurvature(Vector3 local, float maxCurvature, float minTurnRadius)
        {
            float curvature;

            if (local.z <= 0.01f)
            {
                curvature = local.x >= 0f ? maxCurvature : -maxCurvature;
            }
            else
            {
                curvature = 2f * local.x / Mathf.Max(local.sqrMagnitude, 1e-4f);
                curvature = Mathf.Clamp(curvature, -maxCurvature, maxCurvature);
            }

            if (curvature != 0f && IsInsideTurningCircle(local, curvature, minTurnRadius))
            {
                return 0f;
            }

            return curvature;
        }

        /// <summary>
        /// 목표점이 최소 회전원 안에 들어와 있는가.
        /// 차 기준으로 회전원의 중심은 (±R, 0)이고, 목표점이 그 원 안이면 도달 불가능하다.
        /// </summary>
        private static bool IsInsideTurningCircle(Vector3 local, float curvature, float minTurnRadius)
        {
            Vector3 center = new Vector3(Mathf.Sign(curvature) * minTurnRadius, 0f, 0f);
            return (local - center).magnitude < minTurnRadius;
        }

        /// <summary>
        /// 곡률 κ로 돌 때 허용 횡가속도를 넘지 않는 최대 속도.
        /// 구심가속도 a = v²·κ 에서 v = √(a_max / κ).
        /// 직선(κ = 0)에서 0으로 나누지 않도록 아주 작은 값을 바닥으로 깐다.
        /// </summary>
        public static float CurveSpeedLimit(float curvature, float maxLateralAccel)
        {
            return Mathf.Sqrt(maxLateralAccel / Mathf.Max(Mathf.Abs(curvature), 1e-4f));
        }

        /// <summary>
        /// 남은 거리 안에 멈추려면 지금 낼 수 있는 최대 속도.
        /// 등가속도 공식 v² = v₀² + 2as 에서 도착 속도를 0으로 두면 v = √(2ad).
        /// </summary>
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
