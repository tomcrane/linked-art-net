using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PmcAsync
{
    internal class Library
    {
        public Library() 
        {
        }

        public async Task Load()
        {
            var root = "C:\\Users\\TomCrane\\Dropbox\\digirati\\PMC\\linked.art\\2024-03-18";
            var library = root + "\\2024-03-11_library";

            StreamReader reader = new StreamReader(library + "\\2024-03-11_library.xml", Encoding.UTF8);
            var xLibrary = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
        }
    }
}
