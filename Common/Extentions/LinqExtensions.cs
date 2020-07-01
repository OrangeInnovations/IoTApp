using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Common.Extentions
{
    public static class LinqExtensions
    {
        public static IList ToAnonymousList(this IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new Exception("?? No elements??");
            }
                

            var value = enumerator.Current;
            var returnList = (IList)typeof(List<>)
                .MakeGenericType(value.GetType())
                .GetConstructor(Type.EmptyTypes)
                .Invoke(null);

            returnList.Add(value);

            while (enumerator.MoveNext())
            {
                returnList.Add(enumerator.Current);
            }
               

            return returnList;
        }

        // for IQueryable
        public static IList ToAnonymousList(this IQueryable source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var returnList = (IList)typeof(List<>)
                .MakeGenericType(source.ElementType)
                .GetConstructor(Type.EmptyTypes)
                .Invoke(null);

            foreach (var elem in source)
            {
                returnList.Add(elem);
            }
               

            return returnList;
        }


        public static bool AreEqual(this IEnumerable<string> a, IEnumerable<string> b)
        {
            var areEquivalent = (a.Count() == b.Count()) && !a.Except(b).Any();
            return areEquivalent;
        }


        public static IEnumerable<T> UpdateList<T>(this IEnumerable<T> source, Action<T> act)
        {
            foreach (T element in source) act(element);
            return source;
        }


        public static List<T> AddList<T>(this List<T> source, List<T> newList)
        {
            source.AddRange(newList);
            return source;
        }


        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunksize)
        {
            while (source.Any())
            {
                yield return source.Take(chunksize);
                source = source.Skip(chunksize);
            }
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            return source.Where(element => seenKeys.Add(keySelector(element)));
        }
    }
}
