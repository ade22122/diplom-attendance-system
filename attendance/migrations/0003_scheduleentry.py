import django.db.models.deletion
from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [
        ("attendance", "0002_profile_avatar_url"),
    ]

    operations = [
        migrations.CreateModel(
            name="ScheduleEntry",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                (
                    "weekday",
                    models.PositiveSmallIntegerField(
                        choices=[
                            (1, "Понедельник"),
                            (2, "Вторник"),
                            (3, "Среда"),
                            (4, "Четверг"),
                            (5, "Пятница"),
                            (6, "Суббота"),
                        ],
                        verbose_name="День недели",
                    ),
                ),
                ("start_time", models.TimeField(verbose_name="Начало пары")),
                ("end_time", models.TimeField(verbose_name="Окончание пары")),
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
                ("room", models.CharField(max_length=40, verbose_name="Аудитория")),
                ("building", models.CharField(blank=True, max_length=80, verbose_name="Корпус")),
                (
                    "week_type",
                    models.CharField(
                        choices=[
                            ("every", "Каждую неделю"),
                            ("even", "Четная неделя"),
                            ("odd", "Нечетная неделя"),
                        ],
                        default="every",
                        max_length=10,
                        verbose_name="Неделя",
                    ),
                ),
                ("is_active", models.BooleanField(default=True, verbose_name="Активно")),
                ("comment", models.CharField(blank=True, max_length=255, verbose_name="Комментарий")),
                (
                    "course",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="schedule_entries",
                        to="attendance.course",
                        verbose_name="Журнал",
                    ),
                ),
            ],
            options={
                "verbose_name": "Занятие расписания",
                "verbose_name_plural": "Расписание",
                "ordering": ["weekday", "start_time", "course__group__name", "course__subject__name"],
            },
        ),
        migrations.AddConstraint(
            model_name="scheduleentry",
            constraint=models.UniqueConstraint(
                fields=("course", "weekday", "start_time", "week_type"),
                name="unique_schedule_course_time",
            ),
        ),
    ]
