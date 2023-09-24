/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using JohnsonControls.Numerics.Utilities;

namespace JohnsonControls.Numerics.Tests.Utilities
{
    [TestClass]
    public class ConsoleCapturerTests
    {
        [TestMethod]
        public void HappyPath()
        {
            var capturer = new ConsoleCapturer();
            using (capturer.Capture())
                Console.WriteLine("Hello, world!");
            Assert.AreEqual("Hello, world!\r\n", capturer.Value);

            using (capturer.Capture())
            {
                Console.WriteLine("Hello, world again!");
                Console.WriteLine(".. and again!");
            }
            Assert.AreEqual("Hello, world again!\r\n.. and again!\r\n", capturer.Value);
        }
    }
}
