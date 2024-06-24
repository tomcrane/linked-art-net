using System.Xml;

namespace PMC
{
    public class DScribeUrlResolver : XmlUrlResolver
    {
        // Required because the DTDs asserted in the DScribe export DOCTYPE elements
        // are not the same names as the DTDs supplied with the export.
        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            if (relativeUri != null)
            {
                var pos = relativeUri.IndexOf("_Sub_Mgt_Group_COLL_ARCH.dtd");
                if(pos > 0)
                {
                    relativeUri = relativeUri.Substring(0, pos) + "_archive-descriptions.dtd";
                }
                else
                {
                    pos = relativeUri.IndexOf("_Authorities.dtd");
                    if (pos > 0)
                    {
                        relativeUri = relativeUri.Substring(0, pos) + "_archive-authorities.dtd";
                    }
                }
            }
            return base.ResolveUri(baseUri, relativeUri);
        }
    }
}
