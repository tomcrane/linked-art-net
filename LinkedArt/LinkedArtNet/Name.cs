namespace LinkedArtNet
{
    public class Name : LinkedArtObject
    {
        public Name() { Type = nameof(Name); }

        public Name(string content) 
        { 
            Type = nameof(Name); 
            Content = content;
        }
    }
}
