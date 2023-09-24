/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Buffers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JohnsonControls.Numerics.Algebra
{
    /// <summary>
    /// Keeps non-typed settings free from duplication in <see cref="AlgebraicResolver{TVariable}"/>.
    /// </summary>
    public abstract class AlgebraicResolver
    {
        private const int FunctionPrecedence = byte.MaxValue;
        private const int UnaryPrecedence = byte.MaxValue - 1;
        private const int MaxBinaryPrecedence = byte.MaxValue - 2;

        private protected const int OperatorCapacity = 256;
        private protected const int StacksCapacity = 256;
        private protected const int VariablesCapacity = 256;
        private static readonly bool[] ReservedOperators = new bool[OperatorCapacity];

        static AlgebraicResolver()
        {
            ReservedOperators[' '] = true;
            ReservedOperators['('] = true;
            ReservedOperators[')'] = true;
            ReservedOperators[','] = true;
        }

        private protected readonly Dictionary<SpanKey, OperatorInfo> _functions = new();
        private protected readonly OperatorInfo[] _binaries = new OperatorInfo[256];
        private protected readonly OperatorInfo[] _unaries = new OperatorInfo[256];
        private protected readonly HashSet<char> _preunaries = new() { '(', ',' };
        private protected readonly HashSet<char> _preseeks = new() { '(', ',', ')' };
        private protected char[]? _seeks;

        private protected AlgebraicResolver() { }

        /// <summary>
        /// Adds a unary operator to the resolver, invoked via <see cref="AlgebraicResolver{TVariable}.TryInvokeUnary(char, in TVariable, out TVariable?, out string?)"/>
        /// </summary>
        /// <param name="op">The token for the operator such as '-'</param>
        public void AddUnaryOperator(char op)
        {
            if (_unaries[op] is not null)
                throw OperatorAlreadyExists(op, 1);
            AddOperator(op);
            _unaries[op] = new OperatorInfo(op, 1, UnaryPrecedence, true);
        }

        /// <summary>
        /// Adds a binary operator to the resolver, invoked via <see cref="AlgebraicResolver{TVariable}.TryInvokeBinary(char, in TVariable, in TVariable, out TVariable?, out string?)"/>
        /// </summary>
        /// <param name="op">The token for the operator such as '*'</param>
        /// <param name="precedence">The order of operations precedence for the operator</param>
        /// <param name="rightAssociative">Whether the token is right associative such as how '^' normally is</param>
        public void AddBinaryOperator(char op, byte precedence, bool rightAssociative)
        {
            if (_binaries[op] is not null)
                throw OperatorAlreadyExists(op, 2);
            AddOperator(op);
            if (precedence > MaxBinaryPrecedence) precedence = MaxBinaryPrecedence;
            _binaries[op] = new OperatorInfo(op, 2, precedence, rightAssociative);
        }

        /// <summary>
        /// Adds a function to the resolver, invoked via <see cref="AlgebraicResolver{TVariable}.TryInvokeFunction(ReadOnlySpan{char}, ReadOnlySpan{in TVariable}, out TVariable?, out string?)"/>
        /// </summary>
        /// <param name="name">The token for the function such as 'pow'.</param>
        /// <param name="variableCount">The number of variables the function expects, or null if supports variant input.</param>
        public void AddFunction(string name, byte? variableCount = default)
        {
            if (_seeks is not null)
                throw ResolverCompiled();
            if (string.IsNullOrWhiteSpace(name))
                throw FunctionNameNullOrWhiteSpace(name.AsSpan());
            var sk = SpanKey.Allocate(MemoryMarshal.AsBytes(name.AsSpan()), out var pin);
            if (_functions.ContainsKey(sk))
                throw OperatorAlreadyExists(name, 3);
            _functions.Add(sk, new OperatorInfo(name, variableCount, FunctionPrecedence, true) { Pin = pin });
        }

        private void AddOperator(char op)
        {
            if (_seeks is not null)
                throw ResolverCompiled();
            if (op >= OperatorCapacity)
                throw OperatorNotSupported(op);
            if (ReservedOperators[op])
                throw OperatorReserved(op);
            _preseeks.Add(op);
            _preunaries.Add(op);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException OperatorAlreadyExists(object token, int type) =>
            new($"The {(type switch { 1 => "unary operator", 2 => "binary operator", _ => "function" })} '{token}' has already been added. ");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException ResolverCompiled() =>
            new("Additional operators cannot be registered after resolver starts resolving.");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException OperatorNotSupported(char op) =>
            new($"Operator '{op}' is not supported.", nameof(op));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException OperatorReserved(char op) =>
            new($"Operator '{op}' is reserved and cannot be registered.", nameof(op));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException FunctionNameNullOrWhiteSpace(ReadOnlySpan<char> name) =>
           new($"Function name '{name.ToString()}' cannot be null or whitespace.", nameof(name));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private protected static string MissingOperand(object token, int operandIndex, int lastPosition) =>
           $"Missing operand '{operandIndex}' for '{token}' where the last position was {lastPosition}";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static bool TryParseDouble(ReadOnlySpan<char> expression, out double result)
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            return double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
            return double.TryParse(expression.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
        }

        /// <summary>
        /// A simple helper method to return false and set the error message in a single line.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="error">The propagated error</param>
        /// <returns>False</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Error(string message, out string error)
        {
            error = message;
            return false;
        }

        private protected class OperatorInfo
        {
            public static readonly OperatorInfo Group = new("(,", 0, 0, false);

            public readonly object Token;
            public readonly byte? VariableCount;
            public readonly byte Precedence;
            public readonly bool RightAssociative;

#if NETSTANDARD2_1
            public SpanKey.Pin? Pin;
#else
            public SpanKey.Pin Pin;
#endif

            public OperatorInfo(object token, byte? variableCount, byte precedence, bool rightAssociative)
            {
                Token = token;
                VariableCount = variableCount;
                Precedence = precedence;
                RightAssociative = rightAssociative;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsGroup(OperatorInfo info) =>
                ReferenceEquals(info, Group);
        }
    }

    /// <summary>
    /// A thread-safe algebraic resolver derived from https://en.wikipedia.org/wiki/Shunting-yard_algorithm.
    /// </summary>
    /// <typeparam name="TVariable">The representation of variable passed througout the resolve steps.</typeparam>
    public abstract class AlgebraicResolver<TVariable> : AlgebraicResolver
    {
        [ThreadStatic] private static Stage? _stage;

        /// <summary>
        /// Resolves the expression into a variable, or fails. 
        /// </summary>
        /// <param name="expression">The desired expression to resolve.</param>
        /// <returns>The resolved variable.</returns>
        /// <exception cref="ArgumentException">On resolve failure.</exception>
        public TVariable Resolve(ReadOnlySpan<char> expression) =>
            TryResolve(expression, out var result, out var error) ? result : throw new ArgumentException(error, nameof(expression));

        /// <summary>
        /// Attempts to resolve the provided expression into a variable
        /// </summary>
        /// <param name="expression">The desired expression to resolve.</param>
        /// <param name="result">The resolved variable</param>
        /// <returns>Whether or not the resolve was successful</returns>
        public bool TryResolve(ReadOnlySpan<char> expression, [NotNullWhen(true)] out TVariable? result) =>
            TryResolve(expression, out result, out _);

        /// <summary>
        /// Attempts to resolve the provided expression into a variable providing the error message on failure.
        /// </summary>
        /// <param name="expression">The desired expression to resolve</param>
        /// <param name="result">The resolved variable</param>
        /// <param name="error">The description of why the resolve failed.</param>
        /// <returns>Whether or not the resolve was successful</returns>
        public bool TryResolve(ReadOnlySpan<char> expression, [NotNullWhen(true)] out TVariable? result, [NotNullWhen(false)] out string? error)
        {
            var seeks = (_seeks ??= _preseeks.ToArray()).AsSpan();
            var stage = _stage ??= new Stage();

            try
            {
                return TryResolveCore(stage.Variables, stage.Operators, stage.Buffer, seeks, expression, out result, out error);
            }
            catch (Exception ex)
            {
                result = default;
                error = $"Unhandled exception: {ex.Message}";
                return false;
            }
            finally
            {
                stage.Reset();
            }
        }

#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private bool TryResolveCore(Stack<TVariable> variables, Stack<(OperatorInfo Operator, int Position)> operators, TVariable[] buffer, Span<char> seeks,
            ReadOnlySpan<char> expression, [NotNullWhen(true)] out TVariable? result, [NotNullWhen(false)] out string? error)
        {
            result = default; (OperatorInfo Operator, int Position) sOp;
            var preunaries = _preunaries; var unaries = _unaries;
            var binaries = _binaries; var functions = _functions;

            for (var i = 0; i < expression.Length; i++)
            {
                var token = expression[i];
                switch (token)
                {
                    case ' ': continue;
                    case '(':
                        operators.Push((OperatorInfo.Group, i));
                        break;
                    case ',':
                    case ')':
                        var check = expression.Slice(0, i).TrimEnd();
                        if (check.Length > 0 && (check[check.Length - 1] == ',' || (token == ',' && expression[check.Length - 1] == '(')))
                            return Error($"{expression.ToString()}: Unexpected comma at position {i}", out error);
                        while (operators.TryPop(out sOp) && !OperatorInfo.IsGroup(sOp.Operator))
                            if (!TryInvoke(variables, buffer, sOp.Operator, sOp.Position, expression, out error))
                                return false;
                        if (sOp.Operator is null || !OperatorInfo.IsGroup(sOp.Operator))
                            return Error($"{expression.ToString()}: No matching left parenthesis or comma at position {i}", out error);
                        if (token == ',')
                            operators.Push((OperatorInfo.Group, i));
                        break;
                    default:
                        var prespan = expression.Slice(0, i).Trim();
                        var isUnary = prespan.Length == 0 || preunaries.Contains(prespan[prespan.Length - 1]);
                        var op = isUnary ? unaries[token] : binaries[token];
                        if (op is not null)
                        {
                            while (operators.TryPeek(out var stackOp))
                            {
                                var c = op.Precedence - stackOp.Operator.Precedence;
                                if (c >= 0 && (op.RightAssociative || c != 0))
                                    break;
                                sOp = operators.Pop(); 
                                if (!TryInvoke(variables, buffer, sOp.Operator, sOp.Position, expression, out error))
                                    return false;
                            }
                            operators.Push((op, i));
                            break;
                        }

                        var postspan = expression.Slice(i).Trim();
                        var length = postspan.IndexOfAny(seeks);

                        if (length < 0) length = postspan.Length;
                        else //Peek past operator to support 1e+7, 1e-7, etc. 
                        {
                            var o = length + 1;
                            var oi = postspan.Slice(o).IndexOfAny(seeks);
                            var l = oi > 0 ? o + oi : postspan.Length;
                            if (TryParseDouble(expression.Slice(i, l), out var ov) && TryCreateVariable(ov, out var or))
                            {
                                variables.Push(or);
                                i += l - 1;
                                break;
                            }
                        }

                        var subexpression = expression.Slice(i, length).Trim();
                        if (subexpression.Length == 0)
                            return Error($"Unrecognized operator at {i}", out error);

                        if (TryParseDouble(subexpression, out var v) && TryCreateVariable(v, out var variable))
                            variables.Push(variable);
                        else if ((op = SpanKey.Use(MemoryMarshal.AsBytes(subexpression), functions, (f, sk) => f.TryGetValue(sk, out var op) ? op : null)) is not null)
                            operators.Push((op, i));
                        else if (TryCreateVariable(subexpression, out variable))
                            variables.Push(variable);
                        else
                            return Error($"{expression.ToString()}: {GetExpressionError(subexpression, i)}", out error);

                        i += length - 1;
                        break;
                }
            }

            while (operators.TryPop(out sOp))
            {
                if (OperatorInfo.IsGroup(sOp.Operator))
                    return Error($"{expression.ToString()}: Missing right parenthesis", out error);
                if (!TryInvoke(variables, buffer, sOp.Operator, sOp.Position, expression, out error))
                    return false;
            }

            if (variables.Count == 0)
                return Error($"{expression.ToString()}: No variables found", out error);

            if (variables.Count != 1)
                return Error($"{expression.ToString()}: Variables remain on stack, this may indicate missing operators or incorrect variables provided to functions.", out error);

            result = variables.Pop()!;
            error = default;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryInvoke(Stack<TVariable> variables, TVariable[] buffer, OperatorInfo op, int position, ReadOnlySpan<char> expression,
            [NotNullWhen(false)] out string? error)
        {
            TVariable? result;
            switch (op.Token)
            {
                case string t:
                    if (!TryCountVariables(expression, position, out var count, out error))
                        return false;
                    if (op.VariableCount.HasValue && op.VariableCount.Value != count)
                        return Error($"Function {t} expects {op.VariableCount} variables, but {count} were provided.", out error);
                    var inputs = buffer.AsSpan(0, count);
                    for (var i = 0; i < count; i++)
                    {
                        var j = count - 1 - i;
                        if (!variables.TryPop(out var variable))
                            return Error(MissingOperand(op.Token, j, position), out error);
                        inputs[j] = variable;
                    }
                    if (!TryInvokeFunction(t, inputs, out result, out error))
                        return false;
                    break;
                case char t when op.VariableCount == 2:
                    if (!variables.TryPop(out var y))
                        return Error(MissingOperand(op.Token, 0, position), out error);
                    if (!variables.TryPop(out var x))
                        return Error(MissingOperand(op.Token, 0, position), out error);
                    if (!TryInvokeBinary(t, in x, in y, out result, out error))
                        return false;
                    break;
                case char t:
                    if (!variables.TryPop(out x))
                        return Error(MissingOperand(op.Token, 0, position), out error);
                    if (!TryInvokeUnary(t, in x, out result, out error))
                        return false;
                    break;
                default: throw new NotSupportedException($"Unknown token '{op.Token}' at position during invocation.");
            }

            variables.Push(result);
            error = default;
            return true;
        }

        /// <summary>
        /// The underlying invoke method for registered unary operators.
        /// </summary>
        /// <param name="token">The token that represents the unary operator.</param>
        /// <param name="x">The variable to apply the unary operation to.</param>
        /// <param name="result">The resultant variable.</param>
        /// <param name="error">The error message on failure.</param>
        /// <returns>Whether or not the method succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract bool TryInvokeUnary(char token, in TVariable x, [NotNullWhen(true)] out TVariable? result, [NotNullWhen(false)] out string? error);

        /// <summary>
        /// The underlying invoke method for registered binary operators.
        /// </summary>
        /// <param name="token">The token that represents the binary operator.</param>
        /// <param name="x">The left hand side of the binary operation.</param>
        /// <param name="y">The right hand side of the binary operation.</param>
        /// <param name="result">The resultant variable.</param>
        /// <param name="error">The error message on failure.</param>
        /// <returns>Whether or not the method succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract bool TryInvokeBinary(char token, in TVariable x, in TVariable y, [NotNullWhen(true)] out TVariable? result, [NotNullWhen(false)] out string? error);

        /// <summary>
        /// The underlying invoke method for registered functions.
        /// </summary>
        /// <param name="token">The token that represents the function.</param>
        /// <param name="args">The args of the function.</param>
        /// <param name="result">The resultant variable. </param>
        /// <param name="error">The error message on failure.</param>
        /// <returns>Whether or not the method succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract bool TryInvokeFunction(string token, ReadOnlySpan<TVariable> args, [NotNullWhen(true)] out TVariable? result, [NotNullWhen(false)] out string? error);

        /// <summary>
        /// Attempts to create a variable from a constant.
        /// </summary>
        /// <param name="constant">The constant to convert to a variable.</param>
        /// <param name="variable">The resultant variable.</param>
        /// <returns>Whether or not the method succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract bool TryCreateVariable(double constant, [NotNullWhen(true)] out TVariable? variable);

        /// <summary>
        /// Attempts to create a variable from the expression.
        /// </summary>
        /// <param name="expression">The expression to convert.</param>
        /// <param name="variable">The resultant variable.</param>
        /// <returns>Whether or not the method suceeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract bool TryCreateVariable(ReadOnlySpan<char> expression, [NotNullWhen(true)] out TVariable? variable);

        /// <summary>
        /// Provides a more detailed reason when the expression fails to be resolved. 
        /// </summary>
        /// <param name="expression">The unsupported expression.</param>
        /// <param name="position">The location where the expression was encountered.</param>
        /// <returns>The detailed reason.</returns>
        protected virtual string GetExpressionError(ReadOnlySpan<char> expression, int position) =>
            $"Unexpected token '{expression.ToString()}' at position {position}";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCountVariables(ReadOnlySpan<char> expression, int position, out int count, [NotNullWhen(false)] out string? error)
        {
            expression = expression.Slice(position);
            
            var start = expression.IndexOf('(') + 1;
            var length = expression.Length;

            int x = 0; count = 1;
            if (start > 0)
                for (var i = start; i < length; i++)
                    switch (expression[i])
                    {
                        case ',' when x == 0: count++; break;
                        case '(': x++; break;
                        case ')' when x > 0: x--; break;
                        case ')':
                            error = default;
                            return true;
                    }

            error = $"Missing right parenthesis for left parenthesis for function at {position}";
            return false;
        }

        private sealed class Stage
        {
            public Stack<(OperatorInfo Operator, int Position)> Operators = new(StacksCapacity);
            public Stack<TVariable> Variables = new(StacksCapacity);
            public TVariable[] Buffer = new TVariable[VariablesCapacity];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                if (Operators.Count > StacksCapacity)
                    Operators = new(StacksCapacity);
                else Operators.Clear();

                if (Variables.Count > StacksCapacity)
                    Variables = new(StacksCapacity);
                else Variables.Clear();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                if (RuntimeHelpers.IsReferenceOrContainsReferences<TVariable>())
#endif
                    Array.Clear(Buffer, 0, Buffer.Length);
            }
        }
    }
}
