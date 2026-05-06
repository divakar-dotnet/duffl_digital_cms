namespace DufflDigital.Web.Models
{
    public class AhhfficeUser
    {
        public int UID { get; set; }
        public string EmpID { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string EmailID { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public bool Status { get; set; }
        public string? AddBy { get; set; }
        public int CompanyID { get; set; }

        // For display in list — joined from tblEmpDetails
        public string? FullName { get; set; }
        public string? EmpLoginID { get; set; }
        public string? CompanyName { get; set; }
    }

    public class AhhfficeUserFormViewModel
    {
        public AhhfficeUser User { get; set; } = new AhhfficeUser();
        public IEnumerable<Company> Companies { get; set; } = new List<Company>();
        public IEnumerable<EmpDropdownItem> Employees { get; set; } = new List<EmpDropdownItem>();
    }

    public class EmpDropdownItem
    {
        public string EmpID { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // "DE101 - John"
    }
}