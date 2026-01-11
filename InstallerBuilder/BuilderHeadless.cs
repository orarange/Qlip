using System;

namespace QlipInstallerBuilder;

internal static class BuilderHeadless
{
    public static int Run(string[] args)
    {
        try
        {
            var builder = new BuilderCore(logToConsole: true);
            builder.BuildAsync(configuration: "Release").GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
