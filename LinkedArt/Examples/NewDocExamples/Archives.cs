
using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Archives
    {
        // https://linked.art/model/archives/

        public static void Create()
        {
            Obermeyer_1920();
            In_Sub_Series();
            In_Series();
            Ordering();
            Physical_Hierarchy();
            Letter_In_Box();
        }


        private static void Obermeyer_1920()
        {
            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/letter/1")
                .WithLabel("Obermeyer 1920")
                .WithClassifiedAs(Getty.Letter);

            obermeyer.IdentifiedBy = [new Name("Obermeyer, Bertha (1920)").AsPrimaryName()];

            obermeyer.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/archive_sfl")
                    .WithLabel("Stieglitz Family Letters")
            ];

            Documentation.Save(obermeyer);    

        }


        private static void In_Sub_Series()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/archive_sfl/1")
                .WithLabel("Stieglitz Family Letters")
                .WithClassifiedAs(Getty.ArchivalSubGrouping);

            set.IdentifiedBy = [new Name("Stieglitz Family Letters").AsPrimaryName()];

            set.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/archive_asc")
                    .WithLabel("Alfred Stiegliz Correspondence")
            ];

            Documentation.Save(set);
        }


        private static void In_Series()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/archive_asc/1")
                .WithLabel("Alfred Stiegliz Correspondence")
                .WithClassifiedAs(Getty.ArchivalGrouping);

            set.IdentifiedBy = [new Name("Alfred Stiegliz Correspondence").AsPrimaryName()];

            Documentation.Save(set);
        }


        private static void Ordering()
        {
            var parent = new LinkedArtObject(Types.Set)
                .WithId($"{Documentation.IdRoot}/set/archive_sfl")
                .WithLabel("Stieglitz Family Letters");

            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/letter/2")
                .WithLabel("Obermeyer 1920");

            obermeyer.IdentifiedBy = [
                new Name("Obermeyer, Bertha (1920)").AsPrimaryName(),
                Identifier.SortValue("000001", parent)
            ];

            obermeyer.MemberOf = [ parent ];

            Documentation.Save(obermeyer);
        }


        private static void Physical_Hierarchy()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/archive_sfl/2")
                .WithLabel("Stieglitz Family Letters")
                .WithClassifiedAs (Getty.ArchivalSubGrouping);

            set.IdentifiedBy = [ new Name("Stieglitz Family Letters").AsPrimaryName() ];

            set.MembersContainedBy = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/box55")
                    .WithLabel("Archival Box 55")
            ];

            Documentation.Save (set);
        }

        private static void Letter_In_Box()
        {
            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/letter/3")
                .WithLabel("Obermeyer 1920");

            obermeyer.IdentifiedBy = [new Name("Obermeyer, Bertha (1920)").AsPrimaryName()];

            obermeyer.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/archive_sfl")
                    .WithLabel("Stieglitz Family Letters")
            ];

            obermeyer.HeldOrSupportedBy = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/box55")
                    .WithLabel("Archival Box 55")
            ];

            Documentation.Save(obermeyer);

        }
    }
}
