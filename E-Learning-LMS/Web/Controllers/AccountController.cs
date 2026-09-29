using Core.Database.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Web.Models;
using Web.Models.EF;

namespace Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly FoodContext _dbContext;

        public AccountController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }


        // =========================================================
        // ĐĂNG KÝ
        // =========================================================

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterViewModel());
        }


        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim().ToLower();
            var username = model.Username.Trim();
            var mssv = model.MSSV.Trim();

            // Kiểm tra Email
            bool emailExists = await _dbContext.Users
                .AnyAsync(u => u.Email.ToLower() == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email này đã được đăng ký."
                );
            }

            // Kiểm tra Username
            bool usernameExists = await _dbContext.Users
                .AnyAsync(u =>
                    u.Username.ToLower() == username.ToLower()
                );

            if (usernameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "Tên đăng nhập này đã tồn tại."
                );
            }

            // Kiểm tra MSSV
            bool mssvExists = await _dbContext.Users
                .AnyAsync(u => u.MSSV == mssv);

            if (mssvExists)
            {
                ModelState.AddModelError(
                    nameof(model.MSSV),
                    "Mã số sinh viên này đã tồn tại."
                );
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }


            // Tạo tài khoản sinh viên
            var student = new User
            {
                Id = Guid.NewGuid(),
                MSSV = mssv,
                FullName = model.FullName.Trim(),
                Email = email,
                Username = username,
                Password = model.Password,
                ClassName = model.ClassName.Trim(),

                // Tài khoản mới được phép đăng nhập
                IsActive = true,

                // Chưa đăng nhập nên Offline
                IsOnline = false
            };


            await _dbContext.Users.AddAsync(student);

            await _dbContext.SaveChangesAsync();


            // Đăng ký xong chuyển sang Login
            return RedirectToAction("Login", "Account");
        }



        // =========================================================
        // ĐĂNG NHẬP
        // =========================================================

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl
            });
        }


        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var login = model.Email.Trim();


            // Cho phép đăng nhập bằng Email hoặc Username
            var student = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == login.ToLower()
                    ||
                    u.Username.ToLower() == login.ToLower()
                );


            // Không tìm thấy tài khoản
            if (student == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email/tên đăng nhập hoặc mật khẩu không đúng."
                );

                return View(model);
            }


            // Tài khoản bị khóa
            if (!student.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên."
                );

                return View(model);
            }


            // Kiểm tra mật khẩu
            if (model.Password != student.Password)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email/tên đăng nhập hoặc mật khẩu không đúng."
                );

                return View(model);
            }


            // Đánh dấu Online
            student.IsOnline = true;

            await _dbContext.SaveChangesAsync();


            // Tạo đăng nhập
            await SignInStudentAsync(student);


            // Lưu UserId vào Session
            HttpContext.Session.SetString(
                "UserId",
                student.Id.ToString()
            );


            // Nếu có returnUrl thì quay lại trang trước
            if (!string.IsNullOrEmpty(model.ReturnUrl)
                && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }


            return RedirectToAction("Index", "Home");
        }



        // =========================================================
        // ĐĂNG XUẤT
        // =========================================================

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var userIdString =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (Guid.TryParse(userIdString, out Guid userId))
            {
                var student = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);


                if (student != null)
                {
                    student.IsOnline = false;

                    await _dbContext.SaveChangesAsync();
                }
            }


            // Xóa Session
            HttpContext.Session.Remove("UserId");


            // Đăng xuất Cookie
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );


            return RedirectToAction("Index", "Home");
        }



        // =========================================================
        // PROFILE SINH VIÊN
        // =========================================================

        // GET: /Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdString =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return RedirectToAction("Login", "Account");
            }


            var student = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);


            if (student == null)
            {
                return NotFound();
            }


            return View(student);
        }



        // =========================================================
        // CÀI ĐẶT TÀI KHOẢN
        // =========================================================

        // GET: /Account/Settings
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var student = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }


        // POST: /Account/UpdateProfile — cập nhật họ tên / lớp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string? className)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var student = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["SettingsError"] = "Họ tên không được để trống.";
                return RedirectToAction(nameof(Settings));
            }

            student.FullName = fullName.Trim();
            student.ClassName = string.IsNullOrWhiteSpace(className) ? null : className.Trim();

            await _dbContext.SaveChangesAsync();

            // Cập nhật lại cookie để tên hiển thị mới ngay lập tức
            await SignInStudentAsync(student);

            TempData["SettingsSuccess"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction(nameof(Settings));
        }


        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var student = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student == null)
            {
                return NotFound();
            }

            if (student.Password != currentPassword)
            {
                TempData["SettingsError"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction(nameof(Settings));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["SettingsError"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction(nameof(Settings));
            }

            if (newPassword != confirmPassword)
            {
                TempData["SettingsError"] = "Xác nhận mật khẩu mới không khớp.";
                return RedirectToAction(nameof(Settings));
            }

            student.Password = newPassword;
            await _dbContext.SaveChangesAsync();

            TempData["SettingsSuccess"] = "Đã đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Settings));
        }


        // =========================================================
        // NGÔN NGỮ HIỂN THỊ (lưu lựa chọn bằng cookie)
        // =========================================================

        // GET: /Account/SetLanguage?lang=en&returnUrl=/
        [HttpGet]
        public IActionResult SetLanguage(string lang, string? returnUrl = null)
        {
            lang = (lang == "en") ? "en" : "vi";

            Response.Cookies.Append("lang", lang, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                Path = "/"
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }



        // =========================================================
        // TẠO COOKIE ĐĂNG NHẬP CHO SINH VIÊN
        // =========================================================

        private async Task SignInStudentAsync(User student)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    student.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    student.FullName
                ),

                new Claim(
                    ClaimTypes.Email,
                    student.Email
                ),

                new Claim(
                    ClaimTypes.Role,
                    "Student"
                ),

                new Claim(
                    "MSSV",
                    student.MSSV
                ),

                new Claim(
                    "ClassName",
                    student.ClassName ?? ""
                )
            };


            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );


            var principal = new ClaimsPrincipal(identity);


            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,

                    ExpiresUtc =
                        DateTimeOffset.UtcNow.AddDays(7)
                }
            );
        }



        // =========================================================
        // LỊCH SỬ ĐƠN HÀNG CŨ
        // =========================================================

        // GET: /Account/OrderHistory
        [HttpGet]
        [Route("Account/OrderHistory")]
        public async Task<IActionResult> OrderHistory()
        {
            var currentUserName = User.Identity?.Name;

            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                )?.Value;


            if (string.IsNullOrEmpty(currentUserName)
                && string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "Account");
            }


            var orders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o =>
                    o.Customer != null
                    &&
                    (
                        o.Customer.Name == currentUserName
                        ||
                        o.Customer.Id.ToString() == userIdClaim
                    )
                )
                .OrderByDescending(o => o.Id)
                .ToListAsync();


            return View(orders);
        }
    }
}