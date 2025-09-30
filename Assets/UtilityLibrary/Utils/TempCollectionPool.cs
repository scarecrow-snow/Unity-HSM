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
    }
}