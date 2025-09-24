# 📝 Hệ thống Báo cáo Tuần cho Công ty

## 📌 Giới thiệu
Dự án này là một hệ thống web nội bộ cho phép nhân viên trong công ty gửi báo cáo công việc hàng tuần theo cấu trúc chuẩn.  
Hệ thống hỗ trợ các cấp bậc (nhân viên → trưởng phòng → giám đốc → admin) trong việc theo dõi, phê duyệt và quản lý báo cáo.

Ứng dụng được xây dựng bằng **ASP.NET Core MVC** và **SQL Server**, có thể triển khai trong mạng nội bộ hoặc public qua domain.

<img width="1906" height="956" alt="image" src="https://github.com/user-attachments/assets/373953a1-a46c-4d82-b9bf-99cfa2c68602" />

---

## 🚀 Tính năng chính
- Nhân viên tạo báo cáo tuần theo mẫu chuẩn:
  - Chọn tuần (YxxWyy – hiển thị ngày bắt đầu & kết thúc).
  - Thêm nhiều dòng nội dung báo cáo (Nhiệm vụ, Ngày hoàn thành, Trách nhiệm chính, Hỗ trợ, Mức độ ưu tiên, Tiến độ, Ghi chú).
  - Gửi báo cáo cho cấp trên trong cùng phòng ban.
- Quản lý báo cáo theo cấp:
  - Nhân viên: chỉ xem báo cáo của mình.
  - Trưởng phòng: xem báo cáo của phòng ban.
  - Giám đốc & Admin: xem tất cả.
- Hỗ trợ thêm/xóa dòng báo cáo động (server-side, không dùng JavaScript → dễ bảo trì).
- Kiểm tra trùng tuần: không cho phép tạo báo cáo 2 lần cho cùng tuần.
- Export PDF/Excel (dành cho cấp quản lý – sẽ phát triển tiếp).
- Hệ thống phân quyền qua **ASP.NET Identity**.

---

## 🛠️ Công nghệ sử dụng
- **ASP.NET Core MVC 8.0**
- **Entity Framework Core**
- **SQL Server Express / LocalDB**
- **Identity cho xác thực & phân quyền**
- **Bootstrap 5** cho giao diện

---

## ⚙️ Cấu hình
Trong file `appsettings.json`, cấu hình chuỗi kết nối database:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=QuanLyDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```
## 💾 Backup & Restore Database

Dữ liệu được lưu trong SQL Server (`QuanLyDb`).  
Bạn nên backup định kỳ (ví dụ: hàng tháng) để tránh mất dữ liệu.

---

### 1. Backup bằng SQL Server Management Studio (SSMS)
1. Mở **SQL Server Management Studio (SSMS)**.
2. Kết nối đến server chứa database.
3. Chuột phải vào database `QuanLyDb` → **Tasks** → **Back Up...**.
4. Chọn:
   - **Backup type**: Full
   - **Destination**: chọn thư mục lưu file `.bak`
5. Nhấn **OK** để backup.

---

### 2. Backup bằng command line (`sqlcmd`)
Mở **Command Prompt** và chạy:

```bash
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE [QuanLyDb] TO DISK='C:\Backup\QuanLyDb_2025_09_20.bak'"
```
Giải thích:

  -S .\SQLEXPRESS → server SQL Express cục bộ
  
  -E → dùng Windows Authentication (hoặc -U sa -P matkhau nếu dùng SQL user)
  
  File backup sẽ nằm ở C:\Backup\QuanLyDb_2025_09_20.bak
