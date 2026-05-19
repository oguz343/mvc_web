# Teslim Smoke Testi

Bu kontrol web portal, mobil uygulama, admin listesi, öğretmen listesi ve raporların aynı teslimi canonical kimlikle okuyup yazdığını doğrular.

## Canonical sözleşme

- Canonical koleksiyon: `submissions`
- Canonical belge kimliği: `{normalize(assignmentId)}_{onlyDigits(studentNo)}`
- Web karşılığı: `PortalController.BuildSubmissionId`
- Mobil karşılığı: `AppHelpers.buildSubmissionKey`
- Legacy koleksiyon: `homework_submissions`
- Legacy belge kimliği örnekleri: `homeworks_{assignmentId}_{studentNo}` veya `assignments_{assignmentId}_{studentNo}`

## Senaryo 1 - Web portal teslimi admin listesinde görünür

1. Öğrenci hesabıyla web portala giriş yap.
2. Aktif bir ödeve metin veya dosya ile teslim yap.
3. Firestore `submissions` koleksiyonunda `{normalize(assignmentId)}_{onlyDigits(studentNo)}` belgesinin oluştuğunu doğrula.
4. Admin panelinde `Submissions` listesini aç.
5. Aynı öğrencinin aynı ödev tesliminin tek satır olarak göründüğünü doğrula.

Beklenen sonuç: Admin listesi canonical `submissions` belgesini öncelikli okur; legacy kopya varsa aynı teslim ikinci satır olarak çoğalmaz.

## Senaryo 2 - Mobil teslim web admin ve öğretmen ekranında görünür

1. Öğrenci hesabıyla mobil uygulamaya giriş yap.
2. Aktif bir ödeve metin veya link teslim et.
3. Firestore `submissions` koleksiyonunda canonical belge kimliğini doğrula.
4. Web admin `Submissions` listesini aç.
5. Web öğretmen teslim ekranını aç.

Beklenen sonuç: Mobil teslim web admin ve öğretmen ekranlarında aynı ödev ve öğrenciyle görünür.

## Senaryo 3 - Aynı öğrenci ve ödev için tek canonical belge güncellenir

1. Aynı öğrenciyle aynı ödeve önce webden teslim yap.
2. Aynı öğrenciyle aynı ödeve mobilden tekrar teslim yap veya ters sırayı dene.
3. Firestore `submissions` koleksiyonunda aynı canonical belge kimliğinin güncellendiğini doğrula.
4. Admin listesi, öğretmen listesi ve raporda tek teslim satırı olduğunu doğrula.

Beklenen sonuç: Yeni teslim, aynı canonical belgeyi `SetOptions.MergeAll` / merge ile günceller; aynı öğrenci ve ödev için ikinci canonical belge oluşmaz.

## Senaryo 4 - Legacy kayıt okuması devam eder

1. `homework_submissions` içinde `homeworks_{assignmentId}_{studentNo}` veya `assignments_{assignmentId}_{studentNo}` kimliğiyle legacy bir kayıt hazırla.
2. Aynı kayıt için `submissions` canonical belgesi olmadığını doğrula.
3. Öğrenci portalını, admin teslim listesini, öğretmen teslim listesini ve raporu aç.

Beklenen sonuç: Legacy kayıt okunur ve ilgili ekranlarda teslim edilmiş görünür. Daha sonra aynı öğrenci aynı ödevi tekrar teslim ederse canonical `submissions` belgesi oluşur.

## Kod kontrol listesi

- `PortalController.SubmitHomework`: canonical `submissions` yazar, legacy `homework_submissions` yazımını korur.
- `PortalController.FindHomeworkSubmission`: önce canonical `submissions`, sonra legacy `homework_submissions` okur.
- `SubmissionsController.Index`: `submissions` öncelikli okur, `assignmentId + studentNo` ile tekilleştirir.
- `ReportsController`: `assignmentId + studentNo` ile eşleşir, içerik bazlı fallback kullanır.
- `TeacherController.LoadTeacherSubmissions`: `submissions` öncelikli okur, öğretmenin kendi ödevleriyle sınırlar.
- `student_assignment_service.dart`: `AppHelpers.buildSubmissionKey` ile canonical yazar, legacy yazımı korur.
- `teacher_service.dart`: değerlendirmeyi canonical ve legacy kayda merge eder.

## FAZ 1 doğrulama notu

- `flutter test test/app_helpers_test.dart` canonical ID üretimini doğrular.
- `dotnet build -o artifacts/check` web projesinin derlendiğini doğrular.
- `flutter analyze` mobil analizinin temiz olduğunu doğrular.
