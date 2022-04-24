using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Xunit;

namespace PSDesiredStateConfiguration.Tests;

public class GetDscResourceTests {
    [Fact]
    public void ExpectedResourcesAreFound() {
        // PSModulePath ends up similar to:
        // C:\PSDesiredStateConfiguration.Tests\runtimes\win\lib\net6.0\Modules;C:\PSDesiredStateConfiguration.Tests\DscResourceModules;C:\PSDesiredStateConfiguration.Tests\Modules
        // when this is split on the colon you can see why that would be problematic
        var sut = new PowerShellWrapper(new string[] {
            GetBuiltInModulesPath(),                 // the built in modules that come with the powershell sdk (Microsoft.PowerShell.Management, Microsoft.PowerShell.Security, etc)
            GetApplicationDscResourceModulesPath(),  // module folder that has the PSDscResources module saved
            GetApplicationModulesPath(),             // module folder that has the PSDesiredStateConfiguration 2.0.5 module saved
        });

        var dscResources = sut.InvokeCommand("Get-DscResource");
        Assert.NotEmpty(dscResources);
    }
    [Fact]
    public void ExpectedResourcesAreFoundByChance() {
        // Depending on what the order of path values in PSModulePath is
        // you might actually get some resources back.
        // When the PSModulePath for this powershell instance is split the last value is:
        // \PSDesiredStateConfiguration.Tests\DscResourceModules
        // which later on down the line ends up resolving
        var sut = new PowerShellWrapper(new string[] {
            GetBuiltInModulesPath(),
            GetApplicationModulesPath(),
            GetApplicationDscResourceModulesPath()
        });

        var dscResources = sut.InvokeCommand("Get-DscResource");
        Assert.NotEmpty(dscResources);
    }
    [Fact]
    public void PotentialFixGetsResources() {
        var sut = new PowerShellWrapper(new string[] {
            GetBuiltInModulesPath(),                 // the built in modules that come with the powershell sdk (Microsoft.PowerShell.Management, Microsoft.PowerShell.Security, etc)
            GetApplicationDscResourceModulesPath(),  // module folder that has the PSDscResources module saved
            GetCustomModulesPath()                   // module folder that has my proposed fix to PSDesiredStateConfiguration
        });

        var dscResources = sut.InvokeCommand("Get-DscResource");
        Assert.NotEmpty(dscResources);
    }
    class PowerShellWrapper : IDisposable {
        private readonly Lazy<Runspace> runspace;
        public PowerShellWrapper(IEnumerable<string> psModulePaths) {
            this.runspace = new Lazy<Runspace>(() => {
                var ss = InitialSessionState.CreateDefault2();
                ss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                ss.ThrowOnRunspaceOpenError = true;
                ss.EnvironmentVariables.Add(new SessionStateVariableEntry(
                    "PSModulePath",
                    string.Join(Path.PathSeparator, psModulePaths),
                    null));
                var runspace = RunspaceFactory.CreateRunspace(ss);
                runspace.Open();

                return runspace;
            });
        }
        public System.Collections.ObjectModel.Collection<PSObject> InvokeCommand(string command) {
            using var pwsh = PowerShell.Create(this.runspace.Value);
            var output = pwsh.AddCommand(command).Invoke();
            if (pwsh.Streams.Error.Count > 0) { throw new AggregateException(pwsh.Streams.Error.Select(x => x.Exception)); }
            return output;
        }
        public void Dispose() {
            this.runspace.Value.Dispose();
        }
    }
    public static string GetApplicationDscResourceModulesPath() {
        return Path.Combine(AppContext.BaseDirectory, "DscResourceModules");
    }
    public static string GetApplicationModulesPath() {
        return Path.Combine(AppContext.BaseDirectory, "Modules");
    }
    public static string GetCustomModulesPath() {
        return Path.Combine(AppContext.BaseDirectory, "CustomModules");
    }
    public static string GetBuiltInModulesPath() {
        var pwshModules = System.Management.Automation.ModuleIntrinsics.GetModulePath("", null, null);
        var paths = pwshModules.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return paths
            .Where(x => IsSubPathOf(x, AppContext.BaseDirectory))
            .Single();
    }
    public static bool IsSubPathOf(string path, string potentialParentPath) {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (potentialParentPath == null) { throw new ArgumentNullException(nameof(path)); }
        return path.StartsWith(potentialParentPath, StringComparison.OrdinalIgnoreCase);
    }
}