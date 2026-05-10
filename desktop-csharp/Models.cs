using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiplomDesktop;

public sealed record UserSession(
    int Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    bool IsSuperuser)
{
    public bool IsAdmin => IsSuperuser || Role == "admin";
    public bool IsTeacher => Role == "teacher";
    public bool IsStudent => Role == "student";

    public string RoleDisplay => IsAdmin
        ? "Администратор"
        : Role switch
        {
            "teacher" => "Преподаватель",
            "student" => "Студент",
            _ => "Пользователь"
        };
}

public sealed record MetricCard(string Title, string Value, string Caption);

public sealed record OptionItem(int Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record ValueOption(string Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record UserRow(
    int Id,
    string Username,
    string FullName,
    string Role,
    string Email,
    string IsActive);

public sealed class RoleEditRow : INotifyPropertyChanged
{
    private string _role = "";

    public int UserId { get; init; }
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string CurrentRole { get; init; } = "";

    public string Role
    {
        get => _role;
        set => SetField(ref _role, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record GroupRow(int Id, string Name, string Speciality, int AdmissionYear, string Curator);

public sealed record SubjectRow(int Id, string Code, string Name, string Description);

public sealed record CourseRow(
    int Id,
    string Group,
    string Subject,
    string Teacher,
    string AcademicYear,
    int Semester,
    int Students,
    int Lessons,
    int Grades);

public sealed record ScheduleRow(
    int Id,
    string Weekday,
    string Time,
    string Group,
    string Subject,
    string Teacher,
    string Room,
    string Building,
    string WeekType);

public sealed record ScheduleEditRow(
    int Id,
    string Weekday,
    string Time,
    string Course,
    string LessonType,
    string Room,
    string Building,
    string WeekType,
    string IsActive,
    string Comment);

public sealed record CourseOption(
    int Id,
    string Label,
    string Group,
    string Subject,
    string Teacher,
    string AcademicYear,
    int Semester);

public sealed record LessonOption(
    int Id,
    int CourseId,
    DateTime Date,
    string Topic,
    string LessonType)
{
    public string Label => $"{Date:dd.MM.yyyy} | {Topic}";
}

public sealed record LessonRow(
    int Id,
    string Date,
    string Type,
    string Topic,
    int AttendanceMarked,
    int GradesMarked);

public sealed record TeacherStudentRow(
    int StudentId,
    string StudentName,
    string RecordBookNumber,
    string Attendance,
    string AverageGrade,
    int Grades);

public sealed class FinalGradeRow : INotifyPropertyChanged
{
    private string _finalGrade = "";
    private string _comment = "";

    public int CourseId { get; init; }
    public int StudentId { get; init; }
    public string StudentName { get; init; } = "";
    public string RecordBookNumber { get; init; } = "";
    public string AverageGrade { get; init; } = "-";
    public string Attendance { get; init; } = "-";

    public string FinalGrade
    {
        get => _finalGrade;
        set => SetField(ref _finalGrade, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class JournalRow : INotifyPropertyChanged
{
    private string _attendanceStatus = "Присутствовал";
    private string _gradeValue = "";
    private string _comment = "";

    public int StudentId { get; init; }
    public string StudentName { get; init; } = "";
    public string RecordBookNumber { get; init; } = "";

    public string AttendanceStatus
    {
        get => _attendanceStatus;
        set => SetField(ref _attendanceStatus, value);
    }

    public string GradeValue
    {
        get => _gradeValue;
        set => SetField(ref _gradeValue, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record StudentSummary(
    string FullName,
    string Group,
    string RecordBookNumber,
    int AttendanceTotal,
    int AttendancePercent,
    int GradesTotal,
    string AverageGrade);

public sealed record StudentCourseRow(
    int CourseId,
    string Subject,
    string Teacher,
    string AcademicYear,
    int Semester,
    string Attendance,
    string AverageGrade,
    int Lessons);

public sealed record StudentJournalRow(
    string Date,
    string Subject,
    string Lesson,
    string Attendance,
    string Grade,
    string GradeType);

public sealed record RecordBookRow(
    string Subject,
    string Teacher,
    string AcademicYear,
    int Semester,
    string AverageGrade,
    string FinalGrade,
    string Attendance,
    int GradesCount);

public sealed record AnalyticsRow(
    string Name,
    string Group,
    string Attendance,
    string AverageGrade,
    string RiskLevel,
    string Recommendation);

public sealed record GradeDistributionRow(string Grade, int Count);

public sealed record AttendanceAnalyticsRow(string Group, string Subject, int Total, int Present, int Absent, int Late, int Excused, string Percent);

public sealed class TeacherJournalState
{
    public CourseOption? Course { get; set; }
    public LessonOption? Lesson { get; set; }
    public ObservableCollection<JournalRow> Rows { get; } = new();
}
