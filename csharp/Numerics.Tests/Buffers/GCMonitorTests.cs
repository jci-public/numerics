/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Buffers;
using JohnsonControls.Numerics.Utilities;

namespace JohnsonControls.Numerics.Tests.Buffers
{
    [TestClass]
    public class GCMonitorTests
    {
        private static int _a0x = 0;
        private static int _a1x = 0;
        private static int _a2x = 0;

        [TestMethod]
        public void HappyPath()
        {
            var cc = new ConsoleCapturer();
            using (cc.Capture())
            {
                GCMonitor.Gen2Collection += A0;
                Assert.AreEqual(0, _a0x);

                AssertCount(1, ref _a0x);
                AssertCount(2, ref _a0x);

                GCMonitor.Gen2Collection += A1; 
                Assert.AreEqual(0, _a1x);

                AssertCount(1, ref _a1x);
                Assert.AreEqual(3, _a0x);

                GCMonitor.Gen2Collection -= A1; 
                AssertCount(1, ref _a1x);
                Assert.AreEqual(4, _a0x);
            }

            GCMonitor.Gen2Collection -= A0;
            Assert.IsFalse(cc.Value!.Contains("GCAction failed with "));

            using (cc.Capture())
            {                
                GCMonitor.Gen2Collection += A2;
                Assert.AreEqual(0, _a2x);
                AssertCount(1, ref _a2x);
            }

            Assert.IsTrue(cc.Value.Contains("it broke"));
        }

        private static void A0(object? sender, Gen2CollectionArgs args)
        {
            _a0x++;
            AssertPercentLoad(args.MemoryPressure);
            Assert.IsNotNull(sender); 
        }

        private static void A1(object? sender, Gen2CollectionArgs args)
        {
            _a1x++; 
            AssertPercentLoad(args.MemoryPressure);
            Assert.IsNotNull(sender);
        }

        private static void A2(object? sender, Gen2CollectionArgs args)
        {
            _a2x++;
            AssertPercentLoad(args.MemoryPressure);
            Assert.IsNotNull(sender);
            throw new Exception("it broke");
        }

        private static void AssertCount(int expected, ref int actual)
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            Thread.Sleep(10); 
            Assert.AreEqual(expected, actual);
        }

        private static void AssertPercentLoad(int percentLoad)
        {
#if NET462
            Assert.AreEqual(0, percentLoad);
#else
            Assert.IsTrue(percentLoad > 0);
#endif
        }
    }
}
