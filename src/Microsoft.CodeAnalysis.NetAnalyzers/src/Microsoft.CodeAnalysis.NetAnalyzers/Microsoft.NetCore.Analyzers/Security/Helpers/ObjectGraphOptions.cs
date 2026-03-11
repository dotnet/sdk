// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    /// <summary>
    /// Options for walking object graphs for <see cref="InsecureDeserializationTypeDecider"/>.
    /// </summary>
    internal class ObjectGraphOptions
    {
        public ObjectGraphOptions(
            bool recurse = false,
            bool binarySerialization = false,
            bool dataContractSerialization = false,
            bool xmlSerialization = false,
            bool javaScriptSerializer = false,
            bool newtonsoftJsonNetSerialization = false)
        {
            Recurse = recurse;
            BinarySerialization = binarySerialization;
            DataContractSerialization = dataContractSerialization;
            XmlSerialization = xmlSerialization;
            JavaScriptSerializer = javaScriptSerializer;
            NewtonsoftJsonNetSerialization = newtonsoftJsonNetSerialization;
        }

        /// <summary>
        /// Recurse into the types of fields and properties.
        /// </summary>
        public bool Recurse { get; private set; }

        /// <summary>
        /// "Binary" serialization, like <see cref="T:System.Runtime.Serialization.Binary.BinaryFormatter"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "Type not referenced by assembly.")]
        public bool BinarySerialization { get; private set; }

        /// <summary>
        /// DataContract serialization.
        /// </summary>
        public bool DataContractSerialization { get; private set; }

        /// <summary>
        /// .NET XML serialization with <see cref="T:System.Xml.Serialization.XmlSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "Type not referenced by assembly.")]
        public bool XmlSerialization { get; private set; }

        /// <summary>
        /// Serialization with <see cref="T:System.Web.Script.Serialization.JavaScriptSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "Type not referenced by assembly.")]
        public bool JavaScriptSerializer { get; private set; }

        /// <summary>
        /// Serialization with Newtonsoft Json.NET.
        /// </summary>
        public bool NewtonsoftJsonNetSerialization { get; private set; }

        /// <summary>
        /// Options for BinarySerialization and recursing into member types.
        /// </summary>
        public static ObjectGraphOptions BinarySerializationOptions = new()
        {
            Recurse = true,
            BinarySerialization = true,
        };

        /// <summary>
        /// Options for DataContract serialization and recursing into member types.
        /// </summary>
        public static ObjectGraphOptions DataContractOptions = new()
        {
            Recurse = true,
            DataContractSerialization = true,
        };

        /// <summary>
        /// Options for XML serialization (XmlSerializer) and recursing into member types.
        /// </summary>
        public static ObjectGraphOptions XmlSerializerOptions = new()
        {
            Recurse = true,
            XmlSerialization = true,
        };

        /// <summary>
        /// Options for JavaScriptSerializer serialization and recursing into member types.
        /// </summary>
        public static ObjectGraphOptions JavaScriptSerializerOptions = new()
        {
            Recurse = true,
            JavaScriptSerializer = true,
        };

        /// <summary>
        /// Options for Newtonsoft Json.NET and recursing into member types.
        /// </summary>
        public static ObjectGraphOptions NewtonsoftJsonNetOptions = new()
        {
            Recurse = true,
            NewtonsoftJsonNetSerialization = true,
        };

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ObjectGraphOptions);
        }

        public bool Equals(ObjectGraphOptions? other)
        {
            return other != null
                && this.Recurse == other.Recurse
                && this.BinarySerialization == other.BinarySerialization
                && this.DataContractSerialization == other.DataContractSerialization
                && this.JavaScriptSerializer == other.JavaScriptSerializer
                && this.NewtonsoftJsonNetSerialization == other.NewtonsoftJsonNetSerialization
                && this.XmlSerialization == other.XmlSerialization;
        }

        public override int GetHashCode()
        {
            return (this.Recurse ? 1 : 0)
                | (this.BinarySerialization ? 2 : 0)
                | (this.DataContractSerialization ? 4 : 0)
                | (this.JavaScriptSerializer ? 8 : 0)
                | (this.NewtonsoftJsonNetSerialization ? 16 : 0)
                | (this.XmlSerialization ? 32 : 0);
        }

        /// <summary>
        /// Determines if this instance is a valid argument (at least one type of serialization is specified).
        /// </summary>
        /// <param name="parameterName">Name of the ObjectGraphOptions parameter; used in the ArgumentException.</param>
        internal void ThrowIfInvalid(string parameterName)
        {
            if (this.BinarySerialization
                || this.DataContractSerialization
                || this.XmlSerialization
                || this.JavaScriptSerializer
                || this.NewtonsoftJsonNetSerialization)
            {
                return;
            }

            throw new ArgumentException("ObjectGraphOptions should specify at least one type serialization", parameterName);
        }
    }
}
