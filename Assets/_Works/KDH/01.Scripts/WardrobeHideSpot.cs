using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts
{
    /// <summary>
    /// 공포게임 스타일의 장롱 숨기 상호작용.
    /// E: 문 열기(일반 문과 동일하게 거리/방향 조건 없음).
    /// 문이 열린 상태에서는 카메라 레이캐스트 + E 입력으로만 반응한다.
    ///  - HallWardrobe_Door_L/R(문 메쉬)을 보고 E -> 문 닫힘.
    ///  - insideCollider(장롱 안쪽)를 보고 E -> 들어가 숨음.
    /// 숨어있는 동안 E: 나오기 / 마우스 좌클릭: 문을 살짝 닫았다 열었다(수동 토글, 훔쳐보기).
    /// </summary>
    [RequireComponent(typeof(HallWardrobeDoor))]
    public class WardrobeHideSpot : MonoBehaviour
    {
        private enum State
        {
            Closed,
            Open,
            Hiding
        }

        [Header("Door")]
        [SerializeField] private HallWardrobeDoor door;

        [Header("Gaze Raycast Targets")]
        [SerializeField] private Camera gazeCamera;
        [SerializeField] private Transform doorMeshL;
        [SerializeField] private Transform doorMeshR;
        [SerializeField] private Collider insideCollider;
        [SerializeField] private float interactDistance = 2f;
        [SerializeField] private float raycastStartOffset = 0.7f;

        [Header("Hide Points")]
        [SerializeField] private Transform interactSpot;
        [SerializeField] private Transform hideSpot;

        [Header("Interaction")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private float enterMoveDuration = 0.4f;

        [Header("Player Refs (auto-found if empty)")]
        [SerializeField] private CharacterController playerController;
        [SerializeField] private MonoBehaviour[] playerBehavioursToDisable;
        [SerializeField] private GameObject playerVisual;

        private State state = State.Closed;
        private Transform playerTransform;
        private Vector3 playerReturnPosition;
        private Quaternion playerReturnRotation;
        private bool hidingDoorOpen;
        private Tween playerMoveTween;
        private Tween playerRotateTween;

        private void Awake()
        {
            if (door == null) door = GetComponent<HallWardrobeDoor>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (playerTransform == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo == null) playerGo = GameObject.Find("Player");
                if (playerGo != null) playerTransform = playerGo.transform;
            }

            bool interactPressed = Keyboard.current[interactKey].wasPressedThisFrame;

            switch (state)
            {
                case State.Closed:
                    // 일반 문(DoorRotate)과 동일하게 거리/시선 조건 없이 E 입력만으로 연다.
                    if (interactPressed)
                    {
                        state = State.Open;
                        door.Open();
                    }
                    break;

                case State.Open:
                    if (interactPressed) HandleOpenInteract();
                    break;

                case State.Hiding:
                    if (interactPressed)
                    {
                        ExitHide();
                    }
                    break;
            }
        }

        private void HandleOpenInteract()
        {
            if (!TryGazeRaycast(out RaycastHit hit)) return;

            if (IsDoorMesh(hit.transform))
            {
                state = State.Closed;
                door.Close();
            }
            else if (IsPartOfWardrobe(hit.transform))
            {
                // 문(L/R)만 아니면 장롱 몸체/바닥 등 어디에 닿아도 안쪽으로 판정한다.
                EnterHide();
            }
        }

        private bool IsDoorMesh(Transform hitTransform)
        {
            return hitTransform == doorMeshL || hitTransform == doorMeshR;
        }

        private bool IsPartOfWardrobe(Transform hitTransform)
        {
            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private bool TryGazeRaycast(out RaycastHit hit)
        {
            Camera cam = gazeCamera != null ? gazeCamera : Camera.main;
            if (cam == null)
            {
                hit = default;
                return false;
            }

            // 카메라가 플레이어 콜라이더 내부에 있으므로, 자기 자신을 맞히지 않도록
            // 레이 시작점을 카메라 앞쪽으로 살짝 띄운다.
            Vector3 origin = cam.transform.position + cam.transform.forward * raycastStartOffset;
            Ray ray = new Ray(origin, cam.transform.forward);
            return Physics.Raycast(ray, out hit, interactDistance - raycastStartOffset, ~0, QueryTriggerInteraction.Collide);
        }

        private void EnterHide()
        {
            if (playerTransform == null || hideSpot == null) return;

            state = State.Hiding;

            playerReturnPosition = interactSpot != null ? interactSpot.position : playerTransform.position;
            playerReturnRotation = interactSpot != null ? interactSpot.rotation : playerTransform.rotation;

            // 애니메이션 없이, 이동만 자연스럽게 트윈으로 처리해서 안으로 걸어 들어가는 것처럼 보이게 한다.
            SetPlayerControlEnabled(false, hideVisual: false);
            playerMoveTween?.Kill();
            playerRotateTween?.Kill();
            playerMoveTween = playerTransform.DOMove(hideSpot.position, enterMoveDuration).SetEase(Ease.InOutSine);
            playerRotateTween = playerTransform.DORotateQuaternion(hideSpot.rotation, enterMoveDuration).SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    if (playerVisual != null) playerVisual.SetActive(false);
                });

            // 숨는 즉시 문을 자연스럽게 닫는다.
            hidingDoorOpen = false;
            door.Close();
        }

        private void ExitHide()
        {
            state = State.Open;
            playerMoveTween?.Kill();
            playerRotateTween?.Kill();

            if (!hidingDoorOpen)
            {
                hidingDoorOpen = true;
                door.Open();
            }

            if (playerTransform != null)
            {
                playerTransform.position = playerReturnPosition;
                playerTransform.rotation = playerReturnRotation;
            }

            SetPlayerControlEnabled(true);
        }

        private void SetPlayerControlEnabled(bool enabled, bool hideVisual = true)
        {
            if (playerController != null) playerController.enabled = enabled;

            if (playerBehavioursToDisable != null)
            {
                foreach (MonoBehaviour behaviour in playerBehavioursToDisable)
                {
                    if (behaviour != null) behaviour.enabled = enabled;
                }
            }

            if (hideVisual && playerVisual != null) playerVisual.SetActive(enabled);
        }

        private void OnDrawGizmosSelected()
        {
            if (hideSpot != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(hideSpot.position, 0.2f);
            }

            if (interactSpot != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(interactSpot.position, 0.15f);
            }
        }
    }
}
