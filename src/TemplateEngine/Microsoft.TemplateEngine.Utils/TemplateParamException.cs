// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.TemplateEngine.Utils
{
    public class TemplateParamException : Exception
    {
        public TemplateParamException()
        {
        }

        public TemplateParamException(string message)
            : base(message)
        {
        }

        public TemplateParamException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public TemplateParamException(string paramName, string inputValue, string dataType)
            : base(StandardizedMessage(paramName, inputValue, dataType))
        {
            ParamName = paramName;
            InputValue = inputValue;
            DataType = dataType;
        }

        public TemplateParamException(string message, string paramName, string inputValue, string dataType)
            : base(StandardizedMessage(paramName, inputValue, dataType, message))
        {
            ParamName = paramName;
            InputValue = inputValue;
            DataType = dataType;
        }

        public TemplateParamException(string message, Exception inner, string paramName, string inputValue, string dataType)
            : base(StandardizedMessage(paramName, inputValue, dataType, message), inner)
        {
            ParamName = paramName;
            InputValue = inputValue;
            DataType = dataType;
        }

        public string ParamName { get; private set; }

        public string InputValue { get; private set; }

        public string DataType { get; private set; }

        // Helper to create a standard, generic message including the detailed parm info.
        public static string StandardizedMessage(string paramName, string inputValue = null, string dataType = null, string baseMessage = null)
        {
            StringBuilder fullMessage = new StringBuilder(256);

            if (paramName == null)
            {
                // This shouldn't ever happen, it's defensive against having
                // the exception processing itself cause an exception to be thrown.
                fullMessage.AppendLine("Input param name was null");
                return fullMessage.ToString();
            }

            if (inputValue == null)
            {
                if (dataType == null)
                {
                    fullMessage.AppendFormat("Input param named [{0}] was null", paramName);
                }
                else
                {
                    fullMessage.AppendFormat("Input param named [{0}] was null but must be of data type [{1}]", paramName, dataType);
                }
            }
            else
            {
                if (dataType == null)
                {
                    fullMessage.AppendFormat("Input param named [{0}] was provided the invalid value = [{1}]", paramName, inputValue);
                }
                else
                {
                    fullMessage.AppendFormat("Input param named [{0}] must be of data type = [{1}] but was provided the invalid value = [{2}]", paramName, dataType, inputValue);
                }
            }

            if (baseMessage != null)
            {
                fullMessage.AppendFormat("\nAdditional information:\n{0}", baseMessage);
            }

            return fullMessage.ToString();
        }
    }
}
