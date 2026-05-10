from django import forms

from .models import Lesson, Profile, ScheduleEntry


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
