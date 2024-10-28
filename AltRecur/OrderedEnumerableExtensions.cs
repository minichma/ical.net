namespace AltRecur
{
    public static class OrderedEnumerableExtensions
    {
        public static IEnumerable<T> OrderedDistinct<T>(this IEnumerable<T> orderedItems)
        {
            var first = true;
            var prev = default(T);
            foreach (var item in orderedItems)
            {
                if (!first && Equals(prev, item))
                    continue;

                yield return item;
                first = false;
                prev = item;
            }
        }

        public static IEnumerable<T> OrderedMerge<T>(this IEnumerable<T> items1, IEnumerable<T> items2)
            where T : IComparable<T>
        {
            using var it1 = items1.GetEnumerator();
            using var it2 = items2.GetEnumerator();

            var iterators = new[] { it1, it2 }.Where(it => it.MoveNext()).ToList();

            while (iterators.Any())
            {
                var it = iterators.MinBy(x => x.Current);
                yield return it!.Current;

                if (!it.MoveNext())
                    iterators.Remove(it);
            }
        }

        public static IEnumerable<T> OrderedIntersect<T>(this IEnumerable<T> items1, IEnumerable<T> items2)
            where T : IComparable<T>
        {
            using var it1 = items1.GetEnumerator();
            using var it2 = items2.GetEnumerator();

            var iterators = new[] { it1, it2 }.ToList();

            void MoveAllNext()
            {
                foreach (var ii in iterators)
                    ii.MoveNext();
            }

            MoveAllNext();

            while (iterators.Any())
            {
                var v = iterators.First();
                if (iterators.Skip(1).All(x => x.Current.CompareTo(v.Current) == 0))
                {
                    yield return v.Current;
                    MoveAllNext();
                }
                else
                {
                    var it = iterators.MinBy(x => x.Current);
                    it!.MoveNext();
                }
            }
        }
    }
}
