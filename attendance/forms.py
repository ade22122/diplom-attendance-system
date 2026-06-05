from django import forms
from django.contrib.auth import get_user_model
from django.contrib.auth.forms import UserCreationForm
from django.utils import timezone

from .models import Lesson, Profile, ScheduleEntry, StudentCard, StudyGroup


User = get_user_model()
MAX_AVATAR_SIZE = 3 * 1024 * 1024
ALLOWED_AVATAR_CONTENT_TYPES = {"image/jpeg", "image/png", "image/webp", "image/gif"}


def validate_avatar_file(avatar):
    if not avatar:
        return avatar
    if avatar.size > MAX_AVATAR_SIZE:
        raise forms.ValidationError("Размер аватарки должен быть не больше 3 МБ.")
    content_type = getattr(avatar, "content_type", "")
    if content_type and content_type not in ALLOWED_AVATAR_CONTENT_TYPES:
        raise forms.ValidationError("Загрузите изображение JPG, PNG, WEBP или GIF.")
    return avatar


def generate_record_book_number(user_id):
    base = f"RB{timezone.localdate().year}{user_id:05d}"
    number = base
    counter = 1
    while StudentCard.objects.filter(record_book_number=number).exists():
        counter += 1
        number = f"{base}-{counter}"
    return number


class RegistrationForm(UserCreationForm):
    first_name = forms.CharField(
        label="Имя",
        max_length=150,
        widget=forms.TextInput(attrs={"class": "field", "placeholder": "Иван"}),
    )
    last_name = forms.CharField(
        label="Фамилия",
        max_length=150,
        widget=forms.TextInput(attrs={"class": "field", "placeholder": "Петров"}),
    )
    email = forms.EmailField(
        label="Email",
        required=False,
        widget=forms.EmailInput(attrs={"class": "field", "placeholder": "student@example.com"}),
    )
    group = forms.ModelChoiceField(
        label="Группа",
        queryset=StudyGroup.objects.none(),
        required=True,
        empty_label="Выберите группу",
        widget=forms.Select(attrs={"class": "field"}),
    )
    avatar = forms.ImageField(
        label="Аватарка",
        required=False,
        widget=forms.ClearableFileInput(attrs={"class": "field file-field", "accept": "image/*"}),
    )

    class Meta:
        model = User
        fields = ["username", "first_name", "last_name", "email", "group", "avatar", "password1", "password2"]

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields["group"].queryset = StudyGroup.objects.all()
        self.fields["username"].label = "Логин"
        self.fields["username"].help_text = ""
        self.fields["password1"].label = "Пароль"
        self.fields["password2"].label = "Повтор пароля"
        self.fields["password1"].help_text = "Минимум 10 символов, не только цифры."
        for field_name in ["username", "password1", "password2"]:
            self.fields[field_name].widget.attrs.update({"class": "field"})

    def clean_avatar(self):
        return validate_avatar_file(self.cleaned_data.get("avatar"))

    def save(self, commit=True):
        user = super().save(commit=False)
        user.first_name = self.cleaned_data["first_name"].strip()
        user.last_name = self.cleaned_data["last_name"].strip()
        user.email = self.cleaned_data["email"].strip()

        if commit:
            user.save()
            profile = user.profile
            profile.role = Profile.Role.STUDENT
            if self.cleaned_data.get("avatar"):
                profile.avatar = self.cleaned_data["avatar"]
            profile.save()

            group = self.cleaned_data.get("group")
            if group:
                StudentCard.objects.get_or_create(
                    user=user,
                    defaults={
                        "group": group,
                        "record_book_number": generate_record_book_number(user.id),
                    },
                )

        return user


class ProfileEditForm(forms.Form):
    first_name = forms.CharField(
        label="Имя",
        max_length=150,
        widget=forms.TextInput(attrs={"class": "field"}),
    )
    last_name = forms.CharField(
        label="Фамилия",
        max_length=150,
        widget=forms.TextInput(attrs={"class": "field"}),
    )
    email = forms.EmailField(
        label="Email",
        required=False,
        widget=forms.EmailInput(attrs={"class": "field"}),
    )
    patronymic = forms.CharField(
        label="Отчество",
        max_length=150,
        required=False,
        widget=forms.TextInput(attrs={"class": "field"}),
    )
    phone = forms.CharField(
        label="Телефон",
        max_length=30,
        required=False,
        widget=forms.TextInput(attrs={"class": "field", "placeholder": "+7 ..."}),
    )
    avatar = forms.ImageField(
        label="Новая аватарка",
        required=False,
        widget=forms.ClearableFileInput(attrs={"class": "field file-field", "accept": "image/*"}),
    )
    clear_avatar = forms.BooleanField(
        label="Удалить текущую аватарку",
        required=False,
        widget=forms.CheckboxInput(attrs={"class": "checkbox"}),
    )

    def __init__(self, *args, user, profile, **kwargs):
        self.user = user
        self.profile = profile
        initial = kwargs.pop(
            "initial",
            {
                "first_name": user.first_name,
                "last_name": user.last_name,
                "email": user.email,
                "patronymic": profile.patronymic,
                "phone": profile.phone,
            },
        )
        super().__init__(*args, initial=initial, **kwargs)

    def clean_avatar(self):
        return validate_avatar_file(self.cleaned_data.get("avatar"))

    def save(self):
        self.user.first_name = self.cleaned_data["first_name"].strip()
        self.user.last_name = self.cleaned_data["last_name"].strip()
        self.user.email = self.cleaned_data["email"].strip()
        self.user.save(update_fields=["first_name", "last_name", "email"])

        self.profile.patronymic = self.cleaned_data["patronymic"].strip()
        self.profile.phone = self.cleaned_data["phone"].strip()

        avatar = self.cleaned_data.get("avatar")
        if self.cleaned_data.get("clear_avatar"):
            if self.profile.avatar:
                self.profile.avatar.delete(save=False)
            self.profile.avatar = ""
            self.profile.avatar_url = ""
        if avatar:
            if self.profile.avatar:
                self.profile.avatar.delete(save=False)
            self.profile.avatar = avatar
            self.profile.avatar_url = ""

        self.profile.save(update_fields=["patronymic", "phone", "avatar", "avatar_url"])
        return self.profile


class LessonForm(forms.ModelForm):
    class Meta:
        model = Lesson
        fields = ["date", "lesson_type", "topic"]
        widgets = {
            "date": forms.DateInput(attrs={"type": "date", "class": "field"}),
            "lesson_type": forms.Select(attrs={"class": "field"}),
            "topic": forms.TextInput(attrs={"class": "field", "placeholder": "Тема занятия"}),
        }


class ScheduleEntryForm(forms.ModelForm):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields["course"].queryset = self.fields["course"].queryset.select_related(
            "group",
            "subject",
            "teacher",
        ).order_by("group__name", "subject__name")

    class Meta:
        model = ScheduleEntry
        fields = [
            "course",
            "weekday",
            "start_time",
            "end_time",
            "lesson_type",
            "room",
            "building",
            "week_type",
            "is_active",
            "comment",
        ]
        widgets = {
            "course": forms.Select(attrs={"class": "field"}),
            "weekday": forms.Select(attrs={"class": "field"}),
            "start_time": forms.TimeInput(attrs={"type": "time", "class": "field"}),
            "end_time": forms.TimeInput(attrs={"type": "time", "class": "field"}),
            "lesson_type": forms.Select(attrs={"class": "field"}),
            "room": forms.TextInput(attrs={"class": "field", "placeholder": "Например: 304"}),
            "building": forms.TextInput(attrs={"class": "field", "placeholder": "Например: главный корпус"}),
            "week_type": forms.Select(attrs={"class": "field"}),
            "is_active": forms.CheckboxInput(attrs={"class": "checkbox"}),
            "comment": forms.TextInput(attrs={"class": "field", "placeholder": "Дополнительная информация"}),
        }

    def clean(self):
        cleaned_data = super().clean()
        start_time = cleaned_data.get("start_time")
        end_time = cleaned_data.get("end_time")
        if start_time and end_time and end_time <= start_time:
            raise forms.ValidationError("Время окончания должно быть позже времени начала.")
        return cleaned_data


class RoleUpdateForm(forms.Form):
    role = forms.ChoiceField(choices=Profile.Role.choices, widget=forms.Select(attrs={"class": "field"}))
