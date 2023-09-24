/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Measure;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JohnsonControls.Numerics
{
    /// <summary>
    /// A collection of non-inlining expections the library may throw. 
    /// </summary>
    internal static class Exceptions
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ArgumentException NotConvertible(UnitOfMeasure x, UnitOfMeasure? y) =>
            new($"{x} is not convertible to {y} (they do not share equivalent base unit exponents)");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static JsonException JsonExpectedToken(ref Utf8JsonReader reader) =>
            new($"Expected String but encountered {reader.TokenType} at {reader.TokenStartIndex}");
    }
}
