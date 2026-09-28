using _Works.CJW.Scripts.Customers.Data;using _Works.CJW.Scripts.MapSystems;
using System;
using JetBrains.Annotations;
using Resources.DataBase.Human_Data;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>손님 한 명의 상태들이 공유하는 값. 차 단위 값을 담는 <see cref="VisitContext"/>와는 스코프가 다르다. sealed인 이유는 손님 종류별 상속이 상태 쪽에 다운캐스팅을 만들기 때문이다.</summary>
    public sealed class CustomerContext
    {
        /// <summary> 진상 손님인지 평범한 손님인지 체크 </summary>
        [field:SerializeField] public HumanType HumanType { get; private set; }
        /// <summary>이 컨텍스트의 주인.</summary>
        public AbstractCustomer Customer { get; private set; }

        /// <summary>주인의 상태 머신. 상태가 스스로 인터럽트를 걸 때 쓴다.</summary>
        public CustomerStateMachine Machine { get; private set; }

        /// <summary>목적지 조회용. 상태마다 따로 들지 않도록 여기 하나만 둔다.</summary>
        public MapDataSo MapData { get; private set; }

        /// <summary>현재 방문의 차 단위 값. 방문 밖에서는 null이다.</summary>
        public VisitContext Visit { get; private set; }

        /// <summary> 손님이 가는 맵의 위치. Rentable일 경우 Release를 해야한다. </summary>
        [CanBeNull]
        public RentableMapPosition RentedPosition { get; set; }

        /// <summary>전투 대상이나 파손 대상. 인터럽트를 건 쪽이 채워준다.</summary>
        public Transform Target;

        /// <summary>약속으로 맺어진 상대. 짝이 풀리면 null이 되므로, 기다리는 쪽은 이걸 보고 빠져나온다.
        /// 다른 차의 손님일 수 있어 <see cref="Visit"/>의 손님 목록으로는 찾을 수 없다.</summary>
        [CanBeNull]
        public CustomerContext Partner;

        /// <summary>짝과 만나 내가 설 자리. <see cref="CustomerRendezvousSO"/>가 만날 지점 양쪽으로 나눠 준다.</summary>
        public Vector3 MeetPoint;

        /// <summary>짝과 주고받기(공격·방어)를 시작한 시각. 둘이 같은 값을 써야 차례가 맞는다. 아직 시작 전이면 음수.
        /// 먼저 준비된 쪽이 자기와 상대에게 함께 넣는다.</summary>
        public float ExchangeStartTime = -1f;

        /// <summary>이번 방문에서 고른 갈래. 대사와 춤처럼 여러 후보 중 하나를 고르는 상태들이 같은 값을 써서 서로 짝이 맞게 한다. 아직 안 골랐으면 음수.</summary>
        public int Variant = -1;

        /// <summary>후보 <paramref name="count"/>개 중 이번 방문의 갈래를 돌려준다. 처음 부를 때 한 번 정한다.
        /// 짝이 먼저 골랐으면 그 다음 갈래를 골라 두 사람이 겹치지 않게 한다(싸우는 두 손님의 대사가 다르도록).</summary>
        public int PickVariant(int count)
        {
            if (count <= 1)
            {
                return 0;
            }

            if (Variant < 0)
            {
                Variant = Partner != null && Partner.Variant >= 0 ? Partner.Variant + 1 : UnityEngine.Random.Range(0, 1 << 16);
            }

            return Variant % count;
        }
        /// <summary>이 방문에서 배정받은 좌석 번호. 하차 순서와 승차 좌석에 모두 쓰인다.</summary>
        public int SeatIndex { get; private set; }
        public CustomerDataSO Data => Customer != null ? Customer.Data : null;

        public void Bind(AbstractCustomer customer, CustomerStateMachine machine, MapDataSo mapData)
        {
            Customer = customer;
            Machine = machine;
            MapData = mapData;
        }

        /// <summary>이번 방문에서 말한 대사 index(HumanDB). 평판 리뷰도 같은 index의 글을 쓴다. 아직 말하지 않았으면 0.</summary>
        public int LineIndex { get; set; }

        /// <summary>지금 머리 위에 떠 있는 말풍선을 접는 방법. 떠 있는 말풍선이 없으면 null.</summary>
        private Action _endSpeech;

        /// <summary>띄운 말풍선을 맡긴다. 요구가 풀리거나 손님이 사라질 때 <see cref="EndSpeech"/>로 접힌다.</summary>
        public void SetSpeech(Action end) => _endSpeech = end;

        /// <summary>말풍선이 스스로 끝났을 때 부른다. 맡긴 것과 같을 때만 비운다 — 그새 새 말풍선이 떴을 수 있다.</summary>
        public void ClearSpeech(Action end)
        {
            if (_endSpeech == end)
            {
                _endSpeech = null;
            }
        }

        /// <summary>떠 있는 말풍선을 바로 접는다. 주유를 받아 요구가 풀렸거나, 죽었거나, 풀로 돌아갈 때 부른다.</summary>
        public void EndSpeech()
        {
            Action end = _endSpeech;
            _endSpeech = null;
            end?.Invoke();
        }

        /// <summary>진상 짓을 할 조건이 안 맞아(싸울 짝·때릴 차가 없음) 일반 손님처럼 돌아다니는 중인지. 평판은 이 손님을 일반인으로 셈한다 —
        /// 때리면 깎이고, 퇴치해도 오르지 않고, 그냥 떠나도 깎이지 않는다. 방문 동안 유지되고 풀 반납 때 풀린다.</summary>
        public bool Harmless { get; set; }

        /// <summary>이번 방문에서 이미 차 반대편으로 옮겨 봤는지. 옮겨도 막히면 계속 오가지 않도록 한 번만 허용한다.</summary>
        public bool RelocatedAroundCar { get; set; }

        public void SetVisit(VisitContext visit, int seatIndex = 0)
        {
            Visit = visit;
            SeatIndex = seatIndex;
            RelocatedAroundCar = false;
        }

        /// <summary>풀 반납 시 호출. 풀링에서는 OnDestroy가 거의 불리지 않으므로 여기가 유일한 정리 지점이다.</summary>
        public void Reset()
        {
            RelocatedAroundCar = false;
            Harmless = false;

            // 상태가 취소로 끊겨 자기 finally를 못 지났을 수 있다. 마지막 안전망으로 여기서 짝을 맞춘다.
            // 빼먹으면 손님이 반납되어도 그 지점이 점유 상태로 남는다.
            if (RentedPosition != null)
            {
                MapData?.Release(RentedPosition);
                RentedPosition = null;
            }

            // 짝도 같은 이유로 끊어 준다. 안 끊으면 상대가 반납된 손님을 계속 기다린다.
            if (Partner != null)
            {
                if (Partner.Partner == this)
                {
                    Partner.Partner = null;
                }

                Partner = null;
            }

            // 대사 중에 반납되면 말풍선이 빈자리 위에 남아 끝까지 떠 있다.
            EndSpeech();
            LineIndex = 0;

            Visit = null;
            Target = null;
            MeetPoint = Vector3.zero;
            ExchangeStartTime = -1f;
            Variant = -1;
            SeatIndex = 0;
        }
    }
}
