using System;
using System.Runtime;

namespace Common.Utilities
{
    public static class MemoryHelper
    {
        static MemoryHelper()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        }
        public static void CleanMemory()
        {
            // GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }
    }
}