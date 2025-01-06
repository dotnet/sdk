& dotnet build
Copy-Item C:\Code\sdk\artifacts\bin\Debug\Sdks\Microsoft.NET.Sdk\tools\net10.0\Microsoft.NET.Build.Tasks.dll -Destination C:\code\sdk\artifacts\bin\redist\Debug\dotnet\sdk\10.0.100-dev
Copy-Item C:\Code\sdk\artifacts\bin\Debug\Sdks\Microsoft.NET.Sdk\tools\net10.0\Microsoft.NET.Build.Tasks.dll -Destination C:\Code\sdk\artifacts\bin\redist\Debug\dotnet\sdk\10.0.100-dev\Sdks\Microsoft.NET.Sdk\tools\net10.0
Copy-Item C:\code\sdk\artifacts\bin\dotnet\Debug\net10.0\dotnet.dll -Destination  C:\code\sdk\artifacts\bin\redist\Debug\dotnet\sdk\10.0.100-dev
