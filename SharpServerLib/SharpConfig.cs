using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace SharpServerLib
{
    public class SharpConfig
    {
        public Dictionary<string, string> directive = new Dictionary<string,string>();
        public bool loaded = false;

        /// <summary>
        /// Base constructor that looks for httpd.conf in current directory
        /// </summary>
        public SharpConfig()
        {
            FetchConf("./httpd.conf");
        }

        /// <summary>
        /// Constructor that looks for httpd.conf in specified directory
        /// </summary>
        /// <param name="path"></param>
        public SharpConfig(string path)
        {
            FetchConf(path);
        }

        /// <summary>
        /// Fetches the httpd.conf file and registers the config keys and their values
        /// </summary>
        /// <param name="path">Path to httpd.conf</param>
        private void FetchConf(string path)
        {
            try
            {
                using (StreamReader sr = File.OpenText(path))
                {
                    string input = null;

                    while ((input = sr.ReadLine()) != null)
                    {
                        /* Ignore comments */
                        if (input.Length < 1 || input[0] == '#' || input.Length <= 1)
                            continue;

                        directive.Add(input.Substring(0, input.IndexOf(' ')).Trim(), input.Substring(input.IndexOf(' ')).Trim());
                    }

                    sr.Close();
                }
            }
            catch (FileNotFoundException ex)
            {
				Debug.WriteLine(ex.Message);
				Console.WriteLine(ex.Message);
                loaded = false;
                return;
            }

            loaded = true;
        }
    }
}
