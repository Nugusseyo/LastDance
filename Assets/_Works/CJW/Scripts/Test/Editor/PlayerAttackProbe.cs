using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.JJH._02_Scripts.Agents.Players;
using _Works.JJH._02_Scripts.Agents.Players.Grabs;
using _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks;
using _Works.JJH._02_Scripts.Items;
using _Works.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>실제 플레이어 공격 경로(무기를 들고 AttackSkill.Attack)로 손님을 때려 본다. 입력을 넣을 수 없어 플레이어를 손님 앞에 옮기고
    /// 카메라를 손님 가슴에 겨눈 뒤 공격을 직접 부른다. 거리·방향을 바꿔 가며 (1) 공격 판정에 걸리는지 (2) 맞은 손님이 플레이어를 바라보는지 본다.
    /// 결과는 Temp/CustomerSim/player_attack.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class PlayerAttackProbe
    {
        private const string RunningKey = "CJW.PlayerAttack.Running";
        private const string OutPath = "Temp/CustomerSim/player_attack.txt";
        private const float GiveUpSeconds = 600f;

        /// <summary>카메라에서 손님까지의 수평 거리(m) 후보. 공격 판정 상자가 닿는 거리를 잰다.</summary>
        private static readonly float[] Distances = { 1.0f, 1.5f, 2.0f, 2.5f };

        /// <summary>손님 정면 기준 플레이어가 설 방향(도). 등 뒤나 옆에서 때려도 돌아서는지 본다.</summary>
        private static readonly float[] Angles = { 0f, 90f, 180f, -90f };

        private enum Step { Pick, Attack, Face1, Face2, Wait }

        private static readonly StringBuilder Log = new();
        private static readonly HashSet<AbstractCustomer> Used = new();
        private static Player _player;
        private static Step _step;
        private static AbstractCustomer _target;
        private static int _sample;
        private static int _damagedBefore;
        private static int _damaged;
        private static float _stepAt;
        private static float _startTime;
        private static int _pass;
        private static int _fail;
        private static readonly Dictionary<float, (int hit, int total)> ByDistance = new();
        private static System.Action<HitInfo> _onDamaged;
        private static float _cooldown = 1.5f;
        private static Vector3 _attackFrom;

        static PlayerAttackProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Player Attack Test")]
        private static void StartTest()
        {
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Log.Clear();
                Used.Clear();
                ByDistance.Clear();
                _pass = 0;
                _fail = 0;
                _sample = 0;
                _step = Step.Pick;
                _startTime = Time.time;
                _player = null;
                EditorApplication.update += Tick;
                Write("시작");
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Finish("플레이 모드가 꺼져 중단");
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
                Finish("시간 초과");
                return;
            }

            if (_player == null && !SetupPlayer())
            {
                return;
            }

            switch (_step)
            {
                case Step.Pick:
                    if (_sample >= Distances.Length * Angles.Length)
                    {
                        Finish("완료");
                        return;
                    }

                    if (_target == null || !_target.isActiveAndEnabled || _target.Health == null || !_target.Health.CanBeHit ||
                        _target.Health.CurrentHealth <= 10f)
                    {
                        Unhook();
                        _target = Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None)
                            .FirstOrDefault(c => c.Health != null && c.Health.CanBeHit && c.Session != null && !Used.Contains(c));
                        if (_target == null)
                        {
                            return;
                        }

                        Used.Add(_target);
                        _damaged = 0;
                        _onDamaged = _ => _damaged++;
                        _target.Health.Damaged += _onDamaged;
                    }

                    _step = Step.Attack;
                    return;

                case Step.Attack:
                    Attack(now);
                    return;

                case Step.Face1:
                    if (now - _stepAt < 0.25f)
                    {
                        return;
                    }

                    CheckFacing("맞고 0.25초 뒤");
                    _step = Step.Face2;
                    return;

                case Step.Face2:
                    if (now - _stepAt < 0.6f)
                    {
                        return;
                    }

                    CheckFacing("맞고 0.6초 뒤(움찔 끝)");
                    _step = Step.Wait;
                    return;

                case Step.Wait:
                    // 무기 쿨타임과 무적 시간이 지나도록 기다린다.
                    if (now - _stepAt < _cooldown + 0.3f)
                    {
                        return;
                    }

                    _step = Step.Pick;
                    return;
            }
        }

        private static bool SetupPlayer()
        {
            _player = Object.FindFirstObjectByType<Player>();
            if (_player == null)
            {
                return false;
            }

            GrabItem weapon = Object.FindObjectsByType<GrabItem>(FindObjectsSortMode.None)
                .FirstOrDefault(g => g.CurrentItemData is WeaponItemSO);
            if (weapon == null)
            {
                Write("씬에 무기가 없어 시험할 수 없음");
                Finish("무기 없음");
                return false;
            }

            if (_player.Grab.CurrentItem == null)
            {
                ((PlayerGrabModule)_player.Grab).SwapItem(weapon);
            }

            _player.AttackSkill.ChangeCurrentAttack<AttackSkill>();
            var data = (WeaponItemSO)_player.Grab.CurrentItem.CurrentItemData;
            _cooldown = Mathf.Max(1.5f, data.AttackCooltime);
            Write($"플레이어 {_player.name}, 무기 {_player.Grab.CurrentItem.name} (피해 {data.Damage}, 쿨타임 {data.AttackCooltime}s)");
            return true;
        }

        private static void Attack(float now)
        {
            float distance = Distances[_sample % Distances.Length];
            float angle = Angles[_sample / Distances.Length % Angles.Length];
            _sample++;

            Transform body = _target.transform;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * body.forward;
            dir.y = 0f;
            dir.Normalize();

            // 카메라가 손님에서 distance만큼 떨어지도록 플레이어 몸을 옮긴다. 카메라가 몸 중심에서 앞뒤로 비껴 있을 수 있어 그만큼 보정한다.
            Transform cam = _player.Camera.CameraTrans;
            Transform root = _player.transform;
            root.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            Vector3 camOffset = cam.position - root.position;
            camOffset.y = 0f;

            Vector3 rootPos = body.position + dir * distance - camOffset;
            rootPos.y = root.position.y + (body.position.y - root.position.y);
            var rb = _player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = rootPos;
                rb.linearVelocity = Vector3.zero;
            }

            root.position = rootPos;
            Physics.SyncTransforms();

            // 손님 가슴을 겨눈다.
            Vector3 chest = body.position + Vector3.up * 1.2f;
            cam.rotation = Quaternion.LookRotation(chest - cam.position, Vector3.up);

            _damagedBefore = _damaged;
            _attackFrom = _player.transform.position;
            bool canBeHit = _target.Health.CanBeHit;
            string stateBefore = _target.Fsm?.Machine?.Current?.GetType().Name ?? "-";
            _player.AttackSkill.Attack();
            bool hit = _damaged > _damagedBefore;

            ByDistance.TryGetValue(distance, out (int hit, int total) stat);
            ByDistance[distance] = (stat.hit + (hit ? 1 : 0), stat.total + 1);

            Write($"#{_sample} {_target.name} 거리 {distance:F1}m, 방향 {angle:+0;-0;0}°: {(hit ? "맞음" : "빗나감")} (체력 {_target.Health.CurrentHealth})");

            if (!hit)
            {
                // AttackSkill과 같은 상자를 모든 레이어로 쏴서 무엇이 걸리는지 본다.
                Vector3 boxCenter = cam.position + cam.forward * 1f;
                Collider[] all = Physics.OverlapBox(boxCenter, new Vector3(0.5f, 0.5f, 0.75f), cam.rotation, ~0, QueryTriggerInteraction.Collide);
                Collider own = _target.GetComponent<Collider>();
                Write($"   진단: 카메라 ({cam.position.x:F1},{cam.position.y:F1},{cam.position.z:F1}) 상자중심 y={boxCenter.y:F2}, " +
                      $"손님 루트 y={body.position.y:F2}, 몸통 {(own != null ? $"{own.GetType().Name} enabled={own.enabled} bounds y {own.bounds.min.y:F2}~{own.bounds.max.y:F2} layer {LayerMask.LayerToName(own.gameObject.layer)}" : "없음")}, " +
                      $"CanBeHit={canBeHit}, 탑승={_target.Boarding?.IsBoarded}, 상태 {stateBefore}, 쿨타임중={((AttackSkill)((PlayerAttackSkillModule)_player.AttackSkill).Attacks.First(a => a is AttackSkill)).IsOnCooldown}");
                Write("   상자에 걸린 것: " + string.Join(", ", all.Select(c => $"{c.name}[{LayerMask.LayerToName(c.gameObject.layer)}]")));
            }

            _stepAt = now;
            _step = hit && !_target.Health.IsDead ? Step.Face1 : Step.Wait;
        }

        private static void CheckFacing(string when)
        {
            if (_target == null || _player == null)
            {
                return;
            }

            // 때린 순간의 플레이어 자리를 기준으로 본다. 순간이동한 플레이어가 다른 손님과 겹쳐 밀려날 수 있다.
            Vector3 toPlayer = _attackFrom - _target.transform.position;
            toPlayer.y = 0f;
            float angle = Vector3.Angle(_target.transform.forward, toPlayer);
            float moved = Vector3.Distance(_player.transform.position, _attackFrom);
            string state = _target.Fsm?.Machine?.Current?.GetType().Name ?? "-";
            Check(angle < 20f, $"  {when} 때린 쪽을 바라봄: 어긋난 각도 {angle:F0}° (상태 {state}, 그새 플레이어가 밀린 거리 {moved:F1}m)");
        }

        private static void Unhook()
        {
            if (_target != null && _target.Health != null && _onDamaged != null)
            {
                _target.Health.Damaged -= _onDamaged;
            }

            _onDamaged = null;
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
            SessionState.SetBool(RunningKey, false);
            Unhook();

            Log.AppendLine();
            Log.AppendLine("거리별 판정: " + string.Join(", ", ByDistance.OrderBy(p => p.Key).Select(p => $"{p.Key:F1}m {p.Value.hit}/{p.Value.total}")));
            Log.AppendLine($"끝: {reason}. 통과 {_pass}, 실패 {_fail}");
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
            File.WriteAllText(OutPath, Log.ToString());

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
