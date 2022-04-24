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
    public void FailingOrder1() {
        var sut = new PowerShellWrapper(new string[] {
            GetBuiltInModulesPath(),
            GetApplicationDscResourceModulesPath(),
            GetApplicationModulesPath(),
        });

        var dscResources = sut.InvokeCommand("Get-DscResource");
        Assert.NotEmpty(dscResources);
    }
    [Fact]
    public void PassingOrder1() {
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
            GetBuiltInModulesPath(),
            GetApplicationDscResourceModulesPath(),
            GetCustomModulesPath(),
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