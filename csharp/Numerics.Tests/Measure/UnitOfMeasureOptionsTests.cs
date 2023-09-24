/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Measure;

namespace JohnsonControls.Numerics.Tests.Measure
{
    [TestClass]
    public class UnitOfMeasureOptionsTests
    {
        [TestMethod]
        public void HappyPath()
        {
            var options = UnitOfMeasure.Options.Default;
            Assert.IsTrue(options.Prefixes.ContainsKey("si"));
            Assert.AreEqual(1e24, options.Prefixes["si"]["Y"]);

            Assert.IsTrue(options.BaseUnits.Contains("m"));

            Assert.IsTrue(options.Units.ContainsKey("pcm,partperhundredthousand"));

            Assert.AreEqual(TimeSpan.FromMinutes(5), options.SlidingExpiration);

            Assert.AreEqual(90, options.HighMemoryPressureThreshold);

            Assert.AreEqual(50, options.HighMemoryPressureClearPercentage);
        }
    }
}
