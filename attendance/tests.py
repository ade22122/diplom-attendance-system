from django.contrib.auth import get_user_model
from django.test import TestCase
from datetime import time

from django.core.cache import cache
from .models import Course, Lesson, Profile, ScheduleEntry, StudentCard, StudyGroup, Subject


class AttendanceModelTests(TestCase):
    def test_course_connects_group_teacher_and_subject(self):
        User = get_user_model()
        teacher = User.objects.create_user(username="teacher", password="password")
        teacher.profile.role = Profile.Role.TEACHER
        teacher.profile.save()

        group = StudyGroup.objects.create(
            name="ИС-21",
            speciality="Информационные системы",
            admission_year=2022,
        )
        student = User.objects.create_user(username="student", password="password")
        StudentCard.objects.create(user=student, group=group, record_book_number="001")
        subject = Subject.objects.create(name="Базы данных", code="DB")
        course = Course.objects.create(
            subject=subject,
            group=group,
            teacher=teacher,
            semester=7,
            academic_year="2025/2026",
        )
        lesson = Lesson.objects.create(course=course, topic="PostgreSQL")

        self.assertEqual(str(course), "Базы данных - ИС-21, 2025/2026")
        self.assertEqual(lesson.course.group, group)


class DashboardViewTests(TestCase):
    def setUp(self):
        User = get_user_model()
        self.teacher = User.objects.create_user(
            username="teacher",
            password="password12345",
            first_name="Анна",
            last_name="Смирнова",
        )
        self.teacher.profile.role = Profile.Role.TEACHER
        self.teacher.profile.save()

        self.admin = User.objects.create_superuser(
            username="admin",
            password="password12345",
            email="admin@example.local",
        )

        self.group = StudyGroup.objects.create(
            name="ИС-21",
            speciality="Информационные системы",
            admission_year=2022,
        )
        self.student = User.objects.create_user(
            username="student",
            password="password12345",
            first_name="Иван",
            last_name="Петров",
        )
        self.student_card = StudentCard.objects.create(
            user=self.student,
            group=self.group,
            record_book_number="002",
        )
        self.subject = Subject.objects.create(name="Базы данных", code="DB2")
        self.course = Course.objects.create(
            subject=self.subject,
            group=self.group,
            teacher=self.teacher,
            semester=7,
            academic_year="2025/2026",
        )
        self.lesson = Lesson.objects.create(course=self.course, topic="PostgreSQL")
        self.schedule_entry = ScheduleEntry.objects.create(
            course=self.course,
            weekday=ScheduleEntry.Weekday.MONDAY,
            start_time=time(9, 0),
            end_time=time(10, 30),
            room="304",
        )

    def test_admin_dashboard_renders(self):
        self.client.login(username="admin", password="password12345")
        response = self.client.get("/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Центр управления учебным процессом")
        self.assertContains(response, "Пользователи")

    def test_teacher_dashboard_renders(self):
        self.client.login(username="teacher", password="password12345")
        response = self.client.get("/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Мои журналы")
        self.assertContains(response, "Базы данных")

    def test_student_dashboard_renders(self):
        self.client.login(username="student", password="password12345")
        response = self.client.get("/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Мои дисциплины")
        self.assertContains(response, "Учебная студия")
        self.assertContains(response, "Базы данных")

    def test_schedule_renders_for_student_group(self):
        self.client.login(username="student", password="password12345")
        response = self.client.get("/schedule/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Расписание группы ИС-21")
        self.assertContains(response, "304")

    def test_record_book_renders_for_student(self):
        self.client.login(username="student", password="password12345")
        response = self.client.get("/record-book/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Зачетная книжка")
        self.assertContains(response, "Базы данных")

    def test_admin_roles_page_renders(self):
        self.client.login(username="admin", password="password12345")
        response = self.client.get("/admin-tools/roles/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Роли и права доступа")

    def test_security_headers_are_present(self):
        self.client.login(username="student", password="password12345")
        response = self.client.get("/")

        self.assertEqual(response["X-Frame-Options"], "DENY")
        self.assertIn("default-src 'self'", response["Content-Security-Policy"])
        self.assertIn("camera=()", response["Permissions-Policy"])

    def test_login_is_rate_limited(self):
        cache.clear()
        for _ in range(6):
            response = self.client.post(
                "/accounts/login/",
                {"username": "student", "password": "wrong-password"},
                REMOTE_ADDR="127.0.0.10",
            )

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Слишком много попыток входа")
