/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using JohnsonControls.Numerics.Buffers;
using System.Runtime.CompilerServices;

namespace JohnsonControls.Numerics.Tests.Buffers
{
    [TestClass]
    public class SpanKeyTests
    {
        private static readonly Random _rng = new(0);

        [TestMethod]
        public void HappyPath()
        {
            var key = new byte[3];
            _rng.NextBytes(key);
            var s0 = SpanKey.Allocate(key, out var sp0); Assert.IsNotNull(sp0); 
            _rng.NextBytes(key);
            var s1 = SpanKey.Allocate(key, out var sp1, true); Assert.IsNotNull(sp0);

            Assert.IsFalse(SpanKey.Use(key, s0, (s, s0) => s.Equals(s0)));
            Assert.IsFalse(SpanKey.Use(key, s0, (s, s0) => s.Equals((object)s0)));
            Assert.IsFalse(SpanKey.Use(key, s0, (s, s0) => s.Equals(null)));
            Assert.IsFalse(SpanKey.Use(key, s0, (s, s0) => s == s0));
            Assert.IsTrue(SpanKey.Use(key, s0, (s, s0) => s != s0));
            Assert.AreNotEqual(s0.GetHashCode(), s1.GetHashCode()); 

            Assert.IsTrue(SpanKey.Use(key, s1, (s, s1) => s.Equals(s1)));
            Assert.IsTrue(SpanKey.Use(key, s => s.Equals(s)));

#if !NET5_0_OR_GREATER
            sp0 = null;
            GC.Collect(); 
            sp1.Dispose();
            sp1.Dispose(); 
#endif
        }
    }

    /*
                                                    Job=.NET 6.0  Runtime=.NET 6.0  
        |                Method |       Mean |     Error |    StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
        |---------------------- |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
        |        LargeIntoArray | 121.793 ns | 1.1830 ns | 1.0487 ns |  1.00 |    0.00 | 0.0801 |    1048 B |        1.00 |
        | LargeIntoThreadStatic | 104.741 ns | 1.8737 ns | 1.6610 ns |  0.86 |    0.02 |      - |         - |        0.00 |
        |      LargeIntoSpanKey |  81.746 ns | 0.3926 ns | 0.3673 ns |  0.67 |    0.01 |      - |         - |        0.00 |
        |     LargePreallocated |  89.747 ns | 0.9354 ns | 0.8749 ns |  0.74 |    0.01 |      - |         - |        0.00 |
        |                       |            |           |           |       |         |        |           |             |
        |        SmallIntoArray |  13.446 ns | 0.0802 ns | 0.0626 ns |  1.00 |    0.00 | 0.0024 |      32 B |        1.00 |
        | SmallIntoThreadStatic |  13.063 ns | 0.2259 ns | 0.1886 ns |  0.97 |    0.01 |      - |         - |        0.00 |
        |      SmallIntoSpanKey |   7.585 ns | 0.0796 ns | 0.0706 ns |  0.56 |    0.00 |      - |         - |        0.00 |
        |     SmallPreallocated |   9.131 ns | 0.0548 ns | 0.0486 ns |  0.68 |    0.01 |      - |         - |        0.00 |

                                        Job=.NET Framework 4.6.2  Runtime=.NET Framework 4.6.2  

        |                Method |      Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
        |---------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
        |        LargeIntoArray | 170.02 ns | 2.279 ns | 2.624 ns |  1.00 |    0.00 | 0.1669 |    1051 B |        1.00 |
        | LargeIntoThreadStatic | 134.63 ns | 0.468 ns | 0.438 ns |  0.79 |    0.01 |      - |         - |        0.00 |
        |      LargeIntoSpanKey | 112.07 ns | 0.443 ns | 0.370 ns |  0.66 |    0.01 |      - |         - |        0.00 |
        |     LargePreallocated | 100.68 ns | 1.554 ns | 1.453 ns |  0.59 |    0.01 |      - |         - |        0.00 |
        |                       |           |          |          |       |         |        |           |             |
        |        SmallIntoArray |  52.96 ns | 0.832 ns | 0.695 ns |  1.00 |    0.00 | 0.0051 |      32 B |        1.00 |
        | SmallIntoThreadStatic |  45.32 ns | 0.908 ns | 0.892 ns |  0.86 |    0.02 |      - |         - |        0.00 |
        |      SmallIntoSpanKey |  37.07 ns | 0.104 ns | 0.092 ns |  0.70 |    0.01 |      - |         - |        0.00 |
        |     SmallPreallocated |  25.96 ns | 0.171 ns | 0.143 ns |  0.49 |    0.01 |      - |         - |        0.00 |
     */

    [MemoryDiagnoser, TestClass, Ignore]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
#if NET6_0
    [SimpleJob(RuntimeMoniker.Net60, baseline: true)]
#else
    [SimpleJob(RuntimeMoniker.Net462, baseline: true)]
#endif

    public class SpanKeyBenchmarks
    {
        private static readonly Random _rng = new(0);
        [ThreadStatic] private static byte[]? _bts;

        private readonly byte[] _largeKey = new byte[1024];
        private readonly byte[] _smallKey = new byte[8];
        private readonly Dictionary<byte[], int> _byteDictionary = new(new ByteComparer());
        private readonly Dictionary<SpanKey, (int Value, SpanKey.Pin Pin)> _spanDictionary = new();


        public SpanKeyBenchmarks()
        {
            _rng.NextBytes(_largeKey);
            _rng.NextBytes(_smallKey);

            _byteDictionary.Add(_largeKey.AsSpan().ToArray(), 17);
            _byteDictionary.Add(_smallKey.AsSpan().ToArray(), 13);

            _spanDictionary.Add(SpanKey.Allocate(_largeKey, out var pin), (17, pin));
            _spanDictionary.Add(SpanKey.Allocate(_smallKey, out pin), (13, pin));
        }

        [Benchmark(Baseline = true), BenchmarkCategory("L")]
        public int LargeIntoArray()
        {
            ReadOnlySpan<byte> bigKey = _largeKey;
            return _byteDictionary[bigKey.ToArray()];
        }

        [Benchmark, BenchmarkCategory("L")]
        public int LargeIntoThreadStatic()
        {
            ReadOnlySpan<byte> bigKey = _largeKey;
            var bufferKey = _bts ??= new byte[bigKey.Length];
            if (bufferKey.Length < bigKey.Length) bufferKey = _bts = new byte[bigKey.Length];
            bigKey.CopyTo(bufferKey);
            return _byteDictionary[bufferKey];
        }

        [Benchmark, BenchmarkCategory("L")]
        public int LargeIntoSpanKey() => SpanKey.Use(_largeKey, _spanDictionary, (s, k) => s[k].Value);

        [Benchmark, BenchmarkCategory("L")]
        public int LargePreallocated() => _byteDictionary[_largeKey];

        [Benchmark(Baseline = true), BenchmarkCategory("S")]
        public int SmallIntoArray()
        {
            ReadOnlySpan<byte> smallKey = _smallKey;
            return _byteDictionary[smallKey.ToArray()];
        }


        [Benchmark, BenchmarkCategory("S")]
        public int SmallIntoThreadStatic()
        {
            ReadOnlySpan<byte> smallKey = _smallKey;
            var bufferKey = _bts ??= new byte[smallKey.Length];
            if (bufferKey.Length < smallKey.Length) bufferKey = _bts = new byte[smallKey.Length];
            smallKey.CopyTo(bufferKey);
            return _byteDictionary[bufferKey];
        }

        [Benchmark, BenchmarkCategory("S")]
        public int SmallIntoSpanKey() => SpanKey.Use(_smallKey, _spanDictionary, (s, k) => s[k].Value);

        [Benchmark, BenchmarkCategory("S")]
        public int SmallPreallocated() => _byteDictionary[_smallKey];

        [TestMethod]
        public void Run() => BenchmarkRunner.Run<SpanKeyBenchmarks>();

        private class ByteComparer : IEqualityComparer<byte[]>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetHashCode(byte[] obj) => ((ReadOnlySpan<byte>)obj).GetXXHashCode(); 
        }
    }
}
