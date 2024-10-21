using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace PmcTransformer.Helpers
{
    public class Locations
    {
        public static readonly Group PMCGroup;
        public static readonly string PMCName = "Paul Mellon Centre for Studies in British Art";
        public static readonly Place PMCPlace;
        public static readonly Group PMCGroupRef;
        public static readonly Place PMCPlaceRef;

        public static readonly Group PMCLibraryGroup;
        public static readonly Group PMCLibraryGroupRef;
        public static readonly string PMCLibraryName = $"{PMCName} Library";
        public static readonly LinkedArtObject PMCLibrarySet;
        public static readonly LinkedArtObject PMCLibrarySetRef;

        public static readonly Group PMCArchiveGroup;
        public static readonly Group PMCArchiveGroupRef;
        public static readonly string PMCArchiveName = $"{PMCName} Archive";
        public static readonly LinkedArtObject PMCArchiveSet;
        public static readonly LinkedArtObject PMCArchiveSetRef;

        public static readonly Group PhotoArchiveGroup;
        public static readonly Group PhotoArchiveGroupRef;
        public static readonly string PhotoArchiveName = $"{PMCName} photographic archive";
        public static readonly LinkedArtObject PhotoArchiveSet;
        public static readonly LinkedArtObject PhotoArchiveSetRef;

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
            // Set for Library, Archives and Photo Archives
            // Group for each of these too
            // Each Group is member_of PMC
            // Each Set is curated by the group - used for activity classified as curation and carried out by group
            // Each book HMO is a member of the Library set
            // Each archival set and item is a member of the archive set
            // https://lux.collections.yale.edu/data/set/feb70d01-0cfc-4c01-a643-5eec58b311b6

            PMCGroup = new Group()
                .WithContext()
                .WithId(Identity.GroupBase + "pmc")
                .WithLabel("Paul Mellon Centre for Studies in British Art (London)");
            PMCGroup.IdentifiedBy = [
                new Name("Paul Mellon Centre").AsPrimaryName(),
                new Identifier("GB3010")
                    .WithClassifiedAs(Getty.AatType("Unique Identifier", "3004040012")),
            ];
            PMCGroup.IdentifiedBy[1].AssignedBy = [
                new Activity(Types.AttributeAssignment)
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId("http://vocab.getty.edu/ulan/500475815")
                            .WithLabel("The National Archives")
                    ]
                }
            ];
            PMCGroup.Equivalent = [
                new Group()
                    .WithId("https://lux.collections.yale.edu/data/group/cf6f8fdc-feb8-4f61-a787-0240d04c05a8")
            ];

            PMCGroupRef = new Group()
                .WithId(Identity.GroupBase + "pmc")
                .WithLabel("Paul Mellon Centre");


            // Library
            var libraryGroupName = "Library, Paul Mellon Centre";
            var librarySetName = "Library Collection, Paul Mellon Centre";

            var pmcLibrarySlug = "pmc-library";
            PMCLibraryGroup = new Group()
                .WithContext()
                .WithId(Identity.GroupBase + pmcLibrarySlug)
                .WithLabel(libraryGroupName);
            PMCLibraryGroup.IdentifiedBy = [
                new Name(libraryGroupName).AsPrimaryName(),
            ];
            PMCLibraryGroup.PartOf = [PMCGroupRef];
            PMCLibraryGroupRef = new Group()
                .WithId(Identity.GroupBase + pmcLibrarySlug)
                .WithLabel(libraryGroupName);
            PMCLibrarySet = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId(Identity.SetBase + pmcLibrarySlug)
                .WithLabel(librarySetName)
                .WithClassifiedAs(Getty.Collection);
            PMCLibrarySet.IdentifiedBy = [
                new Name(librarySetName).AsPrimaryName(),
            ];
            var libCurating = new Activity()
                .WithLabel("Curation")
                .WithClassifiedAs(Getty.Curating);
            libCurating.CarriedOutBy = [PMCLibraryGroupRef];
            PMCLibrarySet.UsedFor = [libCurating];

            PMCLibrarySetRef = new LinkedArtObject(Types.Set)
                .WithId(Identity.SetBase + pmcLibrarySlug)
                .WithLabel(PMCLibraryName);


            // Archive

            var archiveGroupName = "Archives, Paul Mellon Centre";
            var archiveSetName = "Archive Collections, Paul Mellon Centre";
            var pmcArchiveSlug = "pmc-archive";
            PMCArchiveGroup = new Group()
                .WithContext()
                .WithId(Identity.GroupBase + pmcArchiveSlug)
                .WithLabel(archiveGroupName);
            PMCArchiveGroup.PartOf = [PMCGroupRef];
            PMCArchiveGroup.IdentifiedBy = [
                new Name(archiveGroupName).AsPrimaryName(),
            ];
            PMCArchiveGroupRef = new Group()
                .WithId(Identity.GroupBase + pmcArchiveSlug)
                .WithLabel(archiveGroupName);
            PMCArchiveSet = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId(Identity.SetBase + pmcArchiveSlug)
                .WithLabel(archiveSetName)
                .WithClassifiedAs(Getty.Collection);
            PMCArchiveSet.IdentifiedBy = [
                new Name(archiveSetName).AsPrimaryName()
            ];
            var archiveCurating = new Activity()
                .WithLabel("Curation")
                .WithClassifiedAs(Getty.Curating);
            archiveCurating.CarriedOutBy = [PMCArchiveGroupRef];
            PMCArchiveSet.UsedFor = [archiveCurating];

            PMCArchiveSetRef = new LinkedArtObject(Types.Set)
                .WithId(Identity.SetBase + pmcArchiveSlug)
                .WithLabel(archiveSetName);



            // Photo Archive

            var photoArchiveGroupName = "Photographic Archive, Paul Mellon Centre";
            var photoArchiveSetName = "Paul Mellon Centre Photographic Archive, Paul Mellon Centre";
            var pmcPhotoArchiveSlug = "pmc-photo-archive";
            PhotoArchiveGroup = new Group()
                .WithContext()
                .WithId(Identity.GroupBase + pmcPhotoArchiveSlug)
                .WithLabel(photoArchiveGroupName);
            PhotoArchiveGroup.IdentifiedBy = [
                new Name(photoArchiveGroupName).AsPrimaryName()
            ];
            PhotoArchiveGroup.PartOf = [PMCGroupRef];
            PhotoArchiveGroupRef = new Group()
                .WithId(Identity.GroupBase + pmcPhotoArchiveSlug)
                .WithLabel(photoArchiveGroupName);
            PhotoArchiveSet = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId(Identity.SetBase + pmcPhotoArchiveSlug)
                .WithLabel(photoArchiveSetName)
                .WithClassifiedAs(Getty.Collection);
            PhotoArchiveSet.IdentifiedBy = [
                new Name(photoArchiveSetName).AsPrimaryName()
            ];
            var photoArchiveCurating = new Activity()
                .WithLabel("Curation")
                .WithClassifiedAs(Getty.Curating);
            photoArchiveCurating.CarriedOutBy = [PhotoArchiveGroupRef];
            PhotoArchiveSet.UsedFor = [photoArchiveCurating];

            PhotoArchiveSetRef = new LinkedArtObject(Types.Set)
                .WithId(Identity.SetBase + pmcPhotoArchiveSlug)
                .WithLabel(Identity.SetBase + pmcPhotoArchiveSlug);

            // Places
            PMCPlace = new Place()
                .WithContext()
                .WithId(Identity.PlaceBase + "pmc")
                .WithLabel("Paul Mellon Centre");
            PMCPlace.IdentifiedBy = [
                new Name("Paul Mellon Centre").AsPrimaryName()
            ];

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
            PlaceDict["Library Store 1"] = LibraryStore1;
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
            Writer.WriteToDisk(PMCGroup);
            Writer.WriteToDisk(PMCPlace);

            Writer.WriteToDisk(PMCLibraryGroup);
            Writer.WriteToDisk(PMCLibrarySet);

            Writer.WriteToDisk(PMCArchiveGroup);
            Writer.WriteToDisk(PMCArchiveSet);

            Writer.WriteToDisk(PhotoArchiveGroup);
            Writer.WriteToDisk(PhotoArchiveSet);

            foreach(var place in PlaceDict.Values.DistinctBy(x => x.Id))
            {
                var diskPlace = new Place()
                    .WithContext()
                    .WithId(place.Id)
                    .WithLabel(place.Label);
                diskPlace.PartOf = [PMCPlace];
                Writer.WriteToDisk (diskPlace);
            }
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
