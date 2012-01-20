using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using SharpServerLib;

namespace SharpServer
{
    class HttpServer
    {
        private TcpListener tcpListener = null;
        private Thread listenThread = null;
        private SharpConfig config = null;
        private MimeTypes mimeTypes = null;
        private ModuleServices Modules = null;
        private CallbackSubSystem CallbackSys = null;
		private int numBusy = 0;
		private ManualResetEvent DoneEvent = new ManualResetEvent(false);

        /// <summary>
        /// Basic constructor using default address and port settings
        /// </summary>
        public HttpServer()
        {
            config = new SharpConfig();
            mimeTypes = new MimeTypes();
            CallbackSys = new CallbackSubSystem();

            if (config.loaded)
            {
                Modules = new ModuleServices(config, ref CallbackSys);
                Modules.FindModules();

                try
                {
                    Debug.WriteLine("Attempting to bind to " + config.directive["Address"] + ":" + config.directive["Port"]);
                    tcpListener = new TcpListener(IPAddress.Parse(config.directive["Address"]), Convert.ToInt32(config.directive["Port"])); // will default to any address and port 80 on this constructor
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                    Debug.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                }
                PrepListenThread();
            }
			else
			{
				Console.WriteLine("config not loaded.");	
			}
        }

        public HttpServer(string path)
        {
            config = new SharpConfig(path);
            CallbackSys = new CallbackSubSystem();

            if (config.loaded)
            {
                Modules = new ModuleServices(config, ref CallbackSys);
                Modules.FindModules();

                try
                {
                    tcpListener = new TcpListener(IPAddress.Parse(config.directive["Address"]), Convert.ToInt32(config.directive["Port"])); // will default to any address and port 80 on this constructor
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                    Debug.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                }
                PrepListenThread();
            }
        }

        /// <summary>
        /// Constructor allowing the specification of address and port to be bound to
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public HttpServer(IPAddress address, int port)
        {
            config = new SharpConfig();
            CallbackSys = new CallbackSubSystem();

            if (config.loaded)
            {
                Modules = new ModuleServices(config, ref CallbackSys);
                Modules.FindModules();

                try
                {
                    tcpListener = new TcpListener(address, port);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                    Debug.WriteLine("Failed to bind to address or port. Exception: " + ex.Message);
                }
                PrepListenThread();
            }
        }

        private void PrepListenThread()
        {
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.Start();
        }

        /// <summary>
        /// Handles listening for incoming connections and spawning children to handle requests
        /// </summary>
        private void ListenForClients()
        {
            try
            {
                /* Start the thread and notify */
                tcpListener.Start();

                Console.WriteLine("Client listener thread has started...");
                Debug.WriteLine("Client listener thread has started...");

                while (true)
                {
                    /* Block until we have a connection... */
                    TcpClient client = this.tcpListener.AcceptTcpClient();

                    /* Set up a handler thread */
                    //Thread clientThread = new Thread(new ParameterizedThreadStart(ClientHandler));
                    //clientThread.Start(client);
					numBusy++;
					ThreadPool.QueueUserWorkItem(new WaitCallback(ClientHandler), (object)client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(">" + ex.Message);
            }
        }

        public void ShutDown()
        {
            if (listenThread != null)
            {
                listenThread.Interrupt();
                listenThread.Abort();
				DoneEvent.WaitOne();
            }
        }

        private void ClientHandler(object client)
        {
            Console.WriteLine("Client handler thread started...");

            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();
            ASCIIEncoding encoder = new ASCIIEncoding();
            StringBuilder RequestLines = new StringBuilder();

            byte[] message = new byte[4096];
            int bytesRead = 0;

            while (encoder.GetString(message, 0, bytesRead).IndexOf("\r\n\r\n") == -1 && encoder.GetString(message, 0, bytesRead).IndexOf("\n\n") == -1)
            {
                bytesRead = 0;

                try
                {
                    /* Wait for message... */
                    bytesRead = clientStream.Read(message, 0, 4096);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Socket Error");
                    Debug.WriteLine("Socket error: " + ex.Message);
                    clientStream.Close();
                    tcpClient.Close();
                    break;
                }

                if (bytesRead == 0)
                {
                    /* Disconnected */
                    clientStream.Close();
                    tcpClient.Close();
                    return;
                }

                /* Message received, was ist das?! */
                RequestLines.Append(encoder.GetString(message, 0, bytesRead));
            }

            /* Go through the lines and gather request info */
            HttpRequestInfo theRequest = new HttpRequestInfo();

            string fullRequ = RequestLines.ToString();
            string[] lines = fullRequ.Split('\n');

            /* Get our method */
            if (lines.Length > 0)
            {
                /* Only GET for now... */
                if(lines[0].IndexOf(HttpMethod.GET) > -1)
                {
                    theRequest.method = HttpMethod.GET;

                    string[] words = lines[0].Split(' ');

                    /* Store the requested file */
                    if (words.Length > 1)
                    {
                        theRequest.file = words[1];
                    }
                }
            }

            /* Process any other directives here */
            for (int i = 1; i < lines.Length; i++)
            {
                int dirEnd = lines[i].IndexOf(": ");

                if (dirEnd > 1)
                {
                    theRequest.directives.Add(lines[i].Substring(0, dirEnd), lines[i].Substring(dirEnd + 2));
                }

            }

            if (!handleRequest(clientStream, theRequest))
            {
                Console.WriteLine("Error handling request for " + theRequest.file);
            }

            clientStream.Close();
            tcpClient.Close();
            theRequest = null;
			
			if (Interlocked.Decrement(ref numBusy) == 0)
			{
			    DoneEvent.Set();
			}
			
            Console.WriteLine("Client handler thread exiting...");
        }


        private bool handleRequest(NetworkStream clientStream, HttpRequestInfo theRequest)
        {
            CallbackSys.RunHook("start.request", ref theRequest);

            /* What are we doing here? */
            switch (theRequest.method)
            {
                /* First, let us check to see if we can find the file */
                case HttpMethod.GET:
                    
                    /* Build list of files to look for if the request was for a directory */
                    theRequest.file = HttpUtility.UrlDecode(theRequest.file);
                    Console.WriteLine("Decoded: " + theRequest.file);
                    if (theRequest.file[theRequest.file.Length - 1] == '/')
                    {
                        string[] indexFiles = config.directive["DirectoryIndex"].Split(' ');
                        string realPath = config.directive["DocumentRoot"];
                        bool found = false;
                        bool isDirectory = false;

                        realPath = realPath.Replace('\\', '/');

                        if (Directory.Exists(realPath))
                        {
                            realPath = realPath + theRequest.file.Substring(1);
                            isDirectory = true;
                        }

                        /* Make sure root / works */
                        if (theRequest.file == "/")
                            isDirectory = false;

                        foreach (string file in indexFiles)
                        {
                            if (File.Exists(realPath + file.Trim()))
                            {
                                realPath += file.Trim();
                                theRequest.file = file.Trim();
                                found = true;
                                break;
                            }
                        }

                        /* Send the page */
                        if (found)
                        {
                            FileInfo fInfo = new FileInfo(realPath);
                            Stream fileStream = File.OpenRead(realPath);
                            byte[] buf = new byte[fInfo.Length];

                            /* Read up the file that was requested */
                            int offset = 0;
                            long remaining = fInfo.Length;
                            while (remaining > 0)
                            {                           
                                int read = fileStream.Read(buf, offset, buf.Length);
                                if (read <= 0)
                                    throw new EndOfStreamException
                                        (String.Format("End of stream reached with {0} bytes left to read", remaining));
                                remaining -= read;
                                offset += read;
                            }

                            theRequest.rawFile = buf;
							theRequest.finalizedFile = theRequest.rawFile;

                            /* Call any modules that may need to process the raw file */
							Console.WriteLine("\tCalling callback hooks...");
                            CallbackSys.RunHook("request.rawfile.process", ref theRequest);


                            theRequest.mimeType = mimeTypes.GetMimeType(theRequest.file.Substring(theRequest.file.LastIndexOf('.') + 1));

                            if (theRequest.finalizedFile.Length == 0)
                            {
                                SendHeaders(clientStream, HttpStatus.OKAY, buf.Length, theRequest.mimeType);
                                clientStream.Write(buf, 0, buf.Length);
                            }
                            else
                            {
                                SendHeaders(clientStream, theRequest.status, theRequest.finalizedFile.Length, theRequest.mimeType);
                                clientStream.Write(theRequest.finalizedFile, 0, theRequest.finalizedFile.Length);
                            }

                            /* 
                             * If a module processed the file read in above and 
                             * ran it to generate HTML (like php) the plugin is 
                             * responsible for serving that to the client as all
                             * we do is serve the content directly
                             * /
                            if (!theRequest.skipBuiltinServe)
                            {
                                clientStream.Write(buf, 0, buf.Length);
                            }
                            else
                            {

                            }


                            
                            
                            //string mimeType = mimeTypes.GetMimeType(theRequest.file.Substring(theRequest.file.LastIndexOf('.') + 1));

                            //SendHeaders(clientStream, HttpStatus.OKAY, fInfo.Length, mimeType);

                            /* Read file in and output it to client *
                            using (FileStream fs = fInfo.OpenRead())
                            {
                                byte[] buf = new byte[1024];
                                int? bytesRead = null;
                                while (bytesRead != 0)
                                {
                                    bytesRead = fs.Read(buf, 0, 1024);
                                    clientStream.Write(buf, 0, buf.Length);
                                }
                            }*/

                            clientStream.Flush();
                        }
                        else if(isDirectory)
                        {
                            /* Attempt to list the directory */
                            SendHeaders(clientStream, HttpStatus.OKAY, "text/html");
                            ASCIIEncoding encoder = new ASCIIEncoding();
                            byte[] data = encoder.GetBytes(ShowDirectory(theRequest.file));
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush();
                        }
                        else
                        {
                            /* Throw a 404 up */
                            SendHeaders(clientStream, HttpStatus.FILENOTFOUND, "text/html");
                            ASCIIEncoding encoder = new ASCIIEncoding();
                            byte[] data = encoder.GetBytes("<h1>Not found</h1>" + "<p>The request " + theRequest.file + " was not found on this server.</p>\n");
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush();
                        }
                    }
                    else // Direct file request
                    {
                        if (theRequest.file[0] == '/')
                        {
                            theRequest.file = theRequest.file.Substring(1);
                        }
                        string realPath = config.directive["DocumentRoot"] + theRequest.file.Trim();
                        bool found = false;
                        bool isDirectory = false;

                        realPath = realPath.Replace('\\', '/');

                        if (Directory.Exists(realPath))
                        {
                            realPath = realPath + theRequest.file;
                            theRequest.file = "/" + theRequest.file;
                            isDirectory = true;
                        }
                        
                        if (File.Exists(realPath))
                        {
                            found = true;
                            Console.WriteLine("File found! " + realPath);
                        }

                        /* Send the page */
                        if (found)
                        {
                            FileInfo fInfo = new FileInfo(realPath);
							byte[] buf = new byte[fInfo.Length];

                            try
                            {
                                theRequest.mimeType = mimeTypes.GetMimeType(theRequest.file.Substring(theRequest.file.LastIndexOf('.') + 1));

                                //SendHeaders(clientStream, HttpStatus.OKAY, fInfo.Length, mimeType);

                                /* Read file in and output it to client *
                                using (FileStream fs = fInfo.OpenRead())
                                {
                                    byte[] buf = new byte[1024];
                                    int? bytesRead = null;
                                    while (bytesRead != 0)
                                    {
                                        bytesRead = fs.Read(buf, 0, 1024);
                                        clientStream.Write(buf, 0, buf.Length);
                                    }
                                }*/
							
								using(FileStream fileStream = fInfo.OpenRead())
								{
								    /* Read up the file that was requested */
		                            int offset = 0;
		                            long remaining = fInfo.Length;
		                            while (remaining > 0)
		                            {                           
		                                int read = fileStream.Read(buf, offset, buf.Length);
		                                if (read <= 0)
		                                    throw new EndOfStreamException
		                                        (String.Format("End of stream reached with {0} bytes left to read", remaining));
		                                remaining -= read;
		                                offset += read;
		                            }
		
		                            theRequest.rawFile = buf;
									theRequest.finalizedFile = theRequest.rawFile;
		
		                            /* Call any modules that may need to process the raw file */
									Console.WriteLine("\tCalling callback hooks...");
		                            CallbackSys.RunHook("request.rawfile.process", ref theRequest);
								
		                            if (theRequest.finalizedFile.Length == 0)
		                            {
		                                SendHeaders(clientStream, HttpStatus.OKAY, buf.Length, theRequest.mimeType);
		                                clientStream.Write(buf, 0, buf.Length);
		                            }
		                            else
		                            {
		                                SendHeaders(clientStream, theRequest.status, theRequest.finalizedFile.Length, theRequest.mimeType);
		                                clientStream.Write(theRequest.finalizedFile, 0, theRequest.finalizedFile.Length);
		                            }
								}
									
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(">>" + ex.Message);
							}
                        }
                        else if (isDirectory)
                        {
                            /* Attempt to list the directory */
                            SendHeaders(clientStream, HttpStatus.OKAY, "text/html");
                            ASCIIEncoding encoder = new ASCIIEncoding();
                            byte[] data = encoder.GetBytes(ShowDirectory(theRequest.file));
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush();
                        }
                        else
                        {
                            /* Throw a 404 up */
                            SendHeaders(clientStream, HttpStatus.FILENOTFOUND, "text/html");
                            ASCIIEncoding encoder = new ASCIIEncoding();
                            byte[] data = encoder.GetBytes("<h1>Not found</h1>" + "<p>The request " + theRequest.file + " was not found on this server.</p>\n");
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush();
                        }
                    }

                    break;

                default:
                    /* Internal Server Error it up! */
                    SendHeaders(clientStream, HttpStatus.INTERNAL_ERROR, "text/html");
                    ASCIIEncoding encodr = new ASCIIEncoding();
                    byte[] data_ = encodr.GetBytes("Internal Server Error\n");
                    clientStream.Write(data_, 0, data_.Length);
                    clientStream.Flush();
                    break;
            }

            //Console.WriteLine("Request ended.");

            return true;
        }

        /// <summary>
        /// Sends HTTP headers out to client
        /// </summary>
        /// <param name="clientStream">Networkstream to client</param>
        /// <param name="status">HTTP Status Code</param>
        /// <param name="length">Content-Length portion of the header for content to be served</param>
        /// <param name="mimeType">MimeType of content to be delivered</param>
        private void SendHeaders(NetworkStream clientStream, int status, long length, string mimeType)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] data = encoder.GetBytes("HTTP/1.1 " + status + " OK\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();

            data = encoder.GetBytes("Server: " + config.directive["ServerName"] + "\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();

            data = encoder.GetBytes("Content-Length: " + length + "\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();

            data = encoder.GetBytes("Content-Type: " + mimeType + "\n\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();
        }

        /// <summary>
        /// Sends HTTP headers out to client
        /// </summary>
        /// <param name="clientStream">Networkstream to client</param>
        /// <param name="status">HTTP Status Code</param>
        /// <param name="mimeType">MimeType of content to be delivered</param>
        private void SendHeaders(NetworkStream clientStream, int status, string mimeType)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            string statusLine = Convert.ToString(status);

            switch (status)
            {
                case HttpStatus.OKAY:
                    statusLine += " OK";
                    break;
                case HttpStatus.FILENOTFOUND:
                    statusLine += " Not Found";
                    break;
                case HttpStatus.INTERNAL_ERROR:
                    statusLine += " Internal Server Error";
                    break;
            }

            byte[] data = encoder.GetBytes("HTTP/1.1 " + statusLine + "\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();

            data = encoder.GetBytes("Server: " + config.directive["ServerName"] + "\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();

            data = encoder.GetBytes("Content-Type: " + mimeType + "\n\n");
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();
        }

        /// <summary>
        /// Builds a directory index page
        /// </summary>
        /// <param name="path">Path to directory to build index for</param>
        /// <returns>HTML list of files in directory</returns>
        private string ShowDirectory(string path)
        {
            string slash = "";
            if (path[path.Length - 1] != '/')
            {
                path += '/';
                slash = path;
            }

            StringBuilder htmlBuilder = new StringBuilder();
            htmlBuilder.Append(
            @"<html>
              <head>
                    <title>Index of " + path + @"</title>
              </head>
              <body>
                <h1>Index of " + path + @"</h1>
              <pre>");

            /* Get info on files in the directory */
            DirectoryInfo dir = new DirectoryInfo(config.directive["DocumentRoot"] + path);
            FileInfo[] fileList = dir.GetFiles();
            DirectoryInfo[] dirList = dir.GetDirectories();

            /* Up a directory link */
            //Console.WriteLine("A:" + config.directive["DocumentRoot"].Substring(0, config.directive["DocumentRoot"].Length - 1));
            //Console.WriteLine("B:" + dir.Parent.FullName);
            if(config.directive["DocumentRoot"].Substring(0,config.directive["DocumentRoot"].Length-1) != (dir.Parent.FullName))
                htmlBuilder.Append("\n<img src=\"/icons/folder.gif\" alt=\"Directory\" /> <a href=\"/" + dir.Parent.Name + "/\">Parent Folder</a>");
            else
                htmlBuilder.Append("\n<img src=\"/icons/folder.gif\" alt=\"Directory\" /> <a href=\"/\">Parent Folder</a>");

            /* Loop through the directories and list them first */
            foreach (DirectoryInfo folder in dirList)
            {
                htmlBuilder.Append("\n<img src=\"/icons/folder.gif\" alt=\"Directory\" /> <a href=\"" + slash + folder.Name + "/\">"    
                    + folder.Name + "</a>");
            }

            /* Loop through the files and list them */
            foreach (FileInfo file in fileList)
            {
                htmlBuilder.Append("\n<img src=\"/icons/file.gif\" alt=\"Directory\" /> <a href=\"" + slash + file.Name + "\">"
                    + file.Name + "</a>");
            }

            htmlBuilder.Append(
            @"</pre>
              <hr /> 
              <div class=""serverStamp"">
                    " + config.directive["ServerName"] + " Port: " + config.directive["Port"] + @"
              </div>
              </body>
              </html>");

            return htmlBuilder.ToString();
        }
    }
}
