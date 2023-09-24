/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Measure;

namespace JohnsonControls.Numerics.Tests
{
    [TestClass]
    public class ExtensionsTests
    {
        [TestMethod]
        public void ConvertToHappyPath()
        {
            Assert.AreEqual(0, (32, "degF").ConvertTo("degC"), 1E-9);
            Assert.AreEqual(32000, (32d, "s").ConvertTo("ms"), 1E-9);
            Assert.ThrowsException<ArgumentException>(() => (32, "degF").ConvertTo("ms"));
            Assert.IsTrue((32, "degF").TryConvertTo("degC", out var result));
            Assert.AreEqual(0, result, 1E-9);
            Assert.IsTrue((32d, "s").TryConvertTo("ms", out result));
            Assert.AreEqual(32000, result, 1E-9);
            Assert.IsFalse((32, "degF").TryConvertTo("ms", out result));
        }

        [TestMethod]
        public void ConvertToTimeSpanHappyPath()
        {
            var s = UnitOfMeasure.Create("32*s"); 
            Assert.AreEqual(TimeSpan.FromSeconds(32), s.ConvertToTimeSpan());
            Assert.IsTrue(s.TryConvertToTimeSpan(out var result));
            Assert.AreEqual(TimeSpan.FromSeconds(32), result);
            Assert.AreEqual(TimeSpan.FromSeconds(32).Ticks, s.ConvertToTicks());
            Assert.IsTrue(s.TryConvertToTicks(out var rt));
            Assert.AreEqual(TimeSpan.FromSeconds(32).Ticks, rt);

            var degF = UnitOfMeasure.Create("degF");
            Assert.ThrowsException<ArgumentException>(() => degF.ConvertToTimeSpan());
            Assert.IsFalse(degF.TryConvertToTimeSpan(out _));
            Assert.ThrowsException<ArgumentException>(() => degF.ConvertToTicks());
            Assert.IsFalse(degF.TryConvertToTicks(out _));
        }

        [TestMethod]
        public void XXHashTests()
        {
            var hash = "Hello hello";
            Assert.AreNotEqual(0, hash.AsSpan().GetXXHashCode());
            var bytes = new byte[100]; bytes[17] = 13; 
            Assert.AreNotEqual(0, ((ReadOnlySpan<byte>)bytes.AsSpan()).GetXXHashCode());
        }

        [TestMethod]
        public void LevenshteinDistanceTests()
        {
            var x0 = "Hello";
            var x1 = "Dello";
            var x2 = "Chicken";

            Assert.IsTrue(x0.LevenshteinDistance(x1.AsSpan()) < x0.LevenshteinDistance(x2.AsSpan()));
        }
    }
}
