using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.ManagingAgents
{
    /// <summary>
    /// IUpdate / IFixedUpdate 대상들을 모아 틱을 전파하는 디스패처.
    /// 순회 도중 등록/해제가 일어나도 안전하다.
    /// </summary>
    public sealed class TickGroup
    {
        /// <summary>
        /// 한 종류의 틱 대상을 보관하는 채널.
        /// 순회용 스냅샷은 원본이 바뀐 다음 틱에만 갱신한다.
        /// </summary>
        private sealed class Channel<T> where T : class
        {
            private readonly List<T> _targets = new();

            private T[] _buffer = Array.Empty<T>();
            private int _bufferCount;
            private bool _dirty;

            public void Register(T target)
            {
                if (_targets.Contains(target))
                {
                    return;
                }

                _targets.Add(target);
                _dirty = true;
            }

            public void Unregister(T target)
            {
                if (!_targets.Remove(target))
                {
                    return;
                }

                // 순회 중이라면 남은 스냅샷에서 건너뛰도록 비워둔다.
                int index = Array.IndexOf(_buffer, target, 0, _bufferCount);
                if (index >= 0)
                {
                    _buffer[index] = null;
                }

                _dirty = true;
            }

            /// <summary>
            /// 최신 스냅샷을 돌려준다. 반환된 배열에는 해제된 자리에 null이 섞일 수 있다.
            /// </summary>
            public T[] GetSnapshot(out int count)
            {
                if (_dirty)
                {
                    if (_buffer.Length < _targets.Count)
                    {
                        _buffer = new T[_targets.Count];
                    }

                    _targets.CopyTo(_buffer);

                    // 줄어든 만큼의 꼬리는 참조를 붙들지 않도록 비운다.
                    for (int i = _targets.Count; i < _bufferCount; i++)
                    {
                        _buffer[i] = null;
                    }

                    _bufferCount = _targets.Count;
                    _dirty = false;
                }

                count = _bufferCount;
                return _buffer;
            }

            public void Clear()
            {
                _targets.Clear();
                Array.Clear(_buffer, 0, _bufferCount);
                _bufferCount = 0;
                _dirty = false;
            }
        }

        private readonly Channel<IUpdate> _updateChannel = new();
        private readonly Channel<IFixedUpdate> _fixedUpdateChannel = new();

        /// <summary>
        /// 틱 대상임이 확실한 곳에서 사용한다. 대상이 아니면 경고를 남긴다.
        /// </summary>
        public void Register(object target)
        {
            if (TryRegister(target))
            {
                return;
            }

            Debug.LogWarning($"[TickGroup] {target?.GetType().Name ?? "null"}은(는) " +
                             $"{nameof(IUpdate)} / {nameof(IFixedUpdate)}를 구현하지 않아 등록되지 않았습니다.");
        }

        /// <summary>
        /// 틱 대상이 섞여 있는 목록을 훑을 때 사용한다. 등록 여부만 돌려주고 경고하지 않는다.
        /// </summary>
        public bool TryRegister(object target)
        {
            IUpdate update = target as IUpdate;
            IFixedUpdate fixedUpdate = target as IFixedUpdate;

            if (update != null)
            {
                _updateChannel.Register(update);
            }

            if (fixedUpdate != null)
            {
                _fixedUpdateChannel.Register(fixedUpdate);
            }

            return update != null || fixedUpdate != null;
        }

        public void Unregister(object target)
        {
            if (target is IUpdate update)
            {
                _updateChannel.Unregister(update);
            }

            if (target is IFixedUpdate fixedUpdate)
            {
                _fixedUpdateChannel.Unregister(fixedUpdate);
            }
        }

        public void Update(float dt)
        {
            IUpdate[] buffer = _updateChannel.GetSnapshot(out int count);
            for (int i = 0; i < count; i++)
            {
                buffer[i]?.OnUpdate(dt);
            }
        }

        public void FixedUpdate(float dt)
        {
            IFixedUpdate[] buffer = _fixedUpdateChannel.GetSnapshot(out int count);
            for (int i = 0; i < count; i++)
            {
                buffer[i]?.OnFixedUpdate(dt);
            }
        }

        public void Clear()
        {
            _updateChannel.Clear();
            _fixedUpdateChannel.Clear();
        }
    }
}
