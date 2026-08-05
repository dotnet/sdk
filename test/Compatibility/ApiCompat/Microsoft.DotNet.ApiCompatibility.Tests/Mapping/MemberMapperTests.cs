// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Moq;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Mapping
{
    [TestClass]
    public class MemberMapperTests
    {
        [TestMethod]
        public void MemberMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;
            ITypeMapper containingType = Mock.Of<ITypeMapper>();

            MemberMapper memberMapper = new(ruleRunner, mapperSettings, rightSetSize, containingType);

            Assert.IsNull(memberMapper.Left);
            Assert.AreEqual(mapperSettings, memberMapper.Settings);
            Assert.HasCount(rightSetSize, memberMapper.Right);
            Assert.AreEqual(containingType, memberMapper.ContainingType);
        }
    }
}
