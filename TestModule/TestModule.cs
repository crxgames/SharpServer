using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpServer.ModuleInterface;
using SharpServerLib;

namespace TestModule
{
    public class TestModule : IModule
    {
        IModuleHost _Host = null;
        string ModuleName = "TestModule";
        string ModuleAuthor = "Cody Mays";
        string ModuleDescription = "This is a simple testing module";
        string ModuleVersion = "1.0.0";

        /* Required methods to be implemented */
        public void Initialize()
        {
            Console.WriteLine("Hello from TestModule!");

            HookCallback cb = new HookCallback(ProcessFile);
            this._Host.RegisterHook("request.rawfile.process", cb);
        }

        public void ProcessFile(ref HttpRequestInfo obj)
        {
            Console.WriteLine("Callback processing of requested file...");

            //HttpRequestInfo theRequest = ref obj;
			if(obj.mimeType == "text/html")
			{
	            string HTML = Encoding.UTF8.GetString(obj.rawFile);
	            HTML = HTML.Replace("<", "&lt;");
	            HTML = HTML.Replace(">", "&gt;");
	            obj.finalizedFile = System.Text.Encoding.UTF8.GetBytes(HTML);
			}
        }

        public void Shutdown()
        {
            Console.WriteLine("Shutting down TestModule...");
        }

        public int ProcessRequest(ref HttpRequestInfo request)
        {
            return 1;
        }


        #region IModule Required
        
        public string Name
        {
            get
            {
                return ModuleName;
            }
        }

        public string Description
        {
            get
            {
                return ModuleDescription;
            }
        }

        public string Author
        {
            get
            {
                return ModuleAuthor;
            }
        }

        public IModuleHost Host
        {
            get
            {
                return _Host;
            }
            set
            {
                _Host = value;
            }
        }

        public string Version
        {
            get
            {
                return ModuleVersion;
            }
        }

        #endregion
    }
}
