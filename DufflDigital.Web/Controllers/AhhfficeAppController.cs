using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using DufflDigital.Web.Models;
using System.Data;
using System.Text;

namespace DufflDigital.Web.Controllers
{
    public class AhhfficeAppController : Controller
    {
        private readonly string _ahhfficeConn;

        public AhhfficeAppController(IConfiguration config)
        {
            _ahhfficeConn = config.GetConnectionString("AhhfficeDb")!;
        }

        // ── Session Guard helper ──────────────────────────────
        private bool IsLoggedIn() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("UserName"));

        // ── Module Landing Page ───────────────────────────────
        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            return View();
        }

        // ════════════════════════════════════════════════════════
        // COMPANY SECTION
        // ════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Companies()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            var companies = await db.QueryAsync<Company>(
                "SELECT * FROM tblCompanyList ORDER BY CID DESC");
            return View(companies);
        }

        [HttpGet]
        public async Task<IActionResult> Manage(int id = 0)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            if (id == 0) return View(new Company { Status = true });

            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            var company = await db.QueryFirstOrDefaultAsync<Company>(
                "SELECT * FROM tblCompanyList WHERE CID = @Id", new { Id = id });
            return View(company ?? new Company { Status = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Company model)
        {
            using IDbConnection db = new SqlConnection(_ahhfficeConn);

            if (model.CID == 0)
            {
                const string sql = @"
                    INSERT INTO tblCompanyList
                        (CompanyName, Status, AddedDate, AddedBy,
                         Latitude, Longitude, Radius, Logo)
                    VALUES
                        (@CompanyName, @Status, GETDATE(), @AddedBy,
                         @Latitude, @Longitude, @Radius, @Logo)";
                model.AddedBy = HttpContext.Session.GetString("UserName");
                await db.ExecuteAsync(sql, model);
            }
            else
            {
                const string sql = @"
                    UPDATE tblCompanyList SET
                        CompanyName = @CompanyName, Status    = @Status,
                        Latitude    = @Latitude,    Longitude = @Longitude,
                        Radius      = @Radius,      Logo      = @Logo
                    WHERE CID = @CID";
                await db.ExecuteAsync(sql, model);
            }
            return RedirectToAction("Companies");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            await db.ExecuteAsync(
                "DELETE FROM tblCompanyList WHERE CID = @Id", new { Id = id });
            return RedirectToAction("Companies");
        }

        // ════════════════════════════════════════════════════════
        // USER SECTION
        // ════════════════════════════════════════════════════════

        // ── User List ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            // Join to get employee name and company name for display
            const string sql = @"
                SELECT u.UID, u.EmpID, u.Role, u.EmailID, u.MobileNo,
                       u.Username, u.Status, u.CompanyID,
                       u.AddDate, u.Updatedate,
                       e.EmpLoginID,
                       LTRIM(RTRIM(e.FirstName + ' ' + ISNULL(e.LastName,'')))
                           AS FullName,
                       c.CompanyName
                FROM   tbluerdetails u
                LEFT JOIN tblEmpDetails  e ON e.EmpID     = u.EmpID
                LEFT JOIN tblCompanyList c ON c.CID       = u.CompanyID
                ORDER  BY u.UID DESC";

            var users = await db.QueryAsync<AhhfficeUser>(sql);
            return View(users);
        }

        // ── Add/Edit GET ──────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ManageUser(int id = 0)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            using IDbConnection db = new SqlConnection(_ahhfficeConn);

            var companies = await db.QueryAsync<Company>(
                "SELECT CID, CompanyName FROM tblCompanyList WHERE Status = 1 ORDER BY CompanyName");

            AhhfficeUser user = new AhhfficeUser { Status = true };

            if (id > 0)
            {
                user = await db.QueryFirstOrDefaultAsync<AhhfficeUser>(
                    "SELECT * FROM tbluerdetails WHERE UID = @Id",
                    new { Id = id })
                    ?? new AhhfficeUser { Status = true };

                // Decrypt password for display in edit mode
                if (!string.IsNullOrEmpty(user.password))
                    user.password = DecryptBase64(user.password);
            }

            // Load employees for the company already selected (or all if new)
            var employees = await GetEmployeesForCompany(db, user.CompanyID);

            var vm = new AhhfficeUserFormViewModel
            {
                User = user,
                Companies = companies,
                Employees = employees
            };

            return View(vm);
        }

        // ── AJAX: reload employee dropdown when company changes ──
        [HttpGet]
        public async Task<IActionResult> GetEmployeesByCompany(int companyId)
        {
            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            var employees = await GetEmployeesForCompany(db, companyId);
            return Json(employees);
        }

        // ── Save User POST ────────────────────────────────────
        // ── Save User POST ────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(AhhfficeUser model)
        {
            using IDbConnection db = new SqlConnection(_ahhfficeConn);

            bool isEdit = model.UID > 0;

            // Encrypt password with same Base64 method as Ahhffice HR
            string encryptedPassword = EncryptBase64(model.password.Trim());

            if (!isEdit)
            {
                // Check duplicate username only
                int exists = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM tbluerdetails
              WHERE Username = @Username",
                    new { model.Username });

                if (exists > 0)
                {
                    TempData["ErrorMsg"] =
                        "Username already exists. Please choose another.";
                    return RedirectToAction("ManageUser");
                }

                const string sql = @"
            INSERT INTO tbluerdetails
                (EmpID, Role, EmailID, MobileNo,
                 Username, password,
                 AddDate, AddBy, Status, CompanyID)
            VALUES
                (@EmpID, @Role, @EmailID, @MobileNo,
                 @Username, @password,
                 GETDATE(), @AddBy, @Status, @CompanyID)";

                await db.ExecuteAsync(sql, new
                {
                    EmpID = model.EmpID ?? "",
                    model.Role,
                    model.EmailID,
                    model.MobileNo,
                    model.Username,
                    password = encryptedPassword,
                    AddBy = HttpContext.Session.GetString("UserName"),
                    model.Status,
                    model.CompanyID
                });

                TempData["SuccessMsg"] = "User added successfully!";
            }
            else
            {
                // Check duplicate username excluding current user
                int exists = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM tbluerdetails
              WHERE Username = @Username AND UID != @UID",
                    new { model.Username, model.UID });

                if (exists > 0)
                {
                    TempData["ErrorMsg"] = "Username already exists.";
                    return RedirectToAction("ManageUser", new { id = model.UID });
                }

                // On edit: if password left blank, keep the existing one
                string finalPassword = encryptedPassword;
                if (string.IsNullOrWhiteSpace(model.password))
                {
                    finalPassword = await db.ExecuteScalarAsync<string>(
                        "SELECT password FROM tbluerdetails WHERE UID = @UID",
                        new { model.UID }) ?? "";
                }

                const string sql = @"
            UPDATE tbluerdetails SET
                EmpID       = @EmpID,
                Role        = @Role,
                EmailID     = @EmailID,
                MobileNo    = @MobileNo,
                Username    = @Username,
                password    = @password,
                Status      = @Status,
                CompanyID   = @CompanyID,
                Updatedate  = GETDATE(),
                UpdateBy    = @UpdateBy
            WHERE UID = @UID";

                await db.ExecuteAsync(sql, new
                {
                    EmpID = model.EmpID ?? "",
                    model.Role,
                    model.EmailID,
                    model.MobileNo,
                    model.Username,
                    password = finalPassword,
                    model.Status,
                    model.CompanyID,
                    UpdateBy = HttpContext.Session.GetString("UserName"),
                    model.UID
                });

                TempData["SuccessMsg"] = "User updated successfully!";
            }

            return RedirectToAction("Users");
        }

        // ── Delete User POST ──────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            using IDbConnection db = new SqlConnection(_ahhfficeConn);
            await db.ExecuteAsync(
                "DELETE FROM tbluerdetails WHERE UID = @Id", new { Id = id });
            TempData["SuccessMsg"] = "User deleted successfully.";
            return RedirectToAction("Users");
        }

        // ── Helpers ───────────────────────────────────────────
        private async Task<IEnumerable<EmpDropdownItem>> GetEmployeesForCompany(
            IDbConnection db, int companyId)
        {
            if (companyId <= 0)
            {
                // Return all active employees if no company selected
                return await db.QueryAsync<EmpDropdownItem>(@"
                    SELECT EmpID,
                           EmpLoginID + ' - ' +
                           LTRIM(RTRIM(FirstName + ' ' + ISNULL(LastName,'')))
                               AS DisplayName
                    FROM   tblEmpDetails
                    WHERE  Status = 1
                    ORDER  BY FirstName");
            }

            return await db.QueryAsync<EmpDropdownItem>(@"
                SELECT EmpID,
                       EmpLoginID + ' - ' +
                       LTRIM(RTRIM(FirstName + ' ' + ISNULL(LastName,'')))
                           AS DisplayName
                FROM   tblEmpDetails
                WHERE  Status  = 1
                AND    Company = @CompanyID
                ORDER  BY FirstName",
                new { CompanyID = companyId });
        }

        // Same Base64 encrypt as Ahhffice HR (AuthService.cs)
        private static string EncryptBase64(string plainText)
        {
            byte[] encode = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(encode);
        }

        // Same Base64 decrypt as Ahhffice HR
        private static string DecryptBase64(string encryptedText)
        {
            try
            {
                byte[] todecode = Convert.FromBase64String(encryptedText);
                return Encoding.UTF8.GetString(todecode);
            }
            catch { return string.Empty; }
        }
    }
}