
using Microsoft.DotNet.Tools.Bootstrapper;

var parseResult = Parser.Parse(args);

return Parser.Invoke(parseResult);
