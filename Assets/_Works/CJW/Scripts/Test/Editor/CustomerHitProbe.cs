using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Health;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>손님 피격을 플레이 모드에서 확인하는 디버그용 도구. 플레이어 공격이 아직 없어 <see cref="IHittable.TakeHit"/>를 직접 부른다.
    /// 차에서 내린 손님 몇 명을 골라 (1) 한 대 (2) 무적 시간 중 한 대 더 (3) 죽을 때까지 때리고, 래그돌·방문 이탈·사라짐과
    /// 그 차의 방문이 끝까지 가는지 본다. 결과는 Temp/CustomerSim/hit.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class CustomerHitProbe
    {
        private const string RunningKey = "CJW.CustomerHit.Running";
        private const string OutPath = "Temp/CustomerSim/hit.txt";

        private const int TargetCount = 3;
        private const float TimeScale = 3f;
        private const float GiveUpSeconds = 400f;

        private const float LightDamage = 10f;
        private const float HitForce = 6f;

        private enum Step { FirstHit, HitAnim, Kill, CheckDead, WaitDespawn, WaitVisit, CheckCarStays, Done }

        private sealed class Target
        {
            public AbstractCustomer Customer;
            public ICustomerHealth Health;
            public VisitSession Session;
            public string Name;
            public Step Step;
            public float NextAt;
            public int DamagedCount;
            public int DiedCount;
            public bool VisitCompleted;
            public float DiedAt;
            public Vector3 AttackerPos;
            public Car Car;
            public Vector3 CarPosAtDeath;
            public int CompanionsAtDeath;

            /// <summary>일행까지 모두 죽여, 탄 사람이 모두 죽은 차가 제자리에 남는지 본다. 두 번째 대상만 켠다.</summary>
            public bool KillAll;

            /// <summary>주유를 받기 전에 죽은 주유 손님인지. 남은 일행이 곧장 출발해야 한다.</summary>
            public bool DiedWaitingFuel;
            public float DepartAt = -1f;
            public System.Action<VisitPhase> OnPhase;
            public System.Action<HitInfo> OnDamaged;
            public System.Action<HitInfo> OnDied;
            public System.Action<VisitSession> OnVisitCompleted;
        }

        private static readonly List<Target> Targets = new();
        private static readonly StringBuilder Log = new();
        private static readonly List<string> Errors = new();
        private static int _pass;
        private static int _fail;
        private static float _startTime;
        private static bool _boardedChecked;
        private static GameObject _attacker;

        static CustomerHitProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Customer Hit Test")]
        private static void StartTest()
        {
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Begin();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Finish("플레이 모드가 꺼져 중단");
            }
        }

        private static void Begin()
        {
            Targets.Clear();
            Log.Clear();
            Errors.Clear();
            _pass = 0;
            _fail = 0;
            _boardedChecked = false;
            _startTime = Time.time;
            Time.timeScale = TimeScale;

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Write("시작");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                Errors.Add(condition.Split('\n')[0]);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            float now = Time.time;

            if (now - _startTime > GiveUpSeconds)
            {
                Check(false, $"{GiveUpSeconds}초 안에 끝나지 않음");
                Finish("시간 초과");
                return;
            }

            PickTargets();
            CheckBoarded();

            foreach (Target t in Targets)
            {
                if (now >= t.NextAt)
                {
                    Advance(t, now);
                }
            }

            if (Targets.Count >= TargetCount && Targets.All(t => t.Step == Step.Done))
            {
                Finish("완료");
            }
        }

        /// <summary>차에서 내려 서 있는 손님을 고른다. 한꺼번에 고르지 않고 내리는 대로 하나씩 채운다.</summary>
        private static void PickTargets()
        {
            if (Targets.Count >= TargetCount)
            {
                return;
            }

            foreach (AbstractCustomer c in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                if (Targets.Count >= TargetCount)
                {
                    return;
                }

                if (c.Health == null || c.Session == null || !c.Health.CanBeHit || Targets.Any(t => t.Customer == c))
                {
                    continue;
                }

                // 한 방문에서 한 명만 고른다. 방문이 끝까지 가는지 따로 보기 위해서다.
                if (Targets.Any(t => t.Session == c.Session))
                {
                    continue;
                }

                // 첫 대상은 일행이 있는 주유 손님을 고른다. 주유 손님이 죽으면 남은 일행이 곧장 떠나는지 보기 위해서다.
                // 한참 안 나오면 아무나 고른다.
                if (Targets.Count == 0 && Time.time - _startTime < 240f &&
                    !(c.Fsm != null && c.Fsm.WantsFuel && c.Session.Customers.Count > 1))
                {
                    continue;
                }

                var t = new Target
                {
                    Customer = c,
                    Health = c.Health,
                    Session = c.Session,
                    Name = $"{c.name}#{c.GetInstanceID()}",
                    Step = Step.FirstHit,
                    KillAll = Targets.Count == 1,
                    Car = c.Session.Car,
                    NextAt = Time.time + 1f,
                };

                t.OnDamaged = _ => t.DamagedCount++;
                // 혼자 탄 손님은 죽은 다음 틱에 방문이 닫혀 세션이 비워진다. 남은 일행·차 위치는 죽는 그 순간에 적는다.
                t.OnDied = _ =>
                {
                    t.DiedCount++;
                    t.DiedAt = Time.time;
                    t.CompanionsAtDeath = t.Session.Customers.Count;
                    t.CarPosAtDeath = t.Car != null ? t.Car.transform.position : Vector3.zero;
                    t.DiedWaitingFuel = c.Fsm != null && c.Fsm.WantsFuel && t.CompanionsAtDeath > 0;
                };
                t.OnVisitCompleted = _ => t.VisitCompleted = true;
                t.Health.Damaged += t.OnDamaged;
                t.Health.Died += t.OnDied;
                t.Session.Completed += t.OnVisitCompleted;

                // 출발(탑승 시작) 시각. 시체가 사라지기 전에 떠날 수 있어 단계가 바뀌는 순간에 적는다.
                t.OnPhase = phase =>
                {
                    if (t.DiedAt > 0f && t.DepartAt < 0f && phase is VisitPhase.Boarding or VisitPhase.Leaving)
                    {
                        t.DepartAt = Time.time;
                    }
                };
                t.Session.OnStateChanged += t.OnPhase;

                Targets.Add(t);
                Write($"대상 {t.Name}: 체력 {t.Health.CurrentHealth}/{t.Health.MaxHealth}, 방문 단계 {t.Session.Phase}");
            }
        }

        /// <summary>차에 탄 손님은 맞지 않아야 한다. 한 번만 본다.</summary>
        private static void CheckBoarded()
        {
            if (_boardedChecked)
            {
                return;
            }

            AbstractCustomer boarded = Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.Health != null && c.Boarding != null && c.Boarding.IsBoarded && !c.Health.IsDead);

            if (boarded == null)
            {
                return;
            }

            _boardedChecked = true;
            float before = boarded.Health.CurrentHealth;
            boarded.Health.TakeHit(new HitInfo(LightDamage, Vector3.forward));
            Check(!boarded.Health.CanBeHit && Mathf.Approximately(before, boarded.Health.CurrentHealth),
                  $"차에 탄 손님({boarded.name})은 맞지 않음: CanBeHit={boarded.Health.CanBeHit}, 체력 {before}→{boarded.Health.CurrentHealth}");
        }

        private static void Advance(Target t, float now)
        {
            ICustomerHealth h = t.Health;
            GameObject attacker = null;
            Vector3 dir = t.Customer.transform.forward * -1f;

            if (t.KillAll && t.Step is Step.WaitDespawn or Step.WaitVisit)
            {
                KillCompanions(t);
            }

            switch (t.Step)
            {
                case Step.FirstHit:
                    if (!h.CanBeHit)
                    {
                        // 그새 차에 탔거나 쓰러졌다. 다시 맞을 수 있을 때까지 기다린다.
                        t.NextAt = now + 0.5f;
                        return;
                    }

                    // 옆(오른쪽 2m)에 선 가짜 플레이어가 때린다. 맞은 손님은 이쪽으로 돌아서야 한다.
                    if (_attacker == null) _attacker = new GameObject("[HitProbe] Attacker");
                    _attacker.transform.position = t.Customer.transform.position + t.Customer.transform.right * 2f;
                    t.AttackerPos = _attacker.transform.position;
                    h.TakeHit(new HitInfo(LightDamage, t.AttackerPos - t.Customer.transform.position, HitForce, _attacker));
                    Check(Mathf.Approximately(h.CurrentHealth, h.MaxHealth - LightDamage) && t.DamagedCount == 1 && !h.IsDead,
                          $"{t.Name} 한 대: 체력 {h.CurrentHealth}/{h.MaxHealth}, Damaged {t.DamagedCount}회, 죽음 {h.IsDead}");

                    // 무적 시간(0.2초) 안에 한 대 더. 들어가면 안 된다.
                    float before = h.CurrentHealth;
                    h.TakeHit(new HitInfo(LightDamage, dir, HitForce, attacker));
                    Check(Mathf.Approximately(before, h.CurrentHealth) && t.DamagedCount == 1,
                          $"{t.Name} 무적 시간 중 재타격 무시: 체력 {before}→{h.CurrentHealth}, Damaged {t.DamagedCount}회");

                    // 플레이어 공격(AttackSkill)은 Customer 레이어 콜라이더에서 부모의 IHittable을 찾는다.
                    Collider body = t.Customer.GetComponent<Collider>();
                    Check(t.Customer.gameObject.layer == LayerMask.NameToLayer("Customer") && body != null && body.GetComponentInParent<IHittable>() != null,
                          $"{t.Name} 플레이어 공격 판정에 걸림: 레이어 {LayerMask.LayerToName(t.Customer.gameObject.layer)}, 몸통 콜라이더 {(body != null ? "있음" : "없음")}");

                    t.Step = Step.HitAnim;
                    t.NextAt = now + 0.15f;
                    return;

                case Step.HitAnim:
                    Animator animator = t.Customer.GetComponentInChildren<Animator>();
                    bool playingHit = animator != null && (animator.GetCurrentAnimatorStateInfo(0).IsName("HIT") || animator.GetNextAnimatorStateInfo(0).IsName("HIT"));
                    string current = t.Customer.Fsm?.Machine?.Current?.GetType().Name ?? "-";
                    Check(playingHit && t.Customer.ActionAnimator != null && t.Customer.ActionAnimator.IsPlaying,
                          $"{t.Name} 맞고 피격 애니메이션: HIT 재생 {playingHit}, 상태 {current}");

                    Vector3 toAttacker = t.AttackerPos - t.Customer.transform.position;
                    toAttacker.y = 0f;
                    float angle = Vector3.Angle(t.Customer.transform.forward, toAttacker);
                    Check(angle < 15f, $"{t.Name} 맞고 때린 쪽을 바라봄: 어긋난 각도 {angle:F0}°");

                    t.Step = Step.Kill;
                    t.NextAt = now + 0.5f;
                    return;

                case Step.Kill:
                    if (h.IsDead)
                    {
                        t.Step = Step.CheckDead;
                        return;
                    }

                    if (!h.CanBeHit)
                    {
                        t.NextAt = now + 0.5f;
                        return;
                    }

                    // 무적 시간이 지나도록 간격을 두고 한 대씩.
                    h.TakeHit(new HitInfo(LightDamage, dir, HitForce, attacker));
                    Write($"{t.Name} 타격: 체력 {h.CurrentHealth}");
                    t.NextAt = now + 0.3f;
                    return;

                case Step.CheckDead:
                    AbstractCustomer c = t.Customer;
                    Check(h.CurrentHealth <= 0f && t.DiedCount == 1, $"{t.Name} 죽음: 체력 {h.CurrentHealth}, Died {t.DiedCount}회");
                    Check(c.IsKnockedDown, $"{t.Name} 래그돌로 쓰러짐: IsKnockedDown={c.IsKnockedDown}");
                    Write($"{t.Name} 죽을 때 남은 일행 {t.CompanionsAtDeath}명");

                    Check(c.Session == null && !t.Session.Customers.Contains(c),
                          $"{t.Name} 방문에서 빠짐: Session={(c.Session == null ? "null" : "남음")}, 목록에 {(t.Session.Customers.Contains(c) ? "있음" : "없음")}");

                    float hpBefore = h.CurrentHealth;
                    h.TakeHit(new HitInfo(LightDamage, dir, HitForce, attacker));
                    Check(!h.CanBeHit && t.DamagedCount > 0 && Mathf.Approximately(hpBefore, h.CurrentHealth),
                          $"{t.Name} 죽은 뒤 맞지 않음: CanBeHit={h.CanBeHit}");

                    t.Step = Step.WaitDespawn;
                    t.NextAt = now + 1.5f;
                    return;

                case Step.WaitDespawn:
                    // 사라지기 전(3초 전)에는 계속 누워 있어야 한다. 래그돌 기본 회복 시간 4초보다 길게 보고 싶어 3초 직전에 한 번 본다.
                    if (now - t.DiedAt < 2.8f)
                    {
                        Check(t.Customer.gameObject.activeInHierarchy && t.Customer.IsKnockedDown,
                              $"{t.Name} 사라지기 전 누워 있음(죽고 {now - t.DiedAt:F1}초)");
                        t.NextAt = t.DiedAt + 3.6f;
                        return;
                    }

                    Check(!t.Customer.gameObject.activeInHierarchy, $"{t.Name} 죽고 {now - t.DiedAt:F1}초 뒤 사라짐: active={t.Customer.gameObject.activeInHierarchy}");
                    t.Step = Step.WaitVisit;
                    t.NextAt = now + 1f;
                    return;

                case Step.WaitVisit:
                    if (!t.VisitCompleted)
                    {
                        t.NextAt = now + 1f;
                        return;
                    }

                    if (t.DiedWaitingFuel)
                    {
                        float departDelay = t.DepartAt >= 0f ? t.DepartAt - t.DiedAt : float.MaxValue;
                        Check(departDelay < 8f, $"{t.Name} 주유 손님이 죽자 남은 일행이 곧장 출발: 죽고 {departDelay:F1}초 뒤 탑승 시작");
                    }

                    Check(true, $"{t.Name}이(가) 타고 온 차의 방문이 끝까지 감(죽고 {now - t.DiedAt:F0}초 뒤 완료)");

                    // 탄 사람이 모두 죽은 차는 자리에 남고, 일행이 남은 차는 평소대로 떠난다. 몇 초 뒤에 본다.
                    t.Step = Step.CheckCarStays;
                    t.NextAt = now + 5f;
                    return;

                case Step.CheckCarStays:
                {
                    bool listed = IsAbandoned(t.Car);
                    bool present = t.Car != null && t.Car.gameObject.activeInHierarchy;
                    float moved = present ? Vector3.Distance(t.Car.transform.position, t.CarPosAtDeath) : -1f;

                    if (t.CompanionsAtDeath == 0)
                    {
                        Check(listed && present && moved < 0.5f,
                              $"{t.Name} 혼자 탄 차는 제자리에 남음: 버려진 차 목록 {listed}, 활성 {present}, 움직인 거리 {moved:F2}m");

                        CheckRemoveApi(t);
                    }
                    else
                    {
                        Check(!listed && (!present || moved > 3f),
                              $"{t.Name} 일행이 남은 차는 떠남: 버려진 차 목록 {listed}, 활성 {present}, 움직인 거리 {moved:F2}m");
                    }

                    Cleanup(t);
                    t.Step = Step.Done;
                    return;
                }
            }
        }

        /// <summary>대상이 죽은 뒤 같은 차의 남은 일행을 맞을 수 있을 때마다 쓰러뜨린다. 모두 죽으면 그때 차 위치를 기준으로 삼는다.</summary>
        private static void KillCompanions(Target t)
        {
            if (t.VisitCompleted || t.CompanionsAtDeath == 0)
            {
                return;
            }

            foreach (AbstractCustomer other in t.Session.Customers.ToList())
            {
                if (other.Health != null && other.Health.CanBeHit)
                {
                    other.Health.TakeHit(new HitInfo(9999f, other.transform.forward));
                    Write($"{t.Name}의 일행 {other.name} 쓰러뜨림");
                }
            }

            if (t.Session.Customers.Count == 0)
            {
                t.CompanionsAtDeath = 0;
                t.CarPosAtDeath = t.Car != null ? t.Car.transform.position : Vector3.zero;
                Write($"{t.Name}의 차에 탄 사람이 모두 죽음, 방문 단계 {t.Session.Phase}");
            }
        }

        /// <summary>팀원이 쓰는 치우기 API(IRemovableCar)를 확인한다. 콜라이더에서 찾아 치우면 풀로 돌아가고 목록에서 빠져야 한다.
        /// 방문 중인 차는 치울 수 없어야 한다.</summary>
        private static void CheckRemoveApi(Target t)
        {
            Car busy = Object.FindObjectsByType<Car>(FindObjectsSortMode.None).FirstOrDefault(c => c != t.Car && !IsAbandoned(c));
            if (busy != null)
            {
                Check(!busy.CanRemove && !busy.Remove() && busy.gameObject.activeInHierarchy,
                      $"방문 중인 차({busy.name})는 치울 수 없음: CanRemove={busy.CanRemove}");
            }

            Collider body = t.Car != null ? t.Car.GetComponentInChildren<Collider>() : null;
            _Works.Shared.Cars.IRemovableCar removable = body != null ? body.GetComponentInParent<_Works.Shared.Cars.IRemovableCar>() : null;
            bool canRemove = removable != null && removable.CanRemove;
            bool removed = canRemove && removable.Remove();

            Check(canRemove && removed && !t.Car.gameObject.activeInHierarchy && !IsAbandoned(t.Car) && !t.Car.CanRemove,
                  $"{t.Name}의 버려진 차를 콜라이더({(body != null ? body.name : "없음")})로 찾아 치움: CanRemove {canRemove}, Remove {removed}, " +
                  $"활성 {t.Car.gameObject.activeInHierarchy}, 목록에 {IsAbandoned(t.Car)}, 치운 뒤 CanRemove {t.Car.CanRemove}");
        }

        private static bool IsAbandoned(Car car)
        {
            var director = Object.FindAnyObjectByType<_Works.CJW.Scripts.Customers.Visit.VisitDirector>();
            if (director == null || car == null)
            {
                return false;
            }

            for (int i = 0; i < director.AbandonedCarCount; i++)
            {
                if (director.TryGetAbandonedCar(i, out Car abandoned) && abandoned == car)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Cleanup(Target t)
        {
            if (t.Health != null)
            {
                t.Health.Damaged -= t.OnDamaged;
                t.Health.Died -= t.OnDied;
            }

            if (t.Session != null)
            {
                t.Session.Completed -= t.OnVisitCompleted;
                t.Session.OnStateChanged -= t.OnPhase;
            }
        }

        private static void Check(bool ok, string what)
        {
            if (ok) _pass++; else _fail++;
            Write($"{(ok ? "[통과]" : "[실패]")} {what}");
        }

        private static void Write(string line)
        {
            Log.AppendLine($"[{Time.time - _startTime,6:F1}s] {line}");
        }

        private static void Finish(string reason)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(RunningKey, false);

            foreach (Target t in Targets)
            {
                if (t.Step != Step.Done)
                {
                    Write($"미완료: {t.Name} 단계 {t.Step}");
                }

                Cleanup(t);
            }

            Log.AppendLine();
            Log.AppendLine($"끝: {reason}. 통과 {_pass}, 실패 {_fail}, 에러 로그 {Errors.Count}");
            foreach (IGrouping<string, string> g in Errors.GroupBy(e => e))
            {
                Log.AppendLine($"  에러 x{g.Count()}: {g.Key}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
            File.WriteAllText(OutPath, Log.ToString());

            Time.timeScale = 1f;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
