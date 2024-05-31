
namespace PmcTransformer
{
    internal class LibraryPersonName
    {
        public required string Name { get; set; }

        // We will later use this to split people with same name if necessary
        //                dates              role         book-id 
        public Dictionary<string, Dictionary<string, List<string>>> DateBuckets = [];
    }

    internal class NormalisedLibraryPersonName
    {
        public required string Name { get; set; }
        public string? Date { get; set; }

        public Dictionary<string, List<string>> RolesToBooks = [];

    }
}
