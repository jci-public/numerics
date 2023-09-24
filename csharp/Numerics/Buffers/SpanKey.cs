/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NET5_0_OR_GREATER
using System.Numerics;
#else
using System.Runtime.ConstrainedExecution;
#endif

namespace JohnsonControls.Numerics.Buffers
{
    /// <summary>
    /// Facilitates using <see cref="ReadOnlySpan{T}"/> as a key for a dictionary
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe readonly struct SpanKey : IEquatable<SpanKey>
    {
#if NET5_0_OR_GREATER
        /// <summary>
        /// Used to hold a reference to the pointer leveraged by <see cref="SpanKey"/>
        /// </summary>
        public readonly struct Pin
        {
            internal readonly byte[] Target; 

            private Pin(byte[] target) => Target = target;

            internal static SpanKey Create(ReadOnlySpan<byte> source, out Pin pin, bool exact)
            {
                var length = source.Length;
                var capacity = RoundCapacity(length, exact); 
                var t = GC.AllocateUninitializedArray<byte>(capacity, true);
                pin = new(t); source.CopyTo(t);
                return new((byte*)Unsafe.AsPointer(ref t[0]), length);
            }
        }
#else
        /// <summary>
        /// Used to hold a reference to the pointer leveraged by <see cref="SpanKey"/>
        /// </summary>
        public sealed class Pin : CriticalFinalizerObject, IDisposable
        {
            internal IntPtr Target;

            private Pin(IntPtr target) => Target = target; 

            ~Pin() => DisposeCore();

            ///<inheritdoc/>
            public void Dispose()
            {
                DisposeCore();
                GC.SuppressFinalize(this);
            }

            private void DisposeCore()
            {
                if (Target == IntPtr.Zero) 
                    return;
                Marshal.FreeHGlobal(Target);
                Target = IntPtr.Zero;
            }

            internal static SpanKey Create(ReadOnlySpan<byte> source, out Pin pin, bool exact)
            {
                var length = source.Length;
                var capacity = RoundCapacity(length, exact);

                var t = Marshal.AllocHGlobal(capacity);
                var ts = new Span<byte>((byte*)t, capacity);

                pin = new(t); source.CopyTo(ts);
                return new((byte*)t, length);
            }
        }
#endif

        private readonly byte* _pointer;
        private readonly int _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SpanKey(byte* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        /// <summary>
        /// Unsafely creates a <see cref="ReadOnlySpan{T}"/> from the underlying key
        /// </summary>
        /// <returns>The <see cref="ReadOnlySpan{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan() => new(_pointer, _length);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? x) => x is SpanKey key && Equals(key);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SpanKey other) => AsSpan().SequenceEqual(other.AsSpan());

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SpanKey left, SpanKey right) => left.Equals(right);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SpanKey left, SpanKey right) => !(left == right);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => AsSpan().GetXXHashCode();

        /// <summary>
        /// Allocates a new <see cref="SpanKey"/> from the given <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="content">The content to copy.</param>
        /// <returns>The created key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanKey Allocate(ReadOnlySpan<byte> source, out Pin pin, bool exact = false) =>
            Pin.Create(source, out pin, exact);

        /// <summary>
        /// Emphemerally creates a new <see cref="SpanKey"/> from the given <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="source">The referenced content.</param>
        /// <param name="factory">The producer of the result.</param>
        /// <returns>The result.</returns>
        /// <remarks>Factory should not capture the key. </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Use<TResult>(ReadOnlySpan<byte> source,
            Func<SpanKey, TResult> factory)
        {
            fixed (byte* p = source)
            {
                var key = new SpanKey(p, source.Length);
                return factory(key);
            }
        }

        /// <summary>
        /// Emphemerally creates a new <see cref="SpanKey"/> from the given <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <typeparam name="TState">The input type to the factory.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="source">The referenced content.</param>
        /// <param name="state">The input to the factory.</param>
        /// <param name="factory">The producer of the result.</param>
        /// <returns>The result.</returns>
        /// <remarks>Factory should not capture the key. </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Use<TState, TResult>(ReadOnlySpan<byte> source, TState state,
            Func<TState, SpanKey, TResult> factory)
        {
            fixed (byte* p = source)
            {
                var key = new SpanKey(p, source.Length);
                return factory(state, key);
            }
        }

        private static int RoundCapacity(int length, bool exact)
        {
            if (exact) return length;
            var capacity = RoundUpToPowerOf2(length);
            if (capacity < 8) capacity = 8;
            return capacity; 
        }

        private static int RoundUpToPowerOf2(int i)
        {
#if NET5_0_OR_GREATER
            return (int)BitOperations.RoundUpToPowerOf2((uint)i);
#else
            // Based on https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            i--;
            i |= i >> 1;
            i |= i >> 2;
            i |= i >> 4;
            i |= i >> 8;
            i |= i >> 16;
            return i + 1;
#endif
        }
    }
}
