namespace LinkedArtNet
{
    public static class DateExtensions
    {
        public static LinkedArtDate LastSecondOfDay(this LinkedArtDate ldt)
        {
            if (ldt.DtOffset.HasValue)
            {
                var dto1 = ldt.DtOffset.Value;
                // Only for CE dates for now
                var dto2 = new DateTimeOffset(dto1.Year, dto1.Month, dto1.Day, dto1.Hour, dto1.Minute, dto1.Second, dto1.Offset);
                return new LinkedArtDate(dto2.AddDays(1).AddSeconds(-1));
            }
            return ldt;
        }
    }
}
