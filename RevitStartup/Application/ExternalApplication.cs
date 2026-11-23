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
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
                RibbonManager.OnStartup(application);
            }
            catch (Exception) { }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
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
