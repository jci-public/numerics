/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Buffers;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace JohnsonControls.Numerics.Measure
{
    /// <summary>
    /// The core units of measure thread safe and immutable class.
    /// </summary>
    [JsonConverter(typeof(JsonConverter))]
    public sealed partial class UnitOfMeasure
    {
        /// <summary>
        /// A representation for "not a unit"
        /// </summary>
        public static UnitOfMeasure NaU { get; private set; }

        //Cache
        private static readonly ConcurrentDictionary<SpanKey, UnitOfMeasure> Cache = new();
        private static TimeSpan _slidingExpiration;
        private static int _highMemoryPressureThreshold;
        private static int _highMemoryPressureClearPercentage;

        //Resolver
        private static Resolver _resolver;

        static UnitOfMeasure()
        {
            (_resolver, NaU) = ConfigureCore(Options.Default);
            GCMonitor.Gen2Collection += ExpirationScan;
        }

        /// <summary>
        /// Allows the user to configure the library.
        /// </summary>
        /// <param name="options">The options to use</param>
        public static void Configure(Options options)
        {
            (_resolver, NaU) = ConfigureCore(options);
            Cache.Clear();
        }

        private readonly string _origin;
        private readonly byte[] _originUtf8;
        private readonly short[] _exponents;
        private readonly double _factor, _offset;
        private readonly SpanKey.Pin _pin;

        private int _hit = 1;
        private DateTime _lastHitScan = DateTime.MinValue;

        private UnitOfMeasure(string origin, short[] exponents, double factor, double offset, SpanKey.Pin pin)
        {
            _origin = origin;
            _originUtf8 = Encoding.UTF8.GetBytes(origin);
            _exponents = exponents;
            _factor = factor;
            _offset = offset;
            _pin = pin;
        }

        /// <summary>
        /// Gets the conversion factor and offset to convert from this unit of measure to the destination unit of measure using the formula y = x * factor + offset.
        /// </summary>
        /// <param name="destination">The unit of the value after applying the formula</param>
        /// <returns>The factor and offset</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double Factor, double Offset) GetConversionTo(UnitOfMeasure? destination)
        {
            if (TryGetConversionTo(destination, out var factor, out var offset))
                return (factor, offset);
            throw Exceptions.NotConvertible(this, destination!);
        }

        /// <summary>
        /// Gets the conversion factor and offset to convert from this unit of measure to the destination unit of measure using the formula y = x * factor + offset.
        /// </summary>
        /// <param name="destination">The unit of the value after applying the formula</param>
        /// <param name="factor">The factor</param>
        /// <param name="offset">The offset</param>
        /// <returns>Whether the conversion was successful</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetConversionTo(UnitOfMeasure? destination, out double factor, out double offset)
        {
            if (destination is null)
            {
                factor = 1; offset = 0;
                return true;
            }

            if (!_resolver.AreEqual(_exponents, destination._exponents))
            {
                factor = offset = 0;
                return false;
            }

            factor = _factor / destination._factor;
            offset = (_offset - destination._offset) / destination._factor;
            return true;
        }

        /// <inheritdoc/>
        public override string ToString() => _origin;

        /// <summary>
        /// Provides access to the utf8 form of the unit of measure. 
        /// </summary>
        /// <returns>The utf8 bytes.</returns>
        public ReadOnlySpan<byte> ToUtf8String() => _originUtf8;

        /// <summary>
        /// An implicit conversion from string to unit of measure. This will return <see cref="NaU"/> if the conversion fails.
        /// </summary>
        /// <param name="source">The string expression</param>
        public static implicit operator UnitOfMeasure(string source)
        {
            if (TryCreate(source.AsSpan(), out var result, out var error))
                return result;

            Trace.TraceError($"{nameof(UnitOfMeasure)} failed to implicitly create {source} because {error}");
            return NaU;
        }

        /// <summary>
        /// Creates a unit of measure from a string
        /// </summary>
        /// <param name="expressionUtf8">The expression</param>
        /// <returns>The unit of measure</returns>
        public unsafe static UnitOfMeasure Create(string expression) =>
            Create(expression.AsSpan());

        /// <summary>
        /// Creates a unit of measure from a UTF8 string
        /// </summary>
        /// <param name="expressionUtf8">The utf8 expression</param>
        /// <returns>The unit of measure</returns>
        public unsafe static UnitOfMeasure Create(ReadOnlySpan<byte> expressionUtf8) =>
            TryCreate(expressionUtf8, out var result, out var error) ? result : throw new ArgumentException(error);

        /// <summary>
        /// Creates a unit of measure from a string
        /// </summary>
        /// <param name="expressionUtf8">The expression</param>
        /// <returns>The unit of measure</returns>
        public unsafe static UnitOfMeasure Create(ReadOnlySpan<char> expression) =>
            TryCreate(expression, out var result, out var error) ? result : throw new ArgumentException(error);

        /// <summary>
        /// Tries to create a unit of measure from a string
        /// </summary>
        /// <param name="expression">The expression</param>
        /// <param name="result">The result</param>
        /// <param name="error">The error</param>
        /// <returns>Indicates if creation was successful</returns>
        public unsafe static bool TryCreate(string expression, [NotNullWhen(true)] out UnitOfMeasure? result, [NotNullWhen(false)] out string? error) =>
            TryCreate(expression.AsSpan(), out result, out error);

        /// <summary>
        /// Tries to create a unit of measure from a UTF8 string
        /// </summary>
        /// <param name="expressionUtf8">The expression</param>
        /// <param name="result">The result</param>
        /// <param name="error">The error</param>
        /// <returns>Indicates if creation was successful</returns>
        public unsafe static bool TryCreate(ReadOnlySpan<byte> expressionUtf8, [NotNullWhen(true)] out UnitOfMeasure? result, [NotNullWhen(false)] out string? error)
        {
            (result, error) = SpanKey.Use(expressionUtf8, key =>
            {
                TryCreate(key, true, out var result, out var error);
                return (result, error);
            });
            return result is not null;
        }

        /// <summary>
        /// Tries to create a unit of measure from a string
        /// </summary>
        /// <param name="expressionUtf8">The expression</param>
        /// <param name="result">The result</param>
        /// <param name="error">The error</param>
        /// <returns>Indicates if creation was successful</returns>
        public unsafe static bool TryCreate(ReadOnlySpan<char> expression, [NotNullWhen(true)] out UnitOfMeasure? result, [NotNullWhen(false)] out string? error)
        {
            var span = MemoryMarshal.AsBytes(expression);
            (result, error) = SpanKey.Use(span, key =>
            {
                TryCreate(key, false, out var result, out var error);
                return (result, error);
            });
            return result is not null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCreate(SpanKey expression, bool isUtf8, [NotNullWhen(true)] out UnitOfMeasure? result, [NotNullWhen(false)] out string? error)
        {
            //NOTE: It is intentional that utf8 and utf16 paths will miss the cache of each other .. this is to avoid conversion costs. 
            //NOTE: We assume the caller has fixed the expression given the use of SpanKey
            if (Cache.TryGetValue(expression, out result))
            {
                result._hit = 1;
                error = default;
                return true;
            }

#if NETSTANDARD2_0
            string input; 
            unsafe
            {
                if (isUtf8)
                {
                    var span = expression.AsSpan();
                    fixed (byte* p = span)
                        input = Encoding.UTF8.GetString(p, span.Length);
                }
                else
                {
                    var span = MemoryMarshal.Cast<byte, char>(expression.AsSpan());
                    input = span.ToString(); 
                }
            }
#else
            var input = isUtf8 ? Encoding.UTF8.GetString(expression.AsSpan())
                : new string(MemoryMarshal.Cast<byte, char>(expression.AsSpan()));
#endif


            if (!_resolver.TryResolve(input.AsSpan(), out var unit, out error))
                return false;

            Cache.GetOrAdd(SpanKey.Allocate(expression.AsSpan(), out var pin), result = new UnitOfMeasure(input, unit.Exponents, unit.Factor, unit.Offset, pin));
            return true;
        }

        private static (Resolver Resolver, UnitOfMeasure NaU) ConfigureCore(Options options)
        {
            var resolver = new Resolver(options);
            _slidingExpiration = options.SlidingExpiration;
            _highMemoryPressureThreshold = options.HighMemoryPressureThreshold;
            _highMemoryPressureClearPercentage = options.HighMemoryPressureClearPercentage;

            var exponents = new short[resolver.ExponentCount];
            exponents.AsSpan().Fill(short.MaxValue);
            return (resolver, new UnitOfMeasure("NaU", exponents, double.NaN, double.NaN, default!));
        }
#if NET6_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static void ExpirationScan(object? sender, Gen2CollectionArgs args)
        {
            try
            {
                var now = DateTime.UtcNow;
                var cutoff = now - _slidingExpiration;
                foreach (var kv in Cache)
                {
                    var value = kv.Value;

                    //Check for a hit since the last scan
                    if (Interlocked.Exchange(ref value._hit, 0) == 1)
                        value._lastHitScan = now;
                    else if (value._lastHitScan < cutoff)
                        Cache.TryRemove(kv.Key, out _);
                }

                //Take a more aggressive approach to cleaning up memory
                if (args.MemoryPressure >= _highMemoryPressureThreshold)
                {
                    var count = Cache.Count * _highMemoryPressureClearPercentage / 100;
                    foreach (var kv in Cache.OrderBy(x => x.Value._lastHitScan).Take(count))
                        Cache.TryRemove(kv.Key, out _);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{nameof(UnitOfMeasure)} failed to perform expiration scan: {ex}");
            }
        }

        private class JsonConverter : JsonConverter<UnitOfMeasure>
        {
            /// <inheritdoc/>
#if NET5_0_OR_GREATER
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
            public override UnitOfMeasure? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                if (reader.TokenType != JsonTokenType.String)
                    throw Exceptions.JsonExpectedToken(ref reader);

                var length = reader.HasValueSequence
                   ? (int)reader.ValueSequence.Length
                   : reader.ValueSpan.Length;

                if (length <= 256)
                {
                    Span<byte> sb = stackalloc byte[length];
                    length = reader.CopyString(sb);
                    return Create(sb.Slice(0, length));
                }

                var apb = ArrayPool<byte>.Shared.Rent(length);
                length = reader.CopyString(apb);
                var u = Create(apb.AsSpan(0, length));
                ArrayPool<byte>.Shared.Return(apb);
                return u;
            }

            /// <inheritdoc/>
#if NET5_0_OR_GREATER
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
            public override void Write(Utf8JsonWriter writer, UnitOfMeasure? unit, JsonSerializerOptions options)
            {
                if (unit is null)
                    writer.WriteNullValue();
                else
                    writer.WriteStringValue(unit._originUtf8);
            }
        }
    }
}