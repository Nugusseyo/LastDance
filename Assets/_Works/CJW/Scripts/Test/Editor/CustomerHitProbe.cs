using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private enum Step { FirstHit, HitAnim, Kill, CheckDead, WaitDespawn, WaitVisit, Done }

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

                var t = new Target
                {
                    Customer = c,
                    Health = c.Health,
                    Session = c.Session,
                    Name = $"{c.name}#{c.GetInstanceID()}",
                    Step = Step.FirstHit,
                    NextAt = Time.time + 1f,
                };

                t.OnDamaged = _ => t.DamagedCount++;
                t.OnDied = _ => { t.DiedCount++; t.DiedAt = Time.time; };
                t.OnVisitCompleted = _ => t.VisitCompleted = true;
                t.Health.Damaged += t.OnDamaged;
                t.Health.Died += t.OnDied;
                t.Session.Completed += t.OnVisitCompleted;

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

                    Check(true, $"{t.Name}이(가) 타고 온 차의 방문이 끝까지 감(죽고 {now - t.DiedAt:F0}초 뒤 완료)");
                    Cleanup(t);
                    t.Step = Step.Done;
                    return;
            }
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
