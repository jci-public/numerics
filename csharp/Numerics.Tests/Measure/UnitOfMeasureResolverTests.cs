/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Measure;
namespace JohnsonControls.Numerics.Tests.Measure
{
    [TestClass]
    public class UnitOfMeasureResolverTests
    {
        private readonly UnitOfMeasure.Resolver _resolver = new(UnitOfMeasure.Options.Default);

        [TestMethod]
        public void HappyPath()
        {
            AssertExpression("m^1.6556", 1, 0, (0, 1655));
            AssertExpression("m^1.6556^2.1", 1, 0, (0, 2882));
            AssertExpression("minute + 1", 60, 1, (2, 1000));

            AssertExpressionFails("", "No variables found");
            AssertExpressionFails(" ", "No variables found");

            AssertExpression("pow( m, -2)", 1, 0, (0, -2000));
            AssertExpression("pow(m , 2) + 1", 1, 1, (0, 2000));
            AssertExpression("1 + pow (m, 2)", 1, 1, (0, 2000));
            AssertExpression("1 + pow(m, 1 + 1)", 1, 1, (0, 2000));
            AssertExpression("1 + pow(m, ( 1) + 1)", 1, 1, (0, 2000));
            AssertExpression("10E-1 + pow(m, (1 /2) + (1/2 ))", 1, 1, (0, 1000));
            AssertExpression("1E0 + pow(m, pow(2, 0.1e1 ))", 1, 1, (0, 2000));
            AssertExpression("1e+3 + pow(m, pow(2, 0.1e1 ))", 1, 1000, (0, 2000));
            AssertExpression("1.2345e+1", 12.345, 0);
            AssertExpression("none+1", 2, 0);
            AssertExpression("none+ 1", 2, 0);
            AssertExpression("none +1", 2, 0);

            AssertExpression("(m^ 2) + 1", 1, 1, (0, 2000));
            AssertExpression("( m^2) - 1", 1, -1, (0, 2000));
            AssertExpression(" m^2 + 1", 1, 1, (0, 2000));
            AssertExpression(" m^2 - 1 ", 1, -1, (0, 2000));
            AssertExpression(" 1 + m^2 ", 1, 1, (0, 2000));
            AssertExpression(" 1 - m ^   2 ", -1, 1, (0, 2000));

            AssertExpression(" 1.25 *  m ^ 0.15 + 17.333", 1.25, 17.333, (0, 150));
            AssertExpression("m + 1^2", 1, 1, (0, 1000));
            AssertExpression("m + 4^-2", 1, 0.0625, (0, 1000));
            AssertExpression("m + 4^ - 2 + 1", 1, 1.0625, (0, 1000));
            AssertExpression("m + 4^ (-2 + 1)", 1, 0.25, (0, 1000));

            AssertExpression("-(5)", -5, 0);
            AssertExpression("(- 5)", -5, 0);
            AssertExpression("-(m^2)", -1, 0, (0, 2000));
            AssertExpression("(-m^2)", 1, 0, (0, 2000));
            AssertExpression("-( m^2) + 1 ", -1, 1, (0, 2000));
            AssertExpression(" ( (-m^2 ) + 1) ", 1, 1, (0, 2000));
            AssertExpression("m^-1", 1, 0, (0, -1000));
            AssertExpression(" m^ - 1 - 1", 1, -1, (0, -1000));

            AssertExpressionFails("deg C^2", "Unrecognized unit expression");
            AssertExpressionFails("degC^2", "Units with offsets (e.g. degC, degF) cannot be raised to a power");
            AssertExpression("m", 1, 0, (0, 1000));
            AssertExpression("1.25 * m ^   0.15", 1.25, 0, (0, 150));
            AssertExpressionFails("m  ^ kg", "Units can only be raised to a unitless power");
            AssertExpressionFails("(2  * m + 2)^2", "Units with offsets (e.g. degC, degF) cannot be raised to a power");
            AssertExpressionFails("(m +    1)^2", "Units with offsets (e.g. degC, degF) cannot be raised to a power");
            AssertExpressionFails("((  m )", "Missing right parenthesis");
            AssertExpressionFails("(m) )", "No matching left parenthesis");
            AssertExpressionFails(")m  (", "No matching left parenthesis");
            AssertExpressionFails("m ( s )", "Variables remain on stack");
            AssertExpressionFails("( m ) s", "Variables remain on stack");
            AssertExpressionFails("( ) m ( ) s", "Variables remain on stack");
            AssertExpression("1.25 * m ^ 2 * s ^ - 1", 1.25, 0, (0, 2000), (2, -1000));
            AssertExpressionFails("degF * s", "Units with offsets (e.g. degC, degF) should be converted to base (e.g. degK)");

            AssertExpressionFails("m + s", "Units must be commensurable to be added");
            AssertExpressionFails("m - s", "Units must be commensurable to be subtracted");

            AssertExpression("m - 100 *cm", 0, 0);
            AssertExpression("m - 10* cm", 0.9, 0, (0, 1000));
            AssertExpression("-m + m", 0, 0);
            AssertExpression("(m*s)/ (kg*s)", 1, 0, (0, 1000), (1, -1000));
            AssertExpressionFails("(m*s)(/kg*s)", "Unrecognized operator");
            AssertExpression("(( (m)*(m))*((m)*(m)))", 1, 0, (0, 4000));
            AssertExpressionFails("10m^(1 /3)", "Unrecognized unit expression");
            AssertExpression(" 10*m^(1/3)", 10, 0, (0, 333));
            AssertExpression("m ^2/s *kg /K*s", 1, 0, (0, 2000), (1, 1000), (4, -1000));
            AssertExpression("(((m^2 )/( s*kg ))*( s)) /(m)", 1, 0, (0, 1000), (1, -1000));
            AssertExpressionFails("2 ** 3", "Unrecognized operator");
            AssertExpression("2 * 3", 6, 0);
            AssertExpression("2 -- 3", 5, 0);
            AssertExpression("2 --- 3", -1, 0);
            AssertExpression("2 - -(-3)", -1, 0);
            AssertExpressionFails(" ,", "No matching left parenthesis or comma");
            AssertExpressionFails(", ,", "No matching left parenthesis or comma");
            AssertExpressionFails("pow (2,,3) ", "Unexpected comma");
            AssertExpressionFails("pow(2,3, )", "Unexpected comma");
            AssertExpressionFails(" pow(,2,3 )", "Unexpected comma");
        }

        private void AssertExpressionFails(string expression, string contains)
        {
            Assert.IsFalse(_resolver.TryResolve(expression.AsSpan(), out _, out var error));
            Assert.IsTrue(error.Contains(contains), error);
        }

        private void AssertExpression(string expression, double factor, double offset, params (int Index, short Value)[] exponents)
        {
            Assert.IsTrue(_resolver.TryResolve(expression.AsSpan(), out var result, out var error), error);
            Assert.IsNull(error);
            Assert.AreEqual(factor, result.Factor, 1E-6);
            Assert.AreEqual(offset, result.Offset, 1E-6);

            var actualExponents = result.Exponents;
            var expectedExponents = exponents.ToDictionary(e => e.Index, e => e.Value);
            for (var i = 0; i < result.Exponents.Length; i++)
            {
                if (expectedExponents.TryGetValue(i, out var expected))
                    Assert.AreEqual(expected, actualExponents[i], $"At index {i}");
                else
                    Assert.AreEqual(0, actualExponents[i], $"At index {i}");
            }
        }
    }
}
