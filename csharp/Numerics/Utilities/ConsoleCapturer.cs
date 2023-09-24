/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using System;
using System.IO;

namespace JohnsonControls.Numerics.Utilities
{
    /// <summary>
    /// A simple mechanism to capture console output for testing
    /// </summary>
    public sealed class ConsoleCapturer
    {
        /// <summary>
        /// The captured console output from the most recently disposed <see cref="Session"/>"/>
        /// </summary>
        public string? Value { get; private set; }

        /// <summary>
        /// Start capturing console output, dispose <see cref="Session"/> to stop capturing
        /// </summary>
        /// <returns>The session that must be diposed to stop capturing</returns>
        public Session Capture() => new(this);

        /// <summary>
        /// The console capture session
        /// </summary>
        public readonly struct Session : IDisposable
        {
            private readonly TextWriter _original;
            private readonly StringWriter _writer;
            private readonly ConsoleCapturer _owner;

            internal Session(ConsoleCapturer owner)
            {
                _original = Console.Out;
                Console.SetOut(_writer = new StringWriter());
                _owner = owner;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                Console.SetOut(_original);
                _writer.Flush();

                _owner.Value = _writer.ToString();
                _writer.Dispose();
            }
        }
    }
}
