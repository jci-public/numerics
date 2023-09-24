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