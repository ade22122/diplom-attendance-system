from django.contrib import admin
from django.contrib.auth import get_user_model
from django.contrib.auth.admin import UserAdmin as DjangoUserAdmin
from django.utils.html import format_html

from .models import (
    AttendanceRecord,
    Course,
    Grade,
    Lesson,
    Profile,
    ScheduleEntry,
    StudentCard,
    StudyGroup,
    Subject,
)


admin.site.site_header = "МГУТУ: электронный журнал"
admin.site.site_title = "Администрирование журнала"
admin.site.index_title = "Управление учебными данными"

User = get_user_model()


class ProfileInline(admin.StackedInline):
    model = Profile
    can_delete = False
    extra = 0
    fields = ("role", "patronymic", "phone", "avatar", "avatar_url")
    verbose_name = "Профиль и роль"
    verbose_name_plural = "Профиль и роль"


try:
    admin.site.unregister(User)
except admin.sites.NotRegistered:
    pass


@admin.register(User)
class UserAdmin(DjangoUserAdmin):
    inlines = (ProfileInline,)
    list_display = ("username", "full_name", "profile_role", "email", "is_active", "is_staff")
    list_filter = ("is_active", "is_staff", "profile__role")
    search_fields = ("username", "first_name", "last_name", "email", "profile__phone")

    @admin.display(description="ФИО")
    def full_name(self, obj):
        return obj.get_full_name() or "-"

    @admin.display(description="Роль")
    def profile_role(self, obj):
        if hasattr(obj, "profile"):
            return obj.profile.get_role_display()
        return "Без профиля"


@admin.register(Profile)
class ProfileAdmin(admin.ModelAdmin):
    list_display = ("avatar_preview", "display_name", "role", "phone")
    list_filter = ("role",)
    search_fields = ("user__username", "user__first_name", "user__last_name", "phone")
    readonly_fields = ("avatar_preview",)
    fieldsets = (
        ("Пользователь", {"fields": ("user", "role")}),
        ("Контакты", {"fields": ("patronymic", "phone")}),
        ("Аватар", {"fields": ("avatar", "avatar_url", "avatar_preview")}),
    )

    @admin.display(description="Аватар")
    def avatar_preview(self, obj):
        if obj.avatar:
            return format_html(
                '<img src="{}" style="width:38px;height:38px;border-radius:50%;object-fit:cover;">',
                obj.avatar.url,
            )
        if obj.avatar_url:
            return format_html('<img src="{}" style="width:38px;height:38px;border-radius:50%;object-fit:cover;">', obj.avatar_url)
        return format_html(
            '<span style="display:inline-flex;width:38px;height:38px;border-radius:50%;'
            'align-items:center;justify-content:center;background:#e7f2f0;color:#0b4d58;'
            'font-weight:700;">{}</span>',
            obj.initials,
        )


@admin.register(StudyGroup)
class StudyGroupAdmin(admin.ModelAdmin):
    list_display = ("name", "speciality", "admission_year", "curator")
    search_fields = ("name", "speciality")


@admin.register(Subject)
class SubjectAdmin(admin.ModelAdmin):
    list_display = ("name", "code")
    search_fields = ("name", "code")


@admin.register(Course)
class CourseAdmin(admin.ModelAdmin):
    list_display = ("subject", "group", "teacher", "semester", "academic_year")
    list_filter = ("academic_year", "semester", "group")
    search_fields = ("subject__name", "group__name", "teacher__username")


@admin.register(StudentCard)
class StudentCardAdmin(admin.ModelAdmin):
    list_display = ("user", "group", "record_book_number", "enrollment_date", "is_active")
    list_filter = ("group", "is_active")
    search_fields = ("user__username", "user__first_name", "user__last_name", "record_book_number")


@admin.register(Lesson)
class LessonAdmin(admin.ModelAdmin):
    list_display = ("date", "course", "lesson_type", "topic")
    list_filter = ("date", "lesson_type", "course__group")
    search_fields = ("topic", "course__subject__name", "course__group__name")


@admin.register(AttendanceRecord)
class AttendanceRecordAdmin(admin.ModelAdmin):
    list_display = ("lesson", "student", "status", "updated_at")
    list_filter = ("status", "lesson__course__group", "lesson__course__subject")
    search_fields = ("student__user__last_name", "student__user__first_name", "lesson__topic")


@admin.register(Grade)
class GradeAdmin(admin.ModelAdmin):
    list_display = ("student", "course", "grade_type", "value", "date")
    list_filter = ("grade_type", "value", "course__group", "course__subject")
    search_fields = ("student__user__last_name", "student__user__first_name", "course__subject__name")


@admin.register(ScheduleEntry)
class ScheduleEntryAdmin(admin.ModelAdmin):
    list_display = (
        "weekday",
        "start_time",
        "end_time",
        "course",
        "teacher_name",
        "room",
        "week_type",
        "is_active",
    )
    list_filter = ("weekday", "week_type", "is_active", "course__group", "course__teacher")
    search_fields = (
        "course__subject__name",
        "course__group__name",
        "course__teacher__first_name",
        "course__teacher__last_name",
        "room",
        "building",
    )
    autocomplete_fields = ("course",)

    @admin.display(description="Преподаватель")
    def teacher_name(self, obj):
        return obj.course.teacher.get_full_name() or obj.course.teacher.username
