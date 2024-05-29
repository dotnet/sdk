& dotnet build
Copy-Item C:\code\sdk\artifacts\bin\Microsoft.NET.Sdk.Testing.Tasks\Debug\net9.0\Microsoft.NET.Sdk.Testing.Tasks.dll -Destination C:\code\sdk\artifacts\bin\redist\Debug\dotnet\sdk\9.0.100-dev
Copy-Item C:\code\sdk\artifacts\bin\dotnet\Debug\net9.0\dotnet.dll -Destination  C:\code\sdk\artifacts\bin\redist\Debug\dotnet\sdk\9.0.100-dev
