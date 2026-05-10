from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [
        ("attendance", "0001_initial"),
    ]

    operations = [
        migrations.AddField(
            model_name="profile",
            name="avatar_url",
            field=models.URLField(blank=True, verbose_name="Ссылка на аватар"),
        ),
    ]
