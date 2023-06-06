# Table of contents

- [Introduction](#introduction)
- [Known Issues](#known-issues)
- [Language Source Files](#language-source-files)
  - [Samples](#samples)
  - [Ignore conditions expressions in language files](#ignore-conditions-expressions-in-language-files)
  - [Related](#related)
- [JSON Files](#json-files)
  - [Samples](#samples)
  - [Related](#related)
- [XML Files](#xml-files)
  - [Samples](#samples)
  - [Related](#related)
- [MSBuild Files](#msbuild-files)
  - [Samples](#samples)
  - [Ignore conditions expressions in MSBuild files](#ignore-conditions-expressions-in-msbuild-files)
  - [Related](#related)
- [Single hash line comments](#single-hash-line-comments)
  - [Samples](#samples)
  - [Related](#related)
- [CSS Files](#css-files)
  - [Samples](#samples)
  - [Related](#related)
- [Command Files](#command-files)
  - [Samples](#samples)
  - [Related](#related)
- [Razor Views](#razor-views)
  - [Samples](#samples)
  - [Related](#related)
- [Haml Files](#haml-files)
  - [Samples](#samples)
  - [Related](#related)
- [Jsx Files](#jsx-files)
  - [Samples](#samples)
  - [Related](#related)
- [Other File Types](#other-file-types)
  - [Samples](#samples)
  - [Related](#related)
  

## Introduction

To add conditional, or dynamic, content you can add Template Engine expressions in your source files. The conditional expression let you to include or exclude part of the file according to a specified condition, and to do this you can use the familiar conditional expressions like **#if**, **#else**, **#elseif**, **#endif**.

To learn more about conditional expressions evaluation go to [Conditions](Conditions.md) description.


| Name     | Description   |   
|----------|---------------|  
|[Language files](#language-source-files)| Common Dotnet language source files.|
|[JSON files](#json-files) | Common Json type files. |
|[XML files](#xml-files) | Common Xml and *tml type files. |
|[MSBuild files](#msbuild-files)| MSBuild project files.|
|[Single hash line comments](#single-hash-line-comments)| Common file types that use single hash line comment syntax.|
|[Css files](#css-files)| Css Files.|
|[Command files](#command-files)| Windows command Files.|
|[Razor Views](#razor-views)| Razor View files.|
|[Haml Files](#haml-files)| Haml files.|
|[JSX Files](#jsx-files)| Jsx and Tsx files.|   
|[Other File](#other-file-types)| Default rules for file type not in this list.|  

## Known Issues

| Name     | Description   | Incorrect Use   | Correct Use    |  
|----------|---------------|-----------------|----------------| 
|[Conditional statement overlaps with Replacement functionality](https://github.com/dotnet/templating/issues/6536)| There is a problem with using different types of conditional comments in replacement statements. In order to workaround it avoid using comments (e.g. "//") as a part of the candidate string for replacing.| "http://localhost:"| "localhost:"|

#### File Extensions
`.cs`, `.fs`,`.cpp`, `.h`, `.hpp`, `.cake`.

In these file types you can use a preprocessor directive. 
   
### Samples

In this sample, according to the value of the `IndividualB2CAuth` and `OrganizationalAuth` the appropriate service is added.
 
```
#if (IndividualB2CAuth)
  services.AddAzureAdB2CBearerAuthentication();
#elseif (OrganizationalAuth)
  services.AddAzureAdBearerAuthentication();
#endif

```

#### C# Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new method, and its use inside the main function.

```csharp
namespace MyProject.Con
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Hello World!");
#if( addMethod )
      HelloWordAgain();
#endif    
  }
    
#if( addMethod )
    static void HelloWordAgain() {
      Console.WriteLine("Hello World Again!");
    }
#endif    
  }
}
```

#### C++ Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new class, and its use inside the main function.

```cpp
#include "stdafx.h"
#include <string>
#include <iostream>

using namespace std;
#if( addMethod )

class HelloClass
{
public:
  HelloClass() {
  }
  ~HelloClass()
  {
  }

  void HelloWordAgain() {
    cout << "Hello World Again!";
  }
};
#endif

int main()
{
  cout << "Hello World!";

#if( addMethod )
  HelloClass *hello = new HelloClass();
  hello->HelloWordAgain();
#endif

  return 0;
}
```

#### F# Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new method, and its use inside the main function.

```fsharp
open System

#if( addMethod )

let helloWorldAgain =
  printfn "HelloAgain"
#endif    

[<EntryPoint>]
let main argv =
  printfn "Hello World from F#!"
#if( addMethod )
  helloWorldAgain
#endif    
  0 // return an integer exit code

```

#### File Extensions

`.vb`.

For Visual Basic file the expressions must be preceded by a `'` comment and conditional expressions are **#If**, **#ElseIf**, **#Else**, **#End If**. 

#### Visual Basic Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new method, and its use inside the main function.

```vb
Module Module1

  Sub Main()
    Console.WriteLine("Hello World!")
'#If( addMethod )    
    HelloWorldAgain()
'#End If
  End Sub

'#If( addMethod )    
  Sub HelloWorldAgain()
    Console.WriteLine("Hello World Again!")
  End Sub
'#End If
End Module
```

#### File Extensions

`.js`,`.ts`.

With these file types the expressions must be preceded by a `//` comment . 

#### JavaScript Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new method, and its use inside the main function.

```javascript
(function () {
  
//#if( addMethod )    
  function helloWorldAgain() {
    console.log("Hello World Again!");
  }
//#endif
  
  console.log("Hello World!");
//#if( addMethod )    
  helloWorldAgain();
//#endif
  
})();
```

#### TypeScript Sample

In this sample, using the parameter symbol `addMethod` we include the definition of a new method, and its use inside the constructor.

```typescript
class Student {

  constructor() {
  console.log("Hello World!");
//#if( addMethod )    
  this.helloWorldAgain();
//#endif
  }

//#if( addMethod )    
  public helloWorldAgain(): void {
  console.log("Hello World Again!");
  }
//#endif
}
```

### Ignore conditions expressions in language files
Template engine attempts to process all conditional statements in language files. In case conditions should not be processed by template engine, this should be explicitly specified.

There are two ways to do it:
- disable processing for the whole file
- disable processing for the part of the file

If the file should never be processed by template engine, it can be specified as `copyOnly` in the `sources` section of the `template.json`. For example:

```json
"sources": [
  {
    "modifiers": [
      {
        "copyOnly": [ "Directory.Build.props" ]
      }
    ]
  }
],
```

If template engine should not process only part of the file, but other parts should be processed, the conditional processing can be turned off for the section that should not be processed by using directives:

`//-:cnd:noEmit`

`//+:cnd:noEmit`

The part `//` is the prefix to use for turning operations on and off in language files. The name `cnd` is the name of the operation to turn on/off. The `-` and `+` near the beginning of these lines indicate that the operation should be turned off, and on, respectively. The `noEmit` part tells templating to not process this line.

For example:

```
#if DEBUG
Comet.Reload.Init();
#endif
//-:cnd:noEmit
#if DEBUG
Xamarin.Calabash.Start();
#endif
//+:cnd:noEmit
```
When invoking the template with the above content, the output looks like this:
```
#if DEBUG
Xamarin.Calabash.Start();
#endif
```
The first expression is not emitted because it is processed, and the condition evaluated to `false`. The second expression is copied as-is because the conditional processing is turned off for that part of the file. 

### Related
[C# Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.cs)  
[C++ Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.cpp)  
[F# Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.fs)  
[VB Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.vb)  
[JavaScript Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.js)  
[TypeScript Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.ts)  

## JSON Files

For .json files, the comment block starts with `//` or `////` to the end of the line. After this marker you can add the conditional expressions.
The choice between `//` or `////` is very important because let you choose between different actions to be executed when the condition is met: in the first case the content in the expression is simply rendered into the output files as is, while in the second a different action can be excuted, by default uncommenting removing an eventual leading `//`.

#### File Extensions

`.json`, `.jsonld`, `.hjson`, `.json5`, `.geojson`, `.topojson`, `.bowerrc`, `.npmrc`, `.job`, `.postcssrc`, `.babelrc`, `.csslintrc`, `.eslintrc`, `.jade-lintrc`, `.pug-lintrc`, `.jshintrc`, `.stylelintrc`, `.yarnrc`.  

### Samples

In this sample, we see the difference between the two ways to define conditional expression.
If the initial `#if` condition is preceded by `//` so, if `param1 == true`, rows below are copied as is.  
second condition `#elseif`, is preceded by `////`, so if `param1 == false` and `param2 == true`, rows below will be copied after the leading `//` has been removed, resulting in

```jsonc
// comment related to the 'elseif' content
content for when param2 is true and param1 is false
```

the latest condition, `#else`, is preceded by `////`, so the comments will removed, resulting in 

```jsonc
// comment related to the 'else' content
content for when both param1 & param2 are false
```
Changing from `////#else` to `//#else` the result will be

```jsonc
//// comment related to the 'else' content
// content for when both param1 & param2 are false
```

```jsonc
//#if (param1)
  // comment related to the 'if' content
  default content // also appropriate if param1 is true
////#elseif (param2)
  //// comment related to the 'elseif' content
  //content for when param2 is true and param1 is false
////#else
  //// comment related to the 'else' content
  // content for when both param1 & param2 are false
//#endif
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.json)

## XML Files

#### File Extensions

`.*htm`, `.*html`, `.jsp`, `.asp`, `.aspx`, `.nuspec`, `.xslt`, `.xsd`, `.vsixmanifest`, `.vsct`, `.storyboard`, `.axml`, `.plist`, `.xib`, `.strings`, `.xml`, `.xaml`, `.axaml`, `.md`, `.appxmanifest`.

#### Well known XML file names

`app.config`, `web.config`, `web.\*.config`, `packages.config`, `nuget.config`.

the comment block starts with `<!--` and ends with `-->`. Inside this block you can add your conditional expressions.   

### Samples

In this sample, conditional expression is inside the comment `<!--` and `-->` in the same line. According to the value of the `IndividualLocalAuth` and `UseLocalDB` symbol, an element is added.
 
```xml
<!--#if (IndividualLocalAuth && UseLocalDB) -->
  <SomeXmlHere>true</SomeXmlHere>
<!--#endif -->
```
 
In this sample, conditional expression is inside the comment `<!--` and `-->` in a block.

```xml
<!--#if (IndividualLocalAuth && UseLocalDB)
  <SomeXmlHere>true</SomeXmlHere>
#endif -->
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.xml)

## MSBuild Files

#### File Extensions

`.*proj`, `.proj.user`, `.msbuild`, `.targets`, `.props`

MSBuild files, in addition to the `#if`, `#else`, `#elseif`, `#endif` inside an xml type comment, a **Condition** expression could be added to an element.  
  
### Samples

In this sample, one **<TargetFramework>** element will be added according to the value of the **TargetFrameworkOverride** symbol. This syntax is more clear and let you to use a single conditional expression to rule the inclusion of a whole block of content inside the template

```xml
<TargetFramework Condition="'$(TargetFrameworkOverride)' == ''">netcoreapp2.0</TargetFramework>
<TargetFramework Condition="'$(TargetFrameworkOverride)' != ''">TargetFrameworkOverride</TargetFramework>
```

In this sample, we can see that if the **TargetFrameworkOverride** symbol is defined all the package references are added to the project file.

```xml
<ItemGroup Condition="'$(TargetFrameworkOverride)' == ''">
  <PackageReference Include="Microsoft.AspNetCore.All" Version="2.0.0-preview2-final" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="2.0.0-preview2-final" PrivateAssets="All" Condition="'$(IndividualAuth)' == 'True'" />
  <PackageReference Include="Microsoft.VisualStudio.Web.CodeGeneration.Design" Version="2.0.0-preview2-final" PrivateAssets="All" Condition="'$(IndividualAuth)' == 'True'" />
</ItemGroup>
```

In this snippet of MSBuild file, we see usage of conditional expression embeded inside a xml comment.

```xml
<!--#if (IndividualLocalAuth && UseLocalDB) -->
  <ItemGroup>
  <None Update="Company.WebApplication1.db" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
<!--#endif -->
```

### Ignore conditions expressions in MSBuild files

 Template engine attempts to process all conditional statements in MSBuild files. In case conditions should not be processed by template engine, this should be explicitly specified.

There are two ways to do it:
- disable processing for the whole file
- disable processing for the part of the file

If the file should never be processed by template engine, it can be specified as `copyOnly` in the `sources` section of the `template.json`. For example:

```json
  "sources": [
  {
    "modifiers": [
    {
      "copyOnly": [ "Directory.Build.props" ]
    }
    ]
  }
  ],
```

If template engine should not process only part of the file, but other parts should be processed, the conditional processing can be turned off for the section that should not be processed by using directives:

`<!--/-:msbuild-conditional:noEmit -->`

`<!--/+:msbuild-conditional:noEmit -->`

The part `<!--/` is the prefix to use for turning operations on and off in msbuild style files. The name `msbuild-conditional` is the name of the operation to turn on/off. The `-` and `+` near the beginning of these lines indicate that the operation should be turned off, and on, respectively. The `noEmit` part tells templating to not process this line.

For example, consider this modified version of `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
<!--/-:msbuild-conditional:noEmit -->
    <Foo Condition="'$(OS)' != 'Windows_NT'">Bar</Foo>
<!--/+:msbuild-conditional:noEmit -->
  </PropertyGroup>

  <PropertyGroup>
    <Foo Condition="'$(OS)' != 'Windows_NT'">Bar</Foo>
  </PropertyGroup>
</Project>
```
When invoking the template with the above content in `Directory.Build.props`, the output looks like this:
```
<Project>
  <PropertyGroup>
    <Foo Condition="'$(OS)' != 'Windows_NT'">Bar</Foo>
  </PropertyGroup>

  <PropertyGroup>
  </PropertyGroup>
</Project>
```
The first `<Foo Condition...` is copied as-is because the `msbuild-conditional` processing is turned off for that part of the file. But the second one is not emitted because it is processed, and the condition evaluated to `false`.

### Related

[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.csproj)

## Single hash line comments

#### File Extensions

`.sln`, `.yml`, `.yaml`, `.sh`, `.ps1`

#### Well-known file names

`.dockerignore`, `.gitignore`, `.gitattributes`, `.editorconfig`, `Dockerfile`, `nginx.conf`, `robots.txt`.

### Samples

For solution files, the comment block starts with # to the end of the line. After this marker you can add the conditional expressions.

```.gitignore
#if (symbol-name == "value")
stuff
#endif
```

In this sample, using the parameter `useSSL` add configuration for the 443 port inside nginx configuration

```
http {
  include  /etc/nginx/proxy.conf;
  limit_req_zone $binary_remote_addr zone=one:10m rate=5r/s;
  server_tokens off;

  sendfile on;
  keepalive_timeout 29; # Adjust to the lowest possible value that makes sense for your use case.
  client_body_timeout 10; client_header_timeout 10; send_timeout 10;

  upstream hellomvc{
    server localhost:5000;
  }

  server {
    listen *:80;
    add_header Strict-Transport-Security max-age=15768000;
    return 301 https://$host$request_uri;
  }

#if( useSSL )  
  server {
    listen *:443  ssl;
    server_name   example.com;
    ssl_certificate /etc/ssl/certs/testCert.crt;
    ssl_certificate_key /etc/ssl/certs/testCert.key;
    ssl_protocols TLSv1.1 TLSv1.2;
    ssl_prefer_server_ciphers on;
    ssl_ciphers "EECDH+AESGCM:EDH+AESGCM:AES256+EECDH:AES256+EDH";
    ssl_ecdh_curve secp384r1;
    ssl_session_cache shared:SSL:10m;
    ssl_session_tickets off;
    ssl_stapling on; #ensure your cert is capable
    ssl_stapling_verify on; #ensure your cert is capable

    add_header Strict-Transport-Security "max-age=63072000; includeSubdomains; preload";
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;

    #Redirects all traffic
    location / {
      proxy_pass  http://hellomvc;
      limit_req   zone=one burst=10;
    }
  }
#endif
}
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.yml)

## CSS Files

#### File Extensions

`.css`, `.css.min`

the comment block starts with `/*` and ends with `*/`. Inside this block you can add your conditional expressions.

### Samples
In this sample, according to the value of the `IndividualLocalAuth` symbol, a few classes are added to the css file.
 
```css
/*#if (IndividualLocalAuth)*/
/* buttons and links extension to use brackets: [ click me ] */
.btn-bracketed::before {
  display: inline-block;
  content: "[";
  padding-right: 0.5em;
}

.btn-bracketed::after {
  display: inline-block;
  content: "]";
  padding-left: 0.5em;
}

.dl-horizontal dt 
{
  white-space: normal;
}

/*#endif*/
```

### Related

[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.css)

## Command Files

The comment block starts with `rem ` to the end of line. After this marker you can add the conditional expressions.

#### File Extensions

`.bat`, `.cmd`

### Samples

In this sample, according to the value of the `enableVerbose` symbol, an environment variable is set to true.

```cmd
rem #if enableVerbose
set environment_verbose = true
rem #endif
```

### Related  
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.cmd)

## Razor Views

#### File Extensions

`.cshtml`

The comment block starts with `@*` and ends with `*@`. Inside this block you can add your conditional expressions

### Samples

In this sample, according to the value of the `IndividualLocalAuth` symbol, the using is added to an cshtml file.

```razor
@*#if (IndividualLocalAuth)
@using Microsoft.AspNetCore.Identity
#endif*@
```

In this sample, according to the value of the `IndividualB2CAuth` symbol, the inject statement is added to an cshtml file.

```razor
@*#if (IndividualB2CAuth)
@inject IOptions<AzureAdB2COptions> AzureAdB2COptions
#endif *@
```

### Related

[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.cshtml)

## Haml Files

#### File Extensions
`.haml`

The comment block starts with `-#` to the end of the line. After this marker you can add the conditional expressions. 

### Samples

In this sample, according to the value of the `addParagraph` symbol, a paragraph is added.

```haml
-##if addParagraph
  %p A new paragraph is added.
-##endif
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.haml)

## Jsx Files

#### File Extensions

`.jsx `,`.tsx`

The comment block starts with `{/*` and ends with `*/}`. Inside this block you can add your conditional expressions. 

### Samples

In this sample, according to the value of the `addParagraph` symbol, a paragraph is added.

```jsx
const myElement = (
  <div>
    {/*#if addParagraph
    <p>A new paragraph is added</p>
    #endif*/}
    <p>I am a paragraph.</p>
  </div>
  );
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.jsx)

## Other File Types

The file types that do not have special configuration predefined in template engine or `template.json` configuration will follow default settings.
The comment block starts with `//` to the end of the line. After this marker you can add the conditional expressions using `#if`, `#elseif`, `#endif`, `#else` directives.

### Samples

In this sample, according to the value of the boolean `param1` symbol, `option1` is added to the file.
```jsx
//#if (param1)
option1
//#endif
option2
```

### Related
[Sample](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.TestTemplates/test_templates/TemplateConditionalProcessing/Test.othertype)
