using System;
using System.Linq;
using System.Windows.Forms;

namespace QlipInstallerBuilder;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--build", StringComparison.OrdinalIgnoreCase)))
        {
            // Headless mode for CI/automation (still a single exe; no PowerShell scripts required).
            return BuilderHeadless.Run(args);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new BuilderForm());
        return 0;
    }
}
