using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Extentions;

namespace Common.Utilities
{
    public class ParallelService
    {
        public async Task<bool> ParallelCallTask<T>(IEnumerable<T> list, Func<IEnumerable<T>, Task<bool>> func, int chunkSize)
        {
            if (!list.Any())
            {
                return true;
            }
            IEnumerable<IEnumerable<T>> enumerables = list.Chunk(chunkSize);

            var result = new ConcurrentBag<bool>();

            IEnumerable<Task> tasks = enumerables.Select(async batchList =>
            {
                result.Add(await func(batchList));
            });

            await Task.WhenAll(tasks);

            return result.All(c => c);
        }

        public async Task<IEnumerable<TResult>> ParallelCallTask<T,TResult>(IEnumerable<T> list, Func<IEnumerable<T>, 
            Task<IEnumerable<TResult>>> func, int chunkSize)
        {
            if (!list.Any())
            {
                return default(IEnumerable<TResult>);
            }
            IEnumerable<IEnumerable<T>> enumerables = list.Chunk(chunkSize);

            var result = new ConcurrentBag<TResult>();

            IEnumerable<Task> tasks = enumerables.Select(async batchList =>
            {
                (await func(batchList)).ToList().ForEach(c=>result.Add(c));
            });

            await Task.WhenAll(tasks);

            return result.ToList();
        }

        public async Task<bool> ParallelCallTask<T1, T2>(IEnumerable<T1> list, T2 helper, Func<IEnumerable<T1>, T2, Task<bool>> func, int chunkSize)
        {
            if (!list.Any())
            {
                return true;
            }
            IEnumerable<IEnumerable<T1>> enumerables = list.Chunk(chunkSize);

            var result = new ConcurrentBag<bool>();

            IEnumerable<Task> tasks = enumerables.Select(async batchList =>
            {
                result.Add(await func(batchList, helper));
            });

            await Task.WhenAll(tasks);

            return result.All(c => c);
        }

        public async Task<bool> ParallelCallAsyncTask<T1, T2>(List<T1> dataList, List<T2> helperList, Func<IEnumerable<T1>, T2, Task<bool>> funcAsync)
        {
            if (!dataList.Any())
            {
                return true;
            }

            int dataSize = dataList.Count;
            int helpSize = helperList.Count;
            int chunkSize = (dataSize + helpSize - 1) / helpSize;

            IEnumerable<IEnumerable<T1>> enumerables = dataList.Chunk(chunkSize);

            var results = new ConcurrentBag<bool>();

            IEnumerable<Task> tasks = enumerables.Select(async (batch, index) =>
            {
                results.Add(await funcAsync(batch, helperList[index]));
            });


            await Task.WhenAll(tasks);

            return results.All(c => c);
        }


        public bool ParallelCall<T1, T2>(IEnumerable<T1> list, T2 helper, Func<IEnumerable<T1>, T2, bool> func, int chunkSize)
        {
            if (!list.Any())
            {
                return true;
            }
            var enumerables = list.Chunk(chunkSize);
            var result = new ConcurrentBag<bool>();

            Parallel.ForEach(enumerables, batch =>
            {
                result.Add(func(batch, helper));
            });

            return result.All(c => c);
        }

        
        public bool ParallelCall<T1, T2>(List<T1> dataList, List<T2> helperList, Func<IEnumerable<T1>, T2, bool> func)
        {
            if (!dataList.Any())
            {
                return true;
            }

            int dataSize = dataList.Count;
            int helpSize = helperList.Count;
            int chunkSize = (dataSize + helpSize - 1) / helpSize;

            var enumerables = dataList.Chunk(chunkSize).Select((c, index) => new TempData<T1, T2>()
            {
                SourceList = c,
                Helper = helperList[index]
            });

            var results = new ConcurrentBag<bool>();
            Parallel.ForEach(enumerables, data =>
            {
                results.Add(func(data.SourceList, data.Helper));
            });

            return results.All(c => c);
        }

        class TempData<T1, T2>
        {
            public IEnumerable<T1> SourceList { get; set; }
            public T2 Helper { get; set; }
        }
    }
}
