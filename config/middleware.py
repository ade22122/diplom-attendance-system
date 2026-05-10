class SecurityHeadersMiddleware:
    """Adds browser security headers that complement Django's SecurityMiddleware."""

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        response = self.get_response(request)

        headers = {
            "Content-Security-Policy": (
                "default-src 'self'; "
                "script-src 'self'; "
                "style-src 'self'; "
                "img-src 'self' data: https:; "
                "font-src 'self' data:; "
                "connect-src 'self'; "
                "object-src 'none'; "
                "base-uri 'self'; "
                "form-action 'self'; "
                "frame-ancestors 'none'"
            ),
            "Permissions-Policy": "camera=(), microphone=(), geolocation=(), payment=()",
            "X-Permitted-Cross-Domain-Policies": "none",
        }

        for header, value in headers.items():
            if not response.has_header(header):
                response[header] = value

        return response
