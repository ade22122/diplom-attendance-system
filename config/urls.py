from django.contrib import admin
from django.urls import include, path

from attendance.auth_views import SecureLoginView


urlpatterns = [
    path("admin/", admin.site.urls),
    path("accounts/login/", SecureLoginView.as_view(), name="login"),
    path("accounts/", include("django.contrib.auth.urls")),
    path("", include("attendance.urls")),
]
