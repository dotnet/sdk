# NativeAOT MSTAT report

This dependency-free .NET 8 tool reads NativeAOT MSTAT 2.x files and a retention
DGML graph, then writes an interactive self-contained HTML size report and two
complete CSV inventories.

```text
dotnet run --project mstat-report.csproj -c Release -- \
  input.mstat scan.dgml.xml output.html \
  generic-instantiations.csv single-dependency.csv
```

The report attributes the code, GC info, and exception-handling sizes recorded
for each method. The retention graph uses the NativeAOT dependency direction
(`source -> target`). It includes every incoming edge for each mapped physical
size node and one shortest path from a zero-incoming-edge graph root.

Run the focused dependency-free tests with:

```text
dotnet run --project mstat-report.csproj -c Release -- --self-test
```

The reader is an independent implementation of the MIT-licensed format emitted
by `MstatObjectDumper` in dotnet/runtime. It uses only .NET BCL APIs.
