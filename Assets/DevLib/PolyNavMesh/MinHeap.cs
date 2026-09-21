using System;
using System.Collections.Generic;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// 최소 힙 기반 우선순위 큐. A* 탐색의 Open List로 사용된다.
    ///
    /// PathFinder.PriorityQueue와 달리 외부 Comparison 델리게이트로 순서를 결정하므로,
    /// T에 IComparable을 구현하지 않아도 된다.
    /// compare(a, b) &lt; 0 이면 a가 b보다 높은 우선순위(먼저 Pop)를 가진다.
    /// </summary>
    internal sealed class MinHeap<T>
    {
        private readonly List<T> _heap = new List<T>();
        private readonly Comparison<T> _compare;

        public int Count => _heap.Count;

        public MinHeap(Comparison<T> compare) => _compare = compare;

        public void Push(T item)
        {
            _heap.Add(item);
            HeapifyUp(_heap.Count - 1);
        }

        public T Pop()
        {
            T top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0) HeapifyDown(0);
            return top;
        }

        public T Peek() => _heap[0];

        /// <summary>선형 탐색으로 조건에 맞는 첫 번째 항목을 반환한다. O(n).</summary>
        public T Find(Predicate<T> match)
        {
            int idx = _heap.FindIndex(match);
            return idx < 0 ? default : _heap[idx];
        }

        /// <summary>
        /// item을 in-place로 수정(F값 감소)한 뒤 호출하면 힙 순서를 복구한다 (Decrease-Key).
        /// 참조 타입이면 레퍼런스 동일성으로 위치를 찾는다.
        /// </summary>
        public void DecreaseKey(T item)
        {
            int idx = _heap.IndexOf(item);
            if (idx >= 0) HeapifyUp(idx);
        }

        // ── 힙 정렬 헬퍼 ────────────────────────────────────────────────────────
        private void HeapifyUp(int idx)
        {
            while (idx > 0)
            {
                int parent = (idx - 1) / 2;
                if (_compare(_heap[idx], _heap[parent]) >= 0) break;  // 순서 맞음, 종료
                (_heap[idx], _heap[parent]) = (_heap[parent], _heap[idx]);
                idx = parent;
            }
        }

        private void HeapifyDown(int idx)
        {
            int count = _heap.Count;
            while (true)
            {
                int smallest = idx;
                int left  = 2 * idx + 1;
                int right = 2 * idx + 2;
                if (left  < count && _compare(_heap[left],  _heap[smallest]) < 0) smallest = left;
                if (right < count && _compare(_heap[right], _heap[smallest]) < 0) smallest = right;
                if (smallest == idx) break;
                (_heap[idx], _heap[smallest]) = (_heap[smallest], _heap[idx]);
                idx = smallest;
            }
        }
    }
}
