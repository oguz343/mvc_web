# Changelog

- UI polish: Admin and teacher panel headers now include compact icon quick links beside the user chip for faster navigation.

- P4 reliability: Teacher announcements page now shows newly published test-titled announcements and shares the dashboard's broader teacher target matching.

- P1 security: Admin login no longer accepts the development `admin123` fallback when the admin user or system account already has a configured password.

- UI polish: Admin and teacher internal sidebar icons now render as clean text badges instead of broken legacy glyphs; login page remains unchanged.

- P1 auth: Mobile custom token CORS origins can now be configured with `MobileAuth:AllowedOrigins` before the production domain is known.

- P1 auth: Mobile custom token API endpoint'i eklendi; başarılı web login doğrulaması Firebase custom token, role ve number claim'leri döner.
- P1 auth: Web login doğrulaması custom token endpoint'i tarafından yeniden kullanılabilecek `AuthLoginService` içine taşındı; mevcut MVC login davranışı korunur.
- P1 security: Web-created activation and password reset codes are now stored as hashes while legacy plaintext activation fallback remains.
- P5 maintenance: Documented a behavior-preserving `TeacherController` split plan and extraction guardrails.
- P2 performance: Auth, forgot-password, and password request user lookups now try role/number Firestore queries before legacy full `users` scans.

- FAZ 1: `docs/SUBMISSION_SMOKE_TEST.md` ile canonical teslim smoke senaryoları dokümante edildi; `flutter_mobile/test/app_helpers_test.dart` canonical teslim ID üretimini doğrular.
- FAZ 2: `Views/Lessons/Index.cshtml` manuel admin Temizle metni ve onayıyla güncellendi; otomatik cleanup çağrılarının yalnızca `LessonsController.Cleanup` içinde kaldığı doğrulandı.
- FAZ 3: `admin123` fallback sadece geliştirme/debug ortamında kalacak şekilde doğrulandı; production/release için eksik `system/admin_account` yapılandırmasında açık hata mesajı eklendi.
- FAZ 4: Admin policy `role=admin` ve `number=0000` olarak tekilleştirildi; admin route guard kullanımı sadeleştirildi ve 0000 dışı admin hesabının desteklenmediği dokümante edildi.
- FAZ 5: Teslim durumları web ve mobilde `Teslim Edildi`, değerlendirme sonrası `Değerlendirildi` olacak şekilde tekilleştirildi; dashboard sayımları status veya not/geri dönüş bilgisiyle tutarlı kaldı.
- FAZ 6: Öğretmen ödev düzenleme/silme/değerlendirme işlemleri sahiplik guard'larıyla doğrulandı; teslim değerlendirmede koleksiyon whitelist'i ve sahipsiz ödev reddi netleştirildi.
- FAZ 7: Flutter dashboard/ödev/teslim/veli/admin ekranlarında Firestore stream'leri build içinde yeniden üretilmeyecek şekilde `initState` akışına taşındı.
- FAZ 8: Mobil Firestore erişimi için Firebase Auth/rules riski ve production güvenlik planı `flutter_mobile/docs/SECURITY.md` altında dokümante edildi.
- FAZ 9: Kullanılmayan web `CreatePassword` artifact'ları kaldırıldı; mobil route guard, proje README'si ve mobil/web parite notları eklendi.
- FAZ 10: Admin-only legacy teslim backfill ekranı eklendi; `homework_submissions` kayıtları canonical `submissions` dokümanlarına idempotent merge edilebilir.
- P0/P3 hardening: SetPassword akışı yalnızca aktivasyon + `mustChangePassword` durumuna bağlandı; portal tesliminde koleksiyon whitelist ve sınıf sahipliği kontrolü eklendi; legacy backfill ikinci çalıştırmada değişmeyen kayıtları `Skip` sayacak şekilde düzeltildi.
- P1/P4 hardening: Web session cookie ayarları sıkılaştırıldı; web/mobil şifre politikası en az 8 karaktere hizalandı; portal dosya teslimlerine 10 MB ve MIME kontrolü eklendi; geçici kod üretimi güvenli rastgele üretime taşındı; mobil `/change-password` oturum guard'ına alındı.
- P2 performance: Portal teslim kontrolünde canonical ve legacy direkt ID aramasından sonra tam koleksiyon taraması yerine öğrenci numarasıyla sınırlı Firestore query fallback'i eklendi.
