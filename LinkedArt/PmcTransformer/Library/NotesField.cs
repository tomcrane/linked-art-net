using LinkedArtNet;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmcTransformer.Library
{
    public class NotesField
    {
        public static void ParseNotesField(
            XElement record,
            LinguisticObject work,
            List<HumanMadeObject> HMOs,
            List<string> hierarchicalPlaces,
            List<string> editionStatementsFromNotes,
            List<string> accessionNumbersFromNotes)
        {
            // notescsvx 
            var notes = record.LibStrings("notescsvx").SingleOrDefault();
            if (notes.HasText())
            {
                var noteParts = notes.Split("||")
                    .Select(p => p.Trim());
                var noteDict = new Dictionary<string, List<string>>();

                string notePattern = @"^\(([A-Z]+)\) (.*)$";
                foreach (var part in noteParts)
                {
                    var partMatch = Regex.Match(part, notePattern);
                    if (partMatch.Success)
                    {
                        var key = partMatch.Groups[1].Value.Trim();
                        noteDict.AddToListForKey(key, partMatch.Groups[2].Value);
                    }
                }
                foreach (var kvp in noteDict)
                {
                    switch (kvp.Key)
                    {
                        case "BIB": // ': Bibliography for this entity.Type: BIBLIOGRAPHY
                        case "REF": // Reference to published descriptions. Type: CITATION
                            AddNotesToObject(work, kvp.Value, Getty.BibliographyStatement);
                            break;


                        case "GEN": // General note.Type: NOTE
                        case "RES": // Additional contributors in note form. Type: NOTE
                        case "DIS": // Dissertation course details. Type: NOTE
                        case "HIS": // Historical note. Type: NOTE
                        case "ADD": // Added entry note. Type: NOTE
                        case "REL": // Relationship with other serials. Type: NOTE
                        case "AUT": // Authority note. Type: NOTE
                        case "CHR": // Chronological (not really?). Type: NOTE
                            AddNotesToObject(work, kvp.Value, Getty.AatType("General note", "300027200"));
                            break;


                        case "COP": // Random notes about this copy. Type: NOTE
                        case "ITE": // Item described. (not really?) Type: NOTE
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.AatType("General note", "300027200"));
                            }
                            break;


                        case "LUG": // Lugt number of the Auction Catalog. e.g. per https://brill.com/display/db/lro?language=en Type: NOTE with display name Future work to investigate auctions as events.
                            AddNotesToObject(work, kvp.Value, Getty.AatType("General note", "300027200"), "Lugt Number");
                            break;

                        case "SEL": // Seller for the Auction. Type: NOTE with display name
                            AddNotesToObject(work, kvp.Value, Getty.AatType("General note", "300027200"), "Seller");
                            break;

                        case "DAT": // Date of the Auction described by this catalog.Type: DATING 
                        case "AUC": // Auction Date, see also DAT. Type: DATING 
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Dating", "300054714"));
                            break;

                        case "ACC": // Accompanying Material. Type: RELATEDMATERIAL
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Related Material", "300444119"));
                            break;

                        case "CON": // Table of Contents for the Work. Type: TABLEOFCONTENTS
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Table of Contents", "300195187"));
                            break;

                        case "PHY": // Physical description or note Type: PHYSDESC
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.AatType("Physical Description", "300435452"));
                            }
                            break;

                        case "PMC": // Note that PMC supported the work. Type: CREDITLINE
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Credit Line", "300026687"));
                            break;

                        case "DON": // Donation. Type: PROVENANCE
                        case "ACD": // Accession date. Type: PROVENANCE
                        case "OWN": // Former Owner of Object. Type: PROVENANCE
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.AatType("Provenance", "300435438"));
                            }
                            break;

                        case "LAN": // Language note. Type: LANGUAGE
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Language", "300435433"));
                            break;

                        case "VER": // Other Versions available. Type: REPRODUCTION
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Reproduction", "300411336"));
                            break;

                        case "BND": // Binding Type: BINDING
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.AatType("Binding", "300055023"));
                            }
                            break;

                        case "IND": // Indexes Note. Type: INDEXING
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Indexing", "300054640"));
                            break;


                        case "WIT": // "With" note, but all are indexes.Type: INDEXING
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.AatType("Indexing", "300054640"));
                            }
                            break;

                        case "PUB": // Publication, Distribution, etc. note. Type PUBLISHING
                            AddNotesToObject(work, kvp.Value, Getty.AatType("Publishing", "300435423"));
                            break;

                        case "NAT": // Nature or Scope of Work. Type: DESCRIPTION
                        case "SUM": //  Summary. Type: DESCRIPTION
                            AddNotesToObject(work, kvp.Value, Getty.Description);
                            break;


                        case "DES": // Description of item.Type: DESCRIPTION
                            foreach (var hmo in HMOs)
                            {
                                AddNotesToObject(hmo, kvp.Value, Getty.Description);
                            }
                            break;


                        // More identifiers:
                        case "TIT": // Alternate Title.  /identified_by [type=Name]/value
                            foreach (var altTtitle in kvp.Value)
                            {
                                work.IdentifiedBy!.Add(new Name(altTtitle));
                            }
                            break;

                        case "SBN": // ISSN or ISBN. /identified_by [type=Identifier, classified_as=ISBN]/value
                            foreach (var sbn in kvp.Value)
                            {
                                work.IdentifiedBy!.Add(new Identifier(sbn).WithClassifiedAs(Getty.ISBN));
                            }
                            break;

                        case "ACN": // HMO! Accession number /identified_by [type=Identifier, classified_as=ACCESSION]/value
                            accessionNumbersFromNotes.AddRange(kvp.Value.Where(v => v.HasText()));
                            break;


                        case "HIE": // Use in preference to place. Hierarchical version of the Place of publication.
                            hierarchicalPlaces.AddRange(kvp.Value.Where(v => v.HasText()));
                            break;


                        case "EDN": // Edition statement. Treat as <edition> 
                            editionStatementsFromNotes.AddRange(kvp.Value.Where(v => v.HasText()));
                            break;

                        // The following note fields are ignored for now
                        case "CIP": // ignore
                        case "AUD": // ignore
                        case "ABS": // ignore
                        case "CHA": // Change of control number, ignore
                        case "HOL": // Holdings, ignore
                        case "RUN": // ignore
                        case "SUB": // ignore
                        case "NUM": // Numbers borne by the item (e.g.auction catalogs) ignore for now
                        case "FRE": // Publication Frequency note for serials. Type: FREQUENCY
                        case "USE": // copyright fee note? Ignore
                        case "BSH": // oversized, no longer used, ignore
                        case "SER": // Series Note. Treat as if in <series>
                        case "BY":  // Edition by. Treat as <edition>
                            break;
                    }

                }
            }
        }

        private static void AddNotesToObject(
            LinkedArtObject thing,
            List<string> notes,
            LinkedArtObject classifier,
            string? label = null)
        {
            foreach (var statement in notes)
            {
                var note = new LinguisticObject()
                    .WithClassifiedAs(classifier)
                    .WithContent(statement);
                thing.ReferredToBy ??= [];
                thing.ReferredToBy.Add(note);

                if (label.HasText())
                {
                    note.IdentifiedBy = [
                        new Name(label).WithClassifiedAs(Getty.DisplayTitle)
                    ];
                }
            }
        }
    }
}
