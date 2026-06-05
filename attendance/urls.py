from django.urls import path

from . import views


urlpatterns = [
    path("register/", views.register, name="register"),
    path("ajax/check-email/", views.check_email_unique, name="check_email_unique"),
    path("profile/", views.profile_edit, name="profile_edit"),
    path("profiles/<int:profile_id>/avatar/", views.profile_avatar, name="profile_avatar"),
    path("", views.dashboard, name="dashboard"),
    path("teacher/courses/", views.teacher_courses, name="teacher_courses"),
    path("teacher/courses/<int:course_id>/", views.teacher_course_detail, name="teacher_course_detail"),
    path(
        "teacher/courses/<int:course_id>/students/<int:student_id>/",
        views.teacher_student_detail,
        name="teacher_student_detail",
    ),
    path(
        "teacher/courses/<int:course_id>/lessons/new/",
        views.lesson_create,
        name="lesson_create",
    ),
    path("teacher/lessons/<int:lesson_id>/journal/", views.lesson_journal, name="lesson_journal"),
    path("student/courses/<int:course_id>/", views.student_course_detail, name="student_course_detail"),
    path("schedule/", views.schedule_view, name="schedule"),
    path("record-book/", views.record_book, name="record_book"),
    path("admin-tools/schedule/", views.admin_schedule_manage, name="admin_schedule_manage"),
    path("admin-tools/roles/", views.admin_roles, name="admin_roles"),
]
