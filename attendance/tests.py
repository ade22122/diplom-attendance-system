from django.contrib.auth import get_user_model
from django.test import TestCase
from datetime import time
from io import BytesIO
import shutil
import tempfile

from django.core.cache import cache
from django.core.files.uploadedfile import SimpleUploadedFile
from django.test import override_settings
from PIL import Image
from .models import AttendanceRecord, Course, Grade, Lesson, Profile, ScheduleEntry, StudentCard, StudyGroup, Subject


def make_png():
    image = Image.new("RGB", (1, 1), color=(255, 255, 255))
    buffer = BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


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

    def test_teacher_can_view_student_course_progress(self):
        AttendanceRecord.objects.create(
            lesson=self.lesson,
            student=self.student_card,
            status=AttendanceRecord.Status.PRESENT,
        )
        Grade.objects.create(
            course=self.course,
            lesson=self.lesson,
            student=self.student_card,
            value=Grade.GradeValue.FIVE,
        )

        self.client.login(username="teacher", password="password12345")
        response = self.client.get(f"/teacher/courses/{self.course.id}/students/{self.student_card.id}/")

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Персональная сводка преподавателя")
        self.assertContains(response, "Посещаемость")
        self.assertContains(response, "100%")

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

    def test_admin_schedule_rejects_teacher_time_conflict(self):
        group = StudyGroup.objects.create(
            name="ИС-22",
            speciality="Информационные системы",
            admission_year=2023,
        )
        course = Course.objects.create(
            subject=self.subject,
            group=group,
            teacher=self.teacher,
            semester=5,
            academic_year="2025/2026",
        )

        self.client.login(username="admin", password="password12345")
        response = self.client.post(
            "/admin-tools/schedule/",
            {
                "course": course.id,
                "weekday": ScheduleEntry.Weekday.MONDAY,
                "start_time": "09:15",
                "end_time": "10:00",
                "lesson_type": Lesson.LessonType.LECTURE,
                "room": "405",
                "building": "Главный корпус",
                "week_type": ScheduleEntry.WeekType.EVERY,
                "is_active": "on",
                "comment": "",
                "action": "save",
            },
        )

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "У преподавателя уже есть пара")

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


class RegistrationAndProfileTests(TestCase):
    def setUp(self):
        self.media_root = tempfile.mkdtemp()
        self.settings_override = override_settings(MEDIA_ROOT=self.media_root)
        self.settings_override.enable()
        self.group = StudyGroup.objects.create(
            name="ПИ-24",
            speciality="Прикладная информатика",
            admission_year=2024,
        )

    def tearDown(self):
        self.settings_override.disable()
        shutil.rmtree(self.media_root, ignore_errors=True)

    def test_registration_creates_student_account(self):
        response = self.client.post(
            "/register/",
            {
                "username": "newstudent",
                "first_name": "Павел",
                "last_name": "Иванов",
                "email": "newstudent@example.local",
                "group": self.group.id,
                "password1": "StrongPass12345",
                "password2": "StrongPass12345",
            },
        )

        self.assertEqual(response.status_code, 302)
        User = get_user_model()
        user = User.objects.get(username="newstudent")
        self.assertEqual(user.profile.role, Profile.Role.STUDENT)
        self.assertFalse(user.is_staff)
        self.assertTrue(StudentCard.objects.filter(user=user, group=self.group).exists())

    def test_registration_rejects_duplicate_email(self):
        User = get_user_model()
        User.objects.create_user(username="existing", password="password12345", email="same@example.local")

        response = self.client.post(
            "/register/",
            {
                "username": "newstudent",
                "first_name": "Павел",
                "last_name": "Иванов",
                "email": "same@example.local",
                "group": self.group.id,
                "password1": "StrongPass12345",
                "password2": "StrongPass12345",
            },
        )

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Пользователь с таким email уже зарегистрирован")
        self.assertFalse(User.objects.filter(username="newstudent").exists())

    def test_email_ajax_reports_duplicate(self):
        User = get_user_model()
        User.objects.create_user(username="existing", password="password12345", email="same@example.local")

        response = self.client.get("/ajax/check-email/", {"email": "same@example.local"})

        self.assertEqual(response.status_code, 200)
        self.assertFalse(response.json()["available"])

    def test_user_can_upload_avatar_in_profile(self):
        User = get_user_model()
        user = User.objects.create_user(
            username="avatarstudent",
            password="password12345",
            first_name="Иван",
            last_name="Петров",
        )
        self.client.login(username="avatarstudent", password="password12345")
        avatar = SimpleUploadedFile("avatar.png", make_png(), content_type="image/png")

        response = self.client.post(
            "/profile/",
            {
                "first_name": "Иван",
                "last_name": "Петров",
                "email": "avatar@example.local",
                "patronymic": "Иванович",
                "phone": "+7 999 000-00-00",
                "avatar": avatar,
            },
            follow=True,
        )

        self.assertEqual(response.status_code, 200)
        self.assertContains(response, "Профиль обновлен")
        user.profile.refresh_from_db()
        self.assertFalse(user.profile.avatar)
        self.assertEqual(user.profile.avatar_content_type, "image/png")
        self.assertTrue(user.profile.avatar_image)
        avatar_response = self.client.get(user.profile.avatar_src)
        self.assertEqual(avatar_response.status_code, 200)
        self.assertEqual(avatar_response["Content-Type"], "image/png")
        self.assertEqual(user.profile.phone, "+7 999 000-00-00")
