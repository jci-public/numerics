/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Algebra;
using System.Diagnostics.CodeAnalysis;

namespace JohnsonControls.Numerics.Tests.Algebra
{
    [TestClass]
    public class AlgebraicResolverTests
    {
        [TestMethod]
        public void HappyPath()
        {
            //NOTE: Much of resolver is tested via the UnitOfMeasureResolverTests, other edge cases are handled here. 
            var resolver = new BasicResolver();
            Assert.AreEqual(1, resolver.Resolve("add1(1)".AsSpan()));
            Assert.AreEqual(3, resolver.Resolve("add2(1, 2)".AsSpan()));
            Assert.AreEqual(6, resolver.Resolve("add3(1, 2, 3)".AsSpan()));
            Assert.AreEqual(1, resolver.Resolve("add(1.0)".AsSpan()));
            Assert.AreEqual(3, resolver.Resolve("add(1.0, 2.0)".AsSpan()));
            Assert.AreEqual(6, resolver.Resolve("add(1, 2.0, 3)".AsSpan()));
            Assert.AreEqual(10, resolver.Resolve("add(1, 2.0, 3, 4.0)".AsSpan()));
        }

        private sealed class BasicResolver : AlgebraicResolver<double>
        {
            public BasicResolver()
            {
                AddFunction("add1", 1);
                AddFunction("add2", 2);
                AddFunction("add3", 3);
                AddFunction("add"); 
                //AddFunction("add"); //TODO: Add support for N-Length do it as add(1) .. 2, 3, 4, 5 ...
            }

            protected override bool TryCreateVariable(double constant, [NotNullWhen(true)] out double variable)
            {
                variable = constant;
                return true;
            }

            protected override bool TryCreateVariable(ReadOnlySpan<char> expression, [NotNullWhen(true)] out double variable)
            {
                variable = default;
                return false;
            }

            protected override bool TryInvokeBinary(char token, in double x, in double y, [NotNullWhen(true)] out double result, [NotNullWhen(false)] out string? error)
            {
                result = default;
                error = "Not Supported";
                return false;
            }

            protected override bool TryInvokeFunction(string token, ReadOnlySpan<double> args, [NotNullWhen(true)] out double result, [NotNullWhen(false)] out string? error)
            {
                error = "error";
                switch (token)
                {
                    case "add1":
                        Assert.AreEqual(1, args.Length);
                        result = args[0];
                        return true;
                    case "add2":
                        Assert.AreEqual(2, args.Length);
                        result = args[0] + args[1];
                        return true;
                    case "add3":
                        Assert.AreEqual(3, args.Length);
                        result = args[0] + args[1] + args[2];
                        return true;
                    case "add":
                        result = 0; 
                        for (var i = 0; i < args.Length; i++)
                            result += args[i];
                        return true;
                    default:
                        result = default;
                        return false;
                }
            }

            protected override bool TryInvokeUnary(char token, in double x, [NotNullWhen(true)] out double result, [NotNullWhen(false)] out string? error)
            {
                result = default;
                error = "Not Supported";
                return false;
            }
        }
    }
}
