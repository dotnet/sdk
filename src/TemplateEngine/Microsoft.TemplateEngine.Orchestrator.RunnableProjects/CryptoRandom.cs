// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CryptoRandom : IDisposable
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private readonly byte[] _uint32Buffer = new byte[4];

        public CryptoRandom() { }

#pragma warning disable IDE0060 // Remove unused parameter
        public CryptoRandom(int ignoredSeed) { }
#pragma warning restore IDE0060 // Remove unused parameter

        public static int NextInt(int minValue, int maxValue)
        {
            using (CryptoRandom random = new CryptoRandom())
            {
                return random.Next(minValue, maxValue);
            }
        }

        public int Next()
        {
            _rng.GetBytes(_uint32Buffer);
            return BitConverter.ToInt32(_uint32Buffer, 0) & 0x7FFFFFFF;
        }

        public int Next(int maxValue)
        {
            if (maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }

            return Next(0, maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue));
            }

            if (minValue == maxValue)
            {
                return minValue;
            }

            long diff = maxValue - minValue;
            // We are avoiding remainder bias by discarding the remainder.
            //  background: https://ericlippert.com/2013/12/16/how-much-bias-is-introduced-by-the-remainder-technique/
            while (true)
            {
                _rng.GetBytes(_uint32Buffer);
                uint rand = BitConverter.ToUInt32(_uint32Buffer, 0);
                long max = 1 + (long)uint.MaxValue;
                long remainder = max % diff;
                if (rand < max - remainder)
                {
                    return (int)(minValue + (rand % diff));
                }
            }
        }

        public double NextDouble()
        {
            _rng.GetBytes(_uint32Buffer);
            uint rand = BitConverter.ToUInt32(_uint32Buffer, 0);
            return rand / (1.0 + uint.MaxValue);
        }

        public void NextBytes(byte[] buffer)
        {
            buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _rng.GetBytes(buffer);
        }

        public void Dispose()
        {
            _rng.Dispose();
        }
    }
}
