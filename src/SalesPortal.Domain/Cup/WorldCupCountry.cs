namespace SalesPortal.Domain.Cup
{
    public sealed class WorldCupCountry
    {
        public int? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
    }
}
