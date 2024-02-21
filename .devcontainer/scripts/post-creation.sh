# Install SDK and tool dependencies before container starts
# Also run the full restore on the repo so that go-to definition
# and other language features will be available in C# files
./restore.sh
# Run eng/sdk-build-env.sh to ensure everything is set up correctly for VSCode
./eng/sdk-build-env.sh
