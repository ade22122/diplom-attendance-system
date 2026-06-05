from django.contrib import messages
from django.contrib.auth import get_user_model
from django.contrib.auth import login
from django.contrib.auth.decorators import login_required
from django.core.exceptions import PermissionDenied
from django.db.models import Count, Q
from django.http import Http404, HttpResponse, JsonResponse
from django.shortcuts import get_object_or_404, redirect, render
from django.utils import timezone

from .forms import LessonForm, ProfileEditForm, RegistrationForm, RoleUpdateForm, ScheduleEntryForm
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
    apply_role_permissions,
)


def get_profile(user):
    profile, _ = Profile.objects.get_or_create(
        user=user,
        defaults={"role": Profile.Role.ADMIN if user.is_superuser else Profile.Role.STUDENT},
    )
    return profile


def is_admin_user(user):
    return user.is_superuser or get_profile(user).role == Profile.Role.ADMIN


def assert_teacher_or_admin_for_course(user, course):
    profile = get_profile(user)
    if is_admin_user(user):
        return
    if profile.role == Profile.Role.TEACHER and course.teacher_id == user.id:
        return
    raise PermissionDenied("Нет доступа к этому журналу.")


def assert_admin_user(user):
    if not is_admin_user(user):
        raise PermissionDenied("Раздел доступен администратору.")


def build_schedule_days(entries):
    entries_by_day = {weekday: [] for weekday, _ in ScheduleEntry.Weekday.choices}
    for entry in entries:
        entries_by_day[entry.weekday].append(entry)
    return [
        {"weekday": weekday, "label": label, "entries": entries_by_day[weekday]}
        for weekday, label in ScheduleEntry.Weekday.choices
    ]


def build_schedule_calendar(entries):
    default_slots = [
        ("09:00", "10:30"),
        ("10:45", "12:15"),
        ("12:40", "14:10"),
        ("14:25", "15:55"),
        ("16:10", "17:40"),
    ]
    slot_map = {start: {"start": start, "end": end} for start, end in default_slots}
    entries_by_slot = {}

    for entry in entries:
        start = entry.start_time.strftime("%H:%M")
        end = entry.end_time.strftime("%H:%M")
        slot_map.setdefault(start, {"start": start, "end": end})
        entries_by_slot.setdefault((start, entry.weekday), []).append(entry)

    rows = []
    for start in sorted(slot_map):
        slot = slot_map[start]
        rows.append(
            {
                "start": slot["start"],
                "end": slot["end"],
                "days": [
                    {
                        "weekday": weekday,
                        "label": label,
                        "entries": entries_by_slot.get((slot["start"], weekday), []),
                    }
                    for weekday, label in ScheduleEntry.Weekday.choices
                ],
            }
        )
    return rows


def build_student_course_progress(course, student_card):
    rows = []
    for lesson in course.lessons.all():
        rows.append(
            {
                "lesson": lesson,
                "attendance": AttendanceRecord.objects.filter(lesson=lesson, student=student_card).first(),
                "grades": Grade.objects.filter(course=course, lesson=lesson, student=student_card),
            }
        )

    grades = Grade.objects.filter(course=course, student=student_card)
    numeric_values = [int(grade.value) for grade in grades if grade.value.isdigit()]
    average = round(sum(numeric_values) / len(numeric_values), 1) if numeric_values else None
    final_grades = grades.filter(lesson__isnull=True)
    attendance_total = AttendanceRecord.objects.filter(student=student_card, lesson__course=course).count()
    attendance_positive = AttendanceRecord.objects.filter(
        student=student_card,
        lesson__course=course,
        status__in=[
            AttendanceRecord.Status.PRESENT,
            AttendanceRecord.Status.LATE,
            AttendanceRecord.Status.EXCUSED,
        ],
    ).count()
    attendance_percent = round(attendance_positive * 100 / attendance_total) if attendance_total else 0

    return {
        "rows": rows,
        "grades": grades,
        "final_grades": final_grades,
        "average": average,
        "attendance_total": attendance_total,
        "attendance_percent": attendance_percent,
        "grades_count": grades.count(),
    }


def check_email_unique(request):
    email = (request.GET.get("email") or "").strip()
    if not email:
        return JsonResponse({"available": True, "message": ""})

    exists = get_user_model().objects.filter(email__iexact=email).exists()
    return JsonResponse(
        {
            "available": not exists,
            "message": (
                "Email свободен."
                if not exists
                else "Этот email уже зарегистрирован. Укажите другой адрес."
            ),
        }
    )


def register(request):
    if request.user.is_authenticated:
        return redirect("dashboard")

    if request.method == "POST":
        form = RegistrationForm(request.POST, request.FILES)
        if form.is_valid():
            user = form.save()
            login(request, user)
            messages.success(request, "Регистрация завершена. Добро пожаловать в электронный журнал.")
            return redirect("dashboard")
    else:
        form = RegistrationForm()

    return render(request, "registration/register.html", {"form": form})


@login_required
def profile_edit(request):
    profile = get_profile(request.user)

    if request.method == "POST":
        form = ProfileEditForm(request.POST, request.FILES, user=request.user, profile=profile)
        if form.is_valid():
            form.save()
            messages.success(request, "Профиль обновлен.")
            return redirect("profile_edit")
    else:
        form = ProfileEditForm(user=request.user, profile=profile)

    return render(request, "attendance/profile_form.html", {"form": form, "profile": profile})


@login_required
def profile_avatar(request, profile_id):
    profile = get_object_or_404(Profile, pk=profile_id)
    if not profile.avatar_image:
        raise Http404("Avatar not found.")

    response = HttpResponse(
        bytes(profile.avatar_image),
        content_type=profile.avatar_content_type or "application/octet-stream",
    )
    response["Cache-Control"] = "private, max-age=86400"
    return response


@login_required
def dashboard(request):
    profile = get_profile(request.user)
    User = get_user_model()

    if is_admin_user(request.user):
        context = {
            "profile": profile,
            "users_count": User.objects.count(),
            "teachers_count": Profile.objects.filter(role=Profile.Role.TEACHER).count(),
            "students_count": StudentCard.objects.filter(is_active=True).count(),
            "groups_count": StudyGroup.objects.count(),
            "subjects_count": Subject.objects.count(),
            "courses_count": Course.objects.count(),
            "schedule_count": ScheduleEntry.objects.filter(is_active=True).count(),
            "lessons_count": Lesson.objects.count(),
            "attendance_count": AttendanceRecord.objects.count(),
            "grades_count": Grade.objects.count(),
            "recent_lessons": Lesson.objects.select_related(
                "course__subject",
                "course__group",
                "course__teacher",
            )[:6],
            "recent_users": Profile.objects.select_related("user").order_by("-id")[:6],
        }
        return render(request, "attendance/dashboard.html", context)

    if profile.role == Profile.Role.TEACHER:
        courses = (
            Course.objects.filter(teacher=request.user)
            .select_related("subject", "group")
            .annotate(
                lessons_total=Count("lessons", distinct=True),
                students_total=Count("group__students", filter=Q(group__students__is_active=True), distinct=True),
                grades_total=Count("grades", distinct=True),
            )
        )
        recent_lessons = Lesson.objects.filter(course__teacher=request.user).select_related(
            "course__subject",
            "course__group",
        )[:5]
        teacher_students_count = StudentCard.objects.filter(
            group__courses__teacher=request.user,
            is_active=True,
        ).distinct().count()
        return render(
            request,
            "attendance/dashboard.html",
            {
                "profile": profile,
                "courses": courses,
                "recent_lessons": recent_lessons,
                "teacher_students_count": teacher_students_count,
            },
        )

    student_card = StudentCard.objects.select_related("group").filter(user=request.user).first()
    courses = Course.objects.none()
    attendance_total = 0
    attendance_positive = 0
    grades = Grade.objects.none()
    student_schedule_preview = ScheduleEntry.objects.none()
    student_grades_count = 0

    if student_card:
        courses = (
            Course.objects.filter(group=student_card.group)
            .select_related("subject", "teacher")
            .annotate(
                lessons_total=Count("lessons", distinct=True),
                grades_total=Count("grades", filter=Q(grades__student=student_card), distinct=True),
            )
        )
        attendance_qs = AttendanceRecord.objects.filter(student=student_card)
        attendance_total = attendance_qs.count()
        attendance_positive = attendance_qs.filter(
            status__in=[
                AttendanceRecord.Status.PRESENT,
                AttendanceRecord.Status.LATE,
                AttendanceRecord.Status.EXCUSED,
            ]
        ).count()
        grades = Grade.objects.filter(student=student_card).select_related("course__subject")[:8]
        student_grades_count = Grade.objects.filter(student=student_card).count()
        student_schedule_preview = ScheduleEntry.objects.filter(
            course__group=student_card.group,
            is_active=True,
        ).select_related("course__subject", "course__teacher", "course__group")[:6]

    attendance_percent = round(attendance_positive * 100 / attendance_total) if attendance_total else 0
    return render(
        request,
        "attendance/dashboard.html",
        {
            "profile": profile,
            "student_card": student_card,
            "courses": courses,
            "attendance_total": attendance_total,
            "attendance_percent": attendance_percent,
            "grades": grades,
            "student_schedule_preview": student_schedule_preview,
            "student_grades_count": student_grades_count,
        },
    )


@login_required
def teacher_courses(request):
    if is_admin_user(request.user):
        courses = Course.objects.select_related("subject", "group", "teacher").annotate(
            lessons_total=Count("lessons", distinct=True),
            students_total=Count("group__students", filter=Q(group__students__is_active=True), distinct=True),
            grades_total=Count("grades", distinct=True),
        )
    else:
        profile = get_profile(request.user)
        if profile.role != Profile.Role.TEACHER:
            raise PermissionDenied("Раздел доступен преподавателям.")
        courses = (
            Course.objects.filter(teacher=request.user)
            .select_related("subject", "group")
            .annotate(
                lessons_total=Count("lessons", distinct=True),
                students_total=Count("group__students", filter=Q(group__students__is_active=True), distinct=True),
                grades_total=Count("grades", distinct=True),
            )
        )

    return render(request, "attendance/teacher_courses.html", {"courses": courses})


@login_required
def teacher_course_detail(request, course_id):
    course = get_object_or_404(Course.objects.select_related("subject", "group", "teacher"), pk=course_id)
    assert_teacher_or_admin_for_course(request.user, course)

    students = StudentCard.objects.filter(group=course.group, is_active=True).select_related("user", "user__profile")
    lessons = course.lessons.annotate(
        attendance_marked=Count("attendance_records", distinct=True),
        grades_marked=Count("grades", distinct=True),
    )
    return render(
        request,
        "attendance/teacher_course_detail.html",
        {"course": course, "students": students, "lessons": lessons, "students_count": students.count()},
    )


@login_required
def teacher_student_detail(request, course_id, student_id):
    course = get_object_or_404(Course.objects.select_related("subject", "group", "teacher"), pk=course_id)
    assert_teacher_or_admin_for_course(request.user, course)
    student_card = get_object_or_404(
        StudentCard.objects.select_related("group", "user", "user__profile"),
        pk=student_id,
        group=course.group,
        is_active=True,
    )
    progress = build_student_course_progress(course, student_card)

    return render(
        request,
        "attendance/teacher_student_detail.html",
        {
            "course": course,
            "student_card": student_card,
            **progress,
        },
    )


@login_required
def lesson_create(request, course_id):
    course = get_object_or_404(Course, pk=course_id)
    assert_teacher_or_admin_for_course(request.user, course)

    if request.method == "POST":
        form = LessonForm(request.POST)
        if form.is_valid():
            lesson = form.save(commit=False)
            lesson.course = course
            lesson.save()
            messages.success(request, "Занятие добавлено. Можно заполнить посещаемость и оценки.")
            return redirect("lesson_journal", lesson_id=lesson.id)
    else:
        form = LessonForm(initial={"date": timezone.localdate()})

    return render(request, "attendance/lesson_form.html", {"course": course, "form": form})


@login_required
def lesson_journal(request, lesson_id):
    lesson = get_object_or_404(
        Lesson.objects.select_related("course__subject", "course__group", "course__teacher"),
        pk=lesson_id,
    )
    course = lesson.course
    assert_teacher_or_admin_for_course(request.user, course)

    students = StudentCard.objects.filter(group=course.group, is_active=True).select_related("user", "user__profile")

    if request.method == "POST":
        for student in students:
            status = request.POST.get(f"attendance_{student.id}") or AttendanceRecord.Status.ABSENT
            comment = request.POST.get(f"comment_{student.id}", "").strip()
            AttendanceRecord.objects.update_or_create(
                lesson=lesson,
                student=student,
                defaults={"status": status, "comment": comment},
            )

            grade_value = request.POST.get(f"grade_{student.id}", "").strip()
            grade_comment = request.POST.get(f"grade_comment_{student.id}", "").strip()
            if grade_value:
                Grade.objects.update_or_create(
                    course=course,
                    lesson=lesson,
                    student=student,
                    grade_type=Grade.GradeType.CURRENT,
                    defaults={
                        "value": grade_value,
                        "comment": grade_comment,
                        "date": lesson.date,
                    }
                )
            else:
                Grade.objects.filter(
                    course=course,
                    lesson=lesson,
                    student=student,
                    grade_type=Grade.GradeType.CURRENT,
                ).delete()

        messages.success(request, "Журнал занятия сохранен.")
        return redirect("lesson_journal", lesson_id=lesson.id)

    rows = []
    for student in students:
        rows.append(
            {
                "student": student,
                "attendance": AttendanceRecord.objects.filter(lesson=lesson, student=student).first(),
                "grade": Grade.objects.filter(
                    course=course,
                    lesson=lesson,
                    student=student,
                    grade_type=Grade.GradeType.CURRENT,
                ).first(),
            }
        )

    return render(
        request,
        "attendance/lesson_journal.html",
        {
            "lesson": lesson,
            "course": course,
            "rows": rows,
            "attendance_statuses": AttendanceRecord.Status.choices,
            "grade_values": Grade.GradeValue.choices,
        },
    )


@login_required
def student_course_detail(request, course_id):
    profile = get_profile(request.user)
    if profile.role != Profile.Role.STUDENT and not is_admin_user(request.user):
        raise PermissionDenied("Раздел доступен студентам.")

    student_card = get_object_or_404(StudentCard.objects.select_related("group", "user__profile"), user=request.user)
    course = get_object_or_404(Course.objects.select_related("subject", "teacher", "group"), pk=course_id)

    if course.group_id != student_card.group_id:
        raise PermissionDenied("Нет доступа к данным другой группы.")

    progress = build_student_course_progress(course, student_card)

    return render(
        request,
        "attendance/student_course_detail.html",
        {
            "course": course,
            "student_card": student_card,
            **progress,
        },
    )


@login_required
def schedule_view(request):
    profile = get_profile(request.user)
    User = get_user_model()
    entries = ScheduleEntry.objects.select_related(
        "course__subject",
        "course__group",
        "course__teacher",
    ).filter(is_active=True)

    selected_group_id = request.GET.get("group") or ""
    selected_teacher_id = request.GET.get("teacher") or ""
    groups = StudyGroup.objects.all()
    teachers = User.objects.filter(profile__role=Profile.Role.TEACHER).order_by("last_name", "first_name")
    page_title = "Расписание"

    if is_admin_user(request.user):
        if selected_group_id:
            entries = entries.filter(course__group_id=selected_group_id)
        if selected_teacher_id:
            entries = entries.filter(course__teacher_id=selected_teacher_id)
        page_title = "Общее расписание"
    elif profile.role == Profile.Role.TEACHER:
        entries = entries.filter(course__teacher=request.user)
        page_title = "Мое расписание преподавателя"
    else:
        student_card = get_object_or_404(StudentCard.objects.select_related("group"), user=request.user)
        entries = entries.filter(course__group=student_card.group)
        selected_group_id = str(student_card.group_id)
        page_title = f"Расписание группы {student_card.group.name}"

    return render(
        request,
        "attendance/schedule.html",
        {
            "profile": profile,
            "page_title": page_title,
            "schedule_days": build_schedule_days(entries),
            "calendar_rows": build_schedule_calendar(entries),
            "weekdays": ScheduleEntry.Weekday.choices,
            "groups": groups,
            "teachers": teachers,
            "selected_group_id": selected_group_id,
            "selected_teacher_id": selected_teacher_id,
        },
    )


@login_required
def record_book(request):
    profile = get_profile(request.user)
    students = StudentCard.objects.select_related("user", "group", "user__profile").order_by(
        "group__name",
        "user__last_name",
        "user__first_name",
    )
    selected_student = None

    if is_admin_user(request.user):
        selected_student_id = request.GET.get("student")
        if selected_student_id:
            selected_student = get_object_or_404(students, pk=selected_student_id)
        else:
            selected_student = students.first()
    elif profile.role == Profile.Role.STUDENT:
        selected_student = get_object_or_404(
            StudentCard.objects.select_related("user", "group", "user__profile"),
            user=request.user,
        )
    else:
        raise PermissionDenied("Зачетная книжка доступна студенту и администратору.")

    rows = []
    if selected_student:
        courses = Course.objects.filter(group=selected_student.group).select_related("subject", "teacher")
        for course in courses:
            grades = list(Grade.objects.filter(course=course, student=selected_student))
            numeric_values = [int(grade.value) for grade in grades if grade.value.isdigit()]
            average = round(sum(numeric_values) / len(numeric_values), 1) if numeric_values else None
            attendance_total = AttendanceRecord.objects.filter(
                lesson__course=course,
                student=selected_student,
            ).count()
            attendance_positive = AttendanceRecord.objects.filter(
                lesson__course=course,
                student=selected_student,
                status__in=[
                    AttendanceRecord.Status.PRESENT,
                    AttendanceRecord.Status.LATE,
                    AttendanceRecord.Status.EXCUSED,
                ],
            ).count()
            attendance_percent = round(attendance_positive * 100 / attendance_total) if attendance_total else 0
            final_grade = Grade.objects.filter(
                course=course,
                student=selected_student,
                grade_type=Grade.GradeType.FINAL,
            ).first()
            rows.append(
                {
                    "course": course,
                    "grades": grades,
                    "average": average,
                    "attendance_percent": attendance_percent,
                    "final_grade": final_grade,
                }
            )

    return render(
        request,
        "attendance/record_book.html",
        {
            "profile": profile,
            "students": students,
            "selected_student": selected_student,
            "rows": rows,
        },
    )


@login_required
def admin_schedule_manage(request):
    assert_admin_user(request.user)
    edit_entry = None
    edit_entry_id = request.GET.get("edit")
    if edit_entry_id:
        edit_entry = get_object_or_404(ScheduleEntry, pk=edit_entry_id)

    if request.method == "POST":
        action = request.POST.get("action")
        entry_id = request.POST.get("entry_id")

        if action == "delete" and entry_id:
            entry = get_object_or_404(ScheduleEntry, pk=entry_id)
            entry.delete()
            messages.success(request, "Пара удалена из расписания.")
            return redirect("admin_schedule_manage")

        instance = get_object_or_404(ScheduleEntry, pk=entry_id) if entry_id else None
        form = ScheduleEntryForm(request.POST, instance=instance)
        if form.is_valid():
            form.save()
            messages.success(request, "Расписание сохранено.")
            return redirect("admin_schedule_manage")
    else:
        initial = {}
        if not edit_entry:
            if request.GET.get("weekday"):
                initial["weekday"] = request.GET.get("weekday")
            if request.GET.get("start"):
                initial["start_time"] = request.GET.get("start")
            if request.GET.get("end"):
                initial["end_time"] = request.GET.get("end")
        form = ScheduleEntryForm(instance=edit_entry, initial=initial)

    entries = ScheduleEntry.objects.select_related(
        "course__subject",
        "course__group",
        "course__teacher",
    ).all()

    return render(
        request,
        "attendance/admin_schedule.html",
        {
            "form": form,
            "entries": entries,
            "edit_entry": edit_entry,
            "schedule_days": build_schedule_days(entries),
            "calendar_rows": build_schedule_calendar(entries),
            "weekdays": ScheduleEntry.Weekday.choices,
        },
    )


@login_required
def admin_roles(request):
    assert_admin_user(request.user)

    if request.method == "POST":
        profile = get_object_or_404(Profile.objects.select_related("user"), pk=request.POST.get("profile_id"))
        form = RoleUpdateForm(request.POST)
        if form.is_valid():
            profile.role = form.cleaned_data["role"]
            profile.save(update_fields=["role"])
            apply_role_permissions(profile)
            messages.success(request, f"Роль пользователя {profile.display_name} обновлена.")
            return redirect("admin_roles")

    profiles = Profile.objects.select_related("user").order_by("role", "user__last_name", "user__first_name")
    for item in profiles:
        apply_role_permissions(item)

    return render(
        request,
        "attendance/admin_roles.html",
        {
            "profiles": profiles,
            "role_choices": Profile.Role.choices,
        },
    )
