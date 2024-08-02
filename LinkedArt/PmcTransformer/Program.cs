using PmcTransformer.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace PmcTransformer
{
    internal class Program
    {
        static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true, };

        static void Main(string[] args)
        {
            var root = "C:\\Users\\TomCrane\\Dropbox\\digirati\\PMC\\linked.art\\2024-03-18";
            var archive = root + "\\2024-03-11_archive";
            var library = root + "\\2024-03-11_library";
            var photo_archive = root + "\\2024-03-14_photo-archive";

            // Entities need to be reconciled AFTER we do all three sources.
            // This is where the DB will come in to play.
            if(args.Length == 0 || args[0] == "library")
            {
                StreamReader reader = new StreamReader(library + "\\2024-03-11_library.xml", Encoding.UTF8);
                var xLibrary = XDocument.Load(reader);
                Library.Processor.ProcessLibrary(xLibrary);
            }
            else if (args[0] == "archive")
            {
                Locations.SerialisePlaces();


                var settings = GetSettings();
                XmlReader reader1 = XmlReader.Create(archive + "\\2024-03-11_archive-descriptions.xml", settings);
                XDocument xArchive = XDocument.Load(reader1);
                XmlReader reader2 = XmlReader.Create(archive + "\\2024-03-11_archive-authorities.xml", settings);
                XDocument xAuthorities = XDocument.Load(reader2);
                Archive.Processor.ProcessArchives(xArchive, xAuthorities);

            }
            else if (args[0] == "photo-archive")
            {

            }
            else if (args[0] == "leeds")
            {
                Leeds.Processor.ProcessExamples();
            }
            else
            {
                Console.WriteLine("Don't know what to process.");
            }
        }


        static XmlReaderSettings GetSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = new DScribeUrlResolver() { Credentials = CredentialCache.DefaultCredentials }
            };
        }



    }
}
