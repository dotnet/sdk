namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IEngineEnvironmentSettings
    {
        ISettingsLoader SettingsLoader { get; }

        ITemplateEngineHost Host { get; }

        IEnvironment Environment { get; }

        IPathInfo Paths { get; }
    }
}