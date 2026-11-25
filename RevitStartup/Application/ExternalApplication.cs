using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;

namespace RevitStartup.Application
{
    /// <summary>
    /// Class for running the application in Revit
    /// </summary>
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
                // PluginEntry has already written the details to its log; the ribbon is created anyway (AA-106).
                System.Diagnostics.Debug.WriteLine(ex);
            }

            try
            {
                RibbonManager.OnStartup(application);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                ShutdownPlugin(application);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            return Result.Succeeded;
        }

        /// <summary>Separate method: types of RevitNumbering load only after AssemblyResolve is subscribed.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void InitializePlugin(UIControlledApplication application)
        {
            RevitNumbering.PluginEntry.Initialize(application);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ShutdownPlugin(UIControlledApplication application)
        {
            RevitNumbering.PluginEntry.Shutdown(application);
        }

        public static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            string directoryDLLs = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (args.Name.Contains(".resources"))
                return null;

            var assemblyName = new AssemblyName(args.Name).Name;

            return LoadAssemblyFromDirectory(directoryDLLs, assemblyName);
        }

        public static Assembly LoadAssemblyFromDirectory(string directory, string assemblyName)
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
