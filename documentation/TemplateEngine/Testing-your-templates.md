As part of the dotnet/templating local and CI build processes, various template creation tests are run to ensure that content is properly generated. These tests are mostly run on the templates included in this repo, but tests can be setup for 3rd-party templates as long as the templates can be installed using `dotnet new --install <template pack identifier>`. The framework used to configure these tests makes it easy to setup tests to do any of the following:
* Install a template pack
* Instantiate an installed template with parameters specified
  * Check the created template output for:
    * Directory existence / lack of existence
    * File existence / lack of existence
    * File contains / does not contain a string
* Execute an arbitrary command
  * Check the exit code
  * Check the output (contains / does not contain)
* Make a web request (to test a running template)
  * Check the response
* Find a running process
* Kill a running process

Setting up tests for a template is accomplished by creating a .json file which configures the tests to be performed. The file should be located in this repo in the appropriate subdirectory under the path:

`tools/ProjectTestRunner/TestCases/3rdParty/`

If interested in contributing tests for your templates, please make a pull request with your test file(s) in the above location.

The format for the file is as follows:
```
{
  "install": "<template pack identifier>",
  "create": "<creation command including desired parameters>",
  "name": "<an identifier used in test output>",
  "skip": <a Boolean value, which if true, causes this test to not be run>,
  "tasks": [
     // an array of objects configuring what to test for. See below for details.
   ]  
}
```

## Configuring Test Tasks
The various tests available each have a unique name, called a "handler" in the task configuration. In addition to identifying the basic test type to run, the handler name/value pair is required in all test tasks. The other optional or required parameters are specific to the various tests, as explained below. In task configurations, all paths are assumed to be relative to the base directory of the created template (unless otherwise stated).

There are two variables which can be used in file or path names in the task configurations. They're for situations where template creation modifies the names of directories or files that need to be tested. The variables are:
* targetPath - The full path to the created template root. This isn't usually needed
* targetPathName - The name of the created template root directory. This value is also effectively the --name value when the template is created, unless the `create` configuration specifies a --name parameter (not recommended). 

These variables can be used in any task configuration string by putting a `%` character both before and after the variable name. Example:
`%targetPathName%.csproj`

### Check for Directory Existence
The handler for directory existence checking is named "directoryInspect', configured as:
```
{
  "handler": "directoryInspect",
  "directory": "<path relative to the created template root>",
  "assertion": "<exists | does_not_exist>"
}
```
Example:
```
    {
      "handler": "directoryInspect",
      "directory": "Areas",
      "assertion": "does_not_exist"
    },
```

### Checking File Existence & File Contents
The handler for these checks is named 'fileInspect', configured as:
```
{
  "handler": "fileInspect",
  "file": "<file name - path is relative to the created template root>"
  "expectations": [
    // An array of objects configuring the specific checks on the file.

    // File existence checks:
    {
      "assertion": "<exists | does_not_exist>"
    },

    // File content checks:
    {
      "assertion": "<contains | does_not_contain>"
      "text": "<the text to search for>"
    }
  ]
}
```
Example:
```
{
 "handler": "fileInspect",
 "file": "%targetPathName%.csproj",
 "expectations": [
    {
      "assertion": "exists"
    },
    {
      "assertion": "contains",
      "text": ".db"
    },
    {
      "assertion": "does_not_contain",
      "text": "Microsoft.EntityFrameworkCore.Tools"
    }
  ]
}
```

### Execute an arbitrary command
While any command can be executed by this configuration, in practice it usually only makes sense to run commands related to the template being tested, such as `dotnet restore`, `dotnet build`, or `dotnet exec <path to template dll>`.

Execute task configuration looks like:
```
{
  "name": "<give this task a name, which can be referenced by the taskKill task>",
  "handler": "execute",
  "command": "<the command to execute>",
  "args": "<a single string of additional args to the command>",
  "noExit": <true | false>, // If not specified, defaults to false
                            // true indicates that testing should block until the command exits. 
                            // false indicates that testing should continue. This is useful for starting a website which will be used by subsequent http request tasks.
  "exitTimeout": <The number of milliseconds to wait for the task to exit, default is 1000. Ignored if noExit = true>,
  "exitCode": <A number indicating the expected exit code. The test fails if the exit code is different.>,
  "expectations" [
     // An array of json objects to check the output of the executed command
     {
       "assertion": "<output_contains | output_does_not_contain>",
       "text": "<the string to check for>",
       "comparison": "<optional - if specified, must be a System.StringComparison enum value. Default is OrdinalIgnoreCase>"
     }
  ]
}
```
Example:
```
{
  "name": "RunApp",
  "handler": "execute",
  "command": "dotnet",
  "args": "exec bin/Debug/netcoreapp2.0/%targetPathName%.dll",
  "noExit": true,
  "exitTimeout": 5000
},
```

### Make a Web Request
After running a task to execute a web template, this can be used to make web requests against it, and verify the responses. The configuration for this task looks like:
```
{
  "handler": "httpRequest",
  "url": "<The URL to send the request to>",
  "statusCode": <The expected status code from the response>,
  "verb": "<The HttpMethod for the call, e.g.: Post, Get, Put, etc.>",
  "body": "<optional - The body of the http request>",
  "requestMediaType": "<optional - The HttpWebRequest.MediaType for the request>",
  "requestEncoding": "<optional - The character encoding for sending the request>",
  "expectations": [
    // An array of json object for checking the response to the request.
    // Supported assertions:
    {
      "assertion": "<Response_Contains | Response_Does_Not_Contain>",
      "text": "<The text expected to be (or not be) contained in the response>",
      "comparison": "<optional - if specified, must be a System.StringComparison enum value. Default is OrdinalIgnoreCase>"
    },
    {
      "assertion": "<Response_Header_Contains | Response_Header_Does_Not_Contain>",
      "text": "<The text expected to be (or not be) in a response headers>",
      "key": "<The response header key to check for the text>",
      "comparison": "<optional - if specified, must be a System.StringComparison enum value. Default is OrdinalIgnoreCase>"
    },
    {
      "assertion": "<Has_Header | Does_Not_Have_Header>",
      "key": "<The response header key to check for>",
    }
  ]
}
```
Example:
```
{
  "handler": "httpRequest",
  "url": "http://localhost:5000",
  "statusCode": 200,
  "verb": "GET"
},
```

### Find a Running Process
This task is used to check that a named process, invoked with specified args, is running. The test fails if the process is not found. The configuration for this task looks like:
```
{
  "handler": "find",
  "name": "<the name of the process being searched for>",
  "args": [
     // an array of strings representing args the command must have been invoked with.
  ]
}
```
Example:
```
{
  "handler": "find",
  "name": "dotnet",
  "args": [
    "new3",
    "mvc"
  ]
}
```
Note: The test will pass if the command was invoked with all the args specified in the task configuration, even when there are additional invocation args. The test args must merely be a subset of the actual args.

### Kill a Running Process
This task is used to kill a running process created by a previous Execute task in the same test configuration. The configuration for this task looks like:
```
{
  "handler": "taskKill",
  "name": "<The name value from a previous 'execute' task>"
}
```
Example:
```
{
  "handler": "taskkill",
  "name": "RunApp"
}
```