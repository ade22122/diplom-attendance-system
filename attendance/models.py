from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group, Permission
from django.contrib.contenttypes.models import ContentType
from django.db import models
from django.db.models.signals import post_save
from django.dispatch import receiver
from django.utils import timezone


User = get_user_model()


class Profile(models.Model):
    class Role(models.TextChoices):
        ADMIN = "admin", "Администратор"
        TEACHER = "teacher", "Преподаватель"
        STUDENT = "student", "Студент"

    user = models.OneToOneField(User, on_delete=models.CASCADE, related_name="profile")
    role = models.CharField(max_length=16, choices=Role.choices, default=Role.STUDENT)
    patronymic = models.CharField("Отчество", max_length=150, blank=True)
    phone = models.CharField("Телефон", max_length=30, blank=True)
    avatar = models.ImageField("Файл аватара", upload_to="avatars/", blank=True)
    avatar_url = models.URLField("Ссылка на аватар", blank=True)

    class Meta:
        verbose_name = "Профиль пользователя"
        verbose_name_plural = "Профили пользователей"

    def __str__(self):
        return f"{self.display_name} ({self.get_role_display()})"

    @property
    def display_name(self):
        return self.user.get_full_name() or self.user.username

    @property
    def initials(self):
        parts = [self.user.first_name, self.user.last_name]
        letters = [part[0].upper() for part in parts if part]
        if letters:
            return "".join(letters[:2])
        return self.user.username[:2].upper()

    @property
    def avatar_src(self):
        if self.avatar:
            return self.avatar.url
        return self.avatar_url


class StudyGroup(models.Model):
    name = models.CharField("Группа", max_length=30, unique=True)
    speciality = models.CharField("Направление подготовки", max_length=255)
    admission_year = models.PositiveSmallIntegerField("Год поступления")
    curator = models.ForeignKey(
        User,
        on_delete=models.SET_NULL,
        related_name="curated_groups",
        verbose_name="Куратор",
        blank=True,
        null=True,
    )

    class Meta:
        ordering = ["name"]
        verbose_name = "Учебная группа"
        verbose_name_plural = "Учебные группы"

    def __str__(self):
        return self.name


class Subject(models.Model):
    name = models.CharField("Дисциплина", max_length=255)
    code = models.CharField("Код", max_length=30, blank=True, null=True, unique=True)
    description = models.TextField("Описание", blank=True)

    class Meta:
        ordering = ["name"]
        verbose_name = "Дисциплина"
        verbose_name_plural = "Дисциплины"

    def __str__(self):
        return self.name


class Course(models.Model):
    subject = models.ForeignKey(
        Subject,
        on_delete=models.CASCADE,
        related_name="courses",
        verbose_name="Дисциплина",
    )
    group = models.ForeignKey(
        StudyGroup,
        on_delete=models.CASCADE,
        related_name="courses",
        verbose_name="Группа",
    )
    teacher = models.ForeignKey(
        User,
        on_delete=models.PROTECT,
        related_name="teaching_courses",
        verbose_name="Преподаватель",
    )
    semester = models.PositiveSmallIntegerField("Семестр")
    academic_year = models.CharField("Учебный год", max_length=9, help_text="Например: 2025/2026")

    class Meta:
        ordering = ["academic_year", "semester", "subject__name"]
        constraints = [
            models.UniqueConstraint(
                fields=["subject", "group", "teacher", "semester", "academic_year"],
                name="unique_course_for_group_teacher_year",
            )
        ]
        verbose_name = "Электронный журнал"
        verbose_name_plural = "Электронные журналы"

    def __str__(self):
        return f"{self.subject} - {self.group}, {self.academic_year}"


class StudentCard(models.Model):
    user = models.OneToOneField(
        User,
        on_delete=models.CASCADE,
        related_name="student_card",
        verbose_name="Пользователь",
    )
    group = models.ForeignKey(
        StudyGroup,
        on_delete=models.PROTECT,
        related_name="students",
        verbose_name="Группа",
    )
    record_book_number = models.CharField("Номер зачетной книжки", max_length=30, unique=True)
    enrollment_date = models.DateField("Дата зачисления", default=timezone.now)
    is_active = models.BooleanField("Обучается", default=True)

    class Meta:
        ordering = ["group__name", "user__last_name", "user__first_name"]
        verbose_name = "Карточка студента"
        verbose_name_plural = "Карточки студентов"

    def __str__(self):
        return f"{self.user.get_full_name() or self.user.username} ({self.group})"


class Lesson(models.Model):
    class LessonType(models.TextChoices):
        LECTURE = "lecture", "Лекция"
        PRACTICE = "practice", "Практика"
        LAB = "lab", "Лабораторная"
        SEMINAR = "seminar", "Семинар"
        EXAM = "exam", "Контроль"

    course = models.ForeignKey(
        Course,
        on_delete=models.CASCADE,
        related_name="lessons",
        verbose_name="Журнал",
    )
    date = models.DateField("Дата занятия", default=timezone.now)
    lesson_type = models.CharField(
        "Тип занятия",
        max_length=20,
        choices=LessonType.choices,
        default=LessonType.PRACTICE,
    )
    topic = models.CharField("Тема", max_length=255)
    created_at = models.DateTimeField("Создано", auto_now_add=True)

    class Meta:
        ordering = ["-date", "-created_at"]
        verbose_name = "Занятие"
        verbose_name_plural = "Занятия"

    def __str__(self):
        return f"{self.date:%d.%m.%Y}: {self.topic}"


class AttendanceRecord(models.Model):
    class Status(models.TextChoices):
        PRESENT = "present", "Присутствовал"
        ABSENT = "absent", "Отсутствовал"
        LATE = "late", "Опоздал"
        EXCUSED = "excused", "Уважительная причина"

    lesson = models.ForeignKey(
        Lesson,
        on_delete=models.CASCADE,
        related_name="attendance_records",
        verbose_name="Занятие",
    )
    student = models.ForeignKey(
        StudentCard,
        on_delete=models.CASCADE,
        related_name="attendance_records",
        verbose_name="Студент",
    )
    status = models.CharField("Статус", max_length=16, choices=Status.choices)
    comment = models.CharField("Комментарий", max_length=255, blank=True)
    updated_at = models.DateTimeField("Обновлено", auto_now=True)

    class Meta:
        constraints = [
            models.UniqueConstraint(
                fields=["lesson", "student"],
                name="unique_attendance_for_lesson_student",
            )
        ]
        verbose_name = "Посещаемость"
        verbose_name_plural = "Посещаемость"

    def __str__(self):
        return f"{self.student} - {self.lesson}: {self.get_status_display()}"


class Grade(models.Model):
    class GradeType(models.TextChoices):
        CURRENT = "current", "Текущая"
        CONTROL = "control", "Контрольная"
        EXAM = "exam", "Экзамен"
        COURSEWORK = "coursework", "Курсовая"
        FINAL = "final", "Итоговая"

    class GradeValue(models.TextChoices):
        TWO = "2", "2"
        THREE = "3", "3"
        FOUR = "4", "4"
        FIVE = "5", "5"
        PASS = "pass", "Зачет"
        FAIL = "fail", "Незачет"

    course = models.ForeignKey(
        Course,
        on_delete=models.CASCADE,
        related_name="grades",
        verbose_name="Журнал",
    )
    student = models.ForeignKey(
        StudentCard,
        on_delete=models.CASCADE,
        related_name="grades",
        verbose_name="Студент",
    )
    lesson = models.ForeignKey(
        Lesson,
        on_delete=models.SET_NULL,
        related_name="grades",
        verbose_name="Занятие",
        blank=True,
        null=True,
    )
    grade_type = models.CharField(
        "Тип оценки",
        max_length=20,
        choices=GradeType.choices,
        default=GradeType.CURRENT,
    )
    value = models.CharField("Оценка", max_length=10, choices=GradeValue.choices)
    comment = models.CharField("Комментарий", max_length=255, blank=True)
    date = models.DateField("Дата выставления", default=timezone.now)
    updated_at = models.DateTimeField("Обновлено", auto_now=True)

    class Meta:
        ordering = ["-date", "student__user__last_name"]
        constraints = [
            models.UniqueConstraint(
                fields=["course", "student", "lesson", "grade_type"],
                name="unique_grade_for_lesson_student_type",
            )
        ]
        verbose_name = "Оценка"
        verbose_name_plural = "Оценки"

    def __str__(self):
        return f"{self.student}: {self.get_value_display()} ({self.course})"


class ScheduleEntry(models.Model):
    class Weekday(models.IntegerChoices):
        MONDAY = 1, "Понедельник"
        TUESDAY = 2, "Вторник"
        WEDNESDAY = 3, "Среда"
        THURSDAY = 4, "Четверг"
        FRIDAY = 5, "Пятница"
        SATURDAY = 6, "Суббота"

    class WeekType(models.TextChoices):
        EVERY = "every", "Каждую неделю"
        EVEN = "even", "Четная неделя"
        ODD = "odd", "Нечетная неделя"

    course = models.ForeignKey(
        Course,
        on_delete=models.CASCADE,
        related_name="schedule_entries",
        verbose_name="Журнал",
    )
    weekday = models.PositiveSmallIntegerField("День недели", choices=Weekday.choices)
    start_time = models.TimeField("Начало пары")
    end_time = models.TimeField("Окончание пары")
    lesson_type = models.CharField(
        "Тип занятия",
        max_length=20,
        choices=Lesson.LessonType.choices,
        default=Lesson.LessonType.PRACTICE,
    )
    room = models.CharField("Аудитория", max_length=40)
    building = models.CharField("Корпус", max_length=80, blank=True)
    week_type = models.CharField(
        "Неделя",
        max_length=10,
        choices=WeekType.choices,
        default=WeekType.EVERY,
    )
    is_active = models.BooleanField("Активно", default=True)
    comment = models.CharField("Комментарий", max_length=255, blank=True)

    class Meta:
        ordering = ["weekday", "start_time", "course__group__name", "course__subject__name"]
        constraints = [
            models.UniqueConstraint(
                fields=["course", "weekday", "start_time", "week_type"],
                name="unique_schedule_course_time",
            )
        ]
        verbose_name = "Занятие расписания"
        verbose_name_plural = "Расписание"

    def __str__(self):
        return (
            f"{self.get_weekday_display()} {self.start_time:%H:%M} "
            f"{self.course.group} - {self.course.subject}"
        )


def apply_role_permissions(profile):
    role_labels = {
        Profile.Role.ADMIN: "Администраторы",
        Profile.Role.TEACHER: "Преподаватели",
        Profile.Role.STUDENT: "Студенты",
    }
    role = Profile.Role.ADMIN if profile.user.is_superuser else profile.role
    role_group, _ = Group.objects.get_or_create(name=role_labels[role])

    for group_name in role_labels.values():
        group, _ = Group.objects.get_or_create(name=group_name)
        if group != role_group:
            profile.user.groups.remove(group)

    models_for_permissions = [
        Profile,
        StudyGroup,
        Subject,
        Course,
        StudentCard,
        Lesson,
        AttendanceRecord,
        Grade,
        ScheduleEntry,
    ]

    if role == Profile.Role.ADMIN:
        codenames = ["add", "change", "delete", "view"]
        profile.user.is_staff = True
    elif role == Profile.Role.TEACHER:
        codenames = ["add", "change", "view"]
        profile.user.is_staff = False
    else:
        codenames = ["view"]
        profile.user.is_staff = False

    permissions = []
    for model in models_for_permissions:
        content_type = ContentType.objects.get_for_model(model)
        for action in codenames:
            codename = f"{action}_{model._meta.model_name}"
            permission = Permission.objects.filter(content_type=content_type, codename=codename).first()
            if permission:
                permissions.append(permission)

    role_group.permissions.set(permissions)
    profile.user.groups.add(role_group)
    profile.user.user_permissions.clear()
    profile.user.save(update_fields=["is_staff"])


@receiver(post_save, sender=User)
def ensure_user_profile(sender, instance, created, **kwargs):
    if created:
        role = Profile.Role.ADMIN if instance.is_superuser else Profile.Role.STUDENT
        Profile.objects.create(user=instance, role=role)
    elif instance.is_superuser and hasattr(instance, "profile"):
        if instance.profile.role != Profile.Role.ADMIN:
            instance.profile.role = Profile.Role.ADMIN
            instance.profile.save(update_fields=["role"])


@receiver(post_save, sender=Profile)
def sync_profile_role(sender, instance, **kwargs):
    apply_role_permissions(instance)
