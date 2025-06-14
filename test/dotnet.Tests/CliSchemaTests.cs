// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests;

public class CliSchemaTests : SdkTest
{
    public CliSchemaTests(ITestOutputHelper log) : base(log)
    {
    }

    private static readonly string SolutionListJson = $$"""
{
  "name": "list",
  "version": "{{Product.Version}}",
  "description": "List all projects in a solution file.",
  "hidden": false,
  "aliases": [],
  "arguments": {},
  "options": {
    "--solution-folders": {
      "description": "Display solution folder paths.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    }
  },
  "subcommands": {}
}
""";

    private static readonly string CleanJson = $$"""
{
  "name": "clean",
  "version": "{{Product.Version}}",
  "description": ".NET Clean Command",
  "hidden": false,
  "aliases": [],
  "arguments": {
    "PROJECT | SOLUTION": {
      "description": "The project or solution file to operate on. If a file is not specified, the command will search the current directory for one.",
      "hidden": false,
      "helpName": null,
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": null
      }
    }
  },
  "options": {
    "--artifacts-path": {
      "description": "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.",
      "hidden": false,
      "aliases": [],
      "helpName": "ARTIFACTS_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--configuration": {
      "description": "The configuration to clean for. The default for most projects is 'Debug'.",
      "hidden": false,
      "aliases": [
        "-c"
      ],
      "helpName": "CONFIGURATION",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--disable-build-servers": {
      "description": "Force the command to ignore any persistent build servers.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--framework": {
      "description": "The target framework to clean for. The target framework must also be specified in the project file.",
      "hidden": false,
      "aliases": [
        "-f"
      ],
      "helpName": "FRAMEWORK",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--interactive": {
      "description": "Allows the command to stop and wait for user input or action (for example to complete authentication).",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": true,
      "defaultValue": false,
      "arity": {
        "minimum": 0,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--nologo": {
      "description": "Do not display the startup banner or the copyright message.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--output": {
      "description": "The directory containing the build artifacts to clean.",
      "hidden": false,
      "aliases": [
        "-o"
      ],
      "helpName": "OUTPUT_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--runtime": {
      "description": null,
      "hidden": false,
      "aliases": [
        "-r"
      ],
      "helpName": "RUNTIME_IDENTIFIER",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--verbosity": {
      "description": "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].",
      "hidden": false,
      "aliases": [
        "-v"
      ],
      "helpName": "LEVEL",
      "valueType": "Microsoft.DotNet.Cli.VerbosityOptions",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    }
  },
  "subcommands": {}
}
""";

    private static readonly string ReferenceJson = $$"""
{
  "name": "reference",
  "version": "{{Product.Version}}",
  "description": ".NET Remove Command",
  "hidden": false,
  "aliases": [],
  "arguments": {},
  "options": {
    "--project": {
      "description": "The project file to operate on. If a file is not specified, the command will search the current directory for one.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": true
    }
  },
  "subcommands": {
    "add": {
      "description": "Add a project-to-project reference to the project.",
      "hidden": false,
      "aliases": [],
      "arguments": {
        "PROJECT_PATH": {
          "description": "The paths to the projects to add as references.",
          "hidden": false,
          "helpName": null,
          "valueType": "System.Collections.Generic.IEnumerable<System.String>",
          "hasDefaultValue": false,
          "defaultValue": null,
          "arity": {
            "minimum": 1,
            "maximum": null
          }
        }
      },
      "options": {
        "--framework": {
          "description": "Add the reference only when targeting a specific framework.",
          "hidden": false,
          "aliases": [
            "-f"
          ],
          "helpName": "FRAMEWORK",
          "valueType": "System.String",
          "hasDefaultValue": false,
          "defaultValue": null,
          "arity": {
            "minimum": 1,
            "maximum": 1
          },
          "required": false,
          "recursive": false
        },
        "--interactive": {
          "description": "Allows the command to stop and wait for user input or action (for example to complete authentication).",
          "hidden": false,
          "aliases": [],
          "helpName": null,
          "valueType": "System.Boolean",
          "hasDefaultValue": true,
          "defaultValue": false,
          "arity": {
            "minimum": 0,
            "maximum": 0
          },
          "required": false,
          "recursive": false
        }
      },
      "subcommands": {}
    },
    "list": {
      "description": "List all project-to-project references of the project.",
      "hidden": false,
      "aliases": [],
      "arguments": {},
      "options": {},
      "subcommands": {}
    },
    "remove": {
      "description": "Remove a project-to-project reference from the project.",
      "hidden": false,
      "aliases": [],
      "arguments": {
        "PROJECT_PATH": {
          "description": "The paths to the referenced projects to remove.",
          "hidden": false,
          "helpName": null,
          "valueType": "System.Collections.Generic.IEnumerable<System.String>",
          "hasDefaultValue": false,
          "defaultValue": null,
          "arity": {
            "minimum": 1,
            "maximum": null
          }
        }
      },
      "options": {
        "--framework": {
          "description": "Remove the reference only when targeting a specific framework.",
          "hidden": false,
          "aliases": [
            "-f"
          ],
          "helpName": "FRAMEWORK",
          "valueType": "System.String",
          "hasDefaultValue": false,
          "defaultValue": null,
          "arity": {
            "minimum": 1,
            "maximum": 1
          },
          "required": false,
          "recursive": false
        }
      },
      "subcommands": {}
    }
  }
}
""";

    private static readonly string WorkloadInstallJson = $$"""
{
  "name": "install",
  "version": "{{Product.Version}}",
  "description": "Install one or more workloads.",
  "hidden": false,
  "aliases": [],
  "arguments": {
    "workloadId": {
      "description": "The NuGet package ID of the workload to install.",
      "hidden": false,
      "helpName": "WORKLOAD_ID",
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": null
      }
    }
  },
  "options": {
    "--configfile": {
      "description": "The NuGet configuration file to use.",
      "hidden": false,
      "aliases": [],
      "helpName": "FILE",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--disable-parallel": {
      "description": "Prevent restoring multiple projects in parallel.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--download-to-cache": {
      "description": "Download packages needed to install a workload to a folder that can be used for offline installation.",
      "hidden": true,
      "aliases": [],
      "helpName": "DIRECTORY",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--from-cache": {
      "description": "Complete the operation from cache (offline).",
      "hidden": true,
      "aliases": [],
      "helpName": "DIRECTORY",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--from-rollback-file": {
      "description": "Update workloads based on specified rollback definition file.",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--ignore-failed-sources": {
      "description": "Treat package source failures as warnings.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--include-previews": {
      "description": "Allow prerelease workload manifests.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--interactive": {
      "description": "Allows the command to stop and wait for user input or action (for example to complete authentication).",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": true,
      "defaultValue": false,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-cache": {
      "description": "Do not cache packages and http requests.",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-http-cache": {
      "description": "Do not cache packages and http requests.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--print-download-link-only": {
      "description": "Only print the list of links to download without downloading.",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--sdk-version": {
      "description": "The version of the SDK.",
      "hidden": true,
      "aliases": [],
      "helpName": "VERSION",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--skip-manifest-update": {
      "description": "Skip updating the workload manifests.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--skip-sign-check": {
      "description": "Skip signature verification of workload packages and installers.",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--source": {
      "description": "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.",
      "hidden": false,
      "aliases": [
        "-s"
      ],
      "helpName": "SOURCE",
      "valueType": "System.String[]",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": null
      },
      "required": false,
      "recursive": false
    },
    "--temp-dir": {
      "description": "Specify a temporary directory for this command to download and extract NuGet packages (must be secure).",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--verbosity": {
      "description": "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].",
      "hidden": false,
      "aliases": [
        "-v"
      ],
      "helpName": "LEVEL",
      "valueType": "Microsoft.DotNet.Cli.VerbosityOptions",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--version": {
      "description": "A workload version to display or one or more workloads and their versions joined by the '@' character.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": null
      },
      "required": false,
      "recursive": false
    }
  },
  "subcommands": {}
}
""";

    private static readonly string BuildJson = $$"""
{
  "name": "build",
  "version": "{{Product.Version}}",
  "description": ".NET Builder",
  "hidden": false,
  "aliases": [],
  "arguments": {
    "PROJECT | SOLUTION | FILE": {
      "description": "The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.",
      "hidden": false,
      "helpName": null,
      "valueType": "System.String[]",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": null
      }
    }
  },
  "options": {
    "--arch": {
      "description": "The target architecture.",
      "hidden": false,
      "aliases": [
        "-a"
      ],
      "helpName": "ARCH",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--artifacts-path": {
      "description": "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.",
      "hidden": false,
      "aliases": [],
      "helpName": "ARTIFACTS_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--configfile": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": "FILE",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--configuration": {
      "description": "The configuration to use for building the project. The default for most projects is 'Debug'.",
      "hidden": false,
      "aliases": [
        "-c"
      ],
      "helpName": "CONFIGURATION",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--debug": {
      "description": null,
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--disable-build-servers": {
      "description": "Force the command to ignore any persistent build servers.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--disable-parallel": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--force": {
      "description": "Force all dependencies to be resolved even if the last restore was successful.\r\nThis is equivalent to deleting project.assets.json.",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--framework": {
      "description": "The target framework to build for. The target framework must also be specified in the project file.",
      "hidden": false,
      "aliases": [
        "-f"
      ],
      "helpName": "FRAMEWORK",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--ignore-failed-sources": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--interactive": {
      "description": "Allows the command to stop and wait for user input or action (for example to complete authentication).",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": true,
      "defaultValue": false,
      "arity": {
        "minimum": 0,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--no-cache": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-dependencies": {
      "description": "Do not build project-to-project references and only build the specified project.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-http-cache": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-incremental": {
      "description": "Do not use incremental building.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-restore": {
      "description": "Do not restore the project before building.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--no-self-contained": {
      "description": "Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--nologo": {
      "description": "Do not display the startup banner or the copyright message.",
      "hidden": false,
      "aliases": [],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--os": {
      "description": "The target operating system.",
      "hidden": false,
      "aliases": [],
      "helpName": "OS",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--output": {
      "description": "The output directory to place built artifacts in.",
      "hidden": false,
      "aliases": [
        "-o"
      ],
      "helpName": "OUTPUT_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--packages": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": "PACKAGES_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--property": {
      "description": null,
      "hidden": true,
      "aliases": [
        "--p",
        "-p",
        "-property",
        "/p",
        "/property"
      ],
      "helpName": null,
      "valueType": "System.String[]",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": null
      },
      "required": false,
      "recursive": false
    },
    "--runtime": {
      "description": null,
      "hidden": false,
      "aliases": [
        "-r"
      ],
      "helpName": "RUNTIME_IDENTIFIER",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--self-contained": {
      "description": "Publish the .NET runtime with your application so the runtime doesn't need to be installed on the target machine.\r\nThe default is 'false.' However, when targeting .NET 7 or lower, the default is 'true' if a runtime identifier is specified.",
      "hidden": false,
      "aliases": [
        "--sc"
      ],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--source": {
      "description": "",
      "hidden": true,
      "aliases": [],
      "helpName": "SOURCE",
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": null
      },
      "required": false,
      "recursive": false
    },
    "--use-current-runtime": {
      "description": "Use current runtime as the target runtime.",
      "hidden": false,
      "aliases": [
        "--ucr"
      ],
      "helpName": null,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--verbosity": {
      "description": "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].",
      "hidden": false,
      "aliases": [
        "-v"
      ],
      "helpName": "LEVEL",
      "valueType": "Microsoft.DotNet.Cli.VerbosityOptions",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--version-suffix": {
      "description": "Set the value of the $(VersionSuffix) property to use when building the project.",
      "hidden": false,
      "aliases": [],
      "helpName": "VERSION_SUFFIX",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "defaultValue": null,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    }
  },
  "subcommands": {}
}
""";

    public static TheoryData<string[], string> CommandsJson => new()
    {
        { new[] { "solution", "list", "--cli-schema" }, SolutionListJson },
        { new[] { "clean", "--cli-schema" }, CleanJson },
        { new[] { "reference", "--cli-schema" }, ReferenceJson },
        { new[] { "workload", "install", "--cli-schema" }, WorkloadInstallJson },
        { new[] { "build", "--cli-schema" }, BuildJson }
    };

    [Theory]
    [MemberData(nameof(CommandsJson))]
    public void PrintCliSchema_WritesExpectedJson(string[] commandArgs, string json)
    {
        var commandResult = new DotnetCommand(Log).Execute(commandArgs);
        commandResult.Should().Pass();
        commandResult.Should().HaveStdOut(json.ReplaceLineEndings("\n"));
    }
}
