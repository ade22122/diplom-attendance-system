from django.core.management.base import BaseCommand

from attendance.models import Profile, apply_role_permissions


class Command(BaseCommand):
    help = "Synchronizes profile roles with Django groups and permissions."

    def handle(self, *args, **options):
        count = 0
        for profile in Profile.objects.select_related("user"):
            apply_role_permissions(profile)
            count += 1
        self.stdout.write(self.style.SUCCESS(f"Roles synchronized: {count}"))
