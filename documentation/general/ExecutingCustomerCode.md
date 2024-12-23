# Executing Customer Code

## Summary

When customers discover issues with the .NET SDK, we often need more information to see how exactly their scenarios differ from the (presumably working) mainline scenarios. This additional information often takes the form of a 'repro' or set of steps by which we can see the error ourselves and walk through what is happening as their scenario plays out and how it ultimately diverged from our expectations.

Blindly executing, or even just opening in an IDE like Visual Studio without intending to build, unvetted customer code can be a security hazard, however, not just for the machine executing the code but for any machine on the same network and any service accessible using credentials they can access through those machines. In this way, a malicious actor can exfiltrate sensitive Microsoft data, including information about other Microsoft employees, proprietary code, or private customer data. They may even be able to take down a service or introduce further security bugs in shipping products. Indeed, the most common vector hackers use to gain access is through compromising one or more individual users with employee credentials. At Microsoft where security is paramount, we want to prevent such hacks.

This document contains recommended best practices on how to securely test users' code. They are arranged in order of security with the most secure at the top. This should also be the priority you should use to stay secure when executing code.

## Strategies for Staying Secure

### Binary Logs

MSBuild binary logs (binlogs) are structured data covering what happened during a build. Many operations that aren't obviously 'builds' such as publish (`dotnet publish`) or pack (`dotnet pack`) include building as part or all of the process, which makes them very useful for learning more about and potentially diagnosing a variety of potential problems. Furthermore, they are safe in the sense that analyzing a binlog does not expose your system to risks.

To provide information to customers about collecting binlogs, direct them to:
https://aka.ms/msbuild/binlog

It is important to note, however, that binlogs often collect secrets as part of the information relevant to the build. A binlog of a simplified repro can work as well, but it may be difficult to create a simple repro. Customers are sometimes concerned (with good reason) about sharing binlogs of their real (or simplified) builds. If so, they can attempt to redact secrets from their binlog, either via the [MSBuild Structured Log Viewer](https://msbuildlog.com/#redaction) or with [binlogtool](https://www.nuget.org/packages/binlogtool). If that isn't sufficient, or if the reported issue does not involve MSBuild, another option may be more relevant.

### Windows Sandbox

Windows Sandbox permits safely running any operation in a controlled, isolated environment where it will not affect your machine or Microsoft in any detrimental way. More information can be found [here](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-overview). If the steps a customer provides for reproducing their scenario can be executed in Windows Sandbox, it is the safest place to do so. Unfortunately, Sandbox does come with a few notable downsides:
1. Windows Sandbox only exists for Windows. If a customer's scenario only reproduces on macOS or linux, it will not be able to be reproduced on Windows, and Sandbox is not helpful.
2. When Sandbox exits for any reason, all data is lost. This means, for instance, that if the computer hosting Sandbox restarts while Sandbox is open, any progress you might have made in putting together the customer's repro scenario is lost.
3. Sandbox is essentially empty when you first create it. It has internet access as long as your host computer has internet access, but it will be missing various programs and tools you may need to reproduce the customer's problem. Such programs and tools need to be downloaded anew every time you want to use Sandbox.

Conveniently, however, it is relatively easy to copy files from your host computer into Sandbox, and as noted earlier, it is a secure, isolated environment, so it is an excellent choice if it is viable.

### Create Your Own Solution

Depending on how well you understand the customer's scenario, you may or may not understand ahead of time the most likely problem, but if it's possible to create your own solution that reproduces the issue the customer alluded to in their report, and you understand all the steps it took to create that solution, that is a safe method for obtaining a project to test and debug. Note that this is only a secure method if you understand all steps involved in creating the test project. "Copy project file provided by customer to disk and build it" does not count as a well-understood pair of steps.

### Create a Virtual Machine Without Your Credentials

Creating and using a virtual machine (VM) without your credentials is another secure way to test a customer's code, as it's disconnected from either your credentials or your computer. It is important if using this method to ensure that you do not give your VM your credentials at any point such as, for example, signing into your Microsoft account so you can use Visual Studio Enterprise.

The other disadvantage of this approach is that it can take a lot of time to select appropriate parameters for your VM, start it, connect to it, set it up properly for your scenario, and execute the scenario. Unlike Sandbox, it can retain certain information across restarts, but it may lose active work just as your computer loses active work when you restart it. It's important to keep in mind that many VMs restart automatically at a fixed time every day when not in use to save resources.

That said, this is a secure means for executing a customer's scenario, and it works for any operating system and can even be configured to work for other architectures. Note that using a VM in the cloud costs real money, though a local VM (such as using [Hyper-V](https://learn.microsoft.com/windows-server/virtualization/hyper-v/get-started/create-a-virtual-machine-in-hyper-v)) typically does not.

For Microsoft employees, [DevTestLab](https://ms.portal.azure.com/#browse/Microsoft.Compute%2FVirtualMachines) can help you create VMs.

#### Docker

As a corollary to using VMs to hide your machine from others, containers (notably Docker containers) are intended to create a small, self-contained environment in which to operate. They're cheaper to create than full VMs, though with more stringent resource limitations and some missing or altered functionality, they may not work for some scenarios. Even so, if they work, they can be a safe and cheaper option than creating a VM. Make sure to follow general best practices such as running in rootless mode if possible and avoiding signing in using your credentials.

### Read Code

If none of the above methods work, it may be viable to read all customer code carefully to ensure that no part of it is risky or malicious. Many IDEs such as Visual Studio automatically run design-time builds when code is open. As a result, even just opening a malicious code base in Visual Studio can lead to exploits. Prefer an IDE that does not run design-time builds. Some examples include:
1. Visual Studio Code (if no addons are installed)
2. Notepad
3. Emacs/Vim

Some examples of specific patterns to watch out for that can cause issues include:
1. PackageReferences. These download code from the internet and permit its execution locally. If you are unfamiliar with any package in a PackageReference, this is insecure.
2. MSBuild SDKs. At the top of many .NET projects is a line that includes `Sdk="<SDK>"`. This automatically imports build logic associated with the specified SDK. If you are unfamiliar with the specified SDK, ensure it is secure before building any code including it.
3. MSBuild tasks. Within a `Target` block are MSBuild Tasks. Many of these, including `Exec` and `DownloadFile` execute logic that pulls code or build logic from the internet and may execute it. If you are unfamiliar with any MSBuild Task in a customer-provided project, ensure you fully understand its semantics before executing it (by building).
4. Typos. NuGet packages starting with Microsoft. and System. can only be pushed to nuget.org by Microsoft accounts, but Mircosoft and Sysstem are unregulated.
5. Malicious NuGet.Config files. Even if a PackageReference points to a package you're familiar with, perhaps even a Microsoft. or System. package, a NuGet.Config file can dictate searching in a different package source first that may not be secure and may include a malicious version of the package.

More examples of patterns that can easily be exploited by malicious users can be found [here](https://aka.ms/msbuild-security-documentation)

In addition to those more specific examples, it is important to follow the principle of least privilege. This means, for instance, that when on linux, you should only run a command as root if that's truly necessary to reproduce the customer's scenario. On Windows, you should only use an admin terminal if it's necessary.

## Response to Malicious Repros

If you discover that a customer provided a malicious project or solution, there are several steps. You should do all of these if possible.

1. Most importantly, do not keep any vestige of the repro on your machine. Do not follow their steps.
2. Add a note to the issue that it was malicious. Include this information in the title if possible.
3. Delete the contents of the repro steps. (On GitHub, it can still be found by looking at your edit.)
4. Report the infraction using https://aka.ms/reportitnow
5. Report your finding to your manager and your security team.
6. Depending on the severity of the issue, consider banning the user from GitHub.

If you are external, you may not be able to follow all of these steps. Reach out to someone from our team to help facilitate doing all these.

Last but not least, thank you for helping keep Microsoft secure!