import hashlib

from django.contrib import messages
from django.contrib.auth.views import LoginView
from django.core.cache import cache
from django.utils.translation import gettext_lazy as _


class SecureLoginView(LoginView):
    template_name = "registration/login.html"
    max_attempts = 5
    lockout_seconds = 10 * 60

    def _client_ip(self):
        forwarded_for = self.request.META.get("HTTP_X_FORWARDED_FOR")
        if forwarded_for:
            return forwarded_for.split(",")[0].strip()
        return self.request.META.get("REMOTE_ADDR", "unknown")

    def _attempt_key(self):
        username = self.request.POST.get("username", "").strip().lower()
        raw_key = f"{self._client_ip()}:{username}"
        digest = hashlib.sha256(raw_key.encode("utf-8")).hexdigest()
        return f"login-attempts:{digest}"

    def dispatch(self, request, *args, **kwargs):
        if request.method == "POST":
            key = self._attempt_key()
            if cache.get(key, 0) >= self.max_attempts:
                messages.error(
                    request,
                    _("Слишком много попыток входа. Подождите 10 минут и попробуйте снова."),
                )
                form = self.get_form()
                return self.form_invalid(form)
        return super().dispatch(request, *args, **kwargs)

    def form_valid(self, form):
        cache.delete(self._attempt_key())
        return super().form_valid(form)

    def form_invalid(self, form):
        key = self._attempt_key()
        attempts = cache.get(key, 0) + 1
        cache.set(key, attempts, self.lockout_seconds)
        return super().form_invalid(form)
