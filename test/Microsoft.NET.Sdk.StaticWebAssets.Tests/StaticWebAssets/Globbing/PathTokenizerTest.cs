// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;


using System.Collections.Generic;


using System.Linq;


using System.Text;


using System.Threading.Tasks;


using Microsoft.VisualStudio.TestTools.UnitTesting;




namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Test;





[TestClass]

public class PathTokenizerTest


{


    [TestMethod]


    public void RootSeparator_ProducesEmptySegment()


    {


        var path = "/a/b/c";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("a", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_ProducesInitialSegment()


    {


        var path = "a/b/c";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("a", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_MatchesMultipleCharacters()


    {


        var path = "aa/b/c";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_HandlesConsecutivePathSeparators()


    {


        var path = "aa//b/c";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_HandlesFinalPathSeparator()


    {


        var path = "aa/b/c/";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_HandlesAlternativePathSeparators()


    {


        var path = "aa\\b\\c\\";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void NonRootSeparator_HandlesMixedPathSeparators()


    {


        var path = "aa/b\\c/";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void Ignores_EmpySegments()


    {


        var path = "aa//b//c";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void Ignores_DotSegments()


    {


        var path = "./aa/./b/./c/.";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }





    [TestMethod]


    public void Ignores_DotDotSegments()


    {


        var path = "../aa/../b/../c/..";


        var tokenizer = new PathTokenizer(path.AsMemory().Span);


        var segments = new List<PathTokenizer.Segment>();


        var collection = tokenizer.Fill(segments);


        Assert.AreEqual("aa", collection[0].ToString());


        Assert.AreEqual("b", collection[1].ToString());


        Assert.AreEqual("c", collection[2].ToString());


    }


}


