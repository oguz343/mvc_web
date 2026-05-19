# Changelog

- FAZ 1: `docs/SUBMISSION_SMOKE_TEST.md` ile canonical teslim smoke senaryoları dokümante edildi; `flutter_mobile/test/app_helpers_test.dart` canonical teslim ID üretimini doğrular.
- FAZ 2: `Views/Lessons/Index.cshtml` manuel admin Temizle metni ve onayıyla güncellendi; otomatik cleanup çağrılarının yalnızca `LessonsController.Cleanup` içinde kaldığı doğrulandı.
- FAZ 3: `admin123` fallback sadece geliştirme/debug ortamında kalacak şekilde doğrulandı; production/release için eksik `system/admin_account` yapılandırmasında açık hata mesajı eklendi.
- FAZ 4: Admin policy `role=admin` ve `number=0000` olarak tekilleştirildi; admin route guard kullanımı sadeleştirildi ve 0000 dışı admin hesabının desteklenmediği dokümante edildi.
