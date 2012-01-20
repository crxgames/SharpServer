/*
 * SharpServer
 * ModuleServices.CS
 * 
 * Handles anything module related for the host program
 * Roughly based around: http://www.codeproject.com/KB/cs/c__plugin_architecture.aspx
 * TODO: Lazy Loading
 */
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using SharpServer.ModuleInterface;
using SharpServerLib;

namespace SharpServer
{
    /// <summary>
    /// Handles the management of anything dynamic module related
    /// </summary>
    class ModuleServices : IModuleHost
    {
        private Types.AvailableModules colAvailableModules = new Types.AvailableModules();
        private SharpConfig config = null;
        private CallbackSubSystem cbSystem = null;

        public ModuleServices(SharpConfig loadedConfig, ref CallbackSubSystem cbSys)
        {
            config = loadedConfig;
            cbSystem = cbSys;
        }

        /// <summary>
        /// A Collection of all Plugins Found and Loaded by the FindPlugins() Method
        /// </summary>
        public Types.AvailableModules AvailableModules
        {
            get { return colAvailableModules; }
            set { colAvailableModules = value; }
        }

        /// <summary>
        /// Searches the Application's Startup Directory for Plugins
        /// </summary>
        public void FindModules()
        {
            FindModules(config.directive["ModulesDir"]);
        }

        /// <summary>
        /// Searches the passed Path for Plugins
        /// </summary>
        /// <param name="Path">Directory to search for Plugins in</param>
        public void FindModules(string Path)
        {
            /* Clear everything since we are loading new ones... */
            colAvailableModules.Clear();

            /* Inspect files in the module path */
            foreach (string fileOn in Directory.GetFiles(Path))
            {
                FileInfo file = new FileInfo(fileOn);

                /* Only load the correct file */
                if (file.Extension.Equals(".dll"))
                {
                    this.AddModule(fileOn);
                }
            }
        }

        /// <summary>
        /// Unloads and Closes all AvailableModules
        /// </summary>
        public void CloseModules()
        {
            foreach (Types.AvailableModule moduleOn in colAvailableModules)
            {
                /* 
                 * Close all module instances
                 * Call the module Shutdown() method incase it needs 
                 * to cleanup
                 */
                moduleOn.Instance.Shutdown();

                //After we give the plugin a chance to tidy up, get rid of it
                moduleOn.Instance = null;
            }

            /* Clear the collection */
            colAvailableModules.Clear();
        }

        /// <summary>
        /// Loads a module into memory and does some verification that it is of valid format
        /// </summary>
        /// <param name="FileName"></param>
        private void AddModule(string FileName)
        {
            /* Create a new assembly from the plugin file we're adding.. */
            Assembly moduleAssembly = Assembly.LoadFrom(FileName);

            /* Loop through types in the assembly */
            foreach (Type moduleType in moduleAssembly.GetTypes())
            {
                /* Only concern ourselves with the module's public problems... */
                if (moduleType.IsPublic)
                {
                    /* Non-abstract please...kthx */
                    if (!moduleType.IsAbstract)
                    {
                        /* Needs to match IModule */
                        Type typeInterface = moduleType.GetInterface("SharpServer.ModuleInterface.IModule", true);

                        /* Does this interface exist in the assembly? */
                        if (typeInterface != null)
                        {
                            /* Create a new available module since the type implements the IModule interface */
                            Types.AvailableModule newModule = new Types.AvailableModule();

                            newModule.AssemblyPath = FileName;

                            //Create a new instance and store the instance in the collection for later use
                            //We could change this later on to not load an instance.. we have 2 options
                            //1- Make one instance, and use it whenever we need it.. it's always there
                            //2- Don't make an instance, and instead make an instance whenever we use it, then close it
                            //For now we'll just make an instance of all the modules
                            // TODO: Lazy loading...
                            newModule.Instance = (IModule)Activator.CreateInstance(moduleAssembly.GetType(moduleType.ToString()));

                            /* Set the module's host to this class which inherited IModuleHost */
                            newModule.Instance.Host = this;

                            /* Add it to the collection */
                            this.colAvailableModules.Add(newModule);

                            /* Initialize the module */
                            newModule.Instance.Initialize();

                            /* Squeakin clean memory! */
                            newModule = null;
                        }

                        typeInterface = null; //Mr. Clean - had to keep this from tutorial code
                    }
                }
            }

            moduleAssembly = null;
        }

        public void RegisterHook(string hook, HookCallback method)
        {
            cbSystem.RegisterCallback(hook, method);
        }
    }

    namespace Types
    {
        /// <summary>
        /// Collection for AvailablePlugin Type
        /// </summary>
        public class AvailableModules : CollectionBase
        {
            /// <summary>
            /// Add a module to the collection of Available modules
            /// </summary>
            /// <param name="pluginToAdd">The Plugin to Add</param>
            public void Add(Types.AvailableModule moduleToAdd)
            {
                this.List.Add(moduleToAdd);
            }

            /// <summary>
            /// Remove a module to the collection of Available plugins
            /// </summary>
            /// <param name="moduleToRemove">The module to Remove</param>
            public void Remove(Types.AvailableModule moduleToRemove)
            {
                this.List.Remove(moduleToRemove);
            }

            /// <summary>
            /// Finds a module in the available Plugins
            /// </summary>
            /// <param name="moduleNameOrPath">The name or File path of the module to find</param>
            /// <returns>Available module, or null if the plugin is not found</returns>
            public Types.AvailableModule Find(string moduleNameOrPath)
            {
                Types.AvailableModule toReturn = null;

                //Loop through all the plugins
                foreach (Types.AvailableModule moduleOn in this.List)
                {
                    //Find the one with the matching name or filename
                    if ((moduleOn.Instance.Name.Equals(moduleNameOrPath)) || moduleOn.AssemblyPath.Equals(moduleNameOrPath))
                    {
                        toReturn = moduleOn;
                        break;
                    }
                }
                return toReturn;
            }
        }

        /// <summary>
        /// Data Class for Available Module.  Holds and instance of the loaded module, as well as the module's Assembly Path
        /// </summary>
        public class AvailableModule
        {
            //Holds an instance of the module to access
            //ALso holds assembly path... not really necessary
            private IModule myInstance = null;
            private string myAssemblyPath = "";

            public IModule Instance
            {
                get { return myInstance; }
                set { myInstance = value; }
            }

            public string AssemblyPath
            {
                get { return myAssemblyPath; }
                set { myAssemblyPath = value; }
            }
        }
    }
}
