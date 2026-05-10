from datetime import date, time, timedelta

from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand

from attendance.models import (
    AttendanceRecord,
    Course,
    Grade,
    Lesson,
    Profile,
    ScheduleEntry,
    StudentCard,
    StudyGroup,
    Subject,
    apply_role_permissions,
)


class Command(BaseCommand):
    help = "Creates demo users, groups, courses, grades and schedule for a project review."

    def handle(self, *args, **options):
        User = get_user_model()

        def create_user(username, password, first_name, last_name, role, email="", phone=""):
            user, _ = User.objects.get_or_create(
                username=username,
                defaults={"first_name": first_name, "last_name": last_name, "email": email},
            )
            user.first_name = first_name
            user.last_name = last_name
            user.email = email
            user.set_password(password)
            user.save()
            profile, _ = Profile.objects.get_or_create(user=user)
            profile.role = role
            profile.phone = phone
            profile.save()
            apply_role_permissions(profile)
            return user

        teachers = {
            "teacher1": create_user(
                "teacher1",
                "teacher12345",
                "Анна",
                "Смирнова",
                Profile.Role.TEACHER,
                "teacher1@example.local",
                "+7 900 111-01-01",
            ),
            "teacher2": create_user(
                "teacher2",
                "teacher12345",
                "Дмитрий",
                "Волков",
                Profile.Role.TEACHER,
                "teacher2@example.local",
                "+7 900 111-01-02",
            ),
            "teacher3": create_user(
                "teacher3",
                "teacher12345",
                "Мария",
                "Кузнецова",
                Profile.Role.TEACHER,
                "teacher3@example.local",
                "+7 900 111-01-03",
            ),
            "teacher4": create_user(
                "teacher4",
                "teacher12345",
                "Сергей",
                "Орлов",
                Profile.Role.TEACHER,
                "teacher4@example.local",
                "+7 900 111-01-04",
            ),
        }

        groups_data = [
            ("ИС-21", "Информационные системы и программирование", 2022, "teacher1"),
            ("ИС-22", "Информационные системы и программирование", 2023, "teacher1"),
            ("БИ-21", "Бизнес-информатика", 2022, "teacher3"),
            ("ПМ-23", "Прикладная информатика", 2023, "teacher2"),
        ]
        groups = {}
        for name, speciality, admission_year, curator_username in groups_data:
            group, _ = StudyGroup.objects.update_or_create(
                name=name,
                defaults={
                    "speciality": speciality,
                    "admission_year": admission_year,
                    "curator": teachers[curator_username],
                },
            )
            groups[name] = group

        students_data = {
            "ИС-21": [
                ("student1", "Иван", "Петров", "IS21001"),
                ("student2", "Алина", "Соколова", "IS21002"),
                ("student3", "Никита", "Морозов", "IS21003"),
                ("student4", "Екатерина", "Федорова", "IS21004"),
            ],
            "ИС-22": [
                ("student5", "Артем", "Новиков", "IS22001"),
                ("student6", "Полина", "Лебедева", "IS22002"),
                ("student7", "Даниил", "Егоров", "IS22003"),
                ("student8", "Софья", "Макарова", "IS22004"),
            ],
            "БИ-21": [
                ("student9", "Кирилл", "Алексеев", "BI21001"),
                ("student10", "Вероника", "Павлова", "BI21002"),
                ("student11", "Роман", "Семенов", "BI21003"),
                ("student12", "Дарья", "Крылова", "BI21004"),
            ],
            "ПМ-23": [
                ("student13", "Глеб", "Захаров", "PM23001"),
                ("student14", "Милана", "Беляева", "PM23002"),
                ("student15", "Тимур", "Громов", "PM23003"),
                ("student16", "Ева", "Киселева", "PM23004"),
            ],
        }

        student_cards = []
        for group_name, students in students_data.items():
            for username, first_name, last_name, record_number in students:
                student = create_user(
                    username,
                    "student12345",
                    first_name,
                    last_name,
                    Profile.Role.STUDENT,
                    f"{username}@example.local",
                    "",
                )
                card, _ = StudentCard.objects.update_or_create(
                    user=student,
                    defaults={
                        "group": groups[group_name],
                        "record_book_number": record_number,
                        "enrollment_date": date(groups[group_name].admission_year, 9, 1),
                        "is_active": True,
                    },
                )
                student_cards.append(card)

        subjects_data = [
            ("DB-01", "Базы данных", "Проектирование и работа с PostgreSQL"),
            ("PY-01", "Программирование на Python", "Разработка прикладных систем"),
            ("WEB-01", "Веб-технологии", "HTML, CSS, серверные веб-приложения"),
            ("OS-01", "Операционные системы", "Процессы, память, файловые системы"),
            ("IS-SEC", "Информационная безопасность", "Основы защиты информации"),
            ("MATH-01", "Дискретная математика", "Логика, графы и комбинаторика"),
            ("PM-01", "Управление IT-проектами", "Планирование и контроль проектов"),
            ("AN-01", "Анализ данных", "Методы обработки учебных и прикладных данных"),
        ]
        subjects = {}
        for code, name, description in subjects_data:
            subject, _ = Subject.objects.update_or_create(
                code=code,
                defaults={"name": name, "description": description},
            )
            subjects[code] = subject

        course_defs = [
            ("DB-01", "ИС-21", "teacher1", 7),
            ("PY-01", "ИС-21", "teacher2", 7),
            ("WEB-01", "ИС-21", "teacher2", 7),
            ("IS-SEC", "ИС-21", "teacher4", 7),
            ("DB-01", "ИС-22", "teacher1", 5),
            ("PY-01", "ИС-22", "teacher2", 5),
            ("MATH-01", "ИС-22", "teacher3", 5),
            ("OS-01", "ИС-22", "teacher4", 5),
            ("AN-01", "БИ-21", "teacher3", 7),
            ("PM-01", "БИ-21", "teacher4", 7),
            ("DB-01", "БИ-21", "teacher1", 7),
            ("WEB-01", "ПМ-23", "teacher2", 3),
            ("MATH-01", "ПМ-23", "teacher3", 3),
            ("PM-01", "ПМ-23", "teacher4", 3),
        ]
        courses = {}
        for subject_code, group_name, teacher_username, semester in course_defs:
            course, _ = Course.objects.get_or_create(
                subject=subjects[subject_code],
                group=groups[group_name],
                teacher=teachers[teacher_username],
                semester=semester,
                academic_year="2025/2026",
            )
            courses[(subject_code, group_name)] = course

        schedule_defs = [
            ("DB-01", "ИС-21", 1, time(9, 0), time(10, 30), "lecture", "304", "Главный корпус"),
            ("PY-01", "ИС-21", 1, time(10, 45), time(12, 15), "lab", "212", "IT-корпус"),
            ("WEB-01", "ИС-21", 3, time(9, 0), time(10, 30), "practice", "215", "IT-корпус"),
            ("IS-SEC", "ИС-21", 5, time(12, 40), time(14, 10), "lecture", "118", "Главный корпус"),
            ("DB-01", "ИС-22", 2, time(9, 0), time(10, 30), "practice", "304", "Главный корпус"),
            ("PY-01", "ИС-22", 2, time(10, 45), time(12, 15), "lab", "214", "IT-корпус"),
            ("MATH-01", "ИС-22", 4, time(9, 0), time(10, 30), "seminar", "407", "Главный корпус"),
            ("OS-01", "ИС-22", 5, time(10, 45), time(12, 15), "lecture", "309", "IT-корпус"),
            ("AN-01", "БИ-21", 1, time(12, 40), time(14, 10), "practice", "402", "Главный корпус"),
            ("PM-01", "БИ-21", 3, time(10, 45), time(12, 15), "seminar", "105", "Главный корпус"),
            ("DB-01", "БИ-21", 4, time(12, 40), time(14, 10), "lecture", "304", "Главный корпус"),
            ("WEB-01", "ПМ-23", 2, time(12, 40), time(14, 10), "practice", "216", "IT-корпус"),
            ("MATH-01", "ПМ-23", 3, time(9, 0), time(10, 30), "lecture", "407", "Главный корпус"),
            ("PM-01", "ПМ-23", 5, time(9, 0), time(10, 30), "seminar", "105", "Главный корпус"),
        ]
        for subject_code, group_name, weekday, start, end, lesson_type, room, building in schedule_defs:
            ScheduleEntry.objects.update_or_create(
                course=courses[(subject_code, group_name)],
                weekday=weekday,
                start_time=start,
                week_type=ScheduleEntry.WeekType.EVERY,
                defaults={
                    "end_time": end,
                    "lesson_type": lesson_type,
                    "room": room,
                    "building": building,
                    "is_active": True,
                },
            )

        statuses = [
            AttendanceRecord.Status.PRESENT,
            AttendanceRecord.Status.PRESENT,
            AttendanceRecord.Status.LATE,
            AttendanceRecord.Status.EXCUSED,
        ]
        grade_values = [Grade.GradeValue.FIVE, Grade.GradeValue.FOUR, Grade.GradeValue.THREE, Grade.GradeValue.FIVE]

        for index, course in enumerate(courses.values()):
            lesson, _ = Lesson.objects.get_or_create(
                course=course,
                topic=f"Вводное занятие: {course.subject.name}",
                defaults={
                    "date": date.today() - timedelta(days=index % 12),
                    "lesson_type": Lesson.LessonType.PRACTICE,
                },
            )
            group_students = StudentCard.objects.filter(group=course.group, is_active=True).select_related("user")
            for student_index, student_card in enumerate(group_students):
                AttendanceRecord.objects.update_or_create(
                    lesson=lesson,
                    student=student_card,
                    defaults={"status": statuses[student_index % len(statuses)], "comment": ""},
                )
                Grade.objects.update_or_create(
                    course=course,
                    lesson=lesson,
                    student=student_card,
                    grade_type=Grade.GradeType.CURRENT,
                    defaults={
                        "value": grade_values[(student_index + index) % len(grade_values)],
                        "comment": "Демо-оценка",
                        "date": lesson.date,
                    },
                )
                Grade.objects.update_or_create(
                    course=course,
                    lesson=None,
                    student=student_card,
                    grade_type=Grade.GradeType.FINAL,
                    defaults={
                        "value": grade_values[(student_index + index + 1) % len(grade_values)],
                        "comment": "Промежуточный итог",
                        "date": date.today(),
                    },
                )

        self.stdout.write(self.style.SUCCESS("Demo data created."))
        self.stdout.write("Teachers: teacher1..teacher4 / teacher12345")
        self.stdout.write("Students: student1..student16 / student12345")
