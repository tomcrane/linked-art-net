using LinkedArtNet;

namespace PmcTransformer.Helpers
{
    public class Locations
    {
        public static Group PMCGroup;
        public static Place PMCPlace;
        public static Group PMCGroupRef;
        public static Place PMCPlaceRef;

        public static Place? FromRecordValue(string value, bool ignoreNonMatch = false)
        {
            if(string.IsNullOrEmpty(value)) return null;

            if(PlaceDict.ContainsKey(value)) return PlaceDict[value];

            if(!ignoreNonMatch)
            {
                if (!UnmappedPlaces.ContainsKey(value))
                {
                    UnmappedPlaces[value] = 0;
                }
                UnmappedPlaces[value]++;
            }

            return null;
        }

        public static void ShowUnmapped()
        {
            Console.WriteLine("Unmapped places:");
            foreach(var kvp in UnmappedPlaces)
            {
                Console.WriteLine(kvp.Key + ": " + kvp.Value);
            }
            Console.WriteLine();
        }

        static Locations()
        {
            PMCGroup = new Group()
                .WithContext()
                .WithId(Identity.GroupBase + "pmc")
                .WithLabel("Paul Mellon Centre");
            PMCGroup.IdentifiedBy = [new Name("Paul Mellon Centre").AsPrimaryName()];

            PMCGroupRef = new Group()
                .WithId(Identity.GroupBase + "pmc")
                .WithLabel("Paul Mellon Centre");

            PMCPlace = new Place()
                .WithContext()
                .WithId(Identity.PlaceBase + "pmc")
                .WithLabel("Paul Mellon Centre");
            PMCPlace.IdentifiedBy = [new Name("Paul Mellon Centre").AsPrimaryName()];

            PMCPlaceRef = new Place()
                .WithId(Identity.PlaceBase + "pmc")
                .WithLabel("Paul Mellon Centre");

            LibraryStore1 = new Place().WithId(Identity.PlaceBase + "LS1").WithLabel("Library Store 1");
            LibraryOffice = new Place().WithId(Identity.PlaceBase + "LO").WithLabel("Library Office");
            PublicStudyRoom = new Place().WithId(Identity.PlaceBase + "PSR").WithLabel("Public Study Room");
            LibraryStore2 = new Place().WithId(Identity.PlaceBase + "LS2").WithLabel("Library Store 2");
            LibraryAnnex = new Place().WithId(Identity.PlaceBase + "LA").WithLabel("Library Annex");
            DrawingRoom = new Place().WithId(Identity.PlaceBase + "ABDR").WithLabel("Drawing Room");
            Offices22And23 = new Place().WithId(Identity.PlaceBase + "2.2").WithLabel("Offices 2.2 and 2.3");
            ArchivesOffice = new Place().WithId(Identity.PlaceBase + "RCO").WithLabel("Archives Office");
            CollectedArchives = new Place().WithId(Identity.PlaceBase + "COLARC").WithLabel("Collected Archives");
            InstitutionalArchives = new Place().WithId(Identity.PlaceBase + "IA").WithLabel("Institutional Archives");
            StaffLibrary = new Place().WithId(Identity.PlaceBase + "STAFF").WithLabel("Staff Library");

            // No mapping provided
            Unmapped_UO = new Place().WithId(Identity.PlaceBase + "UO").WithLabel("!!! Unmapped_UO");
            Unmapped_QUA = new Place().WithId(Identity.PlaceBase + "QUA").WithLabel("!!! Unmapped_QUA");
            Unmapped_DUP = new Place().WithId(Identity.PlaceBase + "DUP").WithLabel("!!! Unmapped_DUP");            

            PlaceDict[LibraryStore1.Id!.LastPathElement()] = LibraryStore1;
            PlaceDict[LibraryOffice.Id!.LastPathElement()] = LibraryOffice;
            PlaceDict[PublicStudyRoom.Id!.LastPathElement()] = PublicStudyRoom;
            PlaceDict[LibraryStore2.Id!.LastPathElement()] = LibraryStore2;
            PlaceDict[LibraryAnnex.Id!.LastPathElement()] = LibraryAnnex;
            PlaceDict[DrawingRoom.Id!.LastPathElement()] = DrawingRoom;
            PlaceDict[Offices22And23.Id!.LastPathElement()] = Offices22And23;
            PlaceDict[ArchivesOffice.Id!.LastPathElement()] = ArchivesOffice;
            PlaceDict[CollectedArchives.Id!.LastPathElement()] = CollectedArchives;
            PlaceDict[InstitutionalArchives.Id!.LastPathElement()] = InstitutionalArchives;
            PlaceDict[StaffLibrary.Id!.LastPathElement()] = StaffLibrary;

            // No mapping provided
            PlaceDict[Unmapped_UO.Id!.LastPathElement()] = Unmapped_UO;
            PlaceDict[Unmapped_QUA.Id!.LastPathElement()] = Unmapped_QUA;
            PlaceDict[Unmapped_DUP.Id!.LastPathElement()] = Unmapped_DUP;

            // Strings that appear in <class> but are clearly the same as locations above
            PlaceDict["Library Store 2"] = LibraryStore2;
            PlaceDict["library Store"] = LibraryStore2;
            PlaceDict[$"Library{(char)160}Store{(char)160}2"] = LibraryStore2;
            PlaceDict[$"Library{(char)160}Store{(char)160}1"] = LibraryStore1;
            PlaceDict["Office 2.2"] = Offices22And23;

            // Going to invent a place for photo archive
            // EXCLUDE THESE unless corpauthor==Tate Photographic Archive
            PhotoArchive = new Place().WithId(Identity.PlaceBase + "PHOTOGRAPHIC_ARCHIVE").WithLabel("!!! PHOTOGRAPHIC_ARCHIVE");
            PlaceDict["PHOTOGRAPHIC ARCHIVE"] = PhotoArchive;
            PlaceDict["Photo Archive"] = PhotoArchive;

        }

        public static void SerialisePlaces()
        {
            // Only call after writing books to disk;
            // embellish Places with context and partOf

        }

        private static readonly Dictionary<string, Place> PlaceDict = [];

        public static readonly Dictionary<string, int> UnmappedPlaces = [];

        public static Place LibraryStore1;
        public static Place LibraryOffice;
        public static Place PublicStudyRoom;
        public static Place LibraryStore2;
        public static Place LibraryAnnex;
        public static Place DrawingRoom;
        public static Place Offices22And23;
        public static Place ArchivesOffice;
        public static Place CollectedArchives;
        public static Place InstitutionalArchives;
        public static Place StaffLibrary;

        public static Place Unmapped_UO;
        public static Place Unmapped_QUA;
        public static Place Unmapped_DUP;

        // I don't think this is a place in the same way
        // only appears as class, not accloc
        public static Place PhotoArchive;

    }
}
