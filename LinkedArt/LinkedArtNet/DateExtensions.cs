namespace LinkedArtNet
{
    public static class DateExtensions
    {
        public static LinkedArtDate LastSecondOfDay(this LinkedArtDate ldt)
        {
            if (ldt.Date.HasValue)
            {
                // Only for CE dates for now
                return new LinkedArtDate(ldt.Date.Value.Date.AddDays(1).AddSeconds(-1));
            }
            return ldt;
        }
    }
}
