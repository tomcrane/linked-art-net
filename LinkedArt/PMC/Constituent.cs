using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PMC
{
    public class Constituent
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string AlternateNames { get; set; }
        public string AlphaSort { get; set; }
        public ConstituentType Type { get; set; } 
        public bool PublicAccess { get; set; }
        public string Honorific { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Suffix { get; set; }
        public int DefaultDisplayBioId { get; set; }
        public string DisplayBio { get; set; }
        public string Institution { get; set; }
        public string HeritageE { get; set; }
        public string HeritageF { get; set; }
        public string ULAN { get; set; }
        public string VIAF { get; set; }
        public string LibraryCongress { get; set; }
        public string RKDArtists { get; set; }
        public string WikiData { get; set; }
        public string Oxford { get; set; }
        public string Orcid { get; set; }
    }

    public enum ConstituentType
    {
        NotEntered = 0,
        Individual = 1,
        Institution = 2
    }
}
