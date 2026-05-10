using System.Data;
using System.Globalization;
using System.IO;
using Npgsql;
using NpgsqlTypes;

namespace DiplomDesktop;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    private static readonly IReadOnlyDictionary<string, string> RoleLabels = new Dictionary<string, string>
    {
        ["admin"] = "Администратор",
        ["teacher"] = "Преподаватель",
        ["student"] = "Студент"
    };

    private static readonly IReadOnlyDictionary<string, string> AttendanceLabels = new Dictionary<string, string>
    {
        ["present"] = "Присутствовал",
        ["absent"] = "Отсутствовал",
        ["late"] = "Опоздал",
        ["excused"] = "Уважительная причина"
    };

    private static readonly IReadOnlyDictionary<string, string> AttendanceCodes =
        AttendanceLabels.ToDictionary(item => item.Value, item => item.Key);

    private static readonly IReadOnlyDictionary<string, string> GradeLabels = new Dictionary<string, string>
    {
        ["2"] = "2",
        ["3"] = "3",
        ["4"] = "4",
        ["5"] = "5",
        ["pass"] = "Зачет",
        ["fail"] = "Незачет"
    };

    private static readonly IReadOnlyDictionary<string, string> GradeCodes =
        GradeLabels.ToDictionary(item => item.Value, item => item.Key);

    public DatabaseService()
    {
        var env = LoadEnv();
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = env.GetValueOrDefault("POSTGRES_HOST", "localhost"),
            Port = int.Parse(env.GetValueOrDefault("POSTGRES_PORT", "5432"), CultureInfo.InvariantCulture),
            Database = env.GetValueOrDefault("POSTGRES_DB", "diplom"),
            Username = env.GetValueOrDefault("POSTGRES_USER", "postgres"),
            Password = env.GetValueOrDefault("POSTGRES_PASSWORD", ""),
            IncludeErrorDetail = true,
            Pooling = true
        };
        _connectionString = builder.ConnectionString;
    }

    public static IReadOnlyList<string> AttendanceDisplayValues => AttendanceLabels.Values.ToList();

    public static IReadOnlyList<string> GradeDisplayValues => new[] { "" }.Concat(GradeLabels.Values).ToList();

    public static IReadOnlyList<string> RoleDisplayValues => RoleLabels.Values.ToList();

    public static IReadOnlyList<ValueOption> RoleOptions => RoleLabels
        .Select(item => new ValueOption(item.Key, item.Value))
        .ToList();

    public static IReadOnlyList<ValueOption> LessonTypeOptions => new List<ValueOption>
    {
        new("practice", "Практика"),
        new("lecture", "Лекция"),
        new("lab", "Лабораторная"),
        new("seminar", "Семинар"),
        new("exam", "Контроль")
    };

    public static IReadOnlyList<ValueOption> WeekTypeOptions => new List<ValueOption>
    {
        new("every", "Каждую неделю"),
        new("even", "Четная неделя"),
        new("odd", "Нечетная неделя")
    };

    public static IReadOnlyList<ValueOption> WeekdayOptions => new List<ValueOption>
    {
        new("1", "Понедельник"),
        new("2", "Вторник"),
        new("3", "Среда"),
        new("4", "Четверг"),
        new("5", "Пятница"),
        new("6", "Суббота")
    };

    public UserSession? Authenticate(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                u.id,
                u.username,
                u.password,
                u.first_name,
                u.last_name,
                u.email,
                u.is_active,
                u.is_superuser,
                COALESCE(p.role, CASE WHEN u.is_superuser THEN 'admin' ELSE 'student' END) AS role
            FROM auth_user u
            LEFT JOIN attendance_profile p ON p.user_id = u.id
            WHERE u.username = @username
            """,
            connection);
        command.Parameters.AddWithValue("username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || !reader.GetBoolean(reader.GetOrdinal("is_active")))
        {
            return null;
        }

        var encodedPassword = reader.GetString(reader.GetOrdinal("password"));
        if (!DjangoPasswordHasher.Verify(password, encodedPassword))
        {
            return null;
        }

        var firstName = ReadString(reader, "first_name");
        var lastName = ReadString(reader, "last_name");
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = reader.GetString(reader.GetOrdinal("username"));
        }

        return new UserSession(
            reader.GetInt32(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("username")),
            fullName,
            ReadString(reader, "email"),
            reader.GetString(reader.GetOrdinal("role")),
            reader.GetBoolean(reader.GetOrdinal("is_superuser")));
    }

    public IReadOnlyList<MetricCard> LoadAdminMetrics()
    {
        using var connection = OpenConnection();
        var metrics = new List<MetricCard>
        {
            new("Пользователи", Count(connection, "auth_user").ToString(CultureInfo.InvariantCulture), "все учетные записи"),
            new("Преподаватели", Scalar<int>(connection, "SELECT COUNT(*) FROM attendance_profile WHERE role = 'teacher'").ToString(CultureInfo.InvariantCulture), "активные роли"),
            new("Студенты", Scalar<int>(connection, "SELECT COUNT(*) FROM attendance_studentcard WHERE is_active = TRUE").ToString(CultureInfo.InvariantCulture), "обучаются сейчас"),
            new("Группы", Count(connection, "attendance_studygroup").ToString(CultureInfo.InvariantCulture), "учебные группы"),
            new("Дисциплины", Count(connection, "attendance_subject").ToString(CultureInfo.InvariantCulture), "справочник"),
            new("Журналы", Count(connection, "attendance_course").ToString(CultureInfo.InvariantCulture), "курсы групп"),
            new("Занятия", Count(connection, "attendance_lesson").ToString(CultureInfo.InvariantCulture), "создано занятий"),
            new("Оценки", Count(connection, "attendance_grade").ToString(CultureInfo.InvariantCulture), "записей успеваемости")
        };

        return metrics;
    }

    public IReadOnlyList<MetricCard> LoadTeacherMetrics(int teacherId)
    {
        using var connection = OpenConnection();
        var metrics = new List<MetricCard>
        {
            new("Мои курсы", Scalar<int>(connection, "SELECT COUNT(*) FROM attendance_course WHERE teacher_id = " + teacherId).ToString(CultureInfo.InvariantCulture), "назначенные журналы"),
            new("Мои студенты", Scalar<int>(connection, "SELECT COUNT(DISTINCT st.id) FROM attendance_studentcard st JOIN attendance_course c ON c.group_id = st.group_id WHERE c.teacher_id = " + teacherId + " AND st.is_active = TRUE").ToString(CultureInfo.InvariantCulture), "уникальные студенты"),
            new("Занятий", Scalar<int>(connection, "SELECT COUNT(*) FROM attendance_lesson l JOIN attendance_course c ON c.id = l.course_id WHERE c.teacher_id = " + teacherId).ToString(CultureInfo.InvariantCulture), "создано в журналах"),
            new("Оценок", Scalar<int>(connection, "SELECT COUNT(*) FROM attendance_grade gr JOIN attendance_course c ON c.id = gr.course_id WHERE c.teacher_id = " + teacherId).ToString(CultureInfo.InvariantCulture), "по вашим курсам")
        };

        return metrics;
    }

    public IReadOnlyList<UserRow> LoadUsers()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                u.id,
                u.username,
                u.first_name,
                u.last_name,
                u.email,
                u.is_active,
                COALESCE(p.role, CASE WHEN u.is_superuser THEN 'admin' ELSE 'student' END) AS role
            FROM auth_user u
            LEFT JOIN attendance_profile p ON p.user_id = u.id
            ORDER BY role, u.last_name, u.first_name, u.username
            """,
            connection);

        using var reader = command.ExecuteReader();
        var users = new List<UserRow>();
        while (reader.Read())
        {
            var username = ReadString(reader, "username");
            var fullName = string.Join(" ", new[] { ReadString(reader, "first_name"), ReadString(reader, "last_name") }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            users.Add(new UserRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                username,
                string.IsNullOrWhiteSpace(fullName) ? username : fullName,
                RoleLabels.GetValueOrDefault(ReadString(reader, "role"), ReadString(reader, "role")),
                ReadString(reader, "email"),
                reader.GetBoolean(reader.GetOrdinal("is_active")) ? "Да" : "Нет"));
        }

        return users;
    }

    public IReadOnlyList<RoleEditRow> LoadRoleRows()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                u.id,
                u.username,
                u.first_name,
                u.last_name,
                u.email,
                COALESCE(p.role, CASE WHEN u.is_superuser THEN 'admin' ELSE 'student' END) AS role
            FROM auth_user u
            LEFT JOIN attendance_profile p ON p.user_id = u.id
            WHERE u.is_active = TRUE
            ORDER BY role, u.last_name, u.first_name, u.username
            """,
            connection);

        using var reader = command.ExecuteReader();
        var rows = new List<RoleEditRow>();
        while (reader.Read())
        {
            var username = ReadString(reader, "username");
            var fullName = string.Join(" ", new[] { ReadString(reader, "first_name"), ReadString(reader, "last_name") }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            var role = ReadString(reader, "role");
            rows.Add(new RoleEditRow
            {
                UserId = reader.GetInt32(reader.GetOrdinal("id")),
                Username = username,
                FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName,
                Email = ReadString(reader, "email"),
                CurrentRole = RoleLabels.GetValueOrDefault(role, role),
                Role = RoleLabels.GetValueOrDefault(role, role)
            });
        }

        return rows;
    }

    public void SaveUserRole(int userId, string roleDisplay)
    {
        var role = RoleLabels.FirstOrDefault(item => item.Value == roleDisplay).Key ?? "student";
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var profileCommand = new NpgsqlCommand(
            """
            INSERT INTO attendance_profile (user_id, role, patronymic, phone, avatar_url)
            VALUES (@user_id, @role, '', '', '')
            ON CONFLICT (user_id)
            DO UPDATE SET role = EXCLUDED.role
            """,
            connection,
            transaction))
        {
            profileCommand.Parameters.AddWithValue("user_id", userId);
            profileCommand.Parameters.AddWithValue("role", role);
            profileCommand.ExecuteNonQuery();
        }

        using (var userCommand = new NpgsqlCommand(
            """
            UPDATE auth_user
            SET is_staff = @is_staff,
                is_superuser = CASE WHEN @role = 'admin' THEN is_superuser ELSE FALSE END
            WHERE id = @user_id
            """,
            connection,
            transaction))
        {
            userCommand.Parameters.AddWithValue("user_id", userId);
            userCommand.Parameters.AddWithValue("role", role);
            userCommand.Parameters.AddWithValue("is_staff", role == "admin");
            userCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public int CreateUser(
        string username,
        string password,
        string firstName,
        string lastName,
        string email,
        string roleDisplay,
        int? groupId,
        string recordBookNumber)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Введите логин пользователя.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Введите пароль пользователя.");
        }

        var role = RoleLabels.FirstOrDefault(item => item.Value == roleDisplay).Key ?? "student";
        if (role == "student" && groupId is null)
        {
            throw new InvalidOperationException("Для студента выберите учебную группу.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        int userId;
        using (var command = new NpgsqlCommand(
            """
            INSERT INTO auth_user (
                password,
                last_login,
                is_superuser,
                username,
                first_name,
                last_name,
                email,
                is_staff,
                is_active,
                date_joined
            )
            VALUES (@password, NULL, FALSE, @username, @first_name, @last_name, @email, @is_staff, TRUE, NOW())
            RETURNING id
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("password", DjangoPasswordHasher.Hash(password));
            command.Parameters.AddWithValue("username", username.Trim());
            command.Parameters.AddWithValue("first_name", firstName.Trim());
            command.Parameters.AddWithValue("last_name", lastName.Trim());
            command.Parameters.AddWithValue("email", email.Trim());
            command.Parameters.AddWithValue("is_staff", role == "admin");
            userId = Convert.ToInt32(command.ExecuteScalar()!);
        }

        using (var profileCommand = new NpgsqlCommand(
            """
            INSERT INTO attendance_profile (user_id, role, patronymic, phone, avatar_url)
            VALUES (@user_id, @role, '', '', '')
            """,
            connection,
            transaction))
        {
            profileCommand.Parameters.AddWithValue("user_id", userId);
            profileCommand.Parameters.AddWithValue("role", role);
            profileCommand.ExecuteNonQuery();
        }

        if (role == "student")
        {
            using var studentCommand = new NpgsqlCommand(
                """
                INSERT INTO attendance_studentcard (user_id, group_id, record_book_number, enrollment_date, is_active)
                VALUES (@user_id, @group_id, @record_book_number, CURRENT_DATE, TRUE)
                """,
                connection,
                transaction);
            studentCommand.Parameters.AddWithValue("user_id", userId);
            studentCommand.Parameters.AddWithValue("group_id", groupId!.Value);
            studentCommand.Parameters.AddWithValue("record_book_number", string.IsNullOrWhiteSpace(recordBookNumber) ? $"RB{userId:00000}" : recordBookNumber.Trim());
            studentCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return userId;
    }

    public IReadOnlyList<GroupRow> LoadGroups()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                g.id,
                g.name,
                g.speciality,
                g.admission_year,
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username, '') AS curator
            FROM attendance_studygroup g
            LEFT JOIN auth_user u ON u.id = g.curator_id
            ORDER BY g.name
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rows = new List<GroupRow>();
        while (reader.Read())
        {
            rows.Add(new GroupRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "name"),
                ReadString(reader, "speciality"),
                reader.GetInt32(reader.GetOrdinal("admission_year")),
                ReadString(reader, "curator")));
        }

        return rows;
    }

    public IReadOnlyList<SubjectRow> LoadSubjects()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT id, COALESCE(code, '') AS code, name, description
            FROM attendance_subject
            ORDER BY name
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rows = new List<SubjectRow>();
        while (reader.Read())
        {
            rows.Add(new SubjectRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "code"),
                ReadString(reader, "name"),
                ReadString(reader, "description")));
        }

        return rows;
    }

    public IReadOnlyList<OptionItem> LoadGroupOptions()
    {
        return LoadGroups().Select(group => new OptionItem(group.Id, group.Name)).ToList();
    }

    public IReadOnlyList<OptionItem> LoadSubjectOptions()
    {
        return LoadSubjects().Select(subject => new OptionItem(subject.Id, string.IsNullOrWhiteSpace(subject.Code) ? subject.Name : $"{subject.Code} | {subject.Name}")).ToList();
    }

    public IReadOnlyList<OptionItem> LoadTeacherOptions()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT u.id, COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS full_name
            FROM auth_user u
            JOIN attendance_profile p ON p.user_id = u.id
            WHERE p.role = 'teacher' AND u.is_active = TRUE
            ORDER BY u.last_name, u.first_name, u.username
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rows = new List<OptionItem>();
        while (reader.Read())
        {
            rows.Add(new OptionItem(reader.GetInt32(reader.GetOrdinal("id")), ReadString(reader, "full_name")));
        }

        return rows;
    }

    public IReadOnlyList<OptionItem> LoadCourseOptions()
    {
        return LoadCourses().Select(course => new OptionItem(course.Id, $"{course.Group} | {course.Subject} | {course.Teacher}")).ToList();
    }

    public int CreateGroup(string name, string speciality, int admissionYear, int? curatorId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO attendance_studygroup (name, speciality, admission_year, curator_id)
            VALUES (@name, @speciality, @admission_year, @curator_id)
            ON CONFLICT (name)
            DO UPDATE SET speciality = EXCLUDED.speciality,
                          admission_year = EXCLUDED.admission_year,
                          curator_id = EXCLUDED.curator_id
            RETURNING id
            """,
            connection);
        command.Parameters.AddWithValue("name", name.Trim());
        command.Parameters.AddWithValue("speciality", speciality.Trim());
        command.Parameters.AddWithValue("admission_year", admissionYear);
        var curatorParameter = command.Parameters.Add("curator_id", NpgsqlDbType.Integer);
        curatorParameter.Value = curatorId is null ? DBNull.Value : curatorId.Value;
        return Convert.ToInt32(command.ExecuteScalar()!);
    }

    public int CreateSubject(string code, string name, string description)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO attendance_subject (code, name, description)
            VALUES (NULLIF(@code, ''), @name, @description)
            ON CONFLICT (code)
            DO UPDATE SET name = EXCLUDED.name,
                          description = EXCLUDED.description
            RETURNING id
            """,
            connection);
        command.Parameters.AddWithValue("code", code.Trim());
        command.Parameters.AddWithValue("name", name.Trim());
        command.Parameters.AddWithValue("description", description.Trim());
        return Convert.ToInt32(command.ExecuteScalar()!);
    }

    public int CreateCourse(int subjectId, int groupId, int teacherId, int semester, string academicYear)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO attendance_course (subject_id, group_id, teacher_id, semester, academic_year)
            VALUES (@subject_id, @group_id, @teacher_id, @semester, @academic_year)
            ON CONFLICT ON CONSTRAINT unique_course_for_group_teacher_year
            DO UPDATE SET semester = EXCLUDED.semester
            RETURNING id
            """,
            connection);
        command.Parameters.AddWithValue("subject_id", subjectId);
        command.Parameters.AddWithValue("group_id", groupId);
        command.Parameters.AddWithValue("teacher_id", teacherId);
        command.Parameters.AddWithValue("semester", semester);
        command.Parameters.AddWithValue("academic_year", academicYear.Trim());
        return Convert.ToInt32(command.ExecuteScalar()!);
    }

    public IReadOnlyList<CourseRow> LoadCourses()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                c.id,
                g.name AS group_name,
                s.name AS subject_name,
                c.academic_year,
                c.semester,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name,
                COUNT(DISTINCT st.id) FILTER (WHERE st.is_active = TRUE) AS students_count,
                COUNT(DISTINCT l.id) AS lessons_count,
                COUNT(DISTINCT gr.id) AS grades_count
            FROM attendance_course c
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            LEFT JOIN attendance_studentcard st ON st.group_id = g.id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_grade gr ON gr.course_id = c.id
            GROUP BY c.id, g.name, s.name, c.academic_year, c.semester, t.first_name, t.last_name, t.username
            ORDER BY g.name, s.name
            """,
            connection);

        using var reader = command.ExecuteReader();
        var rows = new List<CourseRow>();
        while (reader.Read())
        {
            rows.Add(new CourseRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "group_name"),
                ReadString(reader, "subject_name"),
                ReadString(reader, "teacher_name"),
                ReadString(reader, "academic_year"),
                reader.GetInt32(reader.GetOrdinal("semester")),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("students_count"))),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("lessons_count"))),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("grades_count")))));
        }

        return rows;
    }

    public IReadOnlyList<ScheduleRow> LoadSchedule(int? userId = null, string role = "")
    {
        using var connection = OpenConnection();
        var where = role switch
        {
            "teacher" => "WHERE e.is_active = TRUE AND c.teacher_id = @user_id",
            "student" => "WHERE e.is_active = TRUE AND c.group_id = (SELECT group_id FROM attendance_studentcard WHERE user_id = @user_id LIMIT 1)",
            _ => "WHERE e.is_active = TRUE"
        };

        using var command = new NpgsqlCommand(
            $$"""
            SELECT
                e.id,
                CASE e.weekday
                    WHEN 1 THEN 'Понедельник'
                    WHEN 2 THEN 'Вторник'
                    WHEN 3 THEN 'Среда'
                    WHEN 4 THEN 'Четверг'
                    WHEN 5 THEN 'Пятница'
                    WHEN 6 THEN 'Суббота'
                    ELSE e.weekday::text
                END AS weekday_label,
                to_char(e.start_time, 'HH24:MI') || '-' || to_char(e.end_time, 'HH24:MI') AS lesson_time,
                g.name AS group_name,
                s.name AS subject_name,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name,
                e.room,
                e.building,
                CASE e.week_type
                    WHEN 'every' THEN 'Каждую неделю'
                    WHEN 'even' THEN 'Четная'
                    WHEN 'odd' THEN 'Нечетная'
                    ELSE e.week_type
                END AS week_type_label
            FROM attendance_scheduleentry e
            JOIN attendance_course c ON c.id = e.course_id
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            {{where}}
            ORDER BY e.weekday, e.start_time, g.name, s.name
            """,
            connection);
        if (userId is not null)
        {
            command.Parameters.AddWithValue("user_id", userId.Value);
        }

        using var reader = command.ExecuteReader();
        var rows = new List<ScheduleRow>();
        while (reader.Read())
        {
            rows.Add(new ScheduleRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "weekday_label"),
                ReadString(reader, "lesson_time"),
                ReadString(reader, "group_name"),
                ReadString(reader, "subject_name"),
                ReadString(reader, "teacher_name"),
                ReadString(reader, "room"),
                ReadString(reader, "building"),
                ReadString(reader, "week_type_label")));
        }

        return rows;
    }

    public IReadOnlyList<ScheduleRow> LoadTodaySchedule(int userId, string role)
    {
        var weekday = DateTime.Today.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => 0
        };
        if (weekday == 0)
        {
            return Array.Empty<ScheduleRow>();
        }

        using var connection = OpenConnection();
        var where = role switch
        {
            "teacher" => "e.is_active = TRUE AND e.weekday = @weekday AND c.teacher_id = @user_id",
            "student" => "e.is_active = TRUE AND e.weekday = @weekday AND c.group_id = (SELECT group_id FROM attendance_studentcard WHERE user_id = @user_id LIMIT 1)",
            _ => "e.is_active = TRUE AND e.weekday = @weekday"
        };
        using var command = new NpgsqlCommand(
            $$"""
            SELECT
                e.id,
                CASE e.weekday
                    WHEN 1 THEN 'Понедельник'
                    WHEN 2 THEN 'Вторник'
                    WHEN 3 THEN 'Среда'
                    WHEN 4 THEN 'Четверг'
                    WHEN 5 THEN 'Пятница'
                    WHEN 6 THEN 'Суббота'
                    ELSE e.weekday::text
                END AS weekday_label,
                to_char(e.start_time, 'HH24:MI') || '-' || to_char(e.end_time, 'HH24:MI') AS lesson_time,
                g.name AS group_name,
                s.name AS subject_name,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name,
                e.room,
                e.building,
                CASE e.week_type
                    WHEN 'every' THEN 'Каждую неделю'
                    WHEN 'even' THEN 'Четная'
                    WHEN 'odd' THEN 'Нечетная'
                    ELSE e.week_type
                END AS week_type_label
            FROM attendance_scheduleentry e
            JOIN attendance_course c ON c.id = e.course_id
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            WHERE {{where}}
            ORDER BY e.start_time, g.name, s.name
            """,
            connection);
        command.Parameters.AddWithValue("weekday", weekday);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var rows = new List<ScheduleRow>();
        while (reader.Read())
        {
            rows.Add(new ScheduleRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "weekday_label"),
                ReadString(reader, "lesson_time"),
                ReadString(reader, "group_name"),
                ReadString(reader, "subject_name"),
                ReadString(reader, "teacher_name"),
                ReadString(reader, "room"),
                ReadString(reader, "building"),
                ReadString(reader, "week_type_label")));
        }

        return rows;
    }

    public IReadOnlyList<ScheduleEditRow> LoadScheduleEditorRows()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                e.id,
                CASE e.weekday
                    WHEN 1 THEN 'Понедельник'
                    WHEN 2 THEN 'Вторник'
                    WHEN 3 THEN 'Среда'
                    WHEN 4 THEN 'Четверг'
                    WHEN 5 THEN 'Пятница'
                    WHEN 6 THEN 'Суббота'
                    ELSE e.weekday::text
                END AS weekday_label,
                to_char(e.start_time, 'HH24:MI') || '-' || to_char(e.end_time, 'HH24:MI') AS lesson_time,
                g.name || ' | ' || s.name || ' | ' || COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS course_label,
                CASE e.lesson_type
                    WHEN 'lecture' THEN 'Лекция'
                    WHEN 'practice' THEN 'Практика'
                    WHEN 'lab' THEN 'Лабораторная'
                    WHEN 'seminar' THEN 'Семинар'
                    WHEN 'exam' THEN 'Контроль'
                    ELSE e.lesson_type
                END AS lesson_type_label,
                e.room,
                e.building,
                CASE e.week_type
                    WHEN 'every' THEN 'Каждую неделю'
                    WHEN 'even' THEN 'Четная неделя'
                    WHEN 'odd' THEN 'Нечетная неделя'
                    ELSE e.week_type
                END AS week_type_label,
                CASE WHEN e.is_active THEN 'Да' ELSE 'Нет' END AS active_label,
                e.comment
            FROM attendance_scheduleentry e
            JOIN attendance_course c ON c.id = e.course_id
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            ORDER BY e.weekday, e.start_time, g.name, s.name
            """,
            connection);

        using var reader = command.ExecuteReader();
        var rows = new List<ScheduleEditRow>();
        while (reader.Read())
        {
            rows.Add(new ScheduleEditRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "weekday_label"),
                ReadString(reader, "lesson_time"),
                ReadString(reader, "course_label"),
                ReadString(reader, "lesson_type_label"),
                ReadString(reader, "room"),
                ReadString(reader, "building"),
                ReadString(reader, "week_type_label"),
                ReadString(reader, "active_label"),
                ReadString(reader, "comment")));
        }

        return rows;
    }

    public int CreateScheduleEntry(
        int courseId,
        int weekday,
        TimeOnly startTime,
        TimeOnly endTime,
        string lessonType,
        string room,
        string building,
        string weekType,
        bool isActive,
        string comment)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO attendance_scheduleentry (
                course_id,
                weekday,
                start_time,
                end_time,
                lesson_type,
                room,
                building,
                week_type,
                is_active,
                comment
            )
            VALUES (@course_id, @weekday, @start_time, @end_time, @lesson_type, @room, @building, @week_type, @is_active, @comment)
            ON CONFLICT ON CONSTRAINT unique_schedule_course_time
            DO UPDATE SET end_time = EXCLUDED.end_time,
                          lesson_type = EXCLUDED.lesson_type,
                          room = EXCLUDED.room,
                          building = EXCLUDED.building,
                          is_active = EXCLUDED.is_active,
                          comment = EXCLUDED.comment
            RETURNING id
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);
        command.Parameters.AddWithValue("weekday", weekday);
        command.Parameters.AddWithValue("start_time", startTime);
        command.Parameters.AddWithValue("end_time", endTime);
        command.Parameters.AddWithValue("lesson_type", lessonType);
        command.Parameters.AddWithValue("room", room.Trim());
        command.Parameters.AddWithValue("building", building.Trim());
        command.Parameters.AddWithValue("week_type", weekType);
        command.Parameters.AddWithValue("is_active", isActive);
        command.Parameters.AddWithValue("comment", comment.Trim());
        return Convert.ToInt32(command.ExecuteScalar()!);
    }

    public void DeleteScheduleEntry(int scheduleEntryId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand("DELETE FROM attendance_scheduleentry WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", scheduleEntryId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<CourseOption> LoadTeacherCourses(int teacherId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                c.id,
                g.name AS group_name,
                s.name AS subject_name,
                c.academic_year,
                c.semester,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name
            FROM attendance_course c
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            WHERE c.teacher_id = @teacher_id
            ORDER BY g.name, s.name
            """,
            connection);
        command.Parameters.AddWithValue("teacher_id", teacherId);

        using var reader = command.ExecuteReader();
        var courses = new List<CourseOption>();
        while (reader.Read())
        {
            var group = ReadString(reader, "group_name");
            var subject = ReadString(reader, "subject_name");
            var year = ReadString(reader, "academic_year");
            var semester = reader.GetInt32(reader.GetOrdinal("semester"));
            var teacher = ReadString(reader, "teacher_name");
            courses.Add(new CourseOption(
                reader.GetInt32(reader.GetOrdinal("id")),
                $"{group} | {subject} | {year}, семестр {semester}",
                group,
                subject,
                teacher,
                year,
                semester));
        }

        return courses;
    }

    public IReadOnlyList<LessonOption> LoadLessons(int courseId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT id, course_id, date, topic, lesson_type
            FROM attendance_lesson
            WHERE course_id = @course_id
            ORDER BY date DESC, created_at DESC
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);

        using var reader = command.ExecuteReader();
        var lessons = new List<LessonOption>();
        while (reader.Read())
        {
            lessons.Add(new LessonOption(
                reader.GetInt32(reader.GetOrdinal("id")),
                reader.GetInt32(reader.GetOrdinal("course_id")),
                ReadDate(reader, "date"),
                ReadString(reader, "topic"),
                ReadString(reader, "lesson_type")));
        }

        return lessons;
    }

    public IReadOnlyList<LessonRow> LoadLessonRows(int courseId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                l.id,
                l.date,
                l.topic,
                CASE l.lesson_type
                    WHEN 'lecture' THEN 'Лекция'
                    WHEN 'practice' THEN 'Практика'
                    WHEN 'lab' THEN 'Лабораторная'
                    WHEN 'seminar' THEN 'Семинар'
                    WHEN 'exam' THEN 'Контроль'
                    ELSE l.lesson_type
                END AS lesson_type_label,
                COUNT(DISTINCT ar.id) AS attendance_marked,
                COUNT(DISTINCT gr.id) AS grades_marked
            FROM attendance_lesson l
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id
            LEFT JOIN attendance_grade gr ON gr.lesson_id = l.id
            WHERE l.course_id = @course_id
            GROUP BY l.id, l.date, l.topic, l.lesson_type
            ORDER BY l.date DESC, l.created_at DESC
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);

        using var reader = command.ExecuteReader();
        var rows = new List<LessonRow>();
        while (reader.Read())
        {
            rows.Add(new LessonRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadDate(reader, "date").ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                ReadString(reader, "lesson_type_label"),
                ReadString(reader, "topic"),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_marked"))),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("grades_marked")))));
        }

        return rows;
    }

    public IReadOnlyList<TeacherStudentRow> LoadTeacherStudents(int courseId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                st.id,
                st.record_book_number,
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS student_name,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive,
                COUNT(DISTINCT gr.id) AS grades_count,
                AVG(CASE WHEN gr.value IN ('2','3','4','5') THEN gr.value::numeric END) AS average_grade
            FROM attendance_course c
            JOIN attendance_studentcard st ON st.group_id = c.group_id AND st.is_active = TRUE
            JOIN auth_user u ON u.id = st.user_id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id AND ar.student_id = st.id
            LEFT JOIN attendance_grade gr ON gr.course_id = c.id AND gr.student_id = st.id
            WHERE c.id = @course_id
            GROUP BY st.id, st.record_book_number, u.first_name, u.last_name, u.username
            ORDER BY u.last_name, u.first_name, u.username
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);

        using var reader = command.ExecuteReader();
        var rows = new List<TeacherStudentRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
            var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
            var attendance = total == 0 ? "-" : $"{(int)Math.Round(positive * 100.0 / total)}%";
            var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
                ? "-"
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade"))).ToString("0.00", CultureInfo.InvariantCulture);
            rows.Add(new TeacherStudentRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "student_name"),
                ReadString(reader, "record_book_number"),
                attendance,
                average,
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("grades_count")))));
        }

        return rows;
    }

    public LessonOption CreateLesson(int courseId, DateTime date, string lessonType, string topic)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO attendance_lesson (course_id, date, lesson_type, topic, created_at)
            VALUES (@course_id, @date::date, @lesson_type, @topic, NOW())
            RETURNING id, course_id, date, topic, lesson_type
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);
        command.Parameters.AddWithValue("date", date);
        command.Parameters.AddWithValue("lesson_type", lessonType);
        command.Parameters.AddWithValue("topic", topic);

        using var reader = command.ExecuteReader();
        reader.Read();
        return new LessonOption(
            reader.GetInt32(reader.GetOrdinal("id")),
            reader.GetInt32(reader.GetOrdinal("course_id")),
            ReadDate(reader, "date"),
            ReadString(reader, "topic"),
            ReadString(reader, "lesson_type"));
    }

    public IReadOnlyList<JournalRow> LoadJournalRows(int courseId, int lessonId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                st.id AS student_id,
                st.record_book_number,
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS student_name,
                ar.status,
                ar.comment,
                gr.value AS grade_value
            FROM attendance_course c
            JOIN attendance_studentcard st ON st.group_id = c.group_id AND st.is_active = TRUE
            JOIN auth_user u ON u.id = st.user_id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = @lesson_id AND ar.student_id = st.id
            LEFT JOIN attendance_grade gr
                ON gr.course_id = c.id
                AND gr.lesson_id = @lesson_id
                AND gr.student_id = st.id
                AND gr.grade_type = 'current'
            WHERE c.id = @course_id
            ORDER BY u.last_name, u.first_name, u.username
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);
        command.Parameters.AddWithValue("lesson_id", lessonId);

        using var reader = command.ExecuteReader();
        var rows = new List<JournalRow>();
        while (reader.Read())
        {
            var status = ReadNullableString(reader, "status");
            var grade = ReadNullableString(reader, "grade_value");
            rows.Add(new JournalRow
            {
                StudentId = reader.GetInt32(reader.GetOrdinal("student_id")),
                StudentName = ReadString(reader, "student_name"),
                RecordBookNumber = ReadString(reader, "record_book_number"),
                AttendanceStatus = status is null ? "Присутствовал" : AttendanceLabels.GetValueOrDefault(status, status),
                GradeValue = grade is null ? "" : GradeLabels.GetValueOrDefault(grade, grade),
                Comment = ReadString(reader, "comment")
            });
        }

        return rows;
    }

    public void SaveJournal(int courseId, int lessonId, DateTime lessonDate, IEnumerable<JournalRow> rows)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var row in rows)
        {
            var status = AttendanceCodes.GetValueOrDefault(row.AttendanceStatus, "present");
            using (var attendanceCommand = new NpgsqlCommand(
                """
                INSERT INTO attendance_attendancerecord (lesson_id, student_id, status, comment, updated_at)
                VALUES (@lesson_id, @student_id, @status, @comment, NOW())
                ON CONFLICT ON CONSTRAINT unique_attendance_for_lesson_student
                DO UPDATE SET status = EXCLUDED.status, comment = EXCLUDED.comment, updated_at = NOW()
                """,
                connection,
                transaction))
            {
                attendanceCommand.Parameters.AddWithValue("lesson_id", lessonId);
                attendanceCommand.Parameters.AddWithValue("student_id", row.StudentId);
                attendanceCommand.Parameters.AddWithValue("status", status);
                attendanceCommand.Parameters.AddWithValue("comment", row.Comment ?? "");
                attendanceCommand.ExecuteNonQuery();
            }

            var gradeCode = GradeCodes.GetValueOrDefault(row.GradeValue ?? "", "");
            if (string.IsNullOrWhiteSpace(gradeCode))
            {
                using var deleteGrade = new NpgsqlCommand(
                    """
                    DELETE FROM attendance_grade
                    WHERE course_id = @course_id
                      AND lesson_id = @lesson_id
                      AND student_id = @student_id
                      AND grade_type = 'current'
                    """,
                    connection,
                    transaction);
                deleteGrade.Parameters.AddWithValue("course_id", courseId);
                deleteGrade.Parameters.AddWithValue("lesson_id", lessonId);
                deleteGrade.Parameters.AddWithValue("student_id", row.StudentId);
                deleteGrade.ExecuteNonQuery();
            }
            else
            {
                using var gradeCommand = new NpgsqlCommand(
                    """
                    INSERT INTO attendance_grade (course_id, student_id, lesson_id, grade_type, value, comment, date, updated_at)
                    VALUES (@course_id, @student_id, @lesson_id, 'current', @value, '', @date::date, NOW())
                    ON CONFLICT ON CONSTRAINT unique_grade_for_lesson_student_type
                    DO UPDATE SET value = EXCLUDED.value, date = EXCLUDED.date, updated_at = NOW()
                    """,
                    connection,
                    transaction);
                gradeCommand.Parameters.AddWithValue("course_id", courseId);
                gradeCommand.Parameters.AddWithValue("student_id", row.StudentId);
                gradeCommand.Parameters.AddWithValue("lesson_id", lessonId);
                gradeCommand.Parameters.AddWithValue("value", gradeCode);
                gradeCommand.Parameters.AddWithValue("date", lessonDate);
                gradeCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public IReadOnlyList<FinalGradeRow> LoadFinalGrades(int courseId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                st.id AS student_id,
                st.record_book_number,
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS student_name,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive,
                AVG(CASE WHEN all_grades.value IN ('2','3','4','5') THEN all_grades.value::numeric END) AS average_grade,
                final_grade.value AS final_value,
                final_grade.comment AS final_comment
            FROM attendance_course c
            JOIN attendance_studentcard st ON st.group_id = c.group_id AND st.is_active = TRUE
            JOIN auth_user u ON u.id = st.user_id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id AND ar.student_id = st.id
            LEFT JOIN attendance_grade all_grades
                ON all_grades.course_id = c.id
                AND all_grades.student_id = st.id
                AND all_grades.grade_type <> 'final'
            LEFT JOIN attendance_grade final_grade
                ON final_grade.course_id = c.id
                AND final_grade.student_id = st.id
                AND final_grade.grade_type = 'final'
            WHERE c.id = @course_id
            GROUP BY st.id, st.record_book_number, u.first_name, u.last_name, u.username, final_grade.value, final_grade.comment
            ORDER BY u.last_name, u.first_name, u.username
            """,
            connection);
        command.Parameters.AddWithValue("course_id", courseId);

        using var reader = command.ExecuteReader();
        var rows = new List<FinalGradeRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
            var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
            var attendance = total == 0 ? "-" : $"{(int)Math.Round(positive * 100.0 / total)}%";
            var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
                ? "-"
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade"))).ToString("0.00", CultureInfo.InvariantCulture);
            var finalValue = ReadNullableString(reader, "final_value");
            rows.Add(new FinalGradeRow
            {
                CourseId = courseId,
                StudentId = reader.GetInt32(reader.GetOrdinal("student_id")),
                StudentName = ReadString(reader, "student_name"),
                RecordBookNumber = ReadString(reader, "record_book_number"),
                Attendance = attendance,
                AverageGrade = average,
                FinalGrade = finalValue is null ? "" : GradeLabels.GetValueOrDefault(finalValue, finalValue),
                Comment = ReadString(reader, "final_comment")
            });
        }

        return rows;
    }

    public void SaveFinalGrades(int courseId, IEnumerable<FinalGradeRow> rows)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var row in rows)
        {
            var gradeCode = GradeCodes.GetValueOrDefault(row.FinalGrade ?? "", "");
            using (var cleanupCommand = new NpgsqlCommand(
                """
                DELETE FROM attendance_grade
                WHERE course_id = @course_id
                  AND student_id = @student_id
                  AND lesson_id IS NULL
                  AND grade_type = 'final'
                """,
                connection,
                transaction))
            {
                cleanupCommand.Parameters.AddWithValue("course_id", courseId);
                cleanupCommand.Parameters.AddWithValue("student_id", row.StudentId);
                cleanupCommand.ExecuteNonQuery();
            }

            if (string.IsNullOrWhiteSpace(gradeCode))
            {
                continue;
            }

            using var command = new NpgsqlCommand(
                """
                INSERT INTO attendance_grade (course_id, student_id, lesson_id, grade_type, value, comment, date, updated_at)
                VALUES (@course_id, @student_id, NULL, 'final', @value, @comment, CURRENT_DATE, NOW())
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("course_id", courseId);
            command.Parameters.AddWithValue("student_id", row.StudentId);
            command.Parameters.AddWithValue("value", gradeCode);
            command.Parameters.AddWithValue("comment", row.Comment ?? "");
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public StudentSummary LoadStudentSummary(int userId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                st.id AS student_id,
                st.record_book_number,
                g.name AS group_name,
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS full_name,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive,
                COUNT(DISTINCT gr.id) AS grades_total,
                AVG(CASE WHEN gr.value IN ('2','3','4','5') THEN gr.value::numeric END) AS average_grade
            FROM attendance_studentcard st
            JOIN auth_user u ON u.id = st.user_id
            JOIN attendance_studygroup g ON g.id = st.group_id
            LEFT JOIN attendance_attendancerecord ar ON ar.student_id = st.id
            LEFT JOIN attendance_grade gr ON gr.student_id = st.id
            WHERE st.user_id = @user_id
            GROUP BY st.id, st.record_book_number, g.name, u.first_name, u.last_name, u.username
            """,
            connection);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new StudentSummary("Студент", "-", "-", 0, 0, 0, "-");
        }

        var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
        var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
        var percent = total == 0 ? 0 : (int)Math.Round(positive * 100.0 / total);
        var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
            ? "-"
            : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade"))).ToString("0.00", CultureInfo.InvariantCulture);

        return new StudentSummary(
            ReadString(reader, "full_name"),
            ReadString(reader, "group_name"),
            ReadString(reader, "record_book_number"),
            total,
            percent,
            Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("grades_total"))),
            average);
    }

    public IReadOnlyList<StudentCourseRow> LoadStudentCourses(int userId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            WITH current_student AS (
                SELECT id, group_id
                FROM attendance_studentcard
                WHERE user_id = @user_id
                LIMIT 1
            )
            SELECT
                c.id,
                s.name AS subject_name,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name,
                c.academic_year,
                c.semester,
                COUNT(DISTINCT l.id) AS lessons_count,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive,
                AVG(CASE WHEN gr.value IN ('2','3','4','5') THEN gr.value::numeric END) AS average_grade
            FROM current_student st
            JOIN attendance_course c ON c.group_id = st.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.student_id = st.id AND ar.lesson_id = l.id
            LEFT JOIN attendance_grade gr ON gr.student_id = st.id AND gr.course_id = c.id
            GROUP BY c.id, s.name, t.first_name, t.last_name, t.username, c.academic_year, c.semester
            ORDER BY s.name
            """,
            connection);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var rows = new List<StudentCourseRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
            var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
            var attendance = total == 0 ? "-" : $"{(int)Math.Round(positive * 100.0 / total)}%";
            var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
                ? "-"
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade"))).ToString("0.00", CultureInfo.InvariantCulture);

            rows.Add(new StudentCourseRow(
                reader.GetInt32(reader.GetOrdinal("id")),
                ReadString(reader, "subject_name"),
                ReadString(reader, "teacher_name"),
                ReadString(reader, "academic_year"),
                reader.GetInt32(reader.GetOrdinal("semester")),
                attendance,
                average,
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("lessons_count")))));
        }

        return rows;
    }

    public IReadOnlyList<StudentJournalRow> LoadStudentJournal(int userId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            WITH current_student AS (
                SELECT id, group_id
                FROM attendance_studentcard
                WHERE user_id = @user_id
                LIMIT 1
            )
            SELECT
                l.date,
                s.name AS subject_name,
                l.topic,
                ar.status,
                gr.value AS grade_value,
                gr.grade_type
            FROM current_student st
            JOIN attendance_course c ON c.group_id = st.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id AND ar.student_id = st.id
            LEFT JOIN attendance_grade gr ON gr.course_id = c.id AND gr.lesson_id = l.id AND gr.student_id = st.id
            ORDER BY l.date DESC, s.name, l.topic
            """,
            connection);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var rows = new List<StudentJournalRow>();
        while (reader.Read())
        {
            var status = ReadNullableString(reader, "status");
            var grade = ReadNullableString(reader, "grade_value");
            var gradeType = ReadNullableString(reader, "grade_type");
            rows.Add(new StudentJournalRow(
                ReadDate(reader, "date").ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                ReadString(reader, "subject_name"),
                ReadString(reader, "topic"),
                status is null ? "-" : AttendanceLabels.GetValueOrDefault(status, status),
                grade is null ? "-" : GradeLabels.GetValueOrDefault(grade, grade),
                gradeType switch
                {
                    "current" => "Текущая",
                    "control" => "Контрольная",
                    "exam" => "Экзамен",
                    "coursework" => "Курсовая",
                    "final" => "Итоговая",
                    _ => "-"
                }));
        }

        return rows;
    }

    public IReadOnlyList<RecordBookRow> LoadRecordBook(int userId)
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            WITH current_student AS (
                SELECT id, group_id
                FROM attendance_studentcard
                WHERE user_id = @user_id
                LIMIT 1
            )
            SELECT
                s.name AS subject_name,
                COALESCE(NULLIF(TRIM(t.first_name || ' ' || t.last_name), ''), t.username) AS teacher_name,
                c.academic_year,
                c.semester,
                AVG(CASE WHEN gr.value IN ('2','3','4','5') AND gr.grade_type <> 'final' THEN gr.value::numeric END) AS average_grade,
                final_grade.value AS final_value,
                COUNT(DISTINCT gr.id) AS grades_count,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive
            FROM current_student st
            JOIN attendance_course c ON c.group_id = st.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            JOIN auth_user t ON t.id = c.teacher_id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id AND ar.student_id = st.id
            LEFT JOIN attendance_grade gr ON gr.course_id = c.id AND gr.student_id = st.id
            LEFT JOIN attendance_grade final_grade
                ON final_grade.course_id = c.id
                AND final_grade.student_id = st.id
                AND final_grade.grade_type = 'final'
                AND final_grade.lesson_id IS NULL
            GROUP BY s.name, t.first_name, t.last_name, t.username, c.academic_year, c.semester, final_grade.value
            ORDER BY c.academic_year, c.semester, s.name
            """,
            connection);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var rows = new List<RecordBookRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
            var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
            var attendance = total == 0 ? "-" : $"{(int)Math.Round(positive * 100.0 / total)}%";
            var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
                ? "-"
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade"))).ToString("0.00", CultureInfo.InvariantCulture);
            var finalValue = ReadNullableString(reader, "final_value");
            rows.Add(new RecordBookRow(
                ReadString(reader, "subject_name"),
                ReadString(reader, "teacher_name"),
                ReadString(reader, "academic_year"),
                reader.GetInt32(reader.GetOrdinal("semester")),
                average,
                finalValue is null ? "-" : GradeLabels.GetValueOrDefault(finalValue, finalValue),
                attendance,
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("grades_count")))));
        }

        return rows;
    }

    public IReadOnlyList<AnalyticsRow> LoadRiskAnalytics(int? teacherId = null, int? userId = null)
    {
        using var connection = OpenConnection();
        var filter = "";
        if (teacherId is not null)
        {
            filter = "WHERE EXISTS (SELECT 1 FROM attendance_course tc WHERE tc.group_id = st.group_id AND tc.teacher_id = @teacher_id)";
        }
        else if (userId is not null)
        {
            filter = "WHERE st.user_id = @user_id";
        }

        using var command = new NpgsqlCommand(
            $$"""
            SELECT
                COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''), u.username) AS student_name,
                g.name AS group_name,
                COUNT(DISTINCT ar.id) AS attendance_total,
                COUNT(DISTINCT ar.id) FILTER (WHERE ar.status IN ('present', 'late', 'excused')) AS attendance_positive,
                AVG(CASE WHEN gr.value IN ('2','3','4','5') THEN gr.value::numeric END) AS average_grade
            FROM attendance_studentcard st
            JOIN auth_user u ON u.id = st.user_id
            JOIN attendance_studygroup g ON g.id = st.group_id
            LEFT JOIN attendance_attendancerecord ar ON ar.student_id = st.id
            LEFT JOIN attendance_grade gr ON gr.student_id = st.id
            {{filter}}
            GROUP BY st.id, u.first_name, u.last_name, u.username, g.name
            ORDER BY g.name, u.last_name, u.first_name, u.username
            """,
            connection);
        if (teacherId is not null)
        {
            command.Parameters.AddWithValue("teacher_id", teacherId.Value);
        }

        if (userId is not null)
        {
            command.Parameters.AddWithValue("user_id", userId.Value);
        }

        using var reader = command.ExecuteReader();
        var rows = new List<AnalyticsRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_total")));
            var positive = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("attendance_positive")));
            var percent = total == 0 ? 0 : (int)Math.Round(positive * 100.0 / total);
            var average = reader.IsDBNull(reader.GetOrdinal("average_grade"))
                ? (decimal?)null
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("average_grade")));

            string risk;
            string recommendation;
            if (total == 0)
            {
                risk = "Нет данных";
                recommendation = "Заполнить журнал занятий для расчета прогноза.";
            }
            else if (percent < 70 || average < 3.2m)
            {
                risk = "Высокий";
                recommendation = "Провести консультацию и проверить пропуски по дисциплинам.";
            }
            else if (percent < 85 || average < 3.8m)
            {
                risk = "Средний";
                recommendation = "Отследить динамику посещаемости и текущих оценок.";
            }
            else
            {
                risk = "Низкий";
                recommendation = "Поддерживать текущую учебную динамику.";
            }

            rows.Add(new AnalyticsRow(
                ReadString(reader, "student_name"),
                ReadString(reader, "group_name"),
                total == 0 ? "-" : $"{percent}%",
                average is null ? "-" : average.Value.ToString("0.00", CultureInfo.InvariantCulture),
                risk,
                recommendation));
        }

        return rows;
    }

    public IReadOnlyList<AttendanceAnalyticsRow> LoadAttendanceAnalytics()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                g.name AS group_name,
                s.name AS subject_name,
                COUNT(ar.id) AS total,
                COUNT(ar.id) FILTER (WHERE ar.status = 'present') AS present_count,
                COUNT(ar.id) FILTER (WHERE ar.status = 'absent') AS absent_count,
                COUNT(ar.id) FILTER (WHERE ar.status = 'late') AS late_count,
                COUNT(ar.id) FILTER (WHERE ar.status = 'excused') AS excused_count
            FROM attendance_course c
            JOIN attendance_studygroup g ON g.id = c.group_id
            JOIN attendance_subject s ON s.id = c.subject_id
            LEFT JOIN attendance_lesson l ON l.course_id = c.id
            LEFT JOIN attendance_attendancerecord ar ON ar.lesson_id = l.id
            GROUP BY g.name, s.name
            ORDER BY g.name, s.name
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rows = new List<AttendanceAnalyticsRow>();
        while (reader.Read())
        {
            var total = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("total")));
            var present = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("present_count")));
            var late = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("late_count")));
            var excused = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("excused_count")));
            var positive = present + late + excused;
            rows.Add(new AttendanceAnalyticsRow(
                ReadString(reader, "group_name"),
                ReadString(reader, "subject_name"),
                total,
                present,
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("absent_count"))),
                late,
                excused,
                total == 0 ? "-" : $"{(int)Math.Round(positive * 100.0 / total)}%"));
        }

        return rows;
    }

    public IReadOnlyList<GradeDistributionRow> LoadGradeDistribution()
    {
        using var connection = OpenConnection();
        using var command = new NpgsqlCommand(
            """
            SELECT
                CASE value
                    WHEN 'pass' THEN 'Зачет'
                    WHEN 'fail' THEN 'Незачет'
                    ELSE value
                END AS grade_label,
                COUNT(*) AS count_value
            FROM attendance_grade
            GROUP BY value
            ORDER BY grade_label
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rows = new List<GradeDistributionRow>();
        while (reader.Read())
        {
            rows.Add(new GradeDistributionRow(
                ReadString(reader, "grade_label"),
                Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("count_value")))));
        }

        return rows;
    }

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static int Count(NpgsqlConnection connection, string table)
    {
        using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", connection);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    private static T Scalar<T>(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static string ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime ReadDate(NpgsqlDataReader reader, string name)
    {
        var value = reader.GetValue(reader.GetOrdinal(name));
        return value switch
        {
            DateTime dateTime => dateTime,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };
    }

    private static Dictionary<string, string> LoadEnv()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    var separator = trimmed.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    env[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
                }

                return env;
            }

            directory = directory.Parent;
        }

        return env;
    }
}
