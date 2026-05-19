# Changelog

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
