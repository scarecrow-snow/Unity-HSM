using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityUtils
{
    /// <summary>
    /// 固定容量のスタック実装。容量を超えた場合、最も古い要素が削除されます。
    /// LRUキャッシュのような用途に適しています。
    /// </summary>
    public class FixedCapacityStack<T> : IEnumerable<T>
    {
        private readonly LinkedList<T> _list = new LinkedList<T>();  // 内部データ構造としてLinkedListを使用
        private readonly int _capacity;  // スタックの最大容量

        /// <summary>
        /// 固定容量スタックを初期化します
        /// </summary>
        /// <param name="capacity">スタックの最大容量</param>
        /// <exception cref="ArgumentOutOfRangeException">容量が0以下の場合にスローされます</exception>
        public FixedCapacityStack(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");
            _capacity = capacity;
        }

        /// <summary>
        /// スタックの先頭に要素を追加します。容量を超えた場合は最も古い要素が削除されます。
        /// </summary>
        /// <param name="item">追加する要素</param>
        public void Push(T item)
        {
            _list.AddFirst(item);  // 新しい要素を先頭に追加

            if (_list.Count > _capacity)
            {
                _list.RemoveLast();  // 容量を超えた場合、最も古いデータを削除
            }
        }

        /// <summary>
        /// スタックの先頭から要素を取り出し、削除します
        /// </summary>
        /// <returns>取り出された要素</returns>
        /// <exception cref="InvalidOperationException">スタックが空の場合にスローされます</exception>
        public T Pop()
        {
            if (_list.Count == 0)
                throw new InvalidOperationException("Stack is empty");

            T value = _list.First.Value;
            _list.RemoveFirst();
            return value;
        }

        /// <summary>
        /// スタックの先頭の要素を取得します（削除せず）
        /// </summary>
        /// <returns>先頭の要素</returns>
        /// <exception cref="InvalidOperationException">スタックが空の場合にスローされます</exception>
        public T Peek()
        {
            if (_list.Count == 0)
                throw new InvalidOperationException("Stack is empty");

            return _list.First.Value;
        }

        /// <summary>
        /// スタックの全要素を削除します
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// 現在のスタック内の要素数
        /// </summary>
        public int Count => _list.Count;
        
        /// <summary>
        /// スタックの最大容量
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// スタック内の要素を列挙するためのイテレータを返します
        /// </summary>
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 循環バッファを利用したスタック実装。
    /// 固定サイズのメモリで効率的なスタック操作を実現します。
    /// </summary>
    public class CircularStack<T>
    {
        private readonly T[] buffer;  // 循環バッファとして使用される配列
        private int top = -1;  // スタックの先頭位置（-1は空の状態）
        private int count = 0;  // 現在の要素数

        /// <summary>
        /// 現在のスタック内の要素数
        /// </summary>
        public int Count => count;
        
        /// <summary>
        /// スタックの最大容量
        /// </summary>
        public int Capacity => buffer.Length;

        /// <summary>
        /// 循環スタックを指定した容量で初期化します
        /// </summary>
        /// <param name="capacity">スタックの最大容量</param>
        /// <exception cref="ArgumentOutOfRangeException">容量が0以下の場合にスローされます</exception>
        public CircularStack(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            buffer = new T[capacity];
        }

        /// <summary>
        /// スタックの先頭に要素を追加します
        /// </summary>
        /// <param name="item">追加する要素</param>
        public void Push(T item)
        {
            top = (top + 1) % Capacity;  // 循環的に次のインデックスを計算
            buffer[top] = item;

            if (count < Capacity)
                count++;  // 容量まで達していない場合のみカウントを増やす
        }

        /// <summary>
        /// スタックの先頭から要素を取り出し、削除します
        /// </summary>
        /// <returns>取り出された要素</returns>
        /// <exception cref="InvalidOperationException">スタックが空の場合にスローされます</exception>
        public T Pop()
        {
            if (count == 0)
                throw new InvalidOperationException("Stack is empty.");

            T item = buffer[top];
            buffer[top] = default;  // 明示的に削除（必要に応じて）
            top = (top - 1 + Capacity) % Capacity;  // 循環的に前のインデックスに戻る
            count--;
            return item;
        }

        /// <summary>
        /// スタックの先頭の要素を取得します（削除せず）
        /// </summary>
        /// <returns>先頭の要素</returns>
        /// <exception cref="InvalidOperationException">スタックが空の場合にスローされます</exception>
        public T Peek()
        {
            if (count == 0)
                throw new InvalidOperationException("Stack is empty.");
            return buffer[top];
        }

        /// <summary>
        /// スタックの全要素を削除します
        /// </summary>
        public void Clear()
        {
            Array.Clear(buffer, 0, buffer.Length);
            top = -1;
            count = 0;
        }

        /// <summary>
        /// スタック内の全要素を列挙します（先頭から順に）
        /// </summary>
        /// <returns>スタック内の要素の列挙子</returns>
        public IEnumerable<T> GetElements()
        {
            for (int i = 0; i < count; i++)
            {
                int index = (top - i + Capacity) % Capacity;  // 循環バッファ内の正しいインデックスを計算
                yield return buffer[index];
            }
        }
    }

    
}