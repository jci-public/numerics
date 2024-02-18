/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Algebra;
using JohnsonControls.Numerics.Buffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JohnsonControls.Numerics.Measure
{
    public partial class UnitOfMeasure
    {
        /// <summary>
        /// A mechanism to resolve a unit expression into its exponents, factor, and offset.
        /// </summary>
        public sealed class Resolver : AlgebraicResolver<Resolver.UnitInfo>
        {
            /// <summary>
            /// The underlying variable type used by the resolver.
            /// </summary>
            public readonly struct UnitInfo
            {
                /// <summary>
                /// The exponent of each base unit.
                /// </summary>
                public readonly short[] Exponents;

                /// <summary>
                /// The factor to convert the unit to the base unit.
                /// </summary>
                public readonly double Factor;

                /// <summary>
                /// The offset to convert the unit to the base unit.
                /// </summary>
                public readonly double Offset;

                /// <summary>
                /// A pin to hold onto the underlying memory of the key.
                /// </summary>
#if NETSTANDARD2_1
                internal readonly SpanKey.Pin? Pin;
#else
                internal readonly SpanKey.Pin Pin;
#endif

                /// <summary>
                /// Constructs the <see cref="UnitInfo"/> from the exponents, factor, and offset.
                /// </summary>
                /// <param name="exponents"></param>
                /// <param name="factor"></param>
                /// <param name="offset"></param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public UnitInfo(short[] exponents, double factor, double offset)
                {
                    Exponents = exponents;
                    Factor = factor;
                    Offset = offset;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal UnitInfo(short[] exponents, double factor, double offset, SpanKey.Pin pin)
                {
                    Exponents = exponents;
                    Factor = factor;
                    Offset = offset;
                    Pin = pin;
                }

                /// <summary>
                /// Deconstructs the <see cref="UnitInfo"/> into the exponents, factor, and offset.
                /// </summary>
                /// <param name="exponents"></param>
                /// <param name="factor"></param>
                /// <param name="offset"></param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Deconstruct(out short[] exponents, out double factor, out double offset)
                {
                    exponents = Exponents;
                    factor = Factor;
                    offset = Offset;
                }
            }

            private const int PrecisionDigits = 3;
            private static readonly double Precision = 1 / Math.Pow(10, PrecisionDigits);
            private static readonly int Tolerance = (int)Math.Round(0.01 / Precision, PrecisionDigits);
            private static readonly char[] Preseeks = [']', ',']; 

            private readonly short[] ZeroExponents;
            private readonly Dictionary<SpanKey, UnitInfo> _units = new();

            /// <summary>
            /// The number of exponents used by the resolver.
            /// </summary>
            public readonly int ExponentCount;

            /// <summary>
            /// Constructs the <see cref="Resolver"/> from the <see cref="Options"/>.
            /// </summary>
            /// <param name="options">Used to configure the underlying supported unit expressions and base unit system</param>
            public Resolver(Options options)
            {
                AddUnaryOperator('+');
                AddUnaryOperator('-');

                AddBinaryOperator('^', 4, true);
                AddBinaryOperator('/', 3, false);
                AddBinaryOperator('*', 3, false);
                AddBinaryOperator('+', 2, false);
                AddBinaryOperator('-', 2, false);

                AddFunction("pow", 2);

                //Setup base units
                var baseUnits = options.BaseUnits;
                var baseCount = baseUnits.Count;

#if NET6_0_OR_GREATER
                var rem = baseCount % Vector<short>.Count;
                var exponentCount = ExponentCount = rem == 0
                    ? baseCount : baseCount - rem + Vector<short>.Count;
#else
                var exponentCount = ExponentCount = baseCount;
#endif

                ZeroExponents = new short[exponentCount];

                var i = 0;
                foreach (var unit in baseUnits)
                {
                    var u = FilterExpression(unit);
                    var exp = new short[exponentCount];
                    exp[i++] = ExponentRoundCast(1 / Precision);
                    var sk = SpanKey.Allocate(MemoryMarshal.AsBytes(u.AsSpan()), out var pin);
                    _units.Add(sk, new(exp, 1, 0, pin));
                }

                //Cleanse prefixes
                var prefixes = options.Prefixes
                    .ToDictionary(kv1 => FilterExpression(kv1.Key), kv1 => kv1.Value
                    .ToDictionary(kv2 => FilterExpression(kv2.Key), kv2 => kv2.Value));

                //Load Units 
                foreach (var kv1 in options.Units)
                {
                    var units = kv1.Key;
                    var unitsSpan = units.AsSpan(); 
                    var expression = kv1.Value;

                    string[]? lastPrefixes = null; 
                    for (var j = 0; j < units.Length; j++)
                    {
                        var token = units[j];
                        switch (token)
                        {
                            case ' ':
                                continue;
                            case '[':
                                var ps = unitsSpan.Slice(j + 1);
                                var k = ps.IndexOf(']');
                                if (k == -1) throw new Exception($"Mismatched [] in prefix in {units}");

                                lastPrefixes = ps.Slice(0, k).ToString().Split(',');
                                j += k; 
                                break;
                            case ',':
                                var ss = unitsSpan.Slice(0, j);
                                ProcessLastUnit(ss); 
                                lastPrefixes = null;
                                break;
                        }
                    }

                    ProcessLastUnit(unitsSpan);
                    
                    void ProcessLastUnit(ReadOnlySpan<char> source)
                    {
                        var k = source.LastIndexOfAny(Preseeks);

                        source = k == -1 ? source : source.Slice(k + 1);
                        var unit = FilterExpression(source.ToString());

                        if (!TryResolve(FilterExpression(expression).AsSpan(), out var x, out var error))
                            throw new Exception($"Unable to resolve {expression} for {unit}: {error}");

                        if (!baseUnits.Contains(unit))
                        {
                            var sk = SpanKey.Allocate(MemoryMarshal.AsBytes(unit.AsSpan()), out var pin);
                            _units.Add(sk, new(x.Exponents, x.Factor, x.Offset, pin));
                        }

                        if (lastPrefixes is null) return;

                        foreach (var prefix in lastPrefixes)
                            foreach (var kv2 in prefixes[prefix])
                            {
                                var px = kv2.Key;
                                var f = kv2.Value;
                                var n = $"{px}{unit}";
                                if (baseUnits.Contains(n)) continue;

                                if (SpanKey.Use(MemoryMarshal.AsBytes(n.AsSpan()), _units, static (u, sk) => u.ContainsKey(sk)))
                                    n = $"[{px}]{unit}"; //mark to indicate conflict

                                var sk = SpanKey.Allocate(MemoryMarshal.AsBytes(n.AsSpan()), out var pin);
                                _units.Add(sk, new(x.Exponents, x.Factor * f, x.Offset, pin));
                            }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected override bool TryInvokeUnary(char token, in UnitInfo x, [NotNullWhen(true)] out UnitInfo result, [NotNullWhen(false)] out string? error)
            {
                result = token switch
                {
                    '+' => x,
                    '-' => new(x.Exponents, -x.Factor, -x.Offset),
                    _ => throw UnknownUnaryToken(token)
                };
                error = null;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected override bool TryInvokeBinary(char token, in UnitInfo x, in UnitInfo y, [NotNullWhen(true)] out UnitInfo result, [NotNullWhen(false)] out string? error)
            {
                var (xe, xf, xo) = x;
                var xConst = xe.SequenceEqual(ZeroExponents);

                var (ye, yf, yo) = y;
                var yConst = ye.SequenceEqual(ZeroExponents);

                if ((yo != 0 && xo != 0) || (yo != 0 && !xConst) || (xo != 0 && !yConst))
                    throw IncommensurableUnits();

                var offset = xo + yo;
                var factor = 1.0;
                var exponents = new short[ExponentCount];
                result = default;
                switch (token)
                {
                    case '^' when yConst:
                        factor = Math.Pow(xf, yf);
                        if (offset != 0)
                            return Error("Units with offsets (e.g. degC, degF) cannot be raised to a power.", out error);
                        for (var i = 0; i < exponents.Length; i++)
                            exponents[i] = ExponentRoundCast(xe![i] * yf);
                        break;
                    case '^':
                        return Error("Units can only be raised to a unitless power", out error);
                    case '*':
                        factor = xf * yf;
                        ExponentOperation(xe, ye, exponents, static (x, y) => x + y);
                        break;
                    case '/':
                        factor = xf / yf;
                        ExponentOperation(xe, ye, exponents, static (x, y) => x - y);
                        break;
                    case '+' when xConst && yConst:
                        factor = xf + yf;
                        exponents = xe;
                        break;
                    case '+' when yConst:
                        factor = xf;
                        offset = xo + yf;
                        exponents = xe;
                        break;
                    case '+' when xConst:
                        factor = yf;
                        offset = yo + xf;
                        exponents = ye;
                        break;
                    case '+':
                        if (!AreEqual(xe, ye))
                            return Error("Units must be commensurable to be added", out error);
                        factor = xf + yf;

                        if (factor == 0)
                            offset = 0;
                        else
                        {
                            offset = xo + yo;
                            exponents = xe;
                        }

                        break;
                    case '-' when yConst && xConst:
                        factor = xf - yf;
                        exponents = xe;
                        break;
                    case '-' when yConst:
                        factor = xf;
                        offset = xo - yf;
                        exponents = xe;
                        break;
                    case '-' when xConst:
                        factor = -yf;
                        offset = -yo + xf;
                        exponents = ye;
                        break;
                    case '-':
                        if (!AreEqual(xe, ye))
                            return Error("Units must be commensurable to be subtracted", out error);

                        factor = xf - yf;
                        if (factor == 0)
                            offset = 0;
                        else
                        {
                            offset = xo - yo;
                            exponents = xe;
                        }
                        break;
                }

                error = default;
                result = new(exponents, factor, offset);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected override bool TryInvokeFunction(string token, ReadOnlySpan<UnitInfo> args, [NotNullWhen(true)] out UnitInfo result, [NotNullWhen(false)] out string? error)
            {
                switch (token)
                {
                    case "pow":
                        return TryInvokeBinary('^', args[0], args[1], out result, out error);
                    default:
                        result = default;
                        return Error($"Unknown function '{token}'", out error);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected override bool TryCreateVariable(double constant, [NotNullWhen(true)] out UnitInfo variable)
            {
                variable = new(new short[ExponentCount], constant, 0);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected override bool TryCreateVariable(ReadOnlySpan<char> expression, [NotNullWhen(true)] out UnitInfo variable)
            {
                variable = SpanKey.Use(MemoryMarshal.AsBytes(expression), _units, (u, sk) => u.TryGetValue(sk, out var v) ? v : default);
                return variable.Exponents is not null;
            }

            protected override string GetExpressionError(ReadOnlySpan<char> expression, int position)
            {
                var search = expression.ToString();
                var searchLower = search.ToLower();
                var matches = _units.Keys
                    .Select(k =>
                    {
                        var v = MemoryMarshal.Cast<byte, char>(k.AsSpan()).ToString();
                        return (Value: v, Distance: searchLower.LevenshteinDistance(v.ToLower().AsSpan()));
                    })
                    .OrderBy(a => a.Distance).Select(a => a.Value).Take(11);
                return $"Unrecognized unit expression '{search}' at position {position}. Did you mean: {string.Join(", ", matches)}?";
            }

#if NET5_0_OR_GREATER
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ExponentOperation(ReadOnlySpan<short> x, ReadOnlySpan<short> y, Span<short> destination,
                Func<Vector<short>, Vector<short>, Vector<short>> func)
            {
                var length = x.Length; 
                Debug.Assert(y.Length == length);
                Debug.Assert(destination.Length == length); 
                Debug.Assert(length % Vector<short>.Count == 0);
                for (var i = 0; i < length; i += Vector<short>.Count)
                    func(new Vector<short>(x[i..]), new Vector<short>(y[i..])).CopyTo(destination[i..]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool AreEqual(ReadOnlySpan<short> x, ReadOnlySpan<short> y)
            {
                var exponentCount = ExponentCount;
                Debug.Assert(x.Length == exponentCount && y.Length == exponentCount);
                Debug.Assert(exponentCount % Vector<short>.Count == 0);

                var error = 0;
                for (var i = 0; i < exponentCount; i += Vector<short>.Count)
                    error += Vector.Sum(Vector.Abs(new Vector<short>(x[i..]) - new Vector<short>(y[i..])));
                return error <= Tolerance;
            }
#else
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ExponentOperation(ReadOnlySpan<short> x, ReadOnlySpan<short> y, Span<short> destination,
                Func<short, short, int> func)
            {
                var length = x.Length;
                Debug.Assert(y.Length == length);
                Debug.Assert(destination.Length == length);

                for (var i = 0; i < length; i++)
                    destination[i] = (short)func(x[i], y[i]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool AreEqual(ReadOnlySpan<short> x, ReadOnlySpan<short> y)
            {
                var exponentCount = ExponentCount;
                Debug.Assert(x.Length == exponentCount && y.Length == exponentCount);

                var error = 0;
                for (var i = 0; i < exponentCount; i++)
                    error += Math.Abs(x[i] - y[i]);
                return error <= Tolerance;
            }
#endif

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static short ExponentRoundCast(double value) =>
                checked((short)Math.Round(value, PrecisionDigits));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static string FilterExpression(string value) =>
                    value.Replace(" ", "");

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static InvalidOperationException IncommensurableUnits() =>
                new("""
                        Units with offsets (e.g. degC, degF) should be converted to base (e.g. degK)
                        or delta variants (delC, delF) before being used in expressions as they are incommensurable.
                        Only combining unitless expressions with offset expressions such as 15 * degF is allowed.
                        """);

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static ArgumentOutOfRangeException UnknownUnaryToken(char token) =>
                new(nameof(token), $"Unsupported unary token {token}");

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static ArgumentOutOfRangeException UnknownBinaryToken(char token) =>
                new(nameof(token), $"Unsupported binary token {token}");

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static ArgumentOutOfRangeException UnknownFunctionToken(string token) =>
                new(nameof(token), $"Unsupported function token {token}");
        }
    }
}
