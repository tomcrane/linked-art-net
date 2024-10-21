using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace PmcTransformer.Archive
{
    public static class Helpers
    {
        static readonly TimespanParser timespanParser = new();

        public static readonly Dictionary<string, int> thumbnailTextCounts = [];

        public static IEnumerable<string> ArcStrings(this XElement record, string field)
        {
            return record.Elements(field)
                .Select(el => el.Value)
                .Where(s => s.HasText());
        }

        public static void SetClassifiedAs(string? level, LinkedArtObject? laSet, HumanMadeObject? laItem)
        {
            // All non HMO levels should have archive. Then can have additional ones.
            laSet?.WithClassifiedAs(Getty.Archives);
            switch (level)
            {
                case "Item":
                    laItem!.WithClassifiedAs(Getty.ArchivalMaterials);
                    break;
                case "File":
                case "file":  // see BS/4/7/3
                case " File": // see BS/5/1/852
                case "File ": // see NWS/1/5
                    laItem!.WithClassifiedAs(Getty.ArchivalMaterials);
                    break;
                case "Sub-Sub Series":
                    // no specific classification
                    break;
                case "Sub-Series":
                    laSet!.WithClassifiedAs(Getty.ArchivalSubGrouping);
                    break;
                case "Series":
                    laSet!.WithClassifiedAs(Getty.ArchivalGrouping);
                    break;
                case "Sub-Collection":
                    // no specific classification
                    break;
                case "Collection":
                    laSet!.WithClassifiedAs(Getty.Collection);
                    break;

                default:
                    throw new Exception("Unknown level " + level);
            }
        }

        public static bool ShouldSkipRecord(XElement record)
        {
            var mgtSubGroup = record.ArcStrings("MgtSubGroup").SingleOrDefault();
            if (mgtSubGroup == "INSTITUTIONAL RECORDS")
            {
                return true; // 235 instances of this at time of writing
            }

            var accessStatus = record.ArcStrings("AccessStatus").SingleOrDefault();
            if (accessStatus != "Open")
            {
                return true; // 0 instances of this at time of writing
            }

            var catalogueStatus = record.ArcStrings("CatalogueStatus").SingleOrDefault();
            if (catalogueStatus != "Catalogued")
            {
                return true; // 78 instances of this at time of writing
            }

            var level = record.ArcStrings("Level").SingleOrDefault();
            if (string.IsNullOrWhiteSpace(level))
            {
                return true; // 2 instances of this at time of writing
                // DAVETEST1
                // QJTH/1
            }

            var refNo = record.ArcStrings("RefNo").Single();
            {
                if (refNo.StartsWith("PMC"))
                {
                    // All but two of these will have already been picked up by the
                    // MgtSubGroup analysis - but PMC7/18 and PMC35/2/2/23 are missing this.
                    return true;
                }
            }


            return false;
        }

        public static void ProcessAltRefNo(XElement record, LinkedArtObject laObj)
        {

            var altRef = record.ArcStrings("AltRefNo").SingleOrDefault();
            if (string.IsNullOrWhiteSpace(altRef))
            {
                return;
            }

            string sPattern = @"^S[0-9]*$";
            var sMatch = Regex.Match(altRef, sPattern, RegexOptions.IgnoreCase);
            if (sMatch.Success)
            {
                laObj.AttributedBy ??= [];
                laObj.AttributedBy.Add(
                    new Activity(Types.AttributeAssignment)
                    {
                        IdentifiedBy = [
                            new Name("Related Library Record")
                                .WithClassifiedAs(Getty.DisplayTitle, Getty.BriefText)
                        ],
                        Assigned = [
                            new LinguisticObject()
                                .WithId($"{Identity.LibraryLinguistic}{altRef.ToUpperInvariant()}")
                        ]
                    }
                );
                return;
            }

            string platePattern = @"Plate[^\s]*\s(.*)";
            var pMatch = Regex.Match(altRef, platePattern, RegexOptions.IgnoreCase);
            if (pMatch.Success)
            {
                laObj.ReferredToBy ??= [];
                laObj.ReferredToBy.Add(
                    new LinguisticObject()
                        .WithClassifiedAs(Getty.RelatedMaterial, Getty.BriefText)
                        .WithContent(altRef)
                );
            }

            return;
        }

        public static (string sortRefNo, string? parentRefNo) GetRefNoVariants(string refNo)
        {
            string sortRefNo = refNo;
            string? parentRefNo = null;
            var parts = refNo.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts[^1].All(char.IsDigit))
            {
                parts[^1] = parts[^1].PadLeft(6, '0');
                sortRefNo = string.Join("/", parts);
            }
            if (parts.Length > 1)
            {
                parentRefNo = string.Join("/", parts[0..^1]);
            }
            // Console.WriteLine($"{refNo} => {parentRefNo}, {sortRefNo}");
            return (sortRefNo, parentRefNo);
        }

        public static void ProcessDate(string? dateField, LinkedArtObject laObj)
        {
            // 1050 records have no Date
            if (dateField != null)
            {
                var timespan = timespanParser.Parse(dateField);
                if (timespan != null)
                {
                    var hmo = laObj as HumanMadeObject;
                    if (hmo != null)
                    {
                        hmo.ProducedBy = new Activity(Types.Production) { TimeSpan = timespan };
                    }
                    else
                    {
                        laObj.MembersExemplifiedBy = [
                            new HumanMadeObject { ProducedBy = new Activity(Types.Production) { TimeSpan = timespan } }
                        ];
                    }
                }
            }
        }

        public static void SimpleStatement(XElement record, LinkedArtObject laObj, string field, LinkedArtObject classifiedAs)
        {
            var statement = record.ArcStrings(field).SingleOrDefault();
            SimpleStatement(statement, laObj, classifiedAs);
        }


        public static void SimpleStatement(string? statement, LinkedArtObject laObj, LinkedArtObject classifiedAs)
        {
            if (statement != null)
            {
                laObj.ReferredToBy ??= [];
                laObj.ReferredToBy.Add(
                    new LinguisticObject()
                        .WithClassifiedAs(classifiedAs, Getty.BriefText)
                        .WithContent(statement)
                );
            }
        }

        public static void ProcessDescription(XElement record, LinkedArtObject laObj)
        {
            SimpleStatement(record, laObj, "Description", Getty.Description);
            /* TODO
             * Parse for “stock number (\w*)$” - see FHS/3/1/1 
      	        – /about = [HMO id=http://getty.edu/tbc/{stockNumber}]
	            Also stock numbers and variations of ; , etc
                Use authority file to obtain ref to artist and include in label
                STRETCH GOAL - separate HMO for artworks with creator 
            */

        }

        internal static void ProcessThumbnailAndDescription(XElement record, LinkedArtObject laObj)
        {
            /*
             * 	-- /representation/ per linked art: https://linked.art/model/digital/#digital-images 
                    UserText1 = caption for thumbnail
                    -- referred_to_by[classified_as=DESCRIPTION]/content on the DigitalObject
                    Example: FHS/3/1/278

                    Could be multiple thumbnail elements. might not be the same number of usertext1 
                    elems, if so, just assign in order and the last ones miss out

                    TC - often there is one UserText1 for two thumbs where the thumbs are recto and verso
                    ...but this can be mixed in with a larger number of thumbs.
                    Try to pick out this pattern where possible.
            */

            var thumbs = record.ArcStrings("Thumbnail").ToList();
            if (thumbs.Count == 0)
            {
                return;
            }

            var texts = record.ArcStrings("UserText1").ToList();
            if(texts.Count == 0)
            {
                thumbnailTextCounts.IncrementCounter("singleText");
                for (var i = 0; i < thumbs.Count; i++)
                {
                    AddVisualItem(laObj, null, FormatThumbnailUrl(thumbs[i]));
                }
            }
            else if(texts.Count == 1)
            {
                thumbnailTextCounts.IncrementCounter("singleText");
                for (var i = 0; i < thumbs.Count; i++)
                {
                    AddVisualItem(laObj, texts[0], FormatThumbnailUrl(thumbs[i]));
                }
            }
            else if (texts.Count == thumbs.Count)
            {
                // pair the image and texts...
                thumbnailTextCounts.IncrementCounter("matchCounts");
                for (var i = 0; i < texts.Count; i++)
                {
                    AddVisualItem(laObj, texts[i], FormatThumbnailUrl(thumbs[i]));
                }
            }
            //else if (thumbs.Count == texts.Count * 2)
            //{
            //    thumbnailTextCounts.IncrementCounter("doubleCounts");
            //    for (var i = 0; i < texts.Count; i++)
            //    {
            //        AddVisualItem(laObj, texts[i / 2], FormatThumbnailUrl(thumbs[i]));
            //    }
            //}
            //else
            //{
            //    if(texts.Count < thumbs.Count)
            //    {
            //        thumbnailTextCounts.IncrementCounter("notEnoughTextsForThumbs");
            //    }
            //    // both of these dicts have the thumb index
            //    var matchedTexts = new Dictionary<int, string>();
            //    var pairs = new Dictionary<int, Tuple<string, string>>();
            //    var singletons = new Dictionary<int, string>();
            //    for (var i = 0; i < thumbs.Count; i++)
            //    {
            //        if (i > 0 && AreRectoAndVerso(thumbs[i - 1], thumbs[i]))
            //        {
            //            pairs[i - 1] = new(thumbs[i - 1], thumbs[i]);
            //        }
            //    }
            //    for (var i = 0; i < thumbs.Count; i++)
            //    {
            //        // fill in all the blanks
            //        if (!pairs.Values.Any(pair => pair.Item1 == thumbs[i] || pair.Item2 == thumbs[i]))
            //        {
            //            singletons[i] = thumbs[i];
            //        }
            //    }
            //    var newTotal = pairs.Count * 2 + singletons.Count;
            //    if(thumbs.Count == newTotal)
            //    {
            //        thumbnailTextCounts.IncrementCounter("matchesAfterPairing");
            //    }
            //    else
            //    {
            //        thumbnailTextCounts.IncrementCounter("stillNoMatchAfterPairing"); // yay 0!
            //    }
            //    int textCounter = 0;
            //    for (var i = 0; i < thumbs.Count; i++)
            //    {
            //        if (singletons.ContainsKey(i))
            //        {
            //            AddVisualItem(laObj, GetTextFromListOrLast(texts, textCounter++), FormatThumbnailUrl(thumbs[i]));
            //        }
            //        else
            //        {
            //            // a pair
            //            AddVisualItem(laObj, GetTextFromListOrLast(texts, textCounter), FormatThumbnailUrl(thumbs[i]));
            //            AddVisualItem(laObj, GetTextFromListOrLast(texts, textCounter++), FormatThumbnailUrl(thumbs[++i]));
            //        }
            //    }
            //}
        }

        private static string GetTextFromListOrLast(List<string> texts, int index)
        {
            if(index < texts.Count)
            {
                return texts[index];
            }
            return texts[^1];
        }

        private static bool AreRectoAndVerso(string s1, string s2)
        {
            // true if:
            // "..." and "..._r..."  diff is additional "_r"
            // "..." and "..._v..."  diff is additional "_v"
            // "..._r..." and "..._v..."  diff is "_r" <=> "_v"

            // This wants a cleverer algorithm, this is not an exact match and might be thrown
            if(s1.Length == s2.Length)
            {
                if(s1.Replace("_r", "**").Replace("_v", "**") == s2.Replace("_r", "**").Replace("_v", "**"))
                {
                    return true;
                }
            }
            if (Math.Abs(s1.Length - s2.Length) == 2)
            {
                var inOrder = (new string[] { s1, s2 }).OrderBy(s => s.Length).ToList();
                if (inOrder[0] == inOrder[1].ReplaceFirst("_r", ""))
                {
                    return true;
                }
                if (inOrder[0] == inOrder[1].ReplaceFirst("_v", ""))
                {
                    return true;
                }
            }

            return false;
        }
        

        private static string FormatThumbnailUrl(string recordValue)
        {
            const string thumbPrefix = "https://calmview.co.uk/PaulMellonCentre/CalmView/GetImage.ashx?db=Catalog&type=default&fname=";

            // record string:         LBN_1_4_Entry for 8 &amp; 9 January, visit to London.jpg
            // to be transformed to:  LBN_1_4_Entry+for+8+%26+9+January%2c+visit+to+London.jpg
            //                        LBN_1_4_Entry+for+8+%26+9+January%2c+visit+to+London.jpg
            // But:
            // HttpUtility.UrlEncode("LBN_1_4_Entry for 8 &amp; 9 January, visit to London.jpg")
            // =>                     LBN_1_4_Entry+for+8+%26amp%3b+9+January%2c+visit+to+London.jpg

            var htmlDecoded = HttpUtility.HtmlDecode(recordValue);
            var urlEncoded = HttpUtility.UrlEncode(htmlDecoded);
            return thumbPrefix + urlEncoded;
        }

        private static void AddVisualItem(LinkedArtObject laObj, string? label, string imageUrl)
        {
            var visual = new Work(Types.VisualItem);
            var digitalImage = new DigitalObject()
                .WithClassifiedAs(Getty.DigitalImage);
            digitalImage.Format = "image/jpeg";
            if (label.HasText())
            {
                digitalImage.ReferredToBy = [
                    new LinguisticObject()
                        .WithClassifiedAs(Getty.Description, Getty.BriefText)
                        .WithContent(label)
                ];
            }
            digitalImage.AccessPoint = [
                new DigitalObject().WithId(imageUrl)
            ];
            visual.DigitallyShownBy = [digitalImage];
            laObj.Representation ??= [];
            laObj.Representation.Add(visual);
        }

        public static Actor GetReferenceObject(this Actor actor, bool withId = true)
        {
            // Should have a general one of these
            Actor refActor = actor is Person ? new Person() : new LinkedArtNet.Group();
            if(withId)
            {
                refActor.Id = actor.Id;
            }
            return refActor;
        }
    }
}
