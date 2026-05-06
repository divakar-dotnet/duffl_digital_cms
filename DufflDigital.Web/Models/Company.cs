namespace DufflDigital.Web.Models
{
    public class Company
    {
        public int CID { get; set; }
        public string CompanyName { get; set; }
        public bool Status { get; set; }
        public DateTime AddedDate { get; set; }
        public string? AddedBy { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? Radius { get; set; }
        public string? Logo { get; set; }
    }
}