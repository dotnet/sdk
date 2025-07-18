// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
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
  "options": {
    "--solution-folders": {
      "description": "Display solution folder paths.",
      "hidden": false,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    }
  }
}
""";

    private static readonly string CleanJson = $$"""
{
  "name": "clean",
  "version": "{{Product.Version}}",
  "description": ".NET Clean Command",
  "hidden": false,
  "arguments": {
    "PROJECT | SOLUTION | FILE": {
      "description": "The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.",
      "order": 0,
      "hidden": false,
      "valueType": "System.String[]",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0
      }
    }
  },
  "options": {
    "--artifacts-path": {
      "description": "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.",
      "hidden": false,
      "helpName": "ARTIFACTS_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--runtime": {
      "description": "The target runtime to clean for.",
      "hidden": false,
      "aliases": [
        "-r"
      ],
      "helpName": "RUNTIME_IDENTIFIER",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--target": {
      "description": "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
      "hidden": true,
      "aliases": [
        "--t",
        "-t",
        "-target",
        "/t",
        "/target"
      ],
      "helpName": "TARGET",
      "valueType": "System.String[]",
      "hasDefaultValue": true,
      "defaultValue": [
        "Clean"
      ],
      "arity": {
        "minimum": 0
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
      "valueType": "Microsoft.DotNet.Cli.Utils.VerbosityOptions",
      "hasDefaultValue": true,
      "defaultValue": "normal",
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    }
  },
  "subcommands": {
    "file-based-apps": {
      "description": "Removes artifacts created for file-based apps",
      "hidden": true,
      "options": {
        "--automatic": {
          "hidden": true,
          "valueType": "System.Boolean",
          "hasDefaultValue": false,
          "arity": {
            "minimum": 0,
            "maximum": 1
          },
          "required": false,
          "recursive": false
        },
        "--days": {
          "description": "How many days an artifact folder needs to be unused in order to be removed",
          "hidden": false,
          "valueType": "System.Int32",
          "hasDefaultValue": true,
          "defaultValue": 30,
          "arity": {
            "minimum": 1,
            "maximum": 1
          },
          "required": false,
          "recursive": false
        },
        "--dry-run": {
          "description": "Determines changes without actually modifying the file system",
          "hidden": false,
          "valueType": "System.Boolean",
          "hasDefaultValue": false,
          "arity": {
            "minimum": 0,
            "maximum": 0
          },
          "required": false,
          "recursive": false
        }
      }
    }
  }
}
""";

    private static readonly string ReferenceJson = $$"""
{
  "name": "reference",
  "version": "{{Product.Version}}",
  "description": ".NET Remove Command",
  "hidden": false,
  "options": {
    "--project": {
      "description": "The project file to operate on. If a file is not specified, the command will search the current directory for one.",
      "hidden": false,
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "arguments": {
        "PROJECT_PATH": {
          "description": "The paths to the projects to add as references.",
          "order": 0,
          "hidden": false,
          "valueType": "System.Collections.Generic.IEnumerable<System.String>",
          "hasDefaultValue": false,
          "arity": {
            "minimum": 1
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
      }
    },
    "list": {
      "description": "List all project-to-project references of the project.",
      "hidden": false
    },
    "remove": {
      "description": "Remove a project-to-project reference from the project.",
      "hidden": false,
      "arguments": {
        "PROJECT_PATH": {
          "description": "The paths to the referenced projects to remove.",
          "order": 0,
          "hidden": false,
          "valueType": "System.Collections.Generic.IEnumerable<System.String>",
          "hasDefaultValue": false,
          "arity": {
            "minimum": 1
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
          "arity": {
            "minimum": 1,
            "maximum": 1
          },
          "required": false,
          "recursive": false
        }
      }
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
  "arguments": {
    "workloadId": {
      "description": "The NuGet package ID of the workload to install.",
      "order": 0,
      "hidden": false,
      "helpName": "WORKLOAD_ID",
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1
      }
    }
  },
  "options": {
    "--configfile": {
      "description": "The NuGet configuration file to use.",
      "hidden": false,
      "helpName": "FILE",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "helpName": "DIRECTORY",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "helpName": "DIRECTORY",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "helpName": "VERSION",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "arity": {
        "minimum": 1
      },
      "required": false,
      "recursive": false
    },
    "--temp-dir": {
      "description": "Specify a temporary directory for this command to download and extract NuGet packages (must be secure).",
      "hidden": false,
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "valueType": "Microsoft.DotNet.Cli.Utils.VerbosityOptions",
      "hasDefaultValue": true,
      "defaultValue": "normal",
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
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1
      },
      "required": false,
      "recursive": false
    }
  }
}
""";

    private static readonly string BuildJson = $$"""
{
  "name": "build",
  "version": "{{Product.Version}}",
  "description": ".NET Builder",
  "hidden": false,
  "arguments": {
    "PROJECT | SOLUTION | FILE": {
      "description": "The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.",
      "order": 0,
      "hidden": false,
      "valueType": "System.String[]",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0
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
      "helpName": "ARTIFACTS_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "helpName": "FILE",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--debug": {
      "hidden": false,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0,
        "maximum": 0
      },
      "required": false,
      "recursive": false
    },
    "--force": {
      "description": "Force all dependencies to be resolved even if the last restore was successful.\nThis is equivalent to deleting project.assets.json.",
      "hidden": true,
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "helpName": "OS",
      "valueType": "System.String",
      "hasDefaultValue": false,
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
      "helpName": "PACKAGES_DIR",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--property": {
      "hidden": true,
      "aliases": [
        "--p",
        "-p",
        "-property",
        "/p",
        "/property"
      ],
      "valueType": "System.Collections.ObjectModel.ReadOnlyDictionary<System.String, System.String>",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0
      },
      "required": false,
      "recursive": false
    },
    "--restoreProperty": {
      "hidden": true,
      "aliases": [
        "--rp",
        "-restoreProperty",
        "-rp",
        "/restoreProperty",
        "/rp"
      ],
      "valueType": "System.Collections.ObjectModel.ReadOnlyDictionary<System.String, System.String>",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 0
      },
      "required": false,
      "recursive": false
    },
    "--runtime": {
      "description": "The target runtime to build for.",
      "hidden": false,
      "aliases": [
        "-r"
      ],
      "helpName": "RUNTIME_IDENTIFIER",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    },
    "--self-contained": {
      "description": "Publish the .NET runtime with your application so the runtime doesn't need to be installed on the target machine.\nThe default is 'false.' However, when targeting .NET 7 or lower, the default is 'true' if a runtime identifier is specified.",
      "hidden": false,
      "aliases": [
        "--sc"
      ],
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
      "helpName": "SOURCE",
      "valueType": "System.Collections.Generic.IEnumerable<System.String>",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1
      },
      "required": false,
      "recursive": false
    },
    "--target": {
      "description": "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
      "hidden": true,
      "aliases": [
        "--t",
        "-t",
        "-target",
        "/t",
        "/target"
      ],
      "helpName": "TARGET",
      "valueType": "System.String[]",
      "hasDefaultValue": true,
      "arity": {
        "minimum": 0
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
      "valueType": "System.Boolean",
      "hasDefaultValue": false,
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
        "--v",
        "-v",
        "-verbosity",
        "/v",
        "/verbosity"
      ],
      "helpName": "LEVEL",
      "valueType": "System.Nullable<Microsoft.DotNet.Cli.Utils.VerbosityOptions>",
      "hasDefaultValue": false,
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
      "helpName": "VERSION_SUFFIX",
      "valueType": "System.String",
      "hasDefaultValue": false,
      "arity": {
        "minimum": 1,
        "maximum": 1
      },
      "required": false,
      "recursive": false
    }
  }
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
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        CliSchema.PrintCliSchema(Parser.Instance.Parse(commandArgs).CommandResult, writer, null);
        stream.Position = 0;
        var reader = new StreamReader(stream);
        var output = reader.ReadToEnd();
        output.Should().BeEquivalentTo(json.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CanGenerateJsonSchemaForCLIOutput()
    {
        var schema = CliSchema.GetJsonSchema();
        schema.Should().NotBeNull();
    }
}
