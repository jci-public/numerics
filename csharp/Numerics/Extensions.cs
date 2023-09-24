/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using K4os.Hash.xxHash;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JohnsonControls.Numerics.Measure;

#if NETSTANDARD2_0
using System.Collections.Generic;
#endif

namespace JohnsonControls.Numerics
{
    public static class Extensions
    {
        /// <summary>
        /// Convient method to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <returns>The result.</returns>
        public static double ConvertTo(this (int, string) source, UnitOfMeasure? destination) =>
           ConvertTo((source.Item1, (UnitOfMeasure)source.Item2), destination);

        /// <summary>
        /// Convienent method to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <returns>The result.</returns>
        public static double ConvertTo(this (double, string) source, UnitOfMeasure? destination) =>
            ConvertTo((source.Item1, (UnitOfMeasure)source.Item2), destination);

        /// <summary>
        /// Convienent method to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <returns>The result.</returns>
        public static double ConvertTo(this (double, UnitOfMeasure) source, UnitOfMeasure? destination)
        {
            if (TryConvertTo(source, destination, out var result))
                return result;
            throw Exceptions.NotConvertible(source.Item2, destination);
        }

        /// <summary>
        /// Convienent method to try to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <param name="result">The result</param>
        /// <returns>Whether conversion was successful</returns>
        public static bool TryConvertTo(this (int, string) source, UnitOfMeasure? destination, out double result) =>
            TryConvertTo((source.Item1, (UnitOfMeasure)source.Item2), destination, out result);

        /// <summary>
        /// Convienent method to try to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <param name="result">The result</param>
        /// <returns>Whether conversion was successful</returns>
        public static bool TryConvertTo(this (double, string) source, UnitOfMeasure? destination, out double result) =>
            TryConvertTo((source.Item1, (UnitOfMeasure)source.Item2), destination, out result);

        /// <summary>
        /// Convienent method to try to convert a value from one unit of measure to another.
        /// </summary>
        /// <param name="source">The described value and unit</param>
        /// <param name="destination">The desired destination of the result</param>
        /// <param name="result">The result</param>
        /// <returns>Whether conversion was successful</returns>
        public static bool TryConvertTo(this (double, UnitOfMeasure) source, UnitOfMeasure? destination, out double result)
        {
            var (v, u) = source;
            if (!u.TryGetConversionTo(destination, out var f, out var o))
            {
                result = default;
                return false;
            }

            result = v * f + o;
            return true;
        }

        /// <summary>
        /// The configured unit of measure for seconds.
        /// </summary>
        public static string SecondsUnit { get; set; } = "s";

        /// <summary>
        /// Convert a unit of measure to a TimeSpan.
        /// </summary>
        /// <param name="source">The supplied unit of measure</param>
        /// <returns>The TimeSpan</returns>
        public static TimeSpan ConvertToTimeSpan(this UnitOfMeasure source)
        {
            if (TryConvertToTimeSpan(source, out var timeSpan))
                return timeSpan;
            throw Exceptions.NotConvertible(source, SecondsUnit);
        }

        /// <summary>
        /// Try to convert a unit of measure to a TimeSpan.
        /// </summary>
        /// <param name="source">The supplied unit of measure</param>
        /// <param name="timeSpan">The resultant TimeSpan</param>
        /// <returns>Whether conversion was successful</returns>
        public static bool TryConvertToTimeSpan(this UnitOfMeasure source, out TimeSpan timeSpan)
        {
            if (!source.TryGetConversionTo(SecondsUnit, out var f, out _))
            {
                timeSpan = default;
                return false;
            }

            timeSpan = TimeSpan.FromSeconds(f);
            return true;
        }

        /// <summary>
        /// Convert a unit of measure to ticks.
        /// </summary>
        /// <param name="source">The supplied unit of measure</param>
        /// <returns>The ticks</returns>
        public static long ConvertToTicks(this UnitOfMeasure source)
        {
            if (TryConvertToTicks(source, out var ticks))
                return ticks;
            throw Exceptions.NotConvertible(source, SecondsUnit);
        }

        /// <summary>
        /// Try to convert a unit of measure to ticks.
        /// </summary>
        /// <param name="source">The supplied unit of measure</param>
        /// <param name="timeSpan">The resultant ticks</param>
        /// <returns>Whether conversion was successful</returns>
        public static bool TryConvertToTicks(this UnitOfMeasure source, out long ticks)
        {
            if (!source.TryGetConversionTo(SecondsUnit, out var f, out _))
            {
                ticks = default;
                return false;
            }

            ticks = (long)(f * TimeSpan.TicksPerSecond);
            return true;
        }

        /// <summary>
        /// A quick method to get the hash code of a span of unmanaged values.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of values</typeparam>
        /// <param name="source">The source to hash</param>
        /// <returns>The hashcode</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetXXHashCode<T>(this ReadOnlySpan<T> source)
            where T : unmanaged
        {
            var bytes = MemoryMarshal.AsBytes(source);
            return GetXXHashCode(bytes);
        }

        /// <summary>
        /// A quick method to get the hash code of a span of bytes.
        /// </summary>
        /// <param name="source">The source to hash</param>
        /// <returns>The hashcode</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetXXHashCode(this ReadOnlySpan<byte> source)
        {
            var h = XXH64.DigestOf(source);
            return (int)(h >> 32) ^ (int)h;
        }

        /// <summary>
        /// Calculates the Levenshtein distance between two strings.
        /// </summary>
        /// <param name="x">The first string</param>
        /// <param name="y">The second string</param>
        /// <returns>The distance</returns>
        public static int LevenshteinDistance(this string x, ReadOnlySpan<char> y) =>
            LevenshteinDistance(x.AsSpan(), y);

        /// <summary>
        /// Calculates the Levenshtein distance between two strings.
        /// </summary>
        /// <param name="x">The first string</param>
        /// <param name="y">The second string</param>
        /// <returns>The distance</returns>
        public static int LevenshteinDistance(this ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {
            int n = x.Length, m = y.Length;
            var d = new int[n + 1, m + 1];
            if (n == 0) return m;
            if (m == 0) return n;
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    var cost = (y[j - 1] == x[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[n, m];
        }

#if NETSTANDARD2_0
        internal static bool TryPop<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count > 0)
            {
                result = stack.Pop();
                return true;
            }

            result = default;
            return false;
        }

        internal static bool TryPeek<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count > 0)
            {
                result = stack.Peek();
                return true;
            }

            result = default;
            return false;
        }

        internal unsafe static string AsString(this ReadOnlySpan<char> chars)
        {
            fixed (char* p = chars)
                return new string(p, 0, chars.Length);
        }
#endif

    }
}


#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter will not be null even if the corresponding type allows it.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]

    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the specified return value condition.</summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>Gets the return value condition.</summary>
        public bool ReturnValue { get; }
    }
}
#endif