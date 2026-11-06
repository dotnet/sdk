// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    [TestClass]
    public class BlockingMemoryStreamTests
    {
        /// <summary>
        /// Tests reading a bigger buffer than what is available.
        /// </summary>
        [TestMethod]
        public void ReadBiggerBuffer()
        {
            using (var stream = new BlockingMemoryStream())
            {
                stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

                byte[] buffer = new byte[10];
                int count = stream.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(3, count);
                Assert.AreEqual(1, buffer[0]);
                Assert.AreEqual(2, buffer[1]);
                Assert.AreEqual(3, buffer[2]);
            }
        }

        /// <summary>
        /// Tests reading smaller buffers than what is available.
        /// </summary>
        [TestMethod]
        public void ReadSmallerBuffers()
        {
            using (var stream = new BlockingMemoryStream())
            {
                stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                stream.Write(new byte[] { 5, 6, 7, 8, 9 }, 0, 5);

                byte[] buffer = new byte[3];

                int count = stream.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(3, count);
                Assert.AreEqual(1, buffer[0]);
                Assert.AreEqual(2, buffer[1]);
                Assert.AreEqual(3, buffer[2]);

                count = stream.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(1, count);
                Assert.AreEqual(4, buffer[0]);

                count = stream.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(3, count);
                Assert.AreEqual(5, buffer[0]);
                Assert.AreEqual(6, buffer[1]);
                Assert.AreEqual(7, buffer[2]);

                count = stream.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(2, count);
                Assert.AreEqual(8, buffer[0]);
                Assert.AreEqual(9, buffer[1]);
            }
        }

        /// <summary>
        /// Tests reading will block until the stream is written to.
        /// </summary>
        [TestMethod]
        public void TestReadBlocksUntilWrite()
        {
            using (var stream = new BlockingMemoryStream())
            {
                ManualResetEvent readerThreadExecuting = new(false);
                bool readerThreadSuccessful = false;

                Thread readerThread = new(() =>
                {
                    byte[] buffer = new byte[10];
                    readerThreadExecuting.Set();
                    int count = stream.Read(buffer, 0, buffer.Length);

                    Assert.AreEqual(3, count);
                    Assert.AreEqual(1, buffer[0]);
                    Assert.AreEqual(2, buffer[1]);
                    Assert.AreEqual(3, buffer[2]);

                    readerThreadSuccessful = true;
                })
                {
                    IsBackground = true
                };
                readerThread.Start();

                // ensure the thread is executing
                readerThreadExecuting.WaitOne();

                Assert.IsTrue(readerThread.IsAlive);

                // give it a little while to ensure it is blocking
                Thread.Sleep(10);
                Assert.IsTrue(readerThread.IsAlive);

                stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

                Assert.IsTrue(readerThread.Join(1000));
                Assert.IsTrue(readerThreadSuccessful);
            }
        }
    }
}
