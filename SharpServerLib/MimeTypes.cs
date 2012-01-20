using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace SharpServerLib
{
    public class InternalMimeRep
    {
        public string ext { get; set; }
        public string type { get; set; }
    }

    public class MimeTypes
    {
        public List<InternalMimeRep> types = new List<InternalMimeRep>();
        public bool loaded = false;

        /// <summary>
        /// Default constructor that looks in current directory for mime.types file.
        /// </summary>
        public MimeTypes()
        {
            FetchMimeTypes("./mime.types");
        }

        /// <summary>
        /// Constructor allowing for the location of mime.types to be specified.
        /// </summary>
        /// <param name="path">Path to mime.types file</param>
        public MimeTypes(string path)
        {
            FetchMimeTypes(path);
        }

        /// <summary>
        /// Fetches the mime.types file and registers the types and extensions
        /// </summary>
        /// <param name="path">Path to mime.types file</param>
        protected void FetchMimeTypes(string path)
        {
            try
            {
                using (StreamReader sr = File.OpenText(path))
                {
                    string input = null;

                    while ((input = sr.ReadLine()) != null)
                    {
                        /* Ignore comments and empty lines */
                        if (input.Length < 1 || input[0] == '#' || input.Length <= 1)
                            continue;

                        /* Convert \t's to spaces for split */
                        input = input.Replace('\t', ' ');
                        
                        /* Attempt to find mime type + extension, if not, ignore type */
                        int indexOfSpace = input.IndexOf(' ');

                        if (indexOfSpace > 0)
                        {
                            string type = input.Substring(0, indexOfSpace);
                            string tmpWords = input.Substring(indexOfSpace).Trim();
                            RegisterMimeType(type, tmpWords);
                        }
                    }

                    sr.Close();
                }
            }
            catch (FileNotFoundException ex)
            {
				Debug.WriteLine(ex.Message);
                loaded = false;
                return;
            }

            loaded = true;
        }

        /// <summary>
        /// Registers a single type with a list of extensions
        /// </summary>
        /// <param name="mime">Actual mime type to be registered</param>
        /// <param name="extensions">List of extensions for the mime type without the period and separated by a single space</param>
        public void RegisterMimeType(string mime, string extensions)
        {
            string[] exts = extensions.Trim().Split(' ');

            /* Make the list of extensions to add to the list of types */
            foreach (string ext in exts)
            {
                InternalMimeRep imr = new InternalMimeRep();
                imr.ext = ext;
                imr.type = mime;
                types.Add(imr);
            }
        }

        /// <summary>
        /// Fetches the mime type for the provided extension
        /// </summary>
        /// <param name="ext">Extension without the period in it</param>
        /// <returns></returns>
        public string GetMimeType(string ext)
        {
            /* Search for the extension */
            foreach (InternalMimeRep imr in types)
            {
                if (imr.ext == ext)
                    return imr.type;
            }

            return null;
        }
    }
}
