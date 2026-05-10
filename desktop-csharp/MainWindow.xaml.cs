using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DiplomDesktop;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database = new();
    private readonly TeacherJournalState _teacherJournal = new();
    private UserSession? _session;
    private const double CardRadius = 8;

    private static readonly Brush InkBrush = BrushFrom("#101820");
    private static readonly Brush MutedBrush = BrushFrom("#5F6F73");
    private static readonly Brush AccentBrush = BrushFrom("#0F6F73");
    private static readonly Brush AccentDarkBrush = BrushFrom("#083F45");
    private static readonly Brush SoftBrush = BrushFrom("#EEF6F3");
    private static readonly Brush LineBrush = BrushFrom("#DFE7E4");
    private static readonly Brush GoldBrush = BrushFrom("#F6B64A");
    private static readonly Brush CoralBrush = BrushFrom("#E85D43");

    public MainWindow()
    {
        InitializeComponent();
        UsernameBox.Focus();
        UsernameBox.KeyDown += LoginField_KeyDown;
        PasswordBox.KeyDown += LoginField_KeyDown;
    }

    private void LoginField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginErrorText.Text = "";

        RunUi("Вход в систему", () =>
        {
            var session = _database.Authenticate(UsernameBox.Text.Trim(), PasswordBox.Password);
            if (session is null)
            {
                LoginErrorText.Text = "Неверный логин или пароль. Проверьте учетную запись.";
                return;
            }

            _session = session;
            LoginView.Visibility = Visibility.Collapsed;
            ShellView.Visibility = Visibility.Visible;
            CurrentUserText.Text = session.FullName;
            CurrentRoleText.Text = session.RoleDisplay;
            SetRoleAvatar(session);
            ConfigureNavigation(session);

            if (session.IsAdmin)
            {
                ShowAdminOverview();
            }
            else if (session.IsTeacher)
            {
                ShowTeacherDashboard();
            }
            else
            {
                ShowStudentDashboard();
            }
        });
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _session = null;
        ShellView.Visibility = Visibility.Collapsed;
        LoginView.Visibility = Visibility.Visible;
        PasswordBox.Password = "";
        UsernameBox.Focus();
    }

    private void AdminOverviewButton_Click(object sender, RoutedEventArgs e) => ShowAdminOverview();
    private void AdminUsersButton_Click(object sender, RoutedEventArgs e) => ShowAdminUsers();
    private void AdminRolesButton_Click(object sender, RoutedEventArgs e) => ShowAdminRoles();
    private void AdminDirectoriesButton_Click(object sender, RoutedEventArgs e) => ShowAdminDirectories();
    private void AdminCoursesButton_Click(object sender, RoutedEventArgs e) => ShowAdminCourses();
    private void AdminScheduleButton_Click(object sender, RoutedEventArgs e) => ShowSchedule("Общее расписание", "Все активные пары по группам и преподавателям.", "admin");
    private void AdminScheduleManageButton_Click(object sender, RoutedEventArgs e) => ShowScheduleManager();
    private void AdminAnalyticsButton_Click(object sender, RoutedEventArgs e) => ShowAnalytics("Аналитика и риски", "Сводка по посещаемости, оценкам и студентам с риском.", "admin");
    private void TeacherDashboardButton_Click(object sender, RoutedEventArgs e) => ShowTeacherDashboard();
    private void TeacherCoursesButton_Click(object sender, RoutedEventArgs e) => ShowTeacherCourses();
    private void TeacherJournalButton_Click(object sender, RoutedEventArgs e) => ShowTeacherJournal();
    private void TeacherFinalGradesButton_Click(object sender, RoutedEventArgs e) => ShowTeacherFinalGrades();
    private void TeacherScheduleButton_Click(object sender, RoutedEventArgs e) => ShowSchedule("Мое расписание", "Пары, назначенные текущему преподавателю.", "teacher");
    private void TeacherAnalyticsButton_Click(object sender, RoutedEventArgs e) => ShowAnalytics("Учебные риски", "Студенты ваших групп, которым может понадобиться внимание.", "teacher");
    private void StudentDashboardButton_Click(object sender, RoutedEventArgs e) => ShowStudentDashboard();
    private void StudentJournalButton_Click(object sender, RoutedEventArgs e) => ShowStudentJournal();
    private void StudentRecordBookButton_Click(object sender, RoutedEventArgs e) => ShowStudentRecordBook();
    private void StudentScheduleButton_Click(object sender, RoutedEventArgs e) => ShowSchedule("Расписание группы", "Актуальное расписание группы студента.", "student");
    private void StudentRecommendationsButton_Click(object sender, RoutedEventArgs e) => ShowAnalytics("Рекомендации", "Персональные подсказки по посещаемости и успеваемости.", "student");

    private void ConfigureNavigation(UserSession session)
    {
        var adminVisibility = session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        var teacherVisibility = session.IsTeacher ? Visibility.Visible : Visibility.Collapsed;
        var studentVisibility = session.IsStudent ? Visibility.Visible : Visibility.Collapsed;

        AdminOverviewButton.Visibility = adminVisibility;
        AdminUsersButton.Visibility = adminVisibility;
        AdminRolesButton.Visibility = adminVisibility;
        AdminDirectoriesButton.Visibility = adminVisibility;
        AdminCoursesButton.Visibility = adminVisibility;
        AdminScheduleButton.Visibility = adminVisibility;
        AdminScheduleManageButton.Visibility = adminVisibility;
        AdminAnalyticsButton.Visibility = adminVisibility;

        TeacherDashboardButton.Visibility = teacherVisibility;
        TeacherCoursesButton.Visibility = teacherVisibility;
        TeacherJournalButton.Visibility = teacherVisibility;
        TeacherFinalGradesButton.Visibility = teacherVisibility;
        TeacherScheduleButton.Visibility = teacherVisibility;
        TeacherAnalyticsButton.Visibility = teacherVisibility;

        StudentDashboardButton.Visibility = studentVisibility;
        StudentJournalButton.Visibility = studentVisibility;
        StudentRecordBookButton.Visibility = studentVisibility;
        StudentScheduleButton.Visibility = studentVisibility;
        StudentRecommendationsButton.Visibility = studentVisibility;
    }

    private void SetRoleAvatar(UserSession session)
    {
        var asset = session.IsAdmin
            ? "avatar-admin.png"
            : session.IsTeacher
                ? "avatar-teacher.png"
                : "avatar-student.png";
        CurrentAvatarImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{asset}", UriKind.Absolute));
    }

    private void ShowAdminOverview()
    {
        SetPage("Сводка системы", "Ключевые показатели электронного журнала и наполненности базы данных.");
        Activate(AdminOverviewButton);

        RunUi("Сводка", () =>
        {
            var metrics = _database.LoadAdminMetrics();
            var root = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = CreateMetricWrap(metrics)
            };
            BodyHost.Content = root;
        });
    }

    private void ShowAdminUsers()
    {
        SetPage("Пользователи", "Учетные записи Django, роли и активность пользователей.");
        Activate(AdminUsersButton);

        RunUi("Пользователи", () =>
        {
            BodyHost.Content = CreateSearchableTableCard(
                _database.LoadUsers(),
                "users.csv",
                ("Id", "ID", 70),
                ("Username", "Логин", 140),
                ("FullName", "ФИО", 240),
                ("Role", "Роль", 150),
                ("Email", "Email", 220),
                ("IsActive", "Активен", 90));
        });
    }

    private void ShowAdminRoles()
    {
        SetPage("Права и роли", "Быстрое назначение ролей администратора, преподавателя и студента.");
        Activate(AdminRolesButton);

        RunUi("Роли", () =>
        {
            var rows = new ObservableCollection<RoleEditRow>(_database.LoadRoleRows());
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = CreateToolbar();
            var saveButton = new Button
            {
                Content = "Сохранить роли",
                Style = (Style)FindResource("PrimaryButton")
            };
            saveButton.Click += (_, _) =>
            {
                RunUi("Сохранение ролей", () =>
                {
                    foreach (var row in rows)
                    {
                        _database.SaveUserRole(row.UserId, row.Role);
                    }

                    MessageBox.Show(this, "Роли обновлены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowAdminRoles();
                });
            };
            toolbar.Children.Add(saveButton);
            root.Children.Add(toolbar);

            var grid = new DataGrid
            {
                ItemsSource = rows,
                IsReadOnly = false
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding(nameof(RoleEditRow.UserId)), IsReadOnly = true, Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Логин", Binding = new Binding(nameof(RoleEditRow.Username)), IsReadOnly = true, Width = 150 });
            grid.Columns.Add(new DataGridTextColumn { Header = "ФИО", Binding = new Binding(nameof(RoleEditRow.FullName)), IsReadOnly = true, Width = 260 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new Binding(nameof(RoleEditRow.Email)), IsReadOnly = true, Width = 220 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Текущая роль", Binding = new Binding(nameof(RoleEditRow.CurrentRole)), IsReadOnly = true, Width = 150 });
            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Новая роль",
                ItemsSource = DatabaseService.RoleDisplayValues,
                SelectedItemBinding = new Binding(nameof(RoleEditRow.Role))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(170)
            });
            var card = CreateTableCard(grid);
            Grid.SetRow(card, 1);
            root.Children.Add(card);
            BodyHost.Content = root;
        });
    }

    private void ShowAdminDirectories()
    {
        SetPage("Справочники", "Создание пользователей, групп, дисциплин и электронных журналов.");
        Activate(AdminDirectoriesButton);

        RunUi("Справочники", () =>
        {
            var tabs = new TabControl();
            tabs.Items.Add(CreateGroupTab());
            tabs.Items.Add(CreateSubjectTab());
            tabs.Items.Add(CreateCourseTab());
            tabs.Items.Add(CreateUserTab());
            BodyHost.Content = tabs;
        });
    }

    private TabItem CreateGroupTab()
    {
        var root = TwoColumnEditor();
        var table = CreateSearchableTableCard(
            _database.LoadGroups(),
            "groups.csv",
            ("Id", "ID", 70),
            ("Name", "Группа", 120),
            ("Speciality", "Специальность", 320),
            ("AdmissionYear", "Год", 90),
            ("Curator", "Куратор", 220));
        root.Children.Add(table);

        var form = CreateFormCard("Новая группа");
        var nameBox = new TextBox();
        var specialityBox = new TextBox { Text = "Информационные системы и программирование" };
        var yearBox = new TextBox { Text = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture) };
        var curatorBox = new ComboBox { ItemsSource = _database.LoadTeacherOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
        var saveButton = new Button { Content = "Сохранить группу", Style = (Style)FindResource("PrimaryButton") };
        saveButton.Click += (_, _) => RunUi("Группа", () =>
        {
            _database.CreateGroup(nameBox.Text, specialityBox.Text, int.Parse(yearBox.Text, CultureInfo.InvariantCulture), (curatorBox.SelectedItem as OptionItem)?.Id);
            ShowAdminDirectories();
        });
        AddFormField(form, "Название", nameBox);
        AddFormField(form, "Специальность", specialityBox);
        AddFormField(form, "Год поступления", yearBox);
        AddFormField(form, "Куратор", curatorBox);
        form.Children.Add(saveButton);
        Grid.SetColumn(form, 1);
        root.Children.Add(form);
        return new TabItem { Header = "Группы", Content = root };
    }

    private TabItem CreateSubjectTab()
    {
        var root = TwoColumnEditor();
        root.Children.Add(CreateSearchableTableCard(
            _database.LoadSubjects(),
            "subjects.csv",
            ("Id", "ID", 70),
            ("Code", "Код", 120),
            ("Name", "Дисциплина", 260),
            ("Description", "Описание", 380)));

        var form = CreateFormCard("Новая дисциплина");
        var codeBox = new TextBox();
        var nameBox = new TextBox();
        var descriptionBox = new TextBox { MinHeight = 90, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        var saveButton = new Button { Content = "Сохранить дисциплину", Style = (Style)FindResource("PrimaryButton") };
        saveButton.Click += (_, _) => RunUi("Дисциплина", () =>
        {
            _database.CreateSubject(codeBox.Text, nameBox.Text, descriptionBox.Text);
            ShowAdminDirectories();
        });
        AddFormField(form, "Код", codeBox);
        AddFormField(form, "Название", nameBox);
        AddFormField(form, "Описание", descriptionBox);
        form.Children.Add(saveButton);
        Grid.SetColumn(form, 1);
        root.Children.Add(form);
        return new TabItem { Header = "Дисциплины", Content = root };
    }

    private TabItem CreateCourseTab()
    {
        var root = TwoColumnEditor();
        root.Children.Add(CreateSearchableTableCard(
            _database.LoadCourses(),
            "courses.csv",
            ("Id", "ID", 70),
            ("Group", "Группа", 110),
            ("Subject", "Дисциплина", 230),
            ("Teacher", "Преподаватель", 210),
            ("AcademicYear", "Год", 120),
            ("Semester", "Семестр", 90)));

        var form = CreateFormCard("Новый электронный журнал");
        var subjectBox = new ComboBox { ItemsSource = _database.LoadSubjectOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
        var groupBox = new ComboBox { ItemsSource = _database.LoadGroupOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
        var teacherBox = new ComboBox { ItemsSource = _database.LoadTeacherOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
        var semesterBox = new TextBox { Text = "1" };
        var yearBox = new TextBox { Text = "2025/2026" };
        var saveButton = new Button { Content = "Создать журнал", Style = (Style)FindResource("PrimaryButton") };
        saveButton.Click += (_, _) => RunUi("Журнал", () =>
        {
            if (subjectBox.SelectedItem is not OptionItem subject || groupBox.SelectedItem is not OptionItem group || teacherBox.SelectedItem is not OptionItem teacher)
            {
                MessageBox.Show(this, "Выберите дисциплину, группу и преподавателя.", "Журнал", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _database.CreateCourse(subject.Id, group.Id, teacher.Id, int.Parse(semesterBox.Text, CultureInfo.InvariantCulture), yearBox.Text);
            ShowAdminDirectories();
        });
        AddFormField(form, "Дисциплина", subjectBox);
        AddFormField(form, "Группа", groupBox);
        AddFormField(form, "Преподаватель", teacherBox);
        AddFormField(form, "Семестр", semesterBox);
        AddFormField(form, "Учебный год", yearBox);
        form.Children.Add(saveButton);
        Grid.SetColumn(form, 1);
        root.Children.Add(form);
        return new TabItem { Header = "Журналы", Content = root };
    }

    private TabItem CreateUserTab()
    {
        var form = CreateFormCard("Новый пользователь");
        form.Width = 520;
        form.HorizontalAlignment = HorizontalAlignment.Left;
        var usernameBox = new TextBox();
        var passwordBox = new PasswordBox();
        var firstNameBox = new TextBox();
        var lastNameBox = new TextBox();
        var emailBox = new TextBox();
        var roleBox = new ComboBox { ItemsSource = DatabaseService.RoleDisplayValues, SelectedIndex = 2 };
        var groupBox = new ComboBox { ItemsSource = _database.LoadGroupOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
        var recordBookBox = new TextBox();
        var saveButton = new Button { Content = "Создать пользователя", Style = (Style)FindResource("PrimaryButton") };
        saveButton.Click += (_, _) => RunUi("Пользователь", () =>
        {
            _database.CreateUser(
                usernameBox.Text,
                passwordBox.Password,
                firstNameBox.Text,
                lastNameBox.Text,
                emailBox.Text,
                roleBox.SelectedItem?.ToString() ?? "Студент",
                (groupBox.SelectedItem as OptionItem)?.Id,
                recordBookBox.Text);
            MessageBox.Show(this, "Пользователь создан.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            ShowAdminDirectories();
        });
        AddFormField(form, "Логин", usernameBox);
        AddFormField(form, "Пароль", passwordBox);
        AddFormField(form, "Имя", firstNameBox);
        AddFormField(form, "Фамилия", lastNameBox);
        AddFormField(form, "Email", emailBox);
        AddFormField(form, "Роль", roleBox);
        AddFormField(form, "Группа студента", groupBox);
        AddFormField(form, "Зачетная книжка", recordBookBox);
        form.Children.Add(saveButton);
        return new TabItem { Header = "Пользователь", Content = new ScrollViewer { Content = form } };
    }

    private void ShowAdminCourses()
    {
        SetPage("Электронные журналы", "Дисциплины, группы, преподаватели и статистика заполнения.");
        Activate(AdminCoursesButton);

        RunUi("Журналы", () =>
        {
            BodyHost.Content = CreateSearchableTableCard(
                _database.LoadCourses(),
                "courses.csv",
                ("Id", "ID", 70),
                ("Group", "Группа", 110),
                ("Subject", "Дисциплина", 250),
                ("Teacher", "Преподаватель", 220),
                ("AcademicYear", "Учебный год", 120),
                ("Semester", "Семестр", 95),
                ("Students", "Студентов", 100),
                ("Lessons", "Занятий", 95),
                ("Grades", "Оценок", 95));
        });
    }

    private void ShowSchedule(string title, string subtitle, string role)
    {
        SetPage(title, subtitle);
        Activate(role switch
        {
            "teacher" => TeacherScheduleButton,
            "student" => StudentScheduleButton,
            _ => AdminScheduleButton
        });

        RunUi("Расписание", () =>
        {
            var userId = role is "teacher" or "student" ? _session?.Id : null;
            BodyHost.Content = CreateSearchableTableCard(
                _database.LoadSchedule(userId, role),
                "schedule.csv",
                ("Weekday", "День", 135),
                ("Time", "Время", 125),
                ("Group", "Группа", 100),
                ("Subject", "Дисциплина", 230),
                ("Teacher", "Преподаватель", 210),
                ("Room", "Аудитория", 110),
                ("Building", "Корпус", 170),
                ("WeekType", "Неделя", 140));
        });
    }

    private void ShowScheduleManager()
    {
        SetPage("Редактор расписания", "Создание, обновление и удаление пар в расписании.");
        Activate(AdminScheduleManageButton);

        RunUi("Редактор расписания", () =>
        {
            var root = TwoColumnEditor();
            var rows = _database.LoadScheduleEditorRows();
            var grid = CreateGrid(
                rows,
                ("Id", "ID", 70),
                ("Weekday", "День", 130),
                ("Time", "Время", 120),
                ("Course", "Журнал", 330),
                ("LessonType", "Тип", 130),
                ("Room", "Аудитория", 110),
                ("Building", "Корпус", 150),
                ("WeekType", "Неделя", 150),
                ("IsActive", "Активно", 90),
                ("Comment", "Комментарий", 220));
            var tableCard = CreateSearchableTableCard(grid, rows, "schedule-editor.csv");
            root.Children.Add(tableCard);

            var form = CreateFormCard("Пара расписания");
            var courseBox = new ComboBox { ItemsSource = _database.LoadCourseOptions(), DisplayMemberPath = nameof(OptionItem.Label) };
            var weekdayBox = new ComboBox { ItemsSource = DatabaseService.WeekdayOptions, DisplayMemberPath = nameof(ValueOption.Label), SelectedValuePath = nameof(ValueOption.Value), SelectedIndex = 0 };
            var startBox = new TextBox { Text = "09:00" };
            var endBox = new TextBox { Text = "10:30" };
            var typeBox = new ComboBox { ItemsSource = DatabaseService.LessonTypeOptions, DisplayMemberPath = nameof(ValueOption.Label), SelectedValuePath = nameof(ValueOption.Value), SelectedIndex = 0 };
            var roomBox = new TextBox { Text = "304" };
            var buildingBox = new TextBox { Text = "Главный корпус" };
            var weekBox = new ComboBox { ItemsSource = DatabaseService.WeekTypeOptions, DisplayMemberPath = nameof(ValueOption.Label), SelectedValuePath = nameof(ValueOption.Value), SelectedIndex = 0 };
            var activeBox = new CheckBox { IsChecked = true, Content = "Активно" };
            var commentBox = new TextBox();
            var saveButton = new Button { Content = "Сохранить пару", Style = (Style)FindResource("PrimaryButton") };
            saveButton.Click += (_, _) => RunUi("Расписание", () =>
            {
                if (courseBox.SelectedItem is not OptionItem course)
                {
                    MessageBox.Show(this, "Выберите журнал.", "Расписание", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _database.CreateScheduleEntry(
                    course.Id,
                    int.Parse(weekdayBox.SelectedValue?.ToString() ?? "1", CultureInfo.InvariantCulture),
                    TimeOnly.Parse(startBox.Text, CultureInfo.InvariantCulture),
                    TimeOnly.Parse(endBox.Text, CultureInfo.InvariantCulture),
                    typeBox.SelectedValue?.ToString() ?? "practice",
                    roomBox.Text,
                    buildingBox.Text,
                    weekBox.SelectedValue?.ToString() ?? "every",
                    activeBox.IsChecked == true,
                    commentBox.Text);
                ShowScheduleManager();
            });

            var deleteButton = new Button { Content = "Удалить выбранную", Style = (Style)FindResource("SecondaryButton"), Margin = new Thickness(0, 8, 0, 0) };
            deleteButton.Click += (_, _) => RunUi("Удаление пары", () =>
            {
                if (grid.SelectedItem is not ScheduleEditRow selected)
                {
                    MessageBox.Show(this, "Выберите строку расписания в таблице.", "Расписание", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (MessageBox.Show(this, $"Удалить пару #{selected.Id}?", "Расписание", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _database.DeleteScheduleEntry(selected.Id);
                    ShowScheduleManager();
                }
            });

            AddFormField(form, "Журнал", courseBox);
            AddFormField(form, "День недели", weekdayBox);
            AddFormField(form, "Начало HH:mm", startBox);
            AddFormField(form, "Окончание HH:mm", endBox);
            AddFormField(form, "Тип занятия", typeBox);
            AddFormField(form, "Аудитория", roomBox);
            AddFormField(form, "Корпус", buildingBox);
            AddFormField(form, "Неделя", weekBox);
            form.Children.Add(activeBox);
            AddFormField(form, "Комментарий", commentBox);
            form.Children.Add(saveButton);
            form.Children.Add(deleteButton);
            Grid.SetColumn(form, 1);
            root.Children.Add(form);
            BodyHost.Content = root;
        });
    }

    private void ShowTeacherDashboard()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Панель преподавателя", "Быстрый обзор дня, учебной нагрузки и заполненности журналов.");
        Activate(TeacherDashboardButton);

        RunUi("Панель преподавателя", () =>
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var hero = new Border
            {
                CornerRadius = new CornerRadius(CardRadius),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 18),
                Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow")
            };
            var heroGrid = new Grid { MinHeight = 205 };
            heroGrid.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Assets/hero-campus.png", UriKind.Absolute)),
                Stretch = Stretch.UniformToFill,
                Opacity = 0.86
            });
            heroGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromArgb(210, 7, 87, 91),
                    Color.FromArgb(95, 231, 111, 81),
                    0)
            });
            var heroText = new StackPanel
            {
                Margin = new Thickness(28),
                VerticalAlignment = VerticalAlignment.Center
            };
            heroText.Children.Add(new TextBlock
            {
                Text = $"Добрый день, {_session.FullName}",
                Foreground = Brushes.White,
                FontSize = 28,
                FontWeight = FontWeights.SemiBold
            });
            heroText.Children.Add(new TextBlock
            {
                Text = "Журналы, пары на сегодня, студенты группы и аналитика доступны из одного рабочего пространства.",
                Foreground = (Brush)new BrushConverter().ConvertFromString("#E7F3F2")!,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 720,
                Margin = new Thickness(0, 8, 0, 18)
            });
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal };
            var journalButton = new Button { Content = "Открыть журнал", Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 10, 0) };
            journalButton.Click += (_, _) => ShowTeacherJournal();
            var risksButton = new Button { Content = "Проверить риски", Style = (Style)FindResource("SecondaryButton") };
            risksButton.Click += (_, _) => ShowAnalytics("Учебные риски", "Студенты ваших групп, которым может понадобиться внимание.", "teacher");
            actionRow.Children.Add(journalButton);
            actionRow.Children.Add(risksButton);
            heroText.Children.Add(actionRow);
            heroGrid.Children.Add(heroText);
            hero.Child = heroGrid;
            root.Children.Add(hero);

            var lower = new Grid();
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(lower, 1);

            var metrics = CreateMetricWrap(_database.LoadTeacherMetrics(_session.Id));
            lower.Children.Add(new ScrollViewer { Content = metrics, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            var today = CreateSearchableTableCard(
                _database.LoadTodaySchedule(_session.Id, "teacher"),
                "teacher-today.csv",
                ("Time", "Время", 110),
                ("Group", "Группа", 95),
                ("Subject", "Дисциплина", 220),
                ("Room", "Аудитория", 105),
                ("Building", "Корпус", 150));
            today.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(today, 1);
            lower.Children.Add(today);
            root.Children.Add(lower);
            BodyHost.Content = root;
        });
    }

    private void ShowTeacherCourses()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Мои курсы", "Список электронных журналов, состав групп и занятия.");
        Activate(TeacherCoursesButton);

        RunUi("Мои курсы", () =>
        {
            var courses = _database.LoadTeacherCourses(_session.Id);
            if (courses.Count == 0)
            {
                BodyHost.Content = CreateEmptyState("Для преподавателя пока не назначены курсы.");
                return;
            }

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var courseBox = new ComboBox
            {
                ItemsSource = courses,
                DisplayMemberPath = nameof(CourseOption.Label),
                SelectedIndex = 0,
                MinHeight = 42,
                Margin = new Thickness(0, 0, 0, 16)
            };
            root.Children.Add(courseBox);

            var tabs = new TabControl();
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            void LoadCourseDetails()
            {
                if (courseBox.SelectedItem is not CourseOption course)
                {
                    return;
                }

                tabs.Items.Clear();
                tabs.Items.Add(new TabItem
                {
                    Header = "Студенты",
                    Content = CreateSearchableTableCard(
                        _database.LoadTeacherStudents(course.Id),
                        "teacher-students.csv",
                        ("StudentName", "Студент", 260),
                        ("RecordBookNumber", "Зачетка", 140),
                        ("Attendance", "Посещаемость", 130),
                        ("AverageGrade", "Средний балл", 130),
                        ("Grades", "Оценок", 95))
                });
                tabs.Items.Add(new TabItem
                {
                    Header = "Занятия",
                    Content = CreateSearchableTableCard(
                        _database.LoadLessonRows(course.Id),
                        "teacher-lessons.csv",
                        ("Id", "ID", 70),
                        ("Date", "Дата", 115),
                        ("Type", "Тип", 130),
                        ("Topic", "Тема", 360),
                        ("AttendanceMarked", "Посещаемость", 130),
                        ("GradesMarked", "Оценки", 100))
                });
            }

            courseBox.SelectionChanged += (_, _) => RunUi("Курс", LoadCourseDetails);
            LoadCourseDetails();
            BodyHost.Content = root;
        });
    }

    private void ShowTeacherFinalGrades()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Итоговые оценки", "Выставление итоговых оценок по электронному журналу.");
        Activate(TeacherFinalGradesButton);

        RunUi("Итоговые оценки", () =>
        {
            var courses = _database.LoadTeacherCourses(_session.Id);
            if (courses.Count == 0)
            {
                BodyHost.Content = CreateEmptyState("Для преподавателя пока не назначены курсы.");
                return;
            }

            var rows = new ObservableCollection<FinalGradeRow>();
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = CreateToolbar();
            var courseBox = new ComboBox
            {
                ItemsSource = courses,
                DisplayMemberPath = nameof(CourseOption.Label),
                SelectedIndex = 0,
                Width = 520,
                MinHeight = 42,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var saveButton = new Button { Content = "Сохранить итоговые", Style = (Style)FindResource("PrimaryButton") };
            var autoButton = new Button
            {
                Content = "Заполнить автоматически",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 12, 0)
            };
            toolbar.Children.Add(courseBox);
            toolbar.Children.Add(autoButton);
            toolbar.Children.Add(saveButton);
            root.Children.Add(toolbar);

            var grid = new DataGrid { ItemsSource = rows, IsReadOnly = false };
            grid.Columns.Add(new DataGridTextColumn { Header = "Студент", Binding = new Binding(nameof(FinalGradeRow.StudentName)), IsReadOnly = true, Width = 250 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Зачетка", Binding = new Binding(nameof(FinalGradeRow.RecordBookNumber)), IsReadOnly = true, Width = 130 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Средний балл", Binding = new Binding(nameof(FinalGradeRow.AverageGrade)), IsReadOnly = true, Width = 130 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Посещаемость", Binding = new Binding(nameof(FinalGradeRow.Attendance)), IsReadOnly = true, Width = 130 });
            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Итоговая",
                ItemsSource = DatabaseService.GradeDisplayValues,
                SelectedItemBinding = new Binding(nameof(FinalGradeRow.FinalGrade))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = 130
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Комментарий",
                Binding = new Binding(nameof(FinalGradeRow.Comment))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            var card = CreateTableCard(grid);
            Grid.SetRow(card, 1);
            root.Children.Add(card);

            void LoadRows()
            {
                rows.Clear();
                if (courseBox.SelectedItem is not CourseOption course)
                {
                    return;
                }

                foreach (var row in _database.LoadFinalGrades(course.Id))
                {
                    rows.Add(row);
                }
            }

            courseBox.SelectionChanged += (_, _) => RunUi("Итоговые оценки", LoadRows);
            autoButton.Click += (_, _) =>
            {
                foreach (var row in rows)
                {
                    row.FinalGrade = EstimateFinalGrade(row);
                    if (string.IsNullOrWhiteSpace(row.Comment))
                    {
                        row.Comment = "Автоматическая рекомендация по среднему баллу и посещаемости";
                    }
                }
            };
            saveButton.Click += (_, _) => RunUi("Итоговые оценки", () =>
            {
                if (courseBox.SelectedItem is not CourseOption course)
                {
                    return;
                }

                _database.SaveFinalGrades(course.Id, rows);
                MessageBox.Show(this, "Итоговые оценки сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            LoadRows();
            BodyHost.Content = root;
        });
    }

    private void ShowTeacherJournal()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Журнал преподавателя", "Создавайте занятия, отмечайте посещаемость и выставляйте текущие оценки.");
        Activate(TeacherJournalButton);

        RunUi("Журнал преподавателя", () =>
        {
            var courses = _database.LoadTeacherCourses(_session.Id);
            if (courses.Count == 0)
            {
                BodyHost.Content = CreateEmptyState("Вам пока не назначены электронные журналы.");
                return;
            }

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var controls = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(CardRadius),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 16),
                Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow")
            };

            var controlsGrid = new Grid();
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var courseBox = new ComboBox
            {
                ItemsSource = courses,
                DisplayMemberPath = nameof(CourseOption.Label),
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 14, 0),
                MinHeight = 42
            };
            Grid.SetColumn(courseBox, 0);
            controlsGrid.Children.Add(courseBox);

            var lessonBox = new ComboBox
            {
                DisplayMemberPath = nameof(LessonOption.Label),
                Margin = new Thickness(0, 0, 14, 0),
                MinHeight = 42
            };
            Grid.SetColumn(lessonBox, 1);
            controlsGrid.Children.Add(lessonBox);

            var newLessonButton = new Button
            {
                Content = "Новое занятие",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(newLessonButton, 2);
            controlsGrid.Children.Add(newLessonButton);

            var refreshButton = new Button
            {
                Content = "Обновить",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(refreshButton, 3);
            controlsGrid.Children.Add(refreshButton);

            var allPresentButton = new Button
            {
                Content = "Все +",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            allPresentButton.ToolTip = "Отметить всех присутствующими";
            Grid.SetColumn(allPresentButton, 4);
            controlsGrid.Children.Add(allPresentButton);

            var allAbsentButton = new Button
            {
                Content = "Все -",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            allAbsentButton.ToolTip = "Отметить всех отсутствующими";
            Grid.SetColumn(allAbsentButton, 5);
            controlsGrid.Children.Add(allAbsentButton);

            var clearGradesButton = new Button
            {
                Content = "Очистить",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            clearGradesButton.ToolTip = "Очистить оценки текущего занятия";
            Grid.SetColumn(clearGradesButton, 6);
            controlsGrid.Children.Add(clearGradesButton);

            var saveButton = new Button
            {
                Content = "Сохранить журнал",
                Style = (Style)FindResource("PrimaryButton")
            };
            Grid.SetColumn(saveButton, 7);
            controlsGrid.Children.Add(saveButton);

            controls.Child = controlsGrid;
            root.Children.Add(controls);

            var tableHost = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(CardRadius),
                Padding = new Thickness(18),
                Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow")
            };
            Grid.SetRow(tableHost, 1);
            root.Children.Add(tableHost);

            void LoadLessons()
            {
                if (courseBox.SelectedItem is not CourseOption course)
                {
                    return;
                }

                _teacherJournal.Course = course;
                var lessons = _database.LoadLessons(course.Id);
                lessonBox.ItemsSource = lessons;
                lessonBox.SelectedIndex = lessons.Count > 0 ? 0 : -1;
                if (lessons.Count == 0)
                {
                    _teacherJournal.Rows.Clear();
                    tableHost.Child = CreateEmptyState("В этом журнале пока нет занятий. Создайте первое занятие.");
                }
            }

            void LoadRows()
            {
                if (courseBox.SelectedItem is not CourseOption course || lessonBox.SelectedItem is not LessonOption lesson)
                {
                    return;
                }

                _teacherJournal.Course = course;
                _teacherJournal.Lesson = lesson;
                _teacherJournal.Rows.Clear();
                foreach (var row in _database.LoadJournalRows(course.Id, lesson.Id))
                {
                    _teacherJournal.Rows.Add(row);
                }

                tableHost.Child = CreateJournalGrid(_teacherJournal.Rows);
            }

            courseBox.SelectionChanged += (_, _) =>
            {
                RunUi("Загрузка занятий", () =>
                {
                    LoadLessons();
                    LoadRows();
                });
            };
            lessonBox.SelectionChanged += (_, _) => RunUi("Загрузка журнала", LoadRows);
            refreshButton.Click += (_, _) => RunUi("Обновление журнала", LoadRows);
            allPresentButton.Click += (_, _) => MarkJournalRows("Присутствовал");
            allAbsentButton.Click += (_, _) => MarkJournalRows("Отсутствовал");
            clearGradesButton.Click += (_, _) =>
            {
                foreach (var row in _teacherJournal.Rows)
                {
                    row.GradeValue = "";
                }
            };
            saveButton.Click += (_, _) =>
            {
                RunUi("Сохранение журнала", () =>
                {
                    if (_teacherJournal.Course is null || _teacherJournal.Lesson is null)
                    {
                        MessageBox.Show(this, "Выберите журнал и занятие.", "Журнал", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    _database.SaveJournal(
                        _teacherJournal.Course.Id,
                        _teacherJournal.Lesson.Id,
                        _teacherJournal.Lesson.Date,
                        _teacherJournal.Rows);
                    MessageBox.Show(this, "Журнал сохранен.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            };
            newLessonButton.Click += (_, _) => ShowCreateLessonDialog(courseBox, lessonBox);

            LoadLessons();
            LoadRows();
            BodyHost.Content = root;
        });
    }

    private void ShowCreateLessonDialog(ComboBox courseBox, ComboBox lessonBox)
    {
        if (courseBox.SelectedItem is not CourseOption course)
        {
            return;
        }

        var dialog = new Window
        {
            Owner = this,
            Title = "Новое занятие",
            Width = 520,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)new BrushConverter().ConvertFromString("#F3F7F8")!
        };

        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(26),
            Margin = new Thickness(18),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow")
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Создать занятие",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = course.Label,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 6, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });

        var datePicker = new DatePicker
        {
            SelectedDate = DateTime.Today,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var typeBox = new ComboBox
        {
            ItemsSource = new[]
            {
                new LessonTypeChoice("practice", "Практика"),
                new LessonTypeChoice("lecture", "Лекция"),
                new LessonTypeChoice("lab", "Лабораторная"),
                new LessonTypeChoice("seminar", "Семинар"),
                new LessonTypeChoice("exam", "Контроль")
            },
            DisplayMemberPath = nameof(LessonTypeChoice.Label),
            SelectedValuePath = nameof(LessonTypeChoice.Value),
            SelectedIndex = 0,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var topicBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 18),
            MinHeight = 42
        };

        panel.Children.Add(Labelled("Дата занятия", datePicker));
        panel.Children.Add(Labelled("Тип занятия", typeBox));
        panel.Children.Add(Labelled("Тема занятия", topicBox));

        var createButton = new Button
        {
            Content = "Создать и открыть журнал",
            Style = (Style)FindResource("PrimaryButton")
        };
        createButton.Click += (_, _) =>
        {
            RunUi("Создание занятия", () =>
            {
                var topic = topicBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(topic))
                {
                    MessageBox.Show(dialog, "Введите тему занятия.", "Новое занятие", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var lesson = _database.CreateLesson(
                    course.Id,
                    datePicker.SelectedDate ?? DateTime.Today,
                    typeBox.SelectedValue?.ToString() ?? "practice",
                    topic);
                var lessons = _database.LoadLessons(course.Id);
                lessonBox.ItemsSource = lessons;
                lessonBox.SelectedItem = lessons.FirstOrDefault(item => item.Id == lesson.Id);
                dialog.Close();
            });
        };
        panel.Children.Add(createButton);
        card.Child = panel;
        dialog.Content = card;
        dialog.ShowDialog();
    }

    private void MarkJournalRows(string attendanceStatus)
    {
        foreach (var row in _teacherJournal.Rows)
        {
            row.AttendanceStatus = attendanceStatus;
        }
    }

    private static string EstimateFinalGrade(FinalGradeRow row)
    {
        var average = decimal.TryParse(row.AverageGrade, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAverage)
            ? parsedAverage
            : 0m;
        var attendance = row.Attendance.EndsWith('%') &&
                         int.TryParse(row.Attendance.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAttendance)
            ? parsedAttendance
            : 100;

        if (average >= 4.5m && attendance >= 85)
        {
            return "5";
        }

        if (average >= 3.7m && attendance >= 75)
        {
            return "4";
        }

        if (average >= 2.8m && attendance >= 60)
        {
            return "3";
        }

        return "2";
    }

    private void ShowStudentDashboard()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Личный кабинет", "Сводка по посещаемости, оценкам и учебным журналам.");
        Activate(StudentDashboardButton);

        RunUi("Личный кабинет", () =>
        {
            var summary = _database.LoadStudentSummary(_session.Id);
            var page = new StackPanel();
            page.Children.Add(CreateStudentHero(summary));
            page.Children.Add(CreateMetricWrap(new[]
            {
                new MetricCard("Посещаемость", $"{summary.AttendancePercent}%", $"{summary.AttendanceTotal} отмеченных занятий"),
                new MetricCard("Оценки", summary.GradesTotal.ToString(CultureInfo.InvariantCulture), "записей успеваемости"),
                new MetricCard("Средний балл", summary.AverageGrade, "по числовым оценкам")
            }));

            var lower = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.28, GridUnitType.Star) });
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.72, GridUnitType.Star) });

            var table = CreateSearchableTableCard(
                _database.LoadStudentCourses(_session.Id),
                "student-courses.csv",
                ("Subject", "Дисциплина", 230),
                ("Teacher", "Преподаватель", 190),
                ("AcademicYear", "Учебный год", 110),
                ("Semester", "Семестр", 85),
                ("Lessons", "Занятий", 85),
                ("Attendance", "Посещаемость", 120),
                ("AverageGrade", "Средний балл", 120));
            lower.Children.Add(table);

            var side = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            side.Children.Add(CreateStudentPulseCard(summary));
            var today = CreateSearchableTableCard(
                _database.LoadTodaySchedule(_session.Id, "student"),
                "student-today.csv",
                ("Time", "Время", 95),
                ("Subject", "Дисциплина", 170),
                ("Room", "Ауд.", 70));
            today.Margin = new Thickness(0, 16, 0, 0);
            side.Children.Add(today);
            Grid.SetColumn(side, 1);
            lower.Children.Add(side);
            page.Children.Add(lower);

            BodyHost.Content = new ScrollViewer
            {
                Content = page,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        });
    }

    private Border CreateStudentHero(StudentSummary summary)
    {
        var hero = new Border
        {
            CornerRadius = new CornerRadius(CardRadius),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 16),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow")
        };

        var root = new Grid { MinHeight = 230 };
        root.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/hero-campus.png", UriKind.Absolute)),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.9
        });
        root.Children.Add(new Border
        {
            Background = new LinearGradientBrush(
                Color.FromArgb(238, 16, 24, 32),
                Color.FromArgb(190, 15, 111, 115),
                new Point(0, 0.2),
                new Point(1, 1))
        });

        var content = new Grid { Margin = new Thickness(30) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = "Личный учебный трек",
            Foreground = GoldBrush,
            FontSize = 13,
            FontWeight = FontWeights.Bold
        });
        text.Children.Add(new TextBlock
        {
            Text = summary.FullName,
            Foreground = Brushes.White,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 6)
        });
        text.Children.Add(new TextBlock
        {
            Text = $"Группа {summary.Group} | зачетная книжка {summary.RecordBookNumber}",
            Foreground = BrushFrom("#E5EFEE"),
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
        actions.Children.Add(CreateHeroButton("Оценки", ShowStudentJournal));
        actions.Children.Add(CreateHeroButton("Зачетка", ShowStudentRecordBook));
        actions.Children.Add(CreateHeroButton("Рекомендации", () => ShowAnalytics("Рекомендации", "Персональные подсказки по посещаемости и успеваемости.", "student")));
        text.Children.Add(actions);
        content.Children.Add(text);

        var statusPanel = new Border
        {
            Background = BrushFrom("#EFFFFFFF"),
            BorderBrush = BrushFrom("#33FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };
        var status = new StackPanel();
        status.Children.Add(new TextBlock
        {
            Text = "Прогресс посещаемости",
            Foreground = InkBrush,
            FontWeight = FontWeights.SemiBold
        });
        status.Children.Add(new TextBlock
        {
            Text = $"{summary.AttendancePercent}%",
            Foreground = AttendanceTone(summary.AttendancePercent),
            FontSize = 40,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 4)
        });
        status.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = summary.AttendancePercent,
            Height = 10,
            Foreground = AttendanceTone(summary.AttendancePercent),
            Background = BrushFrom("#DDE8E4"),
            Margin = new Thickness(0, 0, 0, 14)
        });
        status.Children.Add(new TextBlock
        {
            Text = StudentStatusText(summary),
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        });
        statusPanel.Child = status;
        Grid.SetColumn(statusPanel, 1);
        content.Children.Add(statusPanel);

        root.Children.Add(content);
        hero.Child = root;
        return hero;
    }

    private Button CreateHeroButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(18, 10, 18, 10)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Border CreateStudentPulseCard(StudentSummary summary)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Фокус на неделю",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = StudentStatusText(summary),
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 8, 0, 16)
        });

        panel.Children.Add(CreatePulseRow("Посещаемость", $"{summary.AttendancePercent}%", AttendanceTone(summary.AttendancePercent)));
        panel.Children.Add(CreatePulseRow("Средний балл", summary.AverageGrade, AverageTone(summary.AverageGrade)));
        panel.Children.Add(CreatePulseRow("Записей оценок", summary.GradesTotal.ToString(CultureInfo.InvariantCulture), AccentBrush));

        var button = new Button
        {
            Content = "Открыть рекомендации",
            Style = (Style)FindResource("PrimaryButton"),
            Margin = new Thickness(0, 18, 0, 0)
        };
        button.Click += (_, _) => ShowAnalytics("Рекомендации", "Персональные подсказки по посещаемости и успеваемости.", "student");
        panel.Children.Add(button);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(20),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
            Child = panel
        };
    }

    private static Grid CreatePulseRow(string label, string value, Brush accent)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var badge = new Border
        {
            Background = accent,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(10, 5, 10, 5),
            Child = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            }
        };
        Grid.SetColumn(badge, 1);
        row.Children.Add(badge);
        return row;
    }

    private static Brush AttendanceTone(int percent)
    {
        return percent switch
        {
            < 70 => CoralBrush,
            < 85 => GoldBrush,
            _ => AccentBrush
        };
    }

    private static Brush AverageTone(string value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var average) && average < 3.5m
            ? CoralBrush
            : AccentBrush;
    }

    private static string StudentStatusText(StudentSummary summary)
    {
        if (summary.AttendanceTotal == 0)
        {
            return "Пока мало данных: после заполнения журналов здесь появится персональная динамика.";
        }

        if (summary.AttendancePercent < 70)
        {
            return "Есть риск по посещаемости. Проверьте пропуски и откройте рекомендации по дисциплинам.";
        }

        if (summary.AttendancePercent < 85)
        {
            return "Динамика средняя: полезно удержать посещаемость и закрыть слабые темы до контроля.";
        }

        return "Хорошая динамика. Следите за ближайшими занятиями и итоговыми оценками.";
    }

    private void ShowStudentJournal()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Оценки и посещаемость", "Подробный список занятий, отметок и оценок студента.");
        Activate(StudentJournalButton);

        RunUi("Оценки", () =>
        {
            BodyHost.Content = CreateSearchableTableCard(
                _database.LoadStudentJournal(_session.Id),
                "student-journal.csv",
                ("Date", "Дата", 110),
                ("Subject", "Дисциплина", 240),
                ("Lesson", "Занятие", 320),
                ("Attendance", "Посещение", 170),
                ("Grade", "Оценка", 100),
                ("GradeType", "Тип", 130));
        });
    }

    private void ShowStudentRecordBook()
    {
        if (_session is null)
        {
            return;
        }

        SetPage("Зачетная книжка", "Итоговые результаты по дисциплинам, средний балл и посещаемость.");
        Activate(StudentRecordBookButton);

        RunUi("Зачетная книжка", () =>
        {
            BodyHost.Content = CreateSearchableTableCard(
                _database.LoadRecordBook(_session.Id),
                "record-book.csv",
                ("Subject", "Дисциплина", 260),
                ("Teacher", "Преподаватель", 230),
                ("AcademicYear", "Учебный год", 120),
                ("Semester", "Семестр", 90),
                ("AverageGrade", "Средний балл", 125),
                ("FinalGrade", "Итоговая", 110),
                ("Attendance", "Посещаемость", 130),
                ("GradesCount", "Оценок", 95));
        });
    }

    private void ShowAnalytics(string title, string subtitle, string role)
    {
        if (_session is null)
        {
            return;
        }

        SetPage(title, subtitle);
        Activate(role switch
        {
            "teacher" => TeacherAnalyticsButton,
            "student" => StudentRecommendationsButton,
            _ => AdminAnalyticsButton
        });

        RunUi("Аналитика", () =>
        {
            var root = new TabControl();
            int? teacherId = role == "teacher" ? _session.Id : null;
            int? userId = role == "student" ? _session.Id : null;

            root.Items.Add(new TabItem
            {
                Header = role == "student" ? "Мои рекомендации" : "Риски студентов",
                Content = CreateSearchableTableCard(
                    _database.LoadRiskAnalytics(teacherId, userId),
                    "risk-analytics.csv",
                    ("Name", "Студент", 240),
                    ("Group", "Группа", 100),
                    ("Attendance", "Посещаемость", 130),
                    ("AverageGrade", "Средний балл", 130),
                    ("RiskLevel", "Риск", 115),
                    ("Recommendation", "Рекомендация", 420))
            });

            if (role != "student")
            {
                root.Items.Add(new TabItem
                {
                    Header = "Посещаемость",
                    Content = CreateSearchableTableCard(
                        _database.LoadAttendanceAnalytics(),
                        "attendance-analytics.csv",
                        ("Group", "Группа", 110),
                        ("Subject", "Дисциплина", 260),
                        ("Total", "Всего", 85),
                        ("Present", "Присутствий", 115),
                        ("Absent", "Пропусков", 105),
                        ("Late", "Опозданий", 105),
                        ("Excused", "Уважит.", 95),
                        ("Percent", "Процент", 100))
                });
                root.Items.Add(new TabItem
                {
                    Header = "Оценки",
                    Content = CreateBarChart(_database.LoadGradeDistribution())
                });
            }

            BodyHost.Content = root;
        });
    }

    private void SetPage(string title, string subtitle)
    {
        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;
    }

    private StackPanel CreateToolbar()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 14)
        };
    }

    private Grid TwoColumnEditor()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        return grid;
    }

    private StackPanel CreateFormCard(string title)
    {
        var inner = new StackPanel
        {
            Margin = new Thickness(18, 0, 0, 0),
            Background = Brushes.White
        };
        inner.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        });
        return inner;
    }

    private static void AddFormField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 6)
        });
        control.Margin = new Thickness(0, 0, 0, 8);
        control.MinHeight = Math.Max(control.MinHeight, 38);
        panel.Children.Add(control);
    }

    private Border CreateSearchableTableCard<T>(
        IEnumerable<T> items,
        string exportFileName,
        params (string Property, string Header, double Width)[] columns)
    {
        var list = items.ToList();
        var grid = CreateGrid(list, columns);
        return CreateSearchableTableCard(grid, list, exportFileName);
    }

    private Border CreateSearchableTableCard<T>(DataGrid grid, IEnumerable<T> items, string exportFileName)
    {
        var list = items.ToList();
        var root = new DockPanel();
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new TextBlock
        {
            Text = "Данные",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        var countText = new TextBlock
        {
            Foreground = (Brush)new BrushConverter().ConvertFromString("#62787D")!,
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        };
        titlePanel.Children.Add(countText);
        toolbar.Children.Add(titlePanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var searchHost = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString("#F7FBFB")!,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#DDE9EA")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(12, 7, 12, 7),
            MinHeight = 44
        };
        var searchPanel = new DockPanel();
        searchPanel.Children.Add(new TextBlock
        {
            Text = "Поиск",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#62787D")!,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        var searchBox = new TextBox
        {
            Width = 300,
            MinHeight = 30,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        searchPanel.Children.Add(searchBox);
        searchHost.Child = searchPanel;
        actions.Children.Add(searchHost);

        var exportButton = new Button
        {
            Content = "Экспорт CSV",
            Style = (Style)FindResource("SecondaryButton"),
            Margin = new Thickness(12, 0, 0, 0),
            MinHeight = 44,
            Padding = new Thickness(18, 11, 18, 11)
        };
        exportButton.Click += (_, _) => ExportCsv(list, exportFileName);
        actions.Children.Add(exportButton);

        Grid.SetColumn(actions, 1);
        toolbar.Children.Add(actions);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        grid.Margin = new Thickness(0);
        grid.EnableRowVirtualization = true;
        grid.EnableColumnVirtualization = true;
        root.Children.Add(grid);

        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
        UpdateTableCounter(countText, view.Cast<object>().Count(), list.Count);
        searchBox.TextChanged += (_, _) =>
        {
            var query = searchBox.Text.Trim();
            view.Filter = item => string.IsNullOrWhiteSpace(query) || MatchesSearch(item, query);
            view.Refresh();
            UpdateTableCounter(countText, view.Cast<object>().Count(), list.Count);
        };

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#E3EDEE")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(20),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
            Child = root
        };
    }

    private static bool MatchesSearch(object? item, string query)
    {
        if (item is null)
        {
            return false;
        }

        var properties = item.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            var value = property.GetValue(item)?.ToString();
            if (!string.IsNullOrWhiteSpace(value) &&
                value.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return item.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void UpdateTableCounter(TextBlock target, int visibleCount, int totalCount)
    {
        target.Text = visibleCount == totalCount
            ? $"Всего записей: {totalCount}"
            : $"Найдено: {visibleCount} из {totalCount}";
    }

    private void ExportCsv<T>(IEnumerable<T> items, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт CSV",
            FileName = defaultFileName,
            Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToArray();
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(";", properties.Select(property => EscapeCsv(property.Name))));
        foreach (var item in items)
        {
            csv.AppendLine(string.Join(";", properties.Select(property => EscapeCsv(property.GetValue(item)?.ToString() ?? ""))));
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
        MessageBox.Show(this, "CSV-файл сохранен.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(';') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }

    private Border CreateBarChart(IReadOnlyList<GradeDistributionRow> rows)
    {
        var max = rows.Count == 0 ? 1 : Math.Max(1, rows.Max(row => row.Count));
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Распределение оценок",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        });

        foreach (var row in rows)
        {
            var line = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            line.Children.Add(new TextBlock
            {
                Text = row.Grade,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var track = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString("#EAF3F2")!,
                CornerRadius = new CornerRadius(CardRadius),
                Height = 24
            };
            var bar = new Border
            {
                Background = (Brush)FindResource("AccentBrush"),
                CornerRadius = new CornerRadius(CardRadius),
                Width = Math.Max(20, 520.0 * row.Count / max),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var barGrid = new Grid();
            barGrid.Children.Add(track);
            barGrid.Children.Add(bar);
            Grid.SetColumn(barGrid, 1);
            line.Children.Add(barGrid);

            var count = new TextBlock
            {
                Text = row.Count.ToString(CultureInfo.InvariantCulture),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(count, 2);
            line.Children.Add(count);
            panel.Children.Add(line);
        }

        return new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(24),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
            Child = panel
        };
    }

    private void Activate(Button activeButton)
    {
        foreach (var button in new[]
                 {
                     AdminOverviewButton, AdminUsersButton, AdminRolesButton, AdminDirectoriesButton,
                     AdminCoursesButton, AdminScheduleButton, AdminScheduleManageButton, AdminAnalyticsButton,
                     TeacherDashboardButton, TeacherCoursesButton, TeacherJournalButton, TeacherFinalGradesButton, TeacherScheduleButton, TeacherAnalyticsButton,
                     StudentDashboardButton, StudentJournalButton, StudentRecordBookButton, StudentScheduleButton, StudentRecommendationsButton
                 })
        {
            button.Tag = null;
        }

        activeButton.Tag = "Active";
    }

    private static Border CreateMetricWrap(IEnumerable<MetricCard> metrics)
    {
        var wrap = new WrapPanel();
        var index = 0;
        var accents = new[] { AccentBrush, GoldBrush, CoralBrush, AccentDarkBrush };
        foreach (var metric in metrics)
        {
            var card = new Border
            {
                Width = 235,
                MinHeight = 126,
                Background = Brushes.White,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardRadius),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 14, 14)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new Border
            {
                Background = accents[index % accents.Length],
                CornerRadius = new CornerRadius(CardRadius, 0, 0, CardRadius)
            });

            var panel = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
            panel.Children.Add(new TextBlock
            {
                Text = metric.Title,
                Foreground = MutedBrush,
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = metric.Value,
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 4),
                Foreground = InkBrush
            });
            panel.Children.Add(new TextBlock
            {
                Text = metric.Caption,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(panel, 1);
            grid.Children.Add(panel);
            card.Child = grid;
            wrap.Children.Add(card);
            index++;
        }

        return new Border
        {
            Child = wrap,
            Background = Brushes.Transparent
        };
    }

    private Border CreateTableCard(DataGrid grid)
    {
        grid.EnableRowVirtualization = true;
        grid.EnableColumnVirtualization = true;

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#E3EDEE")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(20),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
            Child = grid
        };
    }

    private static DataGrid CreateGrid<T>(IEnumerable<T> items, params (string Property, string Header, double Width)[] columns)
    {
        var grid = new DataGrid
        {
            ItemsSource = items,
            IsReadOnly = true,
            MinHeight = 180
        };

        foreach (var column in columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding(column.Property),
                Width = new DataGridLength(column.Width)
            });
        }

        return grid;
    }

    private DataGrid CreateJournalGrid(ObservableCollection<JournalRow> rows)
    {
        var grid = new DataGrid
        {
            ItemsSource = rows,
            IsReadOnly = false
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Студент",
            Binding = new Binding(nameof(JournalRow.StudentName)),
            IsReadOnly = true,
            Width = new DataGridLength(260)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Зачетная книжка",
            Binding = new Binding(nameof(JournalRow.RecordBookNumber)),
            IsReadOnly = true,
            Width = new DataGridLength(140)
        });
        grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Посещение",
            ItemsSource = DatabaseService.AttendanceDisplayValues,
            SelectedItemBinding = new Binding(nameof(JournalRow.AttendanceStatus))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = new DataGridLength(190)
        });
        grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Оценка",
            ItemsSource = DatabaseService.GradeDisplayValues,
            SelectedItemBinding = new Binding(nameof(JournalRow.GradeValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = new DataGridLength(120)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Комментарий",
            Binding = new Binding(nameof(JournalRow.Comment))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        return grid;
    }

    private Border CreateEmptyState(string text)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Данные можно добавить через веб-версию, Django admin или этот ПК-клиент.",
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 8, 0, 0),
            TextAlignment = TextAlignment.Center
        });
        return new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(34),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
            Child = panel
        };
    }

    private static FrameworkElement Labelled(string label, Control control)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        panel.Children.Add(control);
        return panel;
    }

    private static Brush BrushFrom(string value)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(value)!;
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private void RunUi(string title, Action action)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private sealed record LessonTypeChoice(string Value, string Label);
}
