using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;

namespace RevitStartup.Application
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExternalApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            try
            {
                InitializePlugin(application);
            }
            catch (Exception ex)
            {
                // PluginEntry catches its own failures. This catches only a failure to load RevitNumbering itself.
                LogFailure("Startup", ex);
            }

            try
            {
                // The ribbon is created even if the plugin failed: its button then tells the user that the plugin did not start.
                RibbonManager.OnStartup(application);
            }
            catch (Exception ex)
            {
                LogFailure("Ribbon", ex);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                ShutdownPlugin();
            }
            catch (Exception ex)
            {
                LogFailure("Shutdown", ex);
            }
            finally
            {
                // After ShutdownPlugin: it may still need the handler to load plugin types.
                AppDomain.CurrentDomain.AssemblyResolve -= OnResolveAssembly;
            }

            return Result.Succeeded;
        }

        // A separate method with NoInlining: types of RevitNumbering must load only after AssemblyResolve is subscribed.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void InitializePlugin(UIControlledApplication application)
        {
            RevitNumbering.PluginEntry.Initialize(application);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ShutdownPlugin()
        {
            RevitNumbering.PluginEntry.Shutdown();
        }

        // The failure goes to the plugin log. If the plugin itself did not load, only the debugger sees it.
        private static void LogFailure(string operation, Exception exception)
        {
            try
            {
                WriteToPluginLog(operation, exception);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WriteToPluginLog(string operation, Exception exception)
        {
            RevitNumbering.PluginEntry.LogError(operation, exception);
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            string directoryDLLs = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (args.Name.Contains(".resources"))
                return null;

            var assemblyName = new AssemblyName(args.Name).Name;

            return LoadAssemblyFromDirectory(directoryDLLs, assemblyName);
        }

        private static Assembly LoadAssemblyFromDirectory(string directory, string assemblyName)
        {
            try
            {
                string assemblyPath = Path.Combine(directory, assemblyName + ".dll");
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {assemblyName}: {ex.Message}");
                return null;
            }
        }
    }
}
