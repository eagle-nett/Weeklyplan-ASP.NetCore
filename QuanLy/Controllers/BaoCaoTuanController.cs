using iTextSharp.text; // NuGet: iTextSharp.LGPLv2.Core
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // NuGet: EPPlus
using OfficeOpenXml.Style;
using QuanLy.Data;
using QuanLy.Helpers;
using QuanLy.Models;
using QuanLy.ViewModels;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Authorization;



namespace QuanLy.Controllers
{
    public class BaoCaoTuanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<NguoiDung> _userManager;

        public BaoCaoTuanController(ApplicationDbContext context, UserManager<NguoiDung> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /BaoCao
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var list = _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao)
                .Where(b => b.NguoiBaoCaoId == user.Id)
                .ToList();
            return View(list);
        }

        // GET: /BaoCao/Create?maPhongBan=...
        [HttpGet]
        public async Task<IActionResult> Tao(string? maPhongBan)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Khởi tạo ViewModel
            var vm = new BaoCaoTuanViewModel
            {
                NguoiBaoCaoId = user.Id,
                HoTenNguoiBaoCao = user.HoTen,
                MaPhongBan = maPhongBan ?? "",
                // Khoi tao 1 dong mac dinh
                NoiDungs = new List<NoiDungBaoCaoViewModel> { new NoiDungBaoCaoViewModel() }
            };

            // Danh sách phòng ban
            vm.DanhSachPhongBan = _context.PhongBans
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan))
                .ToList();

            // Danh sách tuần (ví dụ 52 tuần của năm hiện tại, dạng Y25W01…Y25W52)
            int yearNow = DateTime.Now.Year;
            vm.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);


            // Nếu đã chọn phòng ban → load người nhận
            if (!string.IsNullOrEmpty(maPhongBan))
            {
                vm.DanhSachNguoiNhan = _context.Users
                    .Where(u => u.MaPhongBan == maPhongBan && u.Id != user.Id)
                    .Select(u => new SelectListItem(u.HoTen + " – " + u.ChucVu, u.Id))
                    .ToList();
            }
            // Nếu đã chọn tuần → tự động gán TuNgay và DenNgay
            if (!string.IsNullOrEmpty(vm.Tuan))
            {
                var match = System.Text.RegularExpressions.Regex.Match(vm.Tuan, @"Y(?<year>\d{2})W(?<week>\d{1,2})");
                if (match.Success)
                {
                    int year = 2000 + int.Parse(match.Groups["year"].Value);
                    int week = int.Parse(match.Groups["week"].Value);

                    // Tính ngày thứ Hai của tuần
                    var jan1 = new DateTime(year, 1, 1);
                    int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;
                    var firstThursday = jan1.AddDays(daysOffset);
                    var cal = CultureInfo.CurrentCulture.Calendar;
                    var weekRule = CalendarWeekRule.FirstFourDayWeek;
                    var firstWeek = cal.GetWeekOfYear(firstThursday, weekRule, DayOfWeek.Monday);
                    int delta = week - (firstWeek <= 1 ? 1 : 0);
                    var weekStart = firstThursday.AddDays(delta * 7).AddDays(-3);
                    if (weekStart.DayOfWeek != DayOfWeek.Monday)
                    {
                        weekStart = weekStart.AddDays((int)DayOfWeek.Monday - (int)weekStart.DayOfWeek);
                    }

                    vm.TuNgay = weekStart;
                    vm.DenNgay = weekStart.AddDays(6);
                }
            }

            return View(vm);
        }

        //// Chưa thêm được dòng mơi
        ////// POST: /BaoCao/Create
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Tao(BaoCaoTuanViewModel vm)
        //{
        //    var user = await _userManager.GetUserAsync(User);
        //    if (user == null) return Challenge();

        //    // Reload dropdown khi có lỗi
        //    vm.DanhSachPhongBan = _context.PhongBans
        //        .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan))
        //        .ToList();
        //    int yearNow = DateTime.Now.Year;
        //    vm.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

        //    if (!string.IsNullOrEmpty(vm.MaPhongBan))
        //    {
        //        vm.DanhSachNguoiNhan = _context.Users
        //            .Where(u => u.MaPhongBan == vm.MaPhongBan && u.Id != user.Id)
        //            .Select(u => new SelectListItem(u.HoTen + " – " + u.ChucVu, u.Id))
        //            .ToList();
        //    }

        //    if (!ModelState.IsValid)
        //        return View(vm);

        //    // Map sang entity
        //    var entity = new BaoCaoTuan
        //    {
        //        NguoiBaoCaoId = user.Id,
        //        BaoCaoChoId = vm.BaoCaoChoId,
        //        Tuan = vm.Tuan,
        //        TuNgay = vm.TuNgay,
        //        DenNgay = vm.DenNgay,
        //        NgayTao = DateTime.Now
        //    };

        //    foreach (var nd in vm.NoiDungs)
        //    {
        //        entity.NoiDungs.Add(new NoiDungBaoCao
        //        {
        //            NoiDung = nd.NoiDung,
        //            NgayHoanThanh = nd.NgayHoanThanh,
        //            TrachNhiemChinh = nd.TrachNhiemChinh,
        //            TrachNhiemHoTro = nd.TrachNhiemHoTro,
        //            MucDoUuTien = nd.MucDoUuTien,
        //            TienDo = nd.TienDo,
        //            GhiChu = nd.GhiChu
        //        });
        //    }

        //    _context.BaoCaoTuans.Add(entity);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction("Index", "Home");
        //}

        // POST: /BaoCao/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tao(BaoCaoTuanViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // --- Reload dropdowns luôn (dù add/remove/save) ---
            vm.DanhSachPhongBan = _context.PhongBans
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan))
                .ToList();
            int yearNow = DateTime.Now.Year;
            vm.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

            if (!string.IsNullOrEmpty(vm.MaPhongBan))
            {
                vm.DanhSachNguoiNhan = _context.Users
                    .Where(u => u.MaPhongBan == vm.MaPhongBan && u.Id != user.Id)
                    .Select(u => new SelectListItem(u.HoTen + " – " + u.ChucVu, u.Id))
                    .ToList();
            }

            // --- Xử lý nút "Thêm dòng" (server-side) ---
            if (Request.Form.ContainsKey("addRow"))
            {
                // Giữ nguyên dữ liệu hiện tại, chỉ thêm 1 dòng rỗng vào cuối
                if (vm.NoiDungs == null) vm.NoiDungs = new List<NoiDungBaoCaoViewModel>();
                vm.NoiDungs.Add(new NoiDungBaoCaoViewModel());
                // Không validate, trả về view để người dùng tiếp tục nhập
                return View(vm);
            }

            // --- Xử lý nút "Xóa dòng" (server-side) ---
            if (Request.Form.ContainsKey("removeRow"))
            {
                var value = Request.Form["removeRow"].ToString();
                if (int.TryParse(value, out int idx))
                {
                    if (vm.NoiDungs != null && idx >= 0 && idx < vm.NoiDungs.Count)
                    {
                        vm.NoiDungs.RemoveAt(idx);
                    }
                }
                // đảm bảo còn ít nhất 1 dòng để hiển thị form
                if (vm.NoiDungs == null || vm.NoiDungs.Count == 0)
                {
                    vm.NoiDungs = new List<NoiDungBaoCaoViewModel> { new NoiDungBaoCaoViewModel() };
                }
                // Trả view để người dùng tiếp tục chỉnh sửa
                return View(vm);
            }

            // --- Nếu không phải add/remove, thì đây là nút "Gửi báo cáo" thực sự ---
            if (!ModelState.IsValid)
                return View(vm);

            // Map sang entity
            var entity = new BaoCaoTuan
            {
                NguoiBaoCaoId = user.Id,
                BaoCaoChoId = vm.BaoCaoChoId,
                Tuan = vm.Tuan,
                TuNgay = vm.TuNgay,
                DenNgay = vm.DenNgay,
                NgayTao = DateTime.Now
            };

            // ✅ Kiểm tra trùng tuần trước khi lưu
            bool daTonTai = await _context.BaoCaoTuans
                .AnyAsync(b => b.NguoiBaoCaoId == user.Id && b.Tuan == vm.Tuan);

            if (daTonTai)
            {
                ViewData["ThongBaoTrungTuan"] = "Bạn đã tạo báo cáo cho tuần này rồi.";
                ModelState.AddModelError("Tuan", "Bạn đã tạo báo cáo cho tuần này rồi.");
                return View(vm);
            }

            /////
            if (vm.NoiDungs != null)
            {
                foreach (var nd in vm.NoiDungs)
                {
                    // tránh thêm những dòng rỗng hoàn toàn (nếu muốn)
                    if (string.IsNullOrWhiteSpace(nd.NoiDung) && string.IsNullOrWhiteSpace(nd.TrachNhiemChinh))
                        continue;

                    entity.NoiDungs.Add(new NoiDungBaoCao
                    {
                        NoiDung = nd.NoiDung,
                        NgayHoanThanh = nd.NgayHoanThanh,
                        TrachNhiemChinh = nd.TrachNhiemChinh,
                        TrachNhiemHoTro = nd.TrachNhiemHoTro,
                        MucDoUuTien = nd.MucDoUuTien,
                        TienDo = nd.TienDo,
                        GhiChu = nd.GhiChu,
                        // 🔥 bổ sung mới
                        LyDoChuaHoanThanh = nd.LyDoChuaHoanThanh,
                        KetQuaDatDuoc = nd.KetQuaDatDuoc,
                        HuongGiaiQuyet = nd.HuongGiaiQuyet
                    });
                }
            }

            _context.BaoCaoTuans.Add(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }


        // Helper: sinh dropdown tuần ISO (YxxWyy)
        private List<SelectListItem> GenerateWeekDropdown(int startYear, int endYear)
        {
            var items = new List<SelectListItem>();
            var cal = CultureInfo.CurrentCulture.Calendar;
            var weekRule = CalendarWeekRule.FirstFourDayWeek;
            var firstDay = DayOfWeek.Monday;

            for (int year = startYear; year <= endYear; year++)
            {
                // Tính số tuần trong năm
                int weeksInYear = cal.GetWeekOfYear(
                    new DateTime(year, 12, 31),
                    weekRule,
                    firstDay
                );

                for (int week = 1; week <= weeksInYear; week++)
                {
                    // Tìm ngày thứ Năm của tuần đầu tiên
                    var jan1 = new DateTime(year, 1, 1);
                    int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;
                    var firstThursday = jan1.AddDays(daysOffset);
                    int firstWeek = cal.GetWeekOfYear(firstThursday, weekRule, firstDay);
                    int delta = week - (firstWeek <= 1 ? 1 : 0);

                    // Tính ngày thứ Hai của tuần hiện tại
                    var weekStart = firstThursday
                        .AddDays(delta * 7)
                        .AddDays(-3);
                    if (weekStart.DayOfWeek != DayOfWeek.Monday)
                        weekStart = weekStart.AddDays(
                            (int)DayOfWeek.Monday - (int)weekStart.DayOfWeek
                        );

                    string code = $"Y{year % 100:D2}W{week:D2}";
                    string label = $"{code} ({weekStart:dd/MM/yyyy} – {weekStart.AddDays(6):dd/MM/yyyy})";
                    items.Add(new SelectListItem(label, code));
                }
            }

            return items;
        }

        // GET: BaoCaoTuan/XemLai
        [HttpGet]
        public async Task<IActionResult> XemLai(string tuan, string nguoiNhan, string trangThai)
        {
            var nguoiDungId = _userManager.GetUserId(User);

            // Lấy danh sách tất cả báo cáo của người dùng hiện tại
            var query = _context.BaoCaoTuans
                .Where(b => b.NguoiBaoCaoId == nguoiDungId)
                .Include(b => b.BaoCaoCho)
                .Include(b => b.NoiDungs)
                .AsQueryable();

            // Lọc theo tuần nếu có
            if (!string.IsNullOrEmpty(tuan))
            {
                query = query.Where(b => b.Tuan == tuan);
            }

            // Lọc theo người nhận báo cáo
            if (!string.IsNullOrEmpty(nguoiNhan))
            {
                query = query.Where(b => b.BaoCaoChoId == nguoiNhan);
            }

            // Lọc theo trạng thái duyệt
            if (!string.IsNullOrEmpty(trangThai))
            {
                if (trangThai == "ChoDuyet")
                {
                    query = query.Where(b => b.TrangThai == null || b.TrangThai == "");
                }
                else
                {
                    query = query.Where(b => b.TrangThai == trangThai);
                }
            }

            var baoCaoList = await query
                .OrderByDescending(b => b.Tuan)
                .ToListAsync();

            // Truyền danh sách tuần có sẵn để lọc
            var tuanOptions = await _context.BaoCaoTuans
                .Where(b => b.NguoiBaoCaoId == nguoiDungId)
                .Select(b => b.Tuan)
                .Distinct()
                .OrderByDescending(t => t)
                .ToListAsync();

            ViewBag.TuanOptions = tuanOptions
                .Select(t => new SelectListItem { Value = t, Text = t })
                .ToList();

            // Truyền danh sách người nhận từng báo cáo để lọc
            var nguoiNhanOptions = await _context.BaoCaoTuans
                .Where(b => b.NguoiBaoCaoId == nguoiDungId)
                .Include(b => b.BaoCaoCho)
                .Select(b => new { b.BaoCaoChoId, b.BaoCaoCho.HoTen })
                .Distinct()
                .ToListAsync();

            ViewBag.DanhSachNguoiNhan = nguoiNhanOptions
                .Select(x => new SelectListItem
                {
                    Value = x.BaoCaoChoId,
                    Text = x.HoTen
                })
                .ToList();

            return View(baoCaoList);
        }

        // GET: BaoCaoTuan/ChiTiet
        [HttpGet]
        public async Task<IActionResult> ChiTiet(int id)
        {
            var baoCao = await _context.BaoCaoTuans
                .Include(b => b.NoiDungs)
                .Include(b => b.BaoCaoCho)
                .Include(b => b.NguoiBaoCao)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (baoCao == null)
            {
                return NotFound();
            }

            return View(baoCao);
        }

        // GET: /BaoCao/Edit/
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var baoCao = await _context.BaoCaoTuans
                .Include(b => b.NoiDungs)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (baoCao == null)
                return NotFound();

            // Map sang ViewModel
            var vm = new BaoCaoTuanViewModel
            {
                Id = baoCao.Id,
                NguoiBaoCaoId = baoCao.NguoiBaoCaoId,
                BaoCaoChoId = baoCao.BaoCaoChoId,
                MaPhongBan = baoCao.NguoiBaoCao?.MaPhongBan ?? "",
                Tuan = baoCao.Tuan,
                TuNgay = baoCao.TuNgay,
                DenNgay = baoCao.DenNgay,
                NoiDungs = baoCao.NoiDungs.Select(nd => new NoiDungBaoCaoViewModel
                {
                    Id = nd.Id,
                    NoiDung = nd.NoiDung,
                    NgayHoanThanh = nd.NgayHoanThanh,
                    TrachNhiemChinh = nd.TrachNhiemChinh,
                    TrachNhiemHoTro = nd.TrachNhiemHoTro,
                    MucDoUuTien = nd.MucDoUuTien,
                    TienDo = nd.TienDo,
                    GhiChu = nd.GhiChu,
                    // 🔥 bổ sung mới
                    LyDoChuaHoanThanh = nd.LyDoChuaHoanThanh,
                    KetQuaDatDuoc = nd.KetQuaDatDuoc,
                    HuongGiaiQuyet = nd.HuongGiaiQuyet
                }).ToList()
            };

            // Load dropdowns
            vm.DanhSachPhongBan = _context.PhongBans
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan))
                .ToList();

            vm.DanhSachNguoiNhan = _context.Users
                .Where(u => u.MaPhongBan == vm.MaPhongBan && u.Id != vm.NguoiBaoCaoId)
                .Select(u => new SelectListItem(u.HoTen + " – " + u.ChucVu, u.Id))
                .ToList();

            int yearNow = DateTime.Now.Year;
            vm.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

            return View("Edit", vm);
        }

        // POST: /BaoCao/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(BaoCaoTuanViewModel vm)
        {
            var baoCao = await _context.BaoCaoTuans
                .Include(b => b.NoiDungs)
                .FirstOrDefaultAsync(b => b.Id == vm.Id);

            if (baoCao == null)
                return NotFound();

            // Reload dropdowns nếu lỗi
            vm.DanhSachPhongBan = _context.PhongBans
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan))
                .ToList();

            int yearNow = DateTime.Now.Year;
            vm.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

            vm.DanhSachNguoiNhan = _context.Users
                .Where(u => u.MaPhongBan == vm.MaPhongBan && u.Id != vm.NguoiBaoCaoId)
                .Select(u => new SelectListItem(u.HoTen + " – " + u.ChucVu, u.Id))
                .ToList();

            // Xử lý nút add/remove dòng (giống Create)
            if (Request.Form.ContainsKey("addRow"))
            {
                if (vm.NoiDungs == null) vm.NoiDungs = new List<NoiDungBaoCaoViewModel>();
                vm.NoiDungs.Add(new NoiDungBaoCaoViewModel());
                return View("Edit", vm);
            }
            if (Request.Form.ContainsKey("removeRow"))
            {
                var value = Request.Form["removeRow"].ToString();
                if (int.TryParse(value, out int idx))
                {
                    if (vm.NoiDungs != null && idx >= 0 && idx < vm.NoiDungs.Count)
                        vm.NoiDungs.RemoveAt(idx);
                }
                if (vm.NoiDungs == null || vm.NoiDungs.Count == 0)
                    vm.NoiDungs = new List<NoiDungBaoCaoViewModel> { new NoiDungBaoCaoViewModel() };

                return View("Edit", vm);
            }

            if (!ModelState.IsValid)
                return View("Edit", vm);

            // Update thông tin bao cáo
            baoCao.BaoCaoChoId = vm.BaoCaoChoId;
            baoCao.Tuan = vm.Tuan;
            baoCao.TuNgay = vm.TuNgay;
            baoCao.DenNgay = vm.DenNgay;

            // Xóa hết nội dung cũ và thêm lại (cách đơn giản nhất)
            _context.NoiDungBaoCaos.RemoveRange(baoCao.NoiDungs);
            baoCao.NoiDungs.Clear();

            foreach (var nd in vm.NoiDungs)
            {
                if (string.IsNullOrWhiteSpace(nd.NoiDung) && string.IsNullOrWhiteSpace(nd.TrachNhiemChinh))
                    continue;

                baoCao.NoiDungs.Add(new NoiDungBaoCao
                {
                    NoiDung = nd.NoiDung,
                    NgayHoanThanh = nd.NgayHoanThanh,
                    TrachNhiemChinh = nd.TrachNhiemChinh,
                    TrachNhiemHoTro = nd.TrachNhiemHoTro,
                    MucDoUuTien = nd.MucDoUuTien,
                    TienDo = nd.TienDo,
                    GhiChu = nd.GhiChu,
                    // 🔥 bổ sung mới
                    LyDoChuaHoanThanh = nd.LyDoChuaHoanThanh,
                    KetQuaDatDuoc = nd.KetQuaDatDuoc,
                    HuongGiaiQuyet = nd.HuongGiaiQuyet
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("XemLai");
        }

        // GET: /BaoCao/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var baoCao = await _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (baoCao == null)
                return NotFound();

            // Xác nhận xóa trước khi thực hiện
            var vm = new BaoCaoTuanViewModel
            {
                Id = baoCao.Id,
                HoTenNguoiBaoCao = baoCao.NguoiBaoCao?.HoTen ?? "",
                Tuan = baoCao.Tuan,
                TuNgay = baoCao.TuNgay,
                DenNgay = baoCao.DenNgay
            };

            return View("Delete", vm);
        }

        // POST: /BaoCao/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var baoCao = await _context.BaoCaoTuans
                .Include(b => b.NoiDungs)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (baoCao == null)
                return NotFound();

            // Xóa hết nội dung trước để tránh lỗi ràng buộc
            _context.NoiDungBaoCaos.RemoveRange(baoCao.NoiDungs);
            _context.BaoCaoTuans.Remove(baoCao);
            await _context.SaveChangesAsync();

            return RedirectToAction("XemLai");
        }

        // GET: /BaoCaoTuan/ThongKe
        [HttpGet]
        public IActionResult ThongKe(string? tuan, string? maNhanVien)
        {
            // Lấy danh sách tuần
            int yearNow = DateTime.Now.Year;
            ViewBag.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

            // Gắn lại giá trị để view hiển thị lại
            ViewBag.SelectedTuan = tuan;
            ViewBag.MaNhanVien = maNhanVien;

            // 🔒 Chỉ truy vấn khi cả tuần và mã nhân viên đều có
            if (string.IsNullOrEmpty(tuan) || string.IsNullOrEmpty(maNhanVien))
            {
                // Trả về view với danh sách rỗng (chưa lọc)
                return View(new List<BaoCaoTuan>());
            }

            // Lấy dữ liệu báo cáo khi đã đủ điều kiện
            var ds = _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao)
                .Include(b => b.BaoCaoCho)
                .Include(b => b.NoiDungs)
                .Where(b => b.Tuan == tuan && b.NguoiBaoCao.MaNV == maNhanVien)
                .OrderBy(b => b.Tuan)
                .ThenBy(b => b.NguoiBaoCao.HoTen)
                .ToList();

            return View(ds);
        }

        // xuất file Excel view ThongKe
        public IActionResult ExportExcel(string tuan, string maNhanVien)
        {
            var query = _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao)
                .Include(b => b.BaoCaoCho)
                .Include(b => b.NoiDungs)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tuan))
                query = query.Where(b => b.Tuan == tuan);

            if (!string.IsNullOrEmpty(maNhanVien))
                query = query.Where(b => b.NguoiBaoCao.MaNV == maNhanVien);

            var ds = query.OrderBy(b => b.Tuan).ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("BaoCaoTuan");

            // Header
            ws.Cells[1, 1].Value = "STT";
            ws.Cells[1, 2].Value = "Tuần";
            ws.Cells[1, 3].Value = "Mã NV";
            ws.Cells[1, 4].Value = "Nhân viên";
            ws.Cells[1, 5].Value = "Mã Người nhận";
            ws.Cells[1, 6].Value = "Người nhận";
            ws.Cells[1, 7].Value = "Từ ngày";
            ws.Cells[1, 8].Value = "Đến ngày";
            ws.Cells[1, 9].Value = "Nội dung";
            ws.Cells[1, 10].Value = "Ngày hoàn thành";
            ws.Cells[1, 11].Value = "Trách nhiệm chính";
            ws.Cells[1, 12].Value = "Trách nhiệm hỗ trợ";
            ws.Cells[1, 13].Value = "Mức độ ưu tiên";
            ws.Cells[1, 14].Value = "Tiến độ";
            ws.Cells[1, 15].Value = "Lí do chưa hoàn thành";
            ws.Cells[1, 16].Value = "Hướng giải quyết";
            ws.Cells[1, 17].Value = "Kết quả đạt được";
            ws.Cells[1, 18].Value = "Ghi chú";

            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Row(1).Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            int row = 2;
            int stt = 1;
            foreach (var bc in ds)
            {
                foreach (var nd in bc.NoiDungs)
                {
                    ws.Cells[row, 1].Value = stt++;
                    ws.Cells[row, 2].Value = bc.Tuan;
                    ws.Cells[row, 3].Value = bc.NguoiBaoCao?.MaNV;
                    ws.Cells[row, 4].Value = bc.NguoiBaoCao?.HoTen;
                    ws.Cells[row, 5].Value = bc.BaoCaoCho?.MaNV;
                    ws.Cells[row, 6].Value = bc.BaoCaoCho?.HoTen;
                    ws.Cells[row, 7].Value = bc.TuNgay.ToString("dd/MM/yyyy");
                    ws.Cells[row, 8].Value = bc.DenNgay.ToString("dd/MM/yyyy");
                    ws.Cells[row, 9].Value = nd.NoiDung;
                    ws.Cells[row, 10].Value = nd.NgayHoanThanh?.ToString("dd/MM/yyyy");
                    ws.Cells[row, 11].Value = nd.TrachNhiemChinh;
                    ws.Cells[row, 12].Value = nd.TrachNhiemHoTro;
                    ws.Cells[row, 13].Value = nd.MucDoUuTien;
                    ws.Cells[row, 14].Value = nd.TienDo;
                    ws.Cells[row, 15].Value = nd.LyDoChuaHoanThanh;
                    ws.Cells[row, 16].Value = nd.HuongGiaiQuyet;
                    ws.Cells[row, 17].Value = nd.KetQuaDatDuoc;
                    ws.Cells[row, 18].Value = nd.GhiChu;
                    row++;
                }
            }

            ws.Cells.AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoTuan_{tuan}_{maNhanVien}.xlsx");
        }

        // GET: /BaoCaoTuan/ThongKeNangCao
        [Authorize(Roles = "Admin,GiamDoc")]
        public IActionResult ThongKeNangCao(string tuan, string maPhongBan, string maNhanVien, string tienDo)
        {
            var query = _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao)
                        .ThenInclude(nv => nv.PhongBan) // load Phòng ban của nhân viên báo cáo
                .Include(b => b.BaoCaoCho)
                        .ThenInclude(nd => nd.PhongBan) // load phòng ban người nhận
                .Include(b => b.NoiDungs)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tuan))
                query = query.Where(x => x.Tuan == tuan);

            if (!string.IsNullOrEmpty(maPhongBan))
                query = query.Where(x => x.NguoiBaoCao.MaPhongBan == maPhongBan);

            if (!string.IsNullOrEmpty(maNhanVien))
                query = query.Where(x =>
                    x.NguoiBaoCao.MaNV.Contains(maNhanVien) ||
                    x.NguoiBaoCao.HoTen.Contains(maNhanVien));

            if (!string.IsNullOrEmpty(tienDo))
                query = query.Where(x => x.NoiDungs.Any(nd => nd.TienDo == tienDo));

            // Lấy dữ liệu & lọc chi tiết nội dung nếu có lọc theo tiến độ
            var model = query
                .ToList()
                .Select(bc =>
                {
                    if (!string.IsNullOrEmpty(tienDo))
                        bc.NoiDungs = bc.NoiDungs.Where(nd => nd.TienDo == tienDo).ToList();
                    return bc;
                })
                .ToList();

            // Dropdown chọn tuần
            ViewBag.TuanOptions = new SelectList(
                _context.BaoCaoTuans.Select(x => x.Tuan).Distinct().OrderBy(x => x).ToList()
            );

            // Dropdown chọn phòng ban
            ViewBag.PhongBanOptions = new SelectList(
                _context.Users.Select(x => x.MaPhongBan).Distinct().ToList()
            );

            // Dropdown chọn nhân viên (hiển thị Tên (Mã) để tránh nhầm lẫn)
            ViewBag.NhanVienOptions = new SelectList(
                _context.Users
                    .OrderBy(u => u.HoTen)
                    .Select(u => new
                    {
                        Value = u.MaNV,
                        Text = $"{u.HoTen} ({u.MaNV})"
                    })
                    .ToList(),
                "Value", "Text"
            );


            return View(model);
        }

        // Xuất Excel cho Thống kê nâng cao
        public IActionResult ExportExcelNangCao(string tuan, string maPhongBan, string maNhanVien, string tienDo)
        {
            var query = _context.BaoCaoTuans
                .Include(b => b.NguoiBaoCao).ThenInclude(nd => nd.PhongBan)
                .Include(b => b.BaoCaoCho).ThenInclude(nd => nd.PhongBan)
                .Include(b => b.NoiDungs)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tuan))
                query = query.Where(b => b.Tuan == tuan);

            if (!string.IsNullOrEmpty(maPhongBan))
                query = query.Where(b => b.NguoiBaoCao.MaPhongBan == maPhongBan);

            if (!string.IsNullOrEmpty(maNhanVien))
                query = query.Where(b => b.NguoiBaoCao.MaNV.Contains(maNhanVien)
                                      || b.NguoiBaoCao.HoTen.Contains(maNhanVien));

            if (!string.IsNullOrEmpty(tienDo))
                query = query.Where(b => b.NoiDungs.Any(nd => nd.TienDo == tienDo));

            var ds = query.OrderBy(b => b.Tuan).ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("ThongKeNangCao");

            // Header
            ws.Cells[1, 1].Value = "STT";
            ws.Cells[1, 2].Value = "Tuần";
            ws.Cells[1, 3].Value = "Từ ngày";
            ws.Cells[1, 4].Value = "Đến ngày";
            ws.Cells[1, 5].Value = "Mã NV";
            ws.Cells[1, 6].Value = "Tên nhân viên";
            ws.Cells[1, 7].Value = "Mã phòng ban";
            ws.Cells[1, 8].Value = "Tên phòng ban";
            ws.Cells[1, 9].Value = "Báo cáo cho";
            ws.Cells[1, 10].Value = "Phòng ban QLTT";
            ws.Cells[1, 11].Value = "Nội dung";
            ws.Cells[1, 12].Value = "Ngày hoàn thành";
            ws.Cells[1, 13].Value = "TN chính";
            ws.Cells[1, 14].Value = "TN hỗ trợ";
            ws.Cells[1, 15].Value = "Ưu tiên";
            ws.Cells[1, 16].Value = "Tiến độ";
            ws.Cells[1, 17].Value = "Lí do chưa hoàn thành";
            ws.Cells[1, 18].Value = "Hướng giải quyết";
            ws.Cells[1, 19].Value = "Kết quả đạt được";
            ws.Cells[1, 20].Value = "Ghi chú";
            ws.Cells[1, 21].Value = "Thời gian nộp";

            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Row(1).Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            int row = 2;
            int stt = 1;
            foreach (var bc in ds)
            {
                foreach (var nd in bc.NoiDungs)
                {
                    ws.Cells[row, 1].Value = stt++;
                    ws.Cells[row, 2].Value = bc.Tuan;
                    ws.Cells[row, 3].Value = bc.TuNgay.ToString("dd/MM/yyyy");
                    ws.Cells[row, 4].Value = bc.DenNgay.ToString("dd/MM/yyyy");
                    ws.Cells[row, 5].Value = bc.NguoiBaoCao?.MaNV;
                    ws.Cells[row, 6].Value = bc.NguoiBaoCao?.HoTen;
                    ws.Cells[row, 7].Value = bc.NguoiBaoCao?.MaPhongBan;
                    ws.Cells[row, 8].Value = bc.NguoiBaoCao?.PhongBan?.TenPhongBan;
                    ws.Cells[row, 9].Value = $"{bc.BaoCaoCho?.HoTen} ({bc.BaoCaoCho?.MaNV})";
                    ws.Cells[row, 10].Value = $"{bc.BaoCaoCho?.PhongBan?.MaPhongBan} - {bc.BaoCaoCho?.PhongBan?.TenPhongBan}";
                    ws.Cells[row, 11].Value = nd.NoiDung;
                    ws.Cells[row, 12].Value = nd.NgayHoanThanh?.ToString("dd/MM/yyyy");
                    ws.Cells[row, 13].Value = nd.TrachNhiemChinh;
                    ws.Cells[row, 14].Value = nd.TrachNhiemHoTro;
                    ws.Cells[row, 15].Value = nd.MucDoUuTien;
                    ws.Cells[row, 16].Value = nd.TienDo;
                    ws.Cells[row, 17].Value = nd.LyDoChuaHoanThanh;
                    ws.Cells[row, 18].Value = nd.HuongGiaiQuyet;
                    ws.Cells[row, 19].Value = nd.KetQuaDatDuoc;
                    ws.Cells[row, 20].Value = nd.GhiChu;
                    ws.Cells[row, 21].Value = bc.NgayTao.ToString("HH:mm dd/MM/yyyy");
                    row++;
                }
            }

            ws.Cells.AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ThongKeNangCao_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        // GET: /BaoCaoTuan/TrangThai
        [HttpGet]
        public async Task<IActionResult> TrangThai(string? tuan, string? maPhongBan, bool chiHienThiChuaGui = false, bool chiHienThiDaGui = false)
        {
            // Dropdown tuần (dùng helper có sẵn)
            int yearNow = DateTime.Now.Year;
            ViewBag.TuanOptions = GenerateWeekDropdown(yearNow - 1, yearNow + 1);

            // Phòng ban dropdown
            ViewBag.PhongBanOptions = _context.PhongBans
                .Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan))
                .ToList();

            // Gắn lại giá trị đã chọn để view hiển thị
            ViewBag.SelectedTuan = tuan;
            ViewBag.SelectedPhongBan = maPhongBan;
            ViewBag.ChiHienThiChuaGui = chiHienThiChuaGui;

            // Nếu chưa chọn tuần thì trả view rỗng (bảo mật / tiện dùng)
            if (string.IsNullOrEmpty(tuan))
            {
                return View(new List<TrangThaiViewModel>());
            }

            // Lấy employees theo phòng ban (hoặc tất cả nếu không chọn)
            var employeesQuery = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(maPhongBan))
                employeesQuery = employeesQuery.Where(u => u.MaPhongBan == maPhongBan);

            var employees = await employeesQuery
                .OrderBy(u => u.MaNV)
                .ToListAsync();

            // Lấy báo cáo tuần đó
            var baoCaos = await _context.BaoCaoTuans
                .Include(b => b.BaoCaoCho)
                .Where(b => b.Tuan == tuan)
                .ToListAsync();

            // Map báo cáo theo userId (nếu có nhiều bản ghi cho 1 user, lấy bản đầu)
            var baocaoByUser = baoCaos
                .GroupBy(b => b.NguoiBaoCaoId)
                .ToDictionary(g => g.Key, g => g.First());

            // Lấy tên phòng ban map để tránh nhiều query
            var pbDict = await _context.PhongBans
                .ToDictionaryAsync(p => p.MaPhongBan, p => p.TenPhongBan);

            // Compute week range helper (dùng same logic với controller)
            (DateTime weekStart, DateTime weekEnd) = GetWeekRangeFromCode(tuan);

            var list = new List<TrangThaiViewModel>();
            int idx = 1;
            foreach (var nv in employees)
            {
                baocaoByUser.TryGetValue(nv.Id, out var bc);

                list.Add(new TrangThaiViewModel
                {
                    STT = idx++,
                    Tuan = tuan,
                    TuNgay = bc?.TuNgay ?? weekStart,
                    DenNgay = bc?.DenNgay ?? weekEnd,
                    MaPhongBan = nv.MaPhongBan,
                    TenPhongBan = pbDict.TryGetValue(nv.MaPhongBan, out var tpb) ? tpb : "",
                    MaNV = nv.MaNV,
                    TenNV = nv.HoTen,
                    TenQuanLy = bc?.BaoCaoCho?.HoTen ?? "-",
                    ThoiGianGui = bc?.NgayTao,
                    DaGui = bc != null
                });

                //list.Add(item);
            }

            // ✅ Lọc theo 2 checkbox
            // Nếu muốn chỉ hiển thị chưa gửi:
            if (chiHienThiChuaGui && !chiHienThiDaGui)
                list = list.Where(x => !x.DaGui).ToList();
            // Nếu muốn chỉ hiển thị đã gửi
            if (chiHienThiDaGui && !chiHienThiChuaGui)
                list = list.Where(x => x.DaGui).ToList();

            // Optional: thống kê tổng
            ViewBag.Total = list.Count;
            ViewBag.Sent = list.Count(x => x.DaGui);
            ViewBag.NotSent = list.Count(x => !x.DaGui);

            return View(list);
        }

        // Helper private (bỏ vào cuối controller)
        private (DateTime weekStart, DateTime weekEnd) GetWeekRangeFromCode(string tuanCode)
        {
            // Expect Y25W28 or Y2025W28?
            var match = System.Text.RegularExpressions.Regex.Match(tuanCode, @"Y(?<year>\d{2,4})W(?<week>\d{1,2})");
            if (!match.Success)
                return (DateTime.MinValue, DateTime.MinValue);

            int year = int.Parse(match.Groups["year"].Value);
            if (year < 100) year += 2000;
            int week = int.Parse(match.Groups["week"].Value);

            var cal = CultureInfo.CurrentCulture.Calendar;
            var weekRule = CalendarWeekRule.FirstFourDayWeek;
            var firstDay = DayOfWeek.Monday;

            // Find week start (Monday)
            var jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;
            var firstThursday = jan1.AddDays(daysOffset);
            int firstWeek = cal.GetWeekOfYear(firstThursday, weekRule, firstDay);
            int delta = week - (firstWeek <= 1 ? 1 : 0);
            var weekStart = firstThursday.AddDays(delta * 7).AddDays(-3);
            if (weekStart.DayOfWeek != DayOfWeek.Monday)
                weekStart = weekStart.AddDays((int)DayOfWeek.Monday - (int)weekStart.DayOfWeek);

            return (weekStart.Date, weekStart.AddDays(6).Date);
        }
    }

}

