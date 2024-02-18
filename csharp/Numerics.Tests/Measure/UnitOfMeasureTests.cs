/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Measure;
using System.Text;
using System.Text.Json;

namespace JohnsonControls.Numerics.Tests.Measure
{
    [TestClass]
    public class UnitOfMeasureTests
    {
        private struct Thing
        {
            public UnitOfMeasure Unit { get; set; }
            public UnitOfMeasure? NullUnit { get; set; }
        }

        [TestMethod]
        public void BasicUsage()
        {
            //Different ways to create units
            var degF = UnitOfMeasure.Create("degF"); //Direct creation
            Assert.IsNotNull(degF); 

            ReadOnlySpan<char> fSpan = "degF".AsSpan(); //From a ReadOnlySpan<char> or utf8 ReadOnlySpan<byte>
            degF = UnitOfMeasure.Create(fSpan);
            Assert.IsNotNull(degF);

            degF = "degF"; //Implicitly from string
            Assert.IsNotNull(degF); 

            if (!UnitOfMeasure.TryCreate("degC", out var degC, out var error)) //Attempt to create for custom handling
                throw new Exception(error);

            Assert.IsFalse(UnitOfMeasure.TryCreate("degc", out _, out var degcError)); //Library attempts to guess incorrect spelling (units are naturally case sensitive)
            Assert.AreEqual("degc: Unrecognized unit expression 'degc' at position 0. Did you mean: degC, degF, degR, delC, deg, Eg, dag, dg, delF, EGy, daGy?", degcError); 

            //Different ways to convert units
            var (f, o) = degF.GetConversionTo(degC);
            Assert.AreEqual(0, 32 * f + o, 1E-9); // 32 degF = 0 degC

            if (!degF.TryGetConversionTo(degC, out f, out o))
                Assert.Fail(); 
            Assert.AreEqual(0, 32 * f + o, 1E-9);

            Assert.IsTrue(degF.IsConvertibleTo(degC));
            Assert.IsFalse(degF.IsConvertibleTo("meter")); //Implicit unit of measure conversion (meter)

            //Shortcuts
            Assert.AreEqual(0, (32, "degF").ConvertTo("degC"), 1E-9);

            //Compose your own units (simple garbage-free string combinations)
            var mSq = UnitOfMeasure.Create("m") * "m";
            Assert.AreEqual("m*m", mSq.ToString());
            Assert.IsTrue(mSq.IsConvertibleTo("m^2"));
            Assert.IsTrue(mSq.IsConvertibleTo("(m^(3/2))^(4/3)"));

            var m = mSq / "m";
            Assert.AreEqual("m*m/(m)", m.ToString());
            Assert.IsTrue(m.IsConvertibleTo("m"));

            var inch = UnitOfMeasure.Create("in");
            Assert.AreEqual(2d, (inch + inch).GetConversionTo("in").Factor);
            Assert.AreEqual(0d, (inch - inch).GetConversionTo("1").Factor); 
        }

        [TestMethod]
        public async Task HappyPath()
        {
            // Test that the cache is cleared when memory pressure is high
            var options = UnitOfMeasure.Options.Default;
            options.SlidingExpiration = TimeSpan.FromSeconds(0.5);
            options.HighMemoryPressureThreshold = 1;
            UnitOfMeasure.Configure(options);
            for (var i = 0; i < 100000; i++)
                UnitOfMeasure.Create($"{i} * degC");
            GC.Collect();

            // Test that the cache is cleared when the slide time expires
            options = UnitOfMeasure.Options.Default;
            options.SlidingExpiration = TimeSpan.FromSeconds(0.5);
            options.HighMemoryPressureThreshold = 100;
            UnitOfMeasure.Configure(options);
            for (var i = 0; i < 100000; i++)
                UnitOfMeasure.Create($"{i} * degC");

            GC.Collect();
            await Task.Delay(1000);
            GC.Collect();

            UnitOfMeasure x = "no clue";
            Assert.AreEqual(x, UnitOfMeasure.NaU);

            x = "in";

            var xb = x;
            x = "in";
            Assert.AreEqual(xb, x);

            xb = x;
            x = "in";
            Assert.AreEqual(xb, x);

            var t = new Thing { Unit = "in" };
            var tb = JsonSerializer.Deserialize<Thing>(JsonSerializer.Serialize(t));
            Assert.AreEqual(t.Unit.ToString(), tb.Unit.ToString());
            Assert.AreEqual(t.NullUnit, tb.NullUnit);

            var s = new string(Enumerable.Repeat('0', 255).ToArray());
            t = new Thing { Unit = $"1.0{s} * in" };
            tb = JsonSerializer.Deserialize<Thing>(JsonSerializer.Serialize(t));
            Assert.AreEqual(t.Unit.ToString(), tb.Unit.ToString());

            UnitOfMeasure.Configure(UnitOfMeasure.Options.Default);
        }

        [TestMethod]
        public void HappyConversions()
        {
            UnitOfMeasure x = "in"; x = "in"; x = "in";
            Assert.ThrowsException<ArgumentException>(() => x.GetConversionTo("s"));

            AssertConversion(x, "ft", 1 / 12d, 0);
            AssertConversion(x, "mm", 25.4, 0);
            AssertConversion(x, null, 1, 0);

            x = UnitOfMeasure.Create(Encoding.UTF8.GetBytes("in"));
            AssertConversion(x, "yard", 0.02777778, 0);

            AssertConvertible("ft^2", "10*m^2 + 10");
            AssertConvertible("J/s", "W");
            AssertConvertible("m^1.333", "m^(4/3)");
            AssertConvertible("m^1.33", "m^(4/3)");
            AssertConvertible("mvolt", "millivolt"); 
            Assert.ThrowsException<ArgumentException>(() => AssertConvertible("m^1.3", "m^(4/3)"));
            AssertConvertible("2 * m^1.33", "7.8 * m^(4/3)");
        }

        private static void AssertConversion(UnitOfMeasure x, UnitOfMeasure? y, double factor, double offset)
        {
            var (f, o) = x.GetConversionTo(y);
            Assert.AreEqual(factor, f, 1E-6);
            Assert.AreEqual(offset, o, 1E-6);
        }

        private static void AssertConvertible(UnitOfMeasure x, UnitOfMeasure y)
        {
            var (ft, ot) = x.GetConversionTo(y);
            var (ff, of) = y.GetConversionTo(x);
            Assert.AreEqual(1, ft * ff, 1E-6);
            Assert.AreEqual(0, ft * of + ot, 1E-6);
        }
    }
}