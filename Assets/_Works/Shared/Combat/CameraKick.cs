using UnityEngine;

namespace _Works.Shared.Combat
{
    /// <summary>때린 쪽 화면을 순간 튕겼다가 스프링처럼 되돌린다. 카메라 오브젝트에 붙어 반동만 얹는다 —
    /// 다른 스크립트가 매 프레임 카메라 회전을 새로 잡든 안 잡든, 지난 프레임에 얹은 반동을 걷어 낸 뒤 다시 얹으므로 쌓이지 않는다.
    /// 카메라 회전을 잡는 스크립트(LateUpdate)보다 늦게 돌아야 해서 실행 순서를 뒤로 둔다.</summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CameraKick : MonoBehaviour
    {
        /// <summary>되돌아오는 힘. 클수록 빨리 돌아온다.</summary>
        private const float Stiffness = 260f;

        /// <summary>되돌아올 때 흔들림을 죽이는 정도. 작을수록 몇 번 더 출렁인다.</summary>
        private const float Damping = 20f;

        /// <summary>연타해도 화면이 이 각도(도) 넘게 돌아가지 않는다.</summary>
        private const float MaxAngle = 12f;

        private Vector3 _angle;
        private Vector3 _velocity;
        private Quaternion _applied = Quaternion.identity;

        /// <summary><paramref name="attacker"/>가 보는 카메라를 <paramref name="strength"/>(도)만큼 튕긴다.
        /// 때린 쪽을 모르면(던진 물건) 주 카메라를 튕긴다. 때린 쪽에 카메라가 없으면 아무것도 하지 않는다.</summary>
        public static void Kick(GameObject attacker, float strength)
        {
            if (strength <= 0f)
            {
                return;
            }

            Camera cam = attacker != null ? attacker.GetComponentInChildren<Camera>() : Camera.main;
            if (cam == null)
            {
                return;
            }

            CameraKick kick = cam.GetComponent<CameraKick>();
            if (kick == null)
            {
                kick = cam.gameObject.AddComponent<CameraKick>();
            }

            kick.Add(strength);
        }

        public void Add(float strength)
        {
            // 위로 튀는 게 기본이고, 좌우·기울기는 매번 조금씩 달리해 같은 반동이 반복돼 보이지 않게 한다.
            _angle += new Vector3(-strength,
                                  Random.Range(-0.4f, 0.4f) * strength,
                                  Random.Range(-0.8f, 0.8f) * strength);
            _angle = Vector3.ClampMagnitude(_angle, MaxAngle);
        }

        private void Update()
        {
            // 지난 프레임에 얹은 반동을 걷어 낸다. 회전을 매 프레임 새로 잡는 스크립트가 있으면 어차피 그 값으로 덮이고,
            // 없으면 여기서 원래 자세로 돌아온다. 어느 쪽이든 반동이 쌓이지 않는다.
            if (_applied != Quaternion.identity)
            {
                transform.localRotation *= Quaternion.Inverse(_applied);
                _applied = Quaternion.identity;
            }
        }

        private void LateUpdate()
        {
            // 히트스톱으로 시간이 거의 멈춰도 반동은 보여야 한다. 실제 시간으로 돌린다.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _velocity += (-Stiffness * _angle - Damping * _velocity) * dt;
            _angle += _velocity * dt;

            if (_angle.sqrMagnitude < 1e-6f && _velocity.sqrMagnitude < 1e-4f)
            {
                _angle = Vector3.zero;
                _velocity = Vector3.zero;
                return;
            }

            _applied = Quaternion.Euler(_angle);
            transform.localRotation *= _applied;
        }

        private void OnDisable()
        {
            if (_applied != Quaternion.identity)
            {
                transform.localRotation *= Quaternion.Inverse(_applied);
                _applied = Quaternion.identity;
            }

            _angle = Vector3.zero;
            _velocity = Vector3.zero;
        }
    }
}
