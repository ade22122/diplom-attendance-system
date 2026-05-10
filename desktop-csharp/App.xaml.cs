using System.Windows;

namespace DiplomDesktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--smoke-test"))
        {
            var database = new DatabaseService();
            var user = database.Authenticate("teacher1", "teacher12345");
            if (user is null)
            {
                Console.Error.WriteLine("teacher1 authentication failed");
                Shutdown(1);
                return;
            }

            var metrics = database.LoadAdminMetrics();
            _ = database.LoadTeacherMetrics(user.Id);
            _ = database.LoadTodaySchedule(user.Id, "teacher");
            var courses = database.LoadTeacherCourses(user.Id);
            var firstCourse = courses.FirstOrDefault();
            if (firstCourse is not null)
            {
                _ = database.LoadTeacherStudents(firstCourse.Id);
                _ = database.LoadLessonRows(firstCourse.Id);
                _ = database.LoadFinalGrades(firstCourse.Id);
            }

            var student = database.Authenticate("student1", "student12345");
            if (student is not null)
            {
                _ = database.LoadStudentSummary(student.Id);
                _ = database.LoadRecordBook(student.Id);
                _ = database.LoadRiskAnalytics(userId: student.Id);
                _ = database.LoadTodaySchedule(student.Id, "student");
            }

            _ = database.LoadRoleRows();
            _ = database.LoadGroups();
            _ = database.LoadSubjects();
            _ = database.LoadScheduleEditorRows();
            _ = database.LoadAttendanceAnalytics();
            _ = database.LoadGradeDistribution();

            Console.WriteLine($"smoke ok: {user.FullName}, metrics={metrics.Count}, courses={courses.Count}");
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
    }
}
