/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using System.Diagnostics;

namespace JohnsonControls.Numerics.Tests
{
    [TestClass]
    public class Setup
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext _)
        {
            Trace.Listeners.Add(new ConsoleWriter());
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {

        }
    }

    internal class ConsoleWriter : TraceListener
    {
        public override void Write(string? message)
        {
            Console.Write(message);
        }

        public override void WriteLine(string? message)
        {
            Console.WriteLine(message);
        }
    }
}
