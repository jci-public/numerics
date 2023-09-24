/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;

#if !NET6_0_OR_GREATER
using System.Threading;
#endif

namespace JohnsonControls.Numerics.Buffers
{
    /// <summary>
    /// Various details about a Gen2 garbage collection
    /// </summary>
    public readonly struct Gen2CollectionArgs
    {
        public int MemoryPressure { get; }

        internal Gen2CollectionArgs(int memoryPressure)
            => MemoryPressure = memoryPressure;
    }

    /// <summary>
    /// A mechanism to run actions during garbage collection
    /// </summary>
    public sealed class GCMonitor : CriticalFinalizerObject
    {
        /// <summary>
        /// Triggers once per Gen2 garbage collection and maybe more on startup
        /// </summary>
        public static event EventHandler<Gen2CollectionArgs>? Gen2Collection;

        [SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "By design")]
        static GCMonitor() => new GCMonitor();

        private GCMonitor() { }

        ~GCMonitor()
        {
#if NET6_0_OR_GREATER
            var memoryInfo = GC.GetGCMemoryInfo();
            var percentLoad = (int)(100 * memoryInfo.MemoryLoadBytes / memoryInfo.HighMemoryLoadThresholdBytes);
#else
            var percentLoad = 0;
#endif

            try
            {
                Gen2Collection?.Invoke(this, new Gen2CollectionArgs(percentLoad));
            }
            catch (Exception ex)
            {
                Trace.TraceError("Gen2Collection invoke failed with {0}", ex);
            }

#if NET6_0_OR_GREATER
            GC.ReRegisterForFinalize(this);
#else
            ThreadPool.QueueUserWorkItem(static gcm =>
            {
                GC.ReRegisterForFinalize(gcm); 
            }, this);
#endif
        }
    }
}
