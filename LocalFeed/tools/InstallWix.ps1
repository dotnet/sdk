param(
[string]$directory
)

If(!(test-path $directory))
{
New-Item -ItemType Directory -Force -Path $directory
}
# Download Wix version 3.10.2 - https://wix.codeplex.com/releases/view/619491
Invoke-WebRequest -Uri https://wix.codeplex.com/downloads/get/1540241 -Method Get -OutFile $directory\wix310-binaries.zip