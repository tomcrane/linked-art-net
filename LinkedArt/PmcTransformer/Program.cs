using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PmcTransformer.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using PmcTransformer.Reconciliation;
using Microsoft.Extensions.Configuration;

namespace PmcTransformer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json");
            builder.Services.AddSingleton<UlanClient>();
            builder.Services.AddHttpClient<LocClient>(HttpDefaults);
            builder.Services.AddHttpClient<LuxClient>(HttpDefaults);
            builder.Services.AddHttpClient<ViafClient>(HttpDefaults);
            builder.Services.AddHttpClient<WikidataClient>(HttpDefaults);
            builder.Services.AddSingleton<AuthorityService>();
            builder.Services.AddSingleton<Reconciler>();
            builder.Services.AddSingleton<Library.Processor>();
            using IHost host = builder.Build();


            var root = "M:\\Dropbox\\digirati\\PMC\\linked.art\\";
            var libraryDate = "2024-10-22";
            var archiveDate = "2024-03-18";
            var photoDate = "2024-03-18";

            // Entities need to be reconciled AFTER we do all three sources.
            // This is where the DB will come in to play.
            if (args.Length == 0 || args[0] == "library")
            {
                var library = $"{root}{libraryDate}\\{libraryDate}_library";
                StreamReader reader = new($"{library}\\{libraryDate}_library.xml", Encoding.UTF8);
                var xLibrary = XDocument.Load(reader);
                var libraryProcessor = host.Services.GetService<Library.Processor>();
                await libraryProcessor.ProcessLibrary(xLibrary, true);
            }
            else if (args[0] == "archive")
            {
                var archive = $"{root}{archiveDate}\\{archiveDate}_archive";
                Locations.SerialisePlaces();
                var settings = GetSettings();
                XmlReader reader1 = XmlReader.Create($"{archive}\\{archiveDate}_archive-descriptions.xml", settings);
                XDocument xArchive = XDocument.Load(reader1);
                XmlReader reader2 = XmlReader.Create($"{archive}\\{archiveDate}_archive-authorities.xml", settings);
                XDocument xAuthorities = XDocument.Load(reader2);
                Archive.Processor.ProcessArchives(xArchive, xAuthorities);

            }
            else if (args[0] == "photo-archive")
            {
                var photo_archive = $"{root}{photoDate}\\{photoDate}_photo-archive";
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


        static void HttpDefaults(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
        }


    }
}
