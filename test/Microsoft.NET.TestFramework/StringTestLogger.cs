// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class StringTestLogger : ITestOutputHelper
    {
        StringBuilder _stringBuilder = new();

        public string Output => _stringBuilder.ToString();

        public void Write(string message)
        {
            _stringBuilder.Append(message);
        }

        public void Write(string format, params object[] args)
        {
            _stringBuilder.Append(string.Format(format, args));
        }

        public void WriteLine(string message)
        {
            _stringBuilder.AppendLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            _stringBuilder.AppendLine(string.Format(format, args));
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}
