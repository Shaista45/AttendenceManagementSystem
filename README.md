
# 🎓 Attendance Management System (UET AMS)

A robust, web-based application designed to digitize and automate the manual attendance operations of educational institutions. Built with **ASP.NET Core 8.0 MVC**, this system streamlines interactions between administrators, faculty, and students, ensuring accurate record-keeping, real-time reporting, and transparent data access.

---

## 🚀 Features

### 👨‍💼 Admin Module (Administrator)

* **Dashboard Analytics:** Real-time counters for Total Students, Teachers, and Active Courses with system status indicators.
* **User Management:**
* Onboard Teachers and Students.
* **Bulk Upload:** Import Students and Teachers via Excel sheets.


* **Academic Configuration:**
* Manage Departments, Batches, Sections, and Courses.
* **Conflict-Free Timetabling:** Dynamic scheduling system that prevents room and teacher conflicts.


* **Reporting Intelligence:**
* Generate **Defaulter Reports** (Low Attendance < 75%).
* Export detailed reports to **Excel** (`ClosedXML`) and **PDF** (`DinkToPdf`).



### 👨‍🏫 Teacher Module (Faculty)

* **Digital Attendance:** Mark student attendance (Present/Absent) via a secure interface.
* **My Timetable:** Personalized weekly schedule view showing assigned classes and rooms.
* **Student Monitoring:** View detailed attendance history logs for individual students.
* **Quick Actions:** Access to "Today's Classes" directly from the dashboard.

### 👨‍🎓 Student Module (User)

* **Student Dashboard:** Visual progress bars for attendance percentages across all subjects.
* **Auto-Mark Attendance:** Unique feature allowing students to mark their own attendance *only* during active class slots (verified against the timetable).
* **Subject Registration:** Register for semester-specific courses.
* **Attendance Summary:** Detailed breakdown of present/absent days and shortage warnings.

---

## 🛠️ Tech Stack

* **Framework:** ASP.NET Core 8.0 (MVC Architecture)
* **Database:** Microsoft SQL Server
* **ORM:** Entity Framework Core 8.0 (Code-First approach)
* **Frontend:**
* Razor Views (`.cshtml`)
* Bootstrap 5.3 (Responsive Design)
* jQuery & AJAX (Asynchronous operations)


* **Libraries:**
* `ClosedXML` (Excel Export)
* `DinkToPdf` (PDF Generation)
* `FontAwesome` (Iconography)


## ⚙️ Installation & Setup

### Prerequisites

* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Microsoft SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express or Developer)
* Visual Studio 2022 (Recommended) or VS Code

### Steps

1. **Clone the Repository**
```bash
git clone https://github.com/yourusername/AttendenceManagementSystem.git
cd AttendenceManagementSystem

```


2. **Configure Database**
Update the connection string in `appsettings.json` to point to your local SQL Server instance:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=AttendenceDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}

```


3. **Apply Migrations**
Open the terminal in the project directory and run:
```bash
dotnet ef database update

```


*This will create the database and seed initial roles/users based on `DatabaseInitializer.cs`.*
4. **Run the Application**
```bash
dotnet run

```


Access the app at `https://localhost:7153` (or the port specified in your launch settings).

---

## 🔐 Default Credentials

* **Default Student Password Pattern:** `Student@123` (as seen in)
* **Admin Account:** (Check `SeedData.cs` or register a new user and manually assign the Admin role in the database if not seeded).

---

## 🎨 UI/UX Design

The application features a **"Professional & Elegant"** theme tailored for academic institutions:

* **Primary Colors:** Royal Blue (`#0044cc`) & Dark Blue (`#002a80`).
* **Accent Color:** Golden Yellow (`#F7E27C`) for active states and highlights.
* **Design Principles:**
* **Glassmorphism:** Used in sidebar profiles and error pages.
* **Responsive Sidebar:** Collapsible navigation for better accessibility on smaller screens.
* **Feedback Loops:** Toast notifications for success/error messages.



---


**Developed by:** Shaista Noureen

**Department:** Computer Science, UET Lahore
