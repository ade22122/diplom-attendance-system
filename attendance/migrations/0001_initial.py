import django.db.models.deletion
import django.utils.timezone
from django.conf import settings
from django.db import migrations, models


class Migration(migrations.Migration):
    initial = True

    dependencies = [
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
    ]

    operations = [
        migrations.CreateModel(
            name="Profile",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                (
                    "role",
                    models.CharField(
                        choices=[
                            ("admin", "Администратор"),
                            ("teacher", "Преподаватель"),
                            ("student", "Студент"),
                        ],
                        default="student",
                        max_length=16,
                    ),
                ),
                ("patronymic", models.CharField(blank=True, max_length=150, verbose_name="Отчество")),
                ("phone", models.CharField(blank=True, max_length=30, verbose_name="Телефон")),
                (
                    "user",
                    models.OneToOneField(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="profile",
                        to=settings.AUTH_USER_MODEL,
                    ),
                ),
            ],
            options={
                "verbose_name": "Профиль пользователя",
                "verbose_name_plural": "Профили пользователей",
            },
        ),
        migrations.CreateModel(
            name="StudyGroup",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("name", models.CharField(max_length=30, unique=True, verbose_name="Группа")),
                ("speciality", models.CharField(max_length=255, verbose_name="Направление подготовки")),
                ("admission_year", models.PositiveSmallIntegerField(verbose_name="Год поступления")),
                (
                    "curator",
                    models.ForeignKey(
                        blank=True,
                        null=True,
                        on_delete=django.db.models.deletion.SET_NULL,
                        related_name="curated_groups",
                        to=settings.AUTH_USER_MODEL,
                        verbose_name="Куратор",
                    ),
                ),
            ],
            options={
                "verbose_name": "Учебная группа",
                "verbose_name_plural": "Учебные группы",
                "ordering": ["name"],
            },
        ),
        migrations.CreateModel(
            name="Subject",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("name", models.CharField(max_length=255, verbose_name="Дисциплина")),
                ("code", models.CharField(blank=True, max_length=30, null=True, unique=True, verbose_name="Код")),
                ("description", models.TextField(blank=True, verbose_name="Описание")),
            ],
            options={
                "verbose_name": "Дисциплина",
                "verbose_name_plural": "Дисциплины",
                "ordering": ["name"],
            },
        ),
        migrations.CreateModel(
            name="Course",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("semester", models.PositiveSmallIntegerField(verbose_name="Семестр")),
                (
                    "academic_year",
                    models.CharField(help_text="Например: 2025/2026", max_length=9, verbose_name="Учебный год"),
                ),
                (
                    "group",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="courses",
                        to="attendance.studygroup",
                        verbose_name="Группа",
                    ),
                ),
                (
                    "subject",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="courses",
                        to="attendance.subject",
                        verbose_name="Дисциплина",
                    ),
                ),
                (
                    "teacher",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.PROTECT,
                        related_name="teaching_courses",
                        to=settings.AUTH_USER_MODEL,
                        verbose_name="Преподаватель",
                    ),
                ),
            ],
            options={
                "verbose_name": "Электронный журнал",
                "verbose_name_plural": "Электронные журналы",
                "ordering": ["academic_year", "semester", "subject__name"],
            },
        ),
        migrations.CreateModel(
            name="StudentCard",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                (
                    "record_book_number",
                    models.CharField(max_length=30, unique=True, verbose_name="Номер зачетной книжки"),
                ),
                (
                    "enrollment_date",
                    models.DateField(default=django.utils.timezone.now, verbose_name="Дата зачисления"),
                ),
                ("is_active", models.BooleanField(default=True, verbose_name="Обучается")),
                (
                    "group",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.PROTECT,
                        related_name="students",
                        to="attendance.studygroup",
                        verbose_name="Группа",
                    ),
                ),
                (
                    "user",
                    models.OneToOneField(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="student_card",
                        to=settings.AUTH_USER_MODEL,
                        verbose_name="Пользователь",
                    ),
                ),
            ],
            options={
                "verbose_name": "Карточка студента",
                "verbose_name_plural": "Карточки студентов",
                "ordering": ["group__name", "user__last_name", "user__first_name"],
            },
        ),
        migrations.CreateModel(
            name="Lesson",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("date", models.DateField(default=django.utils.timezone.now, verbose_name="Дата занятия")),
                (
                    "lesson_type",
                    models.CharField(
                        choices=[
                            ("lecture", "Лекция"),
                            ("practice", "Практика"),
                            ("lab", "Лабораторная"),
                            ("seminar", "Семинар"),
                            ("exam", "Контроль"),
                        ],
                        default="practice",
                        max_length=20,
                        verbose_name="Тип занятия",
                    ),
                ),
                ("topic", models.CharField(max_length=255, verbose_name="Тема")),
                ("created_at", models.DateTimeField(auto_now_add=True, verbose_name="Создано")),
                (
                    "course",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="lessons",
                        to="attendance.course",
                        verbose_name="Журнал",
                    ),
                ),
            ],
            options={
                "verbose_name": "Занятие",
                "verbose_name_plural": "Занятия",
                "ordering": ["-date", "-created_at"],
            },
        ),
        migrations.CreateModel(
            name="AttendanceRecord",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                (
                    "status",
                    models.CharField(
                        choices=[
                            ("present", "Присутствовал"),
                            ("absent", "Отсутствовал"),
                            ("late", "Опоздал"),
                            ("excused", "Уважительная причина"),
                        ],
                        max_length=16,
                        verbose_name="Статус",
                    ),
                ),
                ("comment", models.CharField(blank=True, max_length=255, verbose_name="Комментарий")),
                ("updated_at", models.DateTimeField(auto_now=True, verbose_name="Обновлено")),
                (
                    "lesson",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="attendance_records",
                        to="attendance.lesson",
                        verbose_name="Занятие",
                    ),
                ),
                (
                    "student",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="attendance_records",
                        to="attendance.studentcard",
                        verbose_name="Студент",
                    ),
                ),
            ],
            options={
                "verbose_name": "Посещаемость",
                "verbose_name_plural": "Посещаемость",
            },
        ),
        migrations.CreateModel(
            name="Grade",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                (
                    "grade_type",
                    models.CharField(
                        choices=[
                            ("current", "Текущая"),
                            ("control", "Контрольная"),
                            ("exam", "Экзамен"),
                            ("coursework", "Курсовая"),
                            ("final", "Итоговая"),
                        ],
                        default="current",
                        max_length=20,
                        verbose_name="Тип оценки",
                    ),
                ),
                (
                    "value",
                    models.CharField(
                        choices=[
                            ("2", "2"),
                            ("3", "3"),
                            ("4", "4"),
                            ("5", "5"),
                            ("pass", "Зачет"),
                            ("fail", "Незачет"),
                        ],
                        max_length=10,
                        verbose_name="Оценка",
                    ),
                ),
                ("comment", models.CharField(blank=True, max_length=255, verbose_name="Комментарий")),
                ("date", models.DateField(default=django.utils.timezone.now, verbose_name="Дата выставления")),
                ("updated_at", models.DateTimeField(auto_now=True, verbose_name="Обновлено")),
                (
                    "course",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="grades",
                        to="attendance.course",
                        verbose_name="Журнал",
                    ),
                ),
                (
                    "lesson",
                    models.ForeignKey(
                        blank=True,
                        null=True,
                        on_delete=django.db.models.deletion.SET_NULL,
                        related_name="grades",
                        to="attendance.lesson",
                        verbose_name="Занятие",
                    ),
                ),
                (
                    "student",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="grades",
                        to="attendance.studentcard",
                        verbose_name="Студент",
                    ),
                ),
            ],
            options={
                "verbose_name": "Оценка",
                "verbose_name_plural": "Оценки",
                "ordering": ["-date", "student__user__last_name"],
            },
        ),
        migrations.AddConstraint(
            model_name="course",
            constraint=models.UniqueConstraint(
                fields=("subject", "group", "teacher", "semester", "academic_year"),
                name="unique_course_for_group_teacher_year",
            ),
        ),
        migrations.AddConstraint(
            model_name="attendancerecord",
            constraint=models.UniqueConstraint(
                fields=("lesson", "student"),
                name="unique_attendance_for_lesson_student",
            ),
        ),
        migrations.AddConstraint(
            model_name="grade",
            constraint=models.UniqueConstraint(
                fields=("course", "student", "lesson", "grade_type"),
                name="unique_grade_for_lesson_student_type",
            ),
        ),
    ]
