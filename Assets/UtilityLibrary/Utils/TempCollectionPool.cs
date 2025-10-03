using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace UnityUtils
{
    public static class TempCollectionPool<TCollection, TElement>
        where TCollection : class, ICollection<TElement>, new()
    {
        private static readonly ObjectPool<TCollection> pool =
            new ObjectPool<TCollection>(
                createFunc: () => new TCollection(),
                actionOnGet: c => c.Clear(),
                actionOnRelease: c => c.Clear(),
                collectionCheck: false // 毎フレーム用途ならチェック不要で軽量化
            );

        public static TCollection Get() => pool.Get();
        public static void Release(TCollection collection) => pool.Release(collection);

        public struct Scope : IDisposable
        {
            private TCollection collection;
            public TCollection Collection => collection;

            internal Scope(TCollection collection) => this.collection = collection;

            public void Dispose()
            {
                if (collection != null)
                {
                    Release(collection);
                    collection = null;
                }
            }
        }

        public static Scope GetScoped() => new Scope(Get());
    }

    // Stack<T>用の特殊化 (ICollection<T>を明示的実装しているため)
    public static class TempStackPool<T>
    {
        private static readonly ObjectPool<Stack<T>> pool =
            new ObjectPool<Stack<T>>(
                createFunc: () => new Stack<T>(),
                actionOnGet: s => s.Clear(),
                actionOnRelease: s => s.Clear(),
                collectionCheck: false
            );

        public static Stack<T> Get() => pool.Get();
        public static void Release(Stack<T> stack) => pool.Release(stack);

        public struct Scope : IDisposable
        {
            private Stack<T> stack;
            public Stack<T> Stack => stack;

            internal Scope(Stack<T> stack) => this.stack = stack;

            public void Dispose()
            {
                if (stack != null)
                {
                    Release(stack);
                    stack = null;
                }
            }
        }

        public static Scope GetScoped() => new Scope(Get());
    }
}