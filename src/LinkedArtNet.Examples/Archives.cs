
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Archives
    {
        // https://linked.art/model/archives/


        [LinkedArtExample("object/letter/1")]
        public static HumanMadeObject Obermeyer_1920()
        {
            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/letter/1")
                .WithLabel("Obermeyer 1920")
                .WithClassifiedAs(Aat.Term("300026879", "Letter"));

            obermeyer.IdentifiedBy = [new Name("Obermeyer, Bertha (1920)").AsPrimaryName()];

            obermeyer.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/archive_sfl")
                    .WithLabel("Stieglitz Family Letters")
            ];

            return obermeyer;

        }


        [LinkedArtExample("set/archive_sfl/1")]
        public static Set In_Sub_Series()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/archive_sfl/1")
                .WithLabel("Stieglitz Family Letters")
                .WithClassifiedAs(Aat.ArchivalSubGrouping);

            set.IdentifiedBy = [new Name("Stieglitz Family Letters").AsPrimaryName()];

            set.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/archive_asc")
                    .WithLabel("Alfred Stiegliz Correspondence")
            ];

            return set;
        }


        [LinkedArtExample("set/archive_asc/1")]
        public static Set In_Series()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/archive_asc/1")
                .WithLabel("Alfred Stiegliz Correspondence")
                .WithClassifiedAs(Aat.ArchivalGrouping);

            set.IdentifiedBy = [new Name("Alfred Stiegliz Correspondence").AsPrimaryName()];

            return set;
        }




        [LinkedArtExample("set/archive_sfl/2")]
        public static Set Physical_Hierarchy()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/archive_sfl/2")
                .WithLabel("Stieglitz Family Letters")
                .WithClassifiedAs (Aat.ArchivalSubGrouping);

            set.IdentifiedBy = [ new Name("Stieglitz Family Letters").AsPrimaryName() ];

            set.MembersContainedBy = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/box55")
                    .WithLabel("Archival Box 55")
            ];

            return set;
        }

        [LinkedArtExample("object/letter/3")]
        public static HumanMadeObject Letter_In_Box()
        {
            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/letter/3")
                .WithLabel("Obermeyer 1920");

            obermeyer.IdentifiedBy = [new Name("Obermeyer, Bertha (1920)").AsPrimaryName()];

            obermeyer.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/archive_sfl")
                    .WithLabel("Stieglitz Family Letters")
            ];

            obermeyer.HeldOrSupportedBy = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/box55")
                    .WithLabel("Archival Box 55")
            ];

            return obermeyer;

        }
    }
}
