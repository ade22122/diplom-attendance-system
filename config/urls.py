from django.contrib import admin
from django.conf import settings
from django.conf.urls.static import static
from django.urls import include, path

from attendance.auth_views import SecureLoginView


urlpatterns = [
    path("admin/", admin.site.urls),
    path("accounts/login/", SecureLoginView.as_view(), name="login"),
    path("accounts/", include("django.contrib.auth.urls")),
    path("", include("attendance.urls")),
]

if settings.DEBUG:
    urlpatterns += static(settings.MEDIA_URL, document_root=settings.MEDIA_ROOT)
